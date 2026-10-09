using RichLife.Domain;
using RichLife.Domain.Banking;
using RichLife.Domain.Entities;
using RichLife.Domain.Enums;

namespace RichLife.Tests.Domain;

public class BankTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Interval = GameConstants.LoanPaymentInterval;

    private static Company NewCompany(decimal cash = 0m)
    {
        var company = Company.Create(Guid.NewGuid(), "Acme");
        company.AccrueOffline(T0);
        company.AddCash(cash);
        return company;
    }

    /// <summary>100,000 at 5 %: 105,000 in 4 installments of 26,250.</summary>
    private static LoanOffer Offer(decimal amount = 100_000m, decimal rate = 0.05m, int installments = 4) =>
        new("test-offer", BankCatalog.All[0], PrestigeLevel.TheHustle, amount, rate, installments, T0.AddHours(6));

    // -- Catalogue and offers ----------------------------------------------------

    [Fact]
    public void BankCatalog_HasTwentyBanks_WithUniqueIds()
    {
        Assert.Equal(20, BankCatalog.All.Count);
        Assert.Equal(20, BankCatalog.All.Select(b => b.Id).Distinct().Count());
    }

    [Theory]
    [InlineData(PrestigeLevel.TheHustle)]
    [InlineData(PrestigeLevel.Entrepreneur)]
    [InlineData(PrestigeLevel.GlobalEmpire)]
    public void Offers_AreFivePerLevel_FromDistinctBanks_WithinThatLevelsRange(PrestigeLevel level)
    {
        var offers = LoanOffers.For(level, T0.AddHours(1));
        var (min, max) = GameConstants.LoanAmountRange(level);

        Assert.Equal(GameConstants.LoanOffersPerLevel, offers.Count);
        Assert.Equal(offers.Count, offers.Select(o => o.Bank.Id).Distinct().Count());
        Assert.All(offers, o =>
        {
            // Rounding to two significant digits may step just past either bound.
            Assert.InRange(o.Amount, min * 0.9m, max * 1.1m);
            Assert.InRange(o.InterestRate, o.Bank.MinRate, o.Bank.MaxRate);
            Assert.InRange(o.Installments, o.Bank.MinInstallments, o.Bank.MaxInstallments);
        });
    }

    [Fact]
    public void Offers_AreStableWithinAWindow_AndChangeInTheNext()
    {
        var early = LoanOffers.For(PrestigeLevel.TheHustle, T0.AddMinutes(5));
        var late = LoanOffers.For(PrestigeLevel.TheHustle, T0.AddHours(5).AddMinutes(59));
        var next = LoanOffers.For(PrestigeLevel.TheHustle, T0.AddHours(6));

        Assert.Equal(early, late);
        Assert.NotEqual(early.Select(o => o.Id), next.Select(o => o.Id));
        Assert.Equal(T0.AddHours(6), LoanOffers.NextRotation(T0.AddMinutes(5)));
    }

    [Fact]
    public void Offers_DependOnPrestige()
    {
        var small = LoanOffers.For(PrestigeLevel.TheHustle, T0);
        var big = LoanOffers.For(PrestigeLevel.Billionaire, T0);

        Assert.True(big.Min(o => o.Amount) > small.Max(o => o.Amount));
    }

    [Fact]
    public void Find_WithAnIdFromAPastWindow_ReturnsNull()
    {
        var id = LoanOffers.For(PrestigeLevel.TheHustle, T0).First().Id;

        Assert.NotNull(LoanOffers.Find(id, PrestigeLevel.TheHustle, T0.AddHours(1)));
        Assert.Null(LoanOffers.Find(id, PrestigeLevel.TheHustle, T0.AddHours(6)));
    }

    [Fact]
    public void Offer_TotalRepay_IsAmountPlusInterest_AndInstallmentsCoverIt()
    {
        var offer = Offer(100_000m, 0.05m, 4);

        Assert.Equal(105_000m, offer.TotalRepay);
        Assert.Equal(26_250m, offer.InstallmentAmount);
    }

    // -- Taking a loan -------------------------------------------------------------

    [Fact]
    public void TakeLoan_AddsTheAmountToCash_ButNotToEarnings()
    {
        var company = NewCompany(1_000m);

        var result = company.TakeLoan(Offer(), T0);

        Assert.True(result.IsSuccess);
        Assert.Equal(101_000m, company.Cash);
        Assert.Equal(1_000m, company.AllTimeEarnings);
        Assert.Equal(T0 + Interval, company.ActiveLoan!.NextPaymentAt);
    }

    [Fact]
    public void TakeLoan_WhileOneIsActive_Fails()
    {
        var company = NewCompany();
        company.TakeLoan(Offer(), T0);

        var result = company.TakeLoan(Offer(), T0);

        Assert.False(result.IsSuccess);
        Assert.Equal("You already have a loan. Repay it first.", result.Error);
        Assert.Single(company.Loans);
    }

    [Fact]
    public void TakeLoan_ForAnotherPrestigeLevel_Fails()
    {
        var company = NewCompany();
        var offer = Offer() with { PrestigeLevel = PrestigeLevel.Tycoon };

        Assert.False(company.TakeLoan(offer, T0).IsSuccess);
    }

    [Fact]
    public void NetWorth_SubtractsWhatIsStillOwed()
    {
        var company = NewCompany();
        company.TakeLoan(Offer(), T0);

        // 100,000 cash, 105,000 owed.
        Assert.Equal(-5_000m, company.NetWorth);
    }

    // -- Collection ----------------------------------------------------------------

    [Fact]
    public void CollectLoanPayments_BeforeTheFirstDueTime_DoesNothing()
    {
        var company = NewCompany();
        company.TakeLoan(Offer(), T0);

        Assert.Null(company.CollectLoanPayments(T0 + Interval - TimeSpan.FromSeconds(1)));
        Assert.Equal(100_000m, company.Cash);
    }

    [Fact]
    public void CollectLoanPayments_TakesOneInstallmentEverySixHours()
    {
        var company = NewCompany();
        company.TakeLoan(Offer(), T0);

        var collection = company.CollectLoanPayments(T0 + Interval);

        Assert.NotNull(collection);
        Assert.Equal(26_250m, collection.Paid);
        Assert.Equal(0m, collection.Penalty);
        Assert.Equal(73_750m, company.Cash);
        Assert.Equal(78_750m, company.ActiveLoan!.Outstanding);
        Assert.Equal(T0 + 2 * Interval, company.ActiveLoan.NextPaymentAt);
    }

    [Fact]
    public void CollectLoanPayments_AfterTimeAway_CollectsEveryInstallmentDue()
    {
        var company = NewCompany();
        company.TakeLoan(Offer(), T0);

        var collection = company.CollectLoanPayments(T0 + 3 * Interval + TimeSpan.FromHours(1));

        Assert.Equal(3 * 26_250m, collection!.Paid);
        Assert.Equal(26_250m, company.ActiveLoan!.Outstanding);
    }

    [Fact]
    public void CollectLoanPayments_TheLastOne_RepaysTheLoan()
    {
        var company = NewCompany(10_000m);
        company.TakeLoan(Offer(), T0);

        var collection = company.CollectLoanPayments(T0 + 4 * Interval);

        Assert.Equal(T0 + 4 * Interval, collection!.Loan.RepaidAt);
        Assert.Null(company.ActiveLoan);
        Assert.Equal(110_000m - 105_000m, company.Cash);
        Assert.Equal(0m, collection.Loan.Outstanding);
    }

    [Fact]
    public void CollectLoanPayments_WhenCashIsShort_TakesWhatIsThere_AddsAPenalty_AndNeverGoesNegative()
    {
        var company = NewCompany();
        company.TakeLoan(Offer(), T0);
        company.DeductCash(80_000m); // 20,000 left, 26,250 due

        var collection = company.CollectLoanPayments(T0 + Interval);

        Assert.Equal(20_000m, collection!.Paid);
        Assert.Equal(625m, collection.Penalty);          // 10 % of the 6,250 shortfall
        Assert.Equal(0m, company.Cash);
        Assert.Equal(105_000m - 20_000m + 625m, company.ActiveLoan!.Outstanding);
        Assert.Equal(1, company.ActiveLoan.MissedPayments);
    }

    [Fact]
    public void CollectLoanPayments_KeepsCollectingAfterThePlannedInstallments_UntilPenaltiesArePaid()
    {
        var company = NewCompany();
        company.TakeLoan(Offer(), T0);
        company.DeductCash(100_000m); // nothing left at all

        company.CollectLoanPayments(T0 + 4 * Interval);  // four missed installments
        Assert.NotNull(company.ActiveLoan);

        company.AddCash(1_000_000m);
        company.CollectLoanPayments(T0 + 20 * Interval);

        Assert.Null(company.ActiveLoan);
        var loan = Assert.Single(company.Loans);
        Assert.Equal(loan.TotalRepay + loan.Penalties, loan.Paid);
    }

    // -- Repay all -----------------------------------------------------------------

    [Fact]
    public void RepayLoan_PaysEverythingOwed_AndAllowsANewLoan()
    {
        var company = NewCompany(10_000m);
        company.TakeLoan(Offer(), T0);

        var result = company.RepayLoan(T0.AddHours(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(110_000m - 105_000m, company.Cash);
        Assert.Null(company.ActiveLoan);
        Assert.True(company.TakeLoan(Offer(), T0.AddHours(1)).IsSuccess);
    }

    [Fact]
    public void RepayLoan_WithoutEnoughCash_Fails_AndChangesNothing()
    {
        var company = NewCompany();
        company.TakeLoan(Offer(), T0);

        var result = company.RepayLoan(T0.AddHours(1));

        Assert.False(result.IsSuccess);
        Assert.Equal("Insufficient funds.", result.Error);
        Assert.Equal(100_000m, company.Cash);
        Assert.NotNull(company.ActiveLoan);
    }

    [Fact]
    public void RepayLoan_WithNoLoan_Fails()
    {
        Assert.Equal("You have no loan to repay.", NewCompany(5m).RepayLoan(T0).Error);
    }

    [Fact]
    public void AdminReset_DeletesLoans()
    {
        var company = NewCompany();
        company.TakeLoan(Offer(), T0);

        company.AdminReset(T0);

        Assert.Empty(company.Loans);
    }
}

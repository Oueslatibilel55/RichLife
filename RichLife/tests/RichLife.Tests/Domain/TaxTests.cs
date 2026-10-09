using RichLife.Domain;
using RichLife.Domain.Catalogue;
using RichLife.Domain.Entities;

namespace RichLife.Tests.Domain;

public class TaxTests
{
    /// <summary>
    /// A company with one business earning 5/s next to the 3/s base income, anchored at the
    /// business's tax period start (stamped from the system clock when it is opened).
    /// </summary>
    private static (Company Company, Business Business, DateTime T) Setup(decimal cashAfterOpening = 0m)
    {
        var company = Company.Create(Guid.NewGuid(), "Acme");
        company.AddCash(1_000m + cashAfterOpening);
        var business = company.OpenBusiness(CatalogueFixtures.Entry()).Value;
        var t = business.TaxPeriodStart;
        company.AccrueOffline(t);
        // The anchor credited a few microseconds of base income: drop them so sums are exact.
        company.DeductCash(company.Cash - cashAfterOpening);
        return (company, business, t);
    }

    /// <summary>Syncs the most the client could have earned in <paramref name="seconds"/>; returns the time reached.</summary>
    private static DateTime EarnOnline(Company company, DateTime from, int seconds)
    {
        company.AccrueOffline(from);   // the sync window starts here (offline base income is not taxable)
        var to = from.AddSeconds(seconds);
        Assert.True(company.Sync(company.MaxPlausibleCash(to), to).IsSuccess);
        return to;
    }

    [Fact]
    public void Sync_CreditsEachBusinessItsShareOfOnlineEarnings()
    {
        var (company, business, t) = Setup();

        company.Sync(800m, t.AddSeconds(100));   // 8/s for 100 s, under the ceiling

        Assert.Equal(500m, business.TaxableEarnings);          // 5 of every 8
        Assert.Equal(35m, business.TaxAccruing);               // 7 %
        Assert.Equal(0m, business.TaxDue);
    }

    [Fact]
    public void AssessTaxes_BeforeThePeriodEnds_BillsNothing()
    {
        var (company, business, t) = Setup();
        company.Sync(800m, t.AddSeconds(100));

        Assert.Equal(0m, company.AssessTaxes(t.AddHours(23)));
        Assert.Equal(0m, business.TaxDue);
    }

    [Fact]
    public void AssessTaxes_AfterThePeriod_BillsSevenPercentAndStartsANewPeriod()
    {
        var (company, business, t) = Setup();
        company.Sync(800m, t.AddSeconds(100));

        var billed = company.AssessTaxes(t.AddHours(24));

        Assert.Equal(35m, billed);
        Assert.Equal(35m, business.TaxDue);
        Assert.Equal(0m, business.TaxableEarnings);
        Assert.Equal(t.AddHours(24), business.TaxPeriodStart);
        Assert.Equal(800m, company.Cash);   // billed, never taken
    }

    [Fact]
    public void AssessTaxes_AfterSeveralPeriods_BillsOnceAndJumpsToTheCurrentPeriod()
    {
        var (company, business, t) = Setup();
        company.Sync(800m, t.AddSeconds(100));

        company.AssessTaxes(t.AddHours(50));

        Assert.Equal(35m, business.TaxDue);
        Assert.Equal(t.AddHours(48), business.TaxPeriodStart);
    }

    [Fact]
    public void UnpaidBills_AddUp()
    {
        var (company, business, t) = Setup();
        company.Sync(800m, t.AddSeconds(100));
        company.AssessTaxes(t.AddHours(24));

        var now = EarnOnline(company, t.AddHours(30), 100);   // ceiling: 8/s × 100 s × 1.05 = 840
        company.AssessTaxes(t.AddHours(48));

        var secondBill = Math.Round(840m * 5m / 8m * GameConstants.TaxRate, 2);
        Assert.True(now < t.AddHours(48));
        Assert.Equal(35m + secondBill, business.TaxDue);
        Assert.Equal(35m + secondBill, company.TaxesDue);
    }

    [Fact]
    public void Offline_TaxesABusinessOnlyForItsManagedShift()
    {
        var (company, business, t) = Setup(cashAfterOpening: 2_000m);
        Assert.True(company.AutomateBusiness(business.Id, ManagerName.Create(1, "Lucy"), t).IsSuccess);

        company.AccrueOffline(t.AddHours(6));   // capped at 4 h; the shift covers all of it

        Assert.Equal(5m * 4 * 3600, business.TaxableEarnings);
    }

    [Fact]
    public void Offline_WithoutAManager_TheBusinessEarnsNothingTaxable()
    {
        var (company, business, t) = Setup();

        company.AccrueOffline(t.AddHours(2));

        Assert.Equal(0m, business.TaxableEarnings);
    }

    [Fact]
    public void PayBusinessTaxes_TakesTheBillFromCash()
    {
        var (company, business, t) = Setup();
        company.Sync(800m, t.AddSeconds(100));
        company.AssessTaxes(t.AddHours(24));

        var paid = company.PayBusinessTaxes(business.Id);

        Assert.True(paid.IsSuccess);
        Assert.Equal(35m, paid.Value);
        Assert.Equal(765m, company.Cash);
        Assert.Equal(0m, business.TaxDue);
        Assert.Equal(35m, company.TaxesPaid);
    }

    [Fact]
    public void PayBusinessTaxes_Fails_WhenNothingIsDue_OrCashIsShort_OrTheBusinessIsUnknown()
    {
        var (company, business, t) = Setup();
        Assert.Equal("No taxes due.", company.PayBusinessTaxes(business.Id).Error);
        Assert.Equal("Business not found.", company.PayBusinessTaxes(Guid.NewGuid()).Error);

        company.Sync(800m, t.AddSeconds(100));
        company.AssessTaxes(t.AddHours(24));
        company.AdminSetCash(10m);

        Assert.Equal("Insufficient funds.", company.PayBusinessTaxes(business.Id).Error);
        Assert.Equal(35m, business.TaxDue);
        Assert.Equal(10m, company.Cash);
    }

    [Fact]
    public void PayAllTaxes_PaysEveryBusiness()
    {
        var (company, business, t) = Setup(cashAfterOpening: 2_000m);
        var second = company.OpenBusiness(CatalogueFixtures.Entry(id: "taxi", baseIncomePerSecond: 3m)).Value;
        company.AccrueOffline(t);   // re-anchor after opening
        var now = EarnOnline(company, t, 1_000);
        company.AssessTaxes(t.AddHours(25));   // the second business opened a moment later
        Assert.True(business.TaxDue > 0m && second.TaxDue > 0m);
        var due = company.TaxesDue;
        var cash = company.Cash;

        var paid = company.PayAllTaxes();

        Assert.True(paid.IsSuccess);
        Assert.Equal(due, paid.Value);
        Assert.Equal(cash - due, company.Cash);
        Assert.Equal(0m, company.TaxesDue);
        Assert.True(now > t);
    }

    [Fact]
    public void PayAllTaxes_Fails_WhenNothingIsDue()
    {
        var (company, _, _) = Setup();

        Assert.Equal("No taxes due.", company.PayAllTaxes().Error);
    }

    [Fact]
    public void Prestige_WaitsUntilEveryTaxIsPaid()
    {
        var (company, business, t) = Setup();
        company.Sync(800m, t.AddSeconds(100));
        company.AssessTaxes(t.AddHours(24));
        company.AdminSetCash(30_000m);

        Assert.Equal("Pay your taxes before prestige.", company.Prestige(t.AddHours(24)).Error);

        Assert.True(company.PayBusinessTaxes(business.Id).IsSuccess);
        Assert.True(company.Prestige(t.AddHours(24)).IsSuccess);
    }

    [Fact]
    public void CloseBusiness_WithTaxDue_IsRefused()
    {
        var (company, business, t) = Setup();
        company.Sync(800m, t.AddSeconds(100));
        company.AssessTaxes(t.AddHours(24));

        Assert.Equal("Pay this business's taxes before closing it.", company.CloseBusiness(business.Id).Error);

        company.PayBusinessTaxes(business.Id);
        Assert.True(company.CloseBusiness(business.Id).IsSuccess);
    }

    [Fact]
    public void NetWorth_SubtractsTaxesDue()
    {
        var (company, business, t) = Setup();
        company.Sync(800m, t.AddSeconds(100));
        var before = company.NetWorth;

        company.AssessTaxes(t.AddHours(24));

        Assert.Equal(before - 35m, company.NetWorth);
        Assert.Equal(35m, business.TaxDue);
    }
}

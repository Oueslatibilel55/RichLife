using RichLife.Domain;
using RichLife.Domain.Entities;
using RichLife.Domain.Store;

namespace RichLife.Tests.Domain;

public class StoreTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Base income only: 3/s, no businesses, prestige ×1.</summary>
    private static Company NewCompany(decimal cash = 0m)
    {
        var company = Company.Create(Guid.NewGuid(), "Acme");
        company.AccrueOffline(T0);
        company.AddCash(cash);
        return company;
    }

    private static Company WithDiamonds(int diamonds)
    {
        var company = NewCompany();
        company.AdminAdjustDiamonds(diamonds - company.Diamonds, "test", T0);
        return company;
    }

    // -- Earning ----------------------------------------------------------------------

    [Fact]
    public void Create_GivesTheWelcomeDiamonds_WithALedgerLine()
    {
        var company = NewCompany();

        Assert.Equal(GameConstants.StartingDiamonds, company.Diamonds);
        var line = Assert.Single(company.DiamondLedger);
        Assert.Equal(DiamondReasons.Welcome, line.Reason);
        Assert.Equal(25, line.Balance);
    }

    [Fact]
    public void UnlockAchievements_PaysDiamondsForEachOne()
    {
        var company = NewCompany(1_000m);   // "earn-1k"

        var fresh = company.UnlockAchievements(T0);

        Assert.Single(fresh);
        Assert.Equal(25 + GameConstants.AchievementDiamonds, company.Diamonds);
        Assert.Equal("earn-1k", company.DiamondLedger[^1].Detail);
    }

    [Fact]
    public void Prestige_PaysDiamondsByNewLevel()
    {
        var company = NewCompany(25_000m);

        Assert.True(company.Prestige(T0).IsSuccess);

        Assert.Equal(25 + 2 * GameConstants.PrestigeDiamondsPerLevel, company.Diamonds);
    }

    // -- Boost ------------------------------------------------------------------------

    [Fact]
    public void BuyBoost_SpendsDiamonds_AndRunsForTheHours()
    {
        var company = WithDiamonds(100);

        Assert.True(company.BuyBoost(1, T0).IsSuccess);

        Assert.Equal(75, company.Diamonds);
        Assert.Equal(T0.AddHours(1), company.BoostUntil);
        Assert.True(company.IsBoostedAt(T0.AddMinutes(59)));
        Assert.False(company.IsBoostedAt(T0.AddHours(1)));
    }

    [Fact]
    public void BuyBoost_WhileOneRuns_AddsTime()
    {
        var company = WithDiamonds(200);
        company.BuyBoost(1, T0);

        company.BuyBoost(3, T0.AddMinutes(30));

        Assert.Equal(T0.AddHours(4), company.BoostUntil);
    }

    [Fact]
    public void BuyBoost_PastTwentyFourHours_Fails_AndCostsNothing()
    {
        var company = WithDiamonds(1_000);
        company.BuyBoost(8, T0);
        company.BuyBoost(8, T0);
        company.BuyBoost(8, T0);   // 24 h

        var result = company.BuyBoost(1, T0);

        Assert.Equal("A boost can run at most 24 hours ahead.", result.Error);
        Assert.Equal(1_000 - 3 * 140, company.Diamonds);
    }

    [Theory]
    [InlineData(2, "Unknown boost.")]
    [InlineData(8, "Not enough diamonds.")]
    public void BuyBoost_Rejects(int hours, string error)
    {
        var company = NewCompany();   // 25 diamonds

        Assert.Equal(error, company.BuyBoost(hours, T0).Error);
        Assert.Null(company.BoostUntil);
    }

    [Fact]
    public void Boost_DoublesOfflineIncome_ForTheBoostedPartOnly()
    {
        var company = WithDiamonds(25);
        company.BuyBoost(1, T0);

        var earned = company.AccrueOffline(T0.AddHours(2));

        // 3/s for 2 h, plus another 3/s for the boosted hour.
        Assert.Equal(3m * (7_200 + 3_600), earned);
    }

    [Fact]
    public void Boost_RaisesTheSyncCeiling()
    {
        var company = WithDiamonds(25);
        company.BuyBoost(1, T0);

        Assert.Equal(3m * 20 * GameConstants.SyncTolerance, company.MaxPlausibleCash(T0.AddSeconds(10)));
    }

    // -- Offline double ---------------------------------------------------------------

    [Fact]
    public void DoubleOfflineEarnings_PaysThemAgain_Once()
    {
        var company = WithDiamonds(30);
        var earned = company.AccrueOffline(T0.AddHours(1));
        company.OfferOfflineDouble(earned, T0.AddHours(1));

        Assert.True(company.DoubleOfflineEarnings(T0.AddHours(1)).IsSuccess);

        Assert.Equal(2 * earned, company.Cash);
        Assert.Equal(2 * earned, company.AllTimeEarnings);
        Assert.Equal(15, company.Diamonds);
        Assert.Equal("No offline earnings to double.", company.DoubleOfflineEarnings(T0.AddHours(1)).Error);
    }

    [Fact]
    public void DoubleOfflineEarnings_AfterTheWindow_Fails()
    {
        var company = WithDiamonds(30);
        company.OfferOfflineDouble(500m, T0);

        var result = company.DoubleOfflineEarnings(T0 + GameConstants.OfflineDoubleWindow + TimeSpan.FromSeconds(1));

        Assert.False(result.IsSuccess);
        Assert.Equal(30, company.Diamonds);
    }

    // -- Exchange ---------------------------------------------------------------------

    [Fact]
    public void ExchangeDiamonds_AddsCashAtThePrestigeRate_ButNotEarnings()
    {
        var company = NewCompany();

        Assert.True(company.ExchangeDiamonds(10, T0).IsSuccess);

        Assert.Equal(10 * GameConstants.DiamondCashValue(company.PrestigeLevel), company.Cash);
        Assert.Equal(0m, company.AllTimeEarnings);
        Assert.Equal(15, company.Diamonds);
    }

    [Theory]
    [InlineData(0, "Choose at least 1 diamond.")]
    [InlineData(26, "Not enough diamonds.")]
    public void ExchangeDiamonds_Rejects(int diamonds, string error)
        => Assert.Equal(error, NewCompany().ExchangeDiamonds(diamonds, T0).Error);

    // -- Badges -----------------------------------------------------------------------

    [Fact]
    public void BuyBadge_OwnsIt_AndTheFirstBecomesFeatured()
    {
        var company = WithDiamonds(100);
        var badge = BadgeCatalog.Find("lucky-clover")!;

        Assert.True(company.BuyBadge(badge, T0).IsSuccess);
        Assert.True(company.BuyBadge(BadgeCatalog.Find("rising-star")!, T0).IsSuccess);

        Assert.Equal(100 - 50 - 20, company.Diamonds);
        Assert.Equal(2, company.Badges.Count);
        Assert.Equal("lucky-clover", company.FeaturedBadgeId);
        Assert.Equal("You already own this badge.", company.BuyBadge(badge, T0).Error);
    }

    [Fact]
    public void FeatureBadge_OnlyAnOwnedOne_OrNone()
    {
        var company = WithDiamonds(100);
        company.BuyBadge(BadgeCatalog.Find("rising-star")!, T0);

        Assert.Equal("You do not own this badge.", company.FeatureBadge("crown").Error);
        Assert.True(company.FeatureBadge(null).IsSuccess);
        Assert.Null(company.FeaturedBadgeId);
    }

    [Fact]
    public void BadgeCatalog_HasUniqueIds_AndRaritiesFollowPrice()
    {
        Assert.Equal(BadgeCatalog.All.Count, BadgeCatalog.All.Select(b => b.Id).Distinct().Count());
        Assert.Equal("common", BadgeCatalog.Find("rising-star")!.Rarity);
        Assert.Equal("legendary", BadgeCatalog.Find("crown")!.Rarity);
    }

    // -- Admin ------------------------------------------------------------------------

    [Theory]
    [InlineData(0, "Amount cannot be zero.")]
    [InlineData(-26, "Diamonds cannot go below zero.")]
    public void AdminAdjustDiamonds_Rejects(int amount, string error)
        => Assert.Equal(error, NewCompany().AdminAdjustDiamonds(amount, null, T0).Error);

    [Fact]
    public void AdminReset_KeepsDiamondsAndBadges_ButStopsTheBoost()
    {
        var company = WithDiamonds(100);
        company.BuyBadge(BadgeCatalog.Find("rising-star")!, T0);
        company.BuyBoost(1, T0);

        company.AdminReset(T0);

        Assert.Equal(55, company.Diamonds);
        Assert.Single(company.Badges);
        Assert.Null(company.BoostUntil);
    }
}

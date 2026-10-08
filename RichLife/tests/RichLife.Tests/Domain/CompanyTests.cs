using RichLife.Domain;
using RichLife.Domain.Achievements;
using RichLife.Domain.Catalogue;
using RichLife.Domain.Entities;
using RichLife.Domain.Enums;
using RichLife.Domain.Events;

namespace RichLife.Tests.Domain;

public class CompanyTests
{
    /// <summary>Fixed instant the fixtures pin LastSyncAt to, so accrual is deterministic.</summary>
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static readonly ManagerName Lucy = ManagerName.Create(1, "Lucy");

    private static Company NewCompany()
    {
        var company = Company.Create(Guid.NewGuid(), "Acme");
        // T0 is before the creation timestamp, so this credits nothing and simply
        // pins the sync watermark.
        company.AccrueOffline(T0);
        return company;
    }

    private static BusinessCatalogueEntry NewEntry(
        string catalogueId = "food-cart",
        decimal openingCost = 1_000m,
        decimal grossIncomePerSecond = 5m) =>
        CatalogueFixtures.Entry(catalogueId, openingCost, grossIncomePerSecond);

    // -- Creation -----------------------------------------------------------

    [Fact]
    public void Create_StartsAtTheHustle_WithBasePassiveIncome()
    {
        var company = NewCompany();

        Assert.Equal(PrestigeLevel.TheHustle, company.PrestigeLevel);
        Assert.Equal(GameConstants.BasePassiveIncomePerSecond, company.PassiveIncomePerSecond);
        Assert.Equal(0m, company.Cash);
        Assert.Equal(1m, company.PrestigeMultiplier);
    }

    // -- Cash ---------------------------------------------------------------

    [Fact]
    public void DeductCash_WithInsufficientFunds_Fails_AndLeavesCashUntouched()
    {
        var company = NewCompany();

        var result = company.DeductCash(100m);

        Assert.False(result.IsSuccess);
        Assert.Equal("Insufficient funds.", result.Error);
        Assert.Equal(0m, company.Cash);
    }

    [Fact]
    public void AddCash_IgnoresNonPositiveAmounts()
    {
        var company = NewCompany();

        company.AddCash(-50m);
        company.AddCash(0m);

        Assert.Equal(0m, company.Cash);
        Assert.Equal(0m, company.AllTimeEarnings);
    }

    // -- Offline progress ---------------------------------------------------

    [Fact]
    public void ApplyOfflineProgress_CapsAtTheOfflineLimit()
    {
        var company = NewCompany();

        var earned = company.ApplyOfflineProgress(TimeSpan.FromHours(10));

        // Capped at 4h, at the $3/s base rate with a 1x multiplier.
        Assert.Equal(3m * (decimal)GameConstants.OfflineCap.TotalSeconds, earned);
        Assert.Equal(earned, company.Cash);
    }

    [Fact]
    public void ApplyOfflineProgress_BelowCap_UsesActualElapsed()
    {
        var company = NewCompany();

        company.ApplyOfflineProgress(TimeSpan.FromMinutes(30));

        Assert.Equal(3m * 1_800m, company.Cash);
    }

    [Fact]
    public void ApplyOfflineProgress_CountsOnlyAutomatedBusinesses()
    {
        var company = NewCompany();
        company.AddCash(10_000m);
        var business = company.OpenBusiness(NewEntry(grossIncomePerSecond: 5m)).Value;

        var manual = company.ApplyOfflineProgress(TimeSpan.FromMinutes(1));

        // Only the $3/s passive income runs while away.
        Assert.Equal(3m * 60m, manual);

        business.HireManager(Lucy, T0);
        var automated = company.ApplyOfflineProgress(TimeSpan.FromMinutes(1));

        // Now the business's $5/s contributes too.
        Assert.Equal(8m * 60m, automated);
    }

    [Fact]
    public void AccrueOffline_CreditsOnce_AndDoesNotDoubleCreditTheSameWindow()
    {
        var company = NewCompany();

        var first = company.AccrueOffline(T0.AddHours(1));
        var second = company.AccrueOffline(T0.AddHours(1));

        Assert.Equal(3m * 3_600m, first);
        Assert.Equal(0m, second);
        Assert.Equal(first, company.Cash);
        Assert.Equal(T0.AddHours(1), company.LastSyncAt);
    }

    // -- Client sync --------------------------------------------------------

    [Fact]
    public void Sync_ClampsCashToWhatCouldPlausiblyHaveBeenEarned()
    {
        var company = NewCompany();

        var result = company.Sync(999_999_999m, T0.AddSeconds(10));

        // 10s at $3/s, plus the tolerance factor.
        var ceiling = 3m * 10m * GameConstants.SyncTolerance;
        Assert.True(result.IsSuccess);
        Assert.Equal(ceiling, company.Cash);
    }

    [Fact]
    public void Sync_AcceptsAPlausibleFigureUnchanged()
    {
        var company = NewCompany();

        company.Sync(25m, T0.AddSeconds(10));

        Assert.Equal(25m, company.Cash);
        Assert.Equal(25m, company.AllTimeEarnings);
        Assert.Equal(T0.AddSeconds(10), company.LastSyncAt);
    }

    [Fact]
    public void Sync_RejectsNegativeCash()
    {
        var company = NewCompany();

        var result = company.Sync(-1m, T0.AddSeconds(10));

        Assert.False(result.IsSuccess);
        Assert.Equal(0m, company.Cash);
    }

    // -- Net worth ----------------------------------------------------------

    [Fact]
    public void NetWorth_IncludesTheValueOfOwnedBusinesses()
    {
        var company = NewCompany();
        company.AddCash(10_000m);
        company.OpenBusiness(NewEntry(openingCost: 1_000m));

        // 10,000 - 1,000 spent, plus the business's 1,000 of value.
        Assert.Equal(9_000m, company.Cash);
        Assert.Equal(10_000m, company.NetWorth);
    }

    // -- Opening and closing ------------------------------------------------

    [Fact]
    public void OpenBusiness_DeductsCost_AndRaisesEvent()
    {
        var company = NewCompany();
        company.AddCash(10_000m);

        var result = company.OpenBusiness(NewEntry(openingCost: 1_000m));

        Assert.True(result.IsSuccess);
        Assert.Equal(9_000m, company.Cash);
        Assert.Single(company.Businesses);
        Assert.Contains(company.DomainEvents, e => e is BusinessOpenedEvent);
    }

    [Fact]
    public void OpenBusiness_RejectsASecondCopyOfTheSameCatalogueEntry()
    {
        var company = NewCompany();
        company.AddCash(10_000m);
        company.OpenBusiness(NewEntry("food-cart"));

        var result = company.OpenBusiness(NewEntry("food-cart"));

        Assert.False(result.IsSuccess);
        Assert.Equal("You already own this business.", result.Error);
        Assert.Single(company.Businesses);
        Assert.Equal(9_000m, company.Cash); // the second attempt cost nothing
    }

    [Fact]
    public void OpenBusiness_WithInsufficientFunds_Fails()
    {
        var company = NewCompany();

        var result = company.OpenBusiness(NewEntry(openingCost: 1_000m));

        Assert.False(result.IsSuccess);
        Assert.Empty(company.Businesses);
    }

    [Fact]
    public void OpenBusiness_AboveTheCompanysPrestige_Fails()
    {
        var company = NewCompany();
        company.AddCash(10_000m);
        var entry = CatalogueFixtures.Entry(requiredPrestige: PrestigeLevel.SmallBusiness);

        var result = company.OpenBusiness(entry);

        Assert.False(result.IsSuccess);
        Assert.Equal("Requires prestige SmallBusiness.", result.Error);
        Assert.Equal(10_000m, company.Cash);
    }

    [Fact]
    public void OpenBusiness_FromARetiredEntry_IsReportedAsNotInTheCatalogue()
    {
        var company = NewCompany();
        company.AddCash(10_000m);
        var entry = NewEntry();
        entry.Update(CatalogueFixtures.Details(), isActive: false);

        var result = company.OpenBusiness(entry);

        Assert.False(result.IsSuccess);
        Assert.Equal("Business not found in catalogue.", result.Error);
        Assert.Empty(company.Businesses);
    }

    [Fact]
    public void OpenBusiness_SnapshotsTheEntry_SoLaterEditsDoNotTouchOwnedBusinesses()
    {
        var company = NewCompany();
        company.AddCash(10_000m);
        var entry = NewEntry(openingCost: 1_000m, grossIncomePerSecond: 5m);

        var business = company.OpenBusiness(entry).Value;
        entry.Update(CatalogueFixtures.Details(openingCost: 9_000m, baseIncomePerSecond: 50m), isActive: true);

        Assert.Equal("food-cart", business.CatalogueId);
        Assert.Equal(1_000m, business.OpeningCost);
        Assert.Equal(5m, business.GrossIncomePerSecond);
    }

    // -- Buying assets ------------------------------------------------------

    [Fact]
    public void BuyAsset_DeductsThePrice_AndAddsTheAssetsIncome()
    {
        var company = NewCompany();
        company.AddCash(10_000m);
        var entry = NewEntry(openingCost: 1_000m, grossIncomePerSecond: 5m);
        entry.AddAsset("menu-item", CatalogueFixtures.AssetDetails(price: 500m, fixedIncomePerSecond: 2m));
        var business = company.OpenBusiness(entry).Value;

        var result = company.BuyAsset(business.Id, entry, "menu-item");

        Assert.True(result.IsSuccess);
        Assert.Equal(8_500m, company.Cash);
        Assert.Equal(7m, business.GrossIncomePerSecond);
        Assert.Equal(1_500m, business.TotalValue);
    }

    [Fact]
    public void BuyAsset_NotListedForThatBusiness_Fails()
    {
        var company = NewCompany();
        company.AddCash(10_000m);
        var entry = NewEntry();
        var business = company.OpenBusiness(entry).Value;

        var result = company.BuyAsset(business.Id, entry, "truck");

        Assert.False(result.IsSuccess);
        Assert.Equal("Asset not available for this business.", result.Error);
    }

    [Fact]
    public void BuyAsset_BeforeItUnlocks_Fails_AndChargesNothing()
    {
        var company = NewCompany();
        company.AddCash(10_000m);
        var entry = NewEntry();
        entry.AddAsset("premium-dish", CatalogueFixtures.AssetDetails(unlockAtAssetCount: 2));
        var business = company.OpenBusiness(entry).Value;
        var cashBefore = company.Cash;

        var result = company.BuyAsset(business.Id, entry, "premium-dish");

        Assert.False(result.IsSuccess);
        Assert.Equal("Need 2 assets to unlock this.", result.Error);
        Assert.Equal(cashBefore, company.Cash);
    }

    [Fact]
    public void BuyAsset_WithInsufficientFunds_Fails()
    {
        var company = NewCompany();
        company.AddCash(1_000m);
        var entry = NewEntry(openingCost: 1_000m);
        entry.AddAsset("menu-item", CatalogueFixtures.AssetDetails(price: 500m));
        var business = company.OpenBusiness(entry).Value;

        var result = company.BuyAsset(business.Id, entry, "menu-item");

        Assert.False(result.IsSuccess);
        Assert.Equal("Insufficient funds.", result.Error);
        Assert.Empty(business.Assets);
    }

    [Fact]
    public void BuyAsset_StillWorksOnARetiredEntry()
    {
        var company = NewCompany();
        company.AddCash(10_000m);
        var entry = NewEntry();
        entry.AddAsset("menu-item", CatalogueFixtures.AssetDetails());
        var business = company.OpenBusiness(entry).Value;
        entry.Update(CatalogueFixtures.Details(), isActive: false);

        Assert.True(company.BuyAsset(business.Id, entry, "menu-item").IsSuccess);
    }

    [Fact]
    public void BuyAsset_ForAnUnknownBusiness_Fails()
    {
        var company = NewCompany();

        var result = company.BuyAsset(Guid.NewGuid(), NewEntry(), "menu-item");

        Assert.False(result.IsSuccess);
        Assert.Equal("Business not found.", result.Error);
    }

    [Fact]
    public void BuyAsset_WithAnotherBusinesssEntry_IsAProgrammerError()
    {
        var company = NewCompany();
        company.AddCash(10_000m);
        var business = company.OpenBusiness(NewEntry("food-cart")).Value;

        Assert.Throws<ArgumentException>(() => company.BuyAsset(business.Id, NewEntry("car-wash"), "menu-item"));
    }

    [Fact]
    public void CloseBusiness_RefundsOpeningCostAndAssets_MinusTheGracefulFee()
    {
        var company = NewCompany();
        company.AddCash(10_000m);
        var business = company.OpenBusiness(NewEntry(openingCost: 1_000m)).Value;
        business.AddAsset(BusinessAsset.Create(business.Id, "Truck", 500m, 0.15m));

        var cashBefore = company.Cash;
        var result = company.CloseBusiness(business.Id);

        Assert.True(result.IsSuccess);
        // (1,000 opening + 500 asset) * 90%
        Assert.Equal(cashBefore + 1_350m, company.Cash);
        Assert.Empty(company.Businesses);
    }

    [Fact]
    public void CloseBusiness_EmergencyChargesTheHigherFee()
    {
        var company = NewCompany();
        company.AddCash(10_000m);
        var business = company.OpenBusiness(NewEntry(openingCost: 1_000m)).Value;

        var cashBefore = company.Cash;
        company.CloseBusiness(business.Id, emergency: true);

        Assert.Equal(cashBefore + 750m, company.Cash); // 1,000 * 75%
    }

    [Fact]
    public void CloseBusiness_RefundIsNotCountedAsEarnings()
    {
        var company = NewCompany();
        company.AddCash(10_000m);
        var business = company.OpenBusiness(NewEntry(openingCost: 1_000m)).Value;

        company.CloseBusiness(business.Id);

        Assert.Equal(10_000m, company.AllTimeEarnings);
    }

    [Fact]
    public void CloseBusiness_WithUnknownId_Fails()
    {
        var company = NewCompany();

        var result = company.CloseBusiness(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal("Business not found.", result.Error);
    }

    // -- Managers -----------------------------------------------------------

    [Fact]
    public void AutomateBusiness_ChargesTwiceTheOpeningCost_AndAutomates()
    {
        var company = NewCompany();
        company.AddCash(5_000m);
        var business = company.OpenBusiness(NewEntry(openingCost: 1_000m)).Value;

        var result = company.AutomateBusiness(business.Id, Lucy, T0);

        Assert.True(result.IsSuccess);
        Assert.True(business.HasManagerAt(T0));
        Assert.Equal(2_000m, business.ManagerCost);
        Assert.Equal("Lucy", business.ManagerName);
        Assert.Equal(2_000m, company.Cash); // 5,000 − 1,000 opening − 2,000 manager
    }

    [Fact]
    public void AutomateBusiness_MakesTheBusinessEarnOffline()
    {
        var company = NewCompany();
        company.AddCash(3_000m);
        var business = company.OpenBusiness(NewEntry(openingCost: 1_000m, grossIncomePerSecond: 5m)).Value;

        company.AutomateBusiness(business.Id, Lucy, T0);
        var earned = company.ApplyOfflineProgress(TimeSpan.FromMinutes(1));

        Assert.Equal(8m * 60m, earned); // $3/s passive + $5/s business
    }

    [Fact]
    public void AutomateBusiness_Twice_Fails_AndChargesOnce()
    {
        var company = NewCompany();
        company.AddCash(10_000m);
        var business = company.OpenBusiness(NewEntry(openingCost: 1_000m)).Value;
        company.AutomateBusiness(business.Id, Lucy, T0);

        var result = company.AutomateBusiness(business.Id, Lucy, T0);

        Assert.False(result.IsSuccess);
        Assert.Equal("Business already has a manager.", result.Error);
        Assert.Equal(7_000m, company.Cash);
    }

    [Fact]
    public void AutomateBusiness_WithInsufficientFunds_ChangesNothing()
    {
        var company = NewCompany();
        company.AddCash(2_500m);
        var business = company.OpenBusiness(NewEntry(openingCost: 1_000m)).Value;

        var result = company.AutomateBusiness(business.Id, Lucy, T0);

        Assert.False(result.IsSuccess);
        Assert.Equal("Insufficient funds.", result.Error);
        Assert.False(business.HasManagerAt(T0));
        Assert.Equal(1_500m, company.Cash);
    }

    [Fact]
    public void AutomateBusiness_UnknownBusiness_Fails()
    {
        var company = NewCompany();

        var result = company.AutomateBusiness(Guid.NewGuid(), Lucy, T0);

        Assert.False(result.IsSuccess);
        Assert.Equal("Business not found.", result.Error);
    }

    [Fact]
    public void AutomateBusiness_DoesNotCountAsEarnings()
    {
        var company = NewCompany();
        company.AddCash(5_000m);
        var business = company.OpenBusiness(NewEntry(openingCost: 1_000m)).Value;

        company.AutomateBusiness(business.Id, Lucy, T0);

        Assert.Equal(5_000m, company.AllTimeEarnings);
    }

    // -- Luxury ---------------------------------------------------------------

    private static LuxuryCatalogueEntry Watch(PrestigeLevel level = PrestigeLevel.TheHustle, decimal price = 10_000m) =>
        LuxuryCatalogueEntry.Create("rolex", "Rolex", LuxuryCategory.Watch, price, level, "/luxury/rolex.jpg", "Someone · CC0");

    [Fact]
    public void BuyLuxury_ChargesThePrice_KeepsNetWorth_AndRecordsTheItem()
    {
        var company = NewCompany();
        company.AddCash(25_000m);

        var result = company.BuyLuxury(Watch(), T0);

        Assert.True(result.IsSuccess);
        Assert.Equal(15_000m, company.Cash);
        Assert.Equal(25_000m, company.NetWorth); // cash turned into a 10,000 watch
        var owned = Assert.Single(company.LuxuryAssets);
        Assert.Equal("rolex", owned.CatalogueId);
        Assert.Equal("/luxury/rolex.jpg", owned.ImageUrl);
        Assert.Equal(T0, owned.CreatedAt);
        Assert.Equal(25_000m, company.AllTimeEarnings); // spending is not lost earnings
    }

    [Fact]
    public void BuyLuxury_AboveTheCompanysPrestige_Fails()
    {
        var company = NewCompany();
        company.AddCash(1_000_000m);

        var result = company.BuyLuxury(Watch(PrestigeLevel.BusinessMogul), T0);

        Assert.False(result.IsSuccess);
        Assert.Equal("Requires prestige BusinessMogul.", result.Error);
        Assert.Equal(1_000_000m, company.Cash);
    }

    [Fact]
    public void BuyLuxury_Twice_Fails_AndChargesOnce()
    {
        var company = NewCompany();
        company.AddCash(50_000m);
        company.BuyLuxury(Watch(), T0);

        var result = company.BuyLuxury(Watch(), T0);

        Assert.False(result.IsSuccess);
        Assert.Equal("You already own this.", result.Error);
        Assert.Equal(40_000m, company.Cash);
    }

    [Fact]
    public void BuyLuxury_WithInsufficientFunds_ChangesNothing()
    {
        var company = NewCompany();
        company.AddCash(5_000m);

        var result = company.BuyLuxury(Watch(), T0);

        Assert.False(result.IsSuccess);
        Assert.Equal("Insufficient funds.", result.Error);
        Assert.Empty(company.LuxuryAssets);
    }

    [Fact]
    public void BuyLuxury_RetiredItem_Fails()
    {
        var company = NewCompany();
        company.AddCash(50_000m);
        var retired = LuxuryCatalogueEntry.Create("old", "Old", LuxuryCategory.Car, 1_000m, PrestigeLevel.TheHustle, isActive: false);

        Assert.Equal("Item not found.", company.BuyLuxury(retired, T0).Error);
    }

    [Fact]
    public void BuyLuxury_UnlocksLivingLarge_AndAdminResetClearsTheCollection()
    {
        var company = NewCompany();
        company.AddCash(50_000m);
        company.BuyLuxury(Watch(), T0);

        Assert.Contains(company.UnlockAchievements(T0), a => a.Code == "first-luxury");

        company.AdminReset(T0.AddHours(1));
        Assert.Empty(company.LuxuryAssets);
    }

    // -- Achievements ---------------------------------------------------------

    [Fact]
    public void Achievements_NewCompany_HasNone()
    {
        var company = NewCompany();

        Assert.Empty(company.UnlockAchievements(T0));
        Assert.Empty(company.Achievements);
    }

    [Fact]
    public void Achievements_UnlockOnceWhenMet_AndRecordTheTime()
    {
        var company = NewCompany();
        company.AddCash(1_000m);

        var first = company.UnlockAchievements(T0);
        var again = company.UnlockAchievements(T0.AddMinutes(1));

        Assert.Contains(first, a => a.Code == "earn-1k");
        Assert.Empty(again);
        Assert.Equal(T0, company.Achievements.Single(a => a.Code == "earn-1k").UnlockedAt);
    }

    [Fact]
    public void Achievements_StayUnlocked_WhenTheMetricDrops()
    {
        var company = NewCompany();
        company.AddCash(5_000m);
        var business = company.OpenBusiness(NewEntry(openingCost: 1_000m)).Value;
        Assert.Contains(company.UnlockAchievements(T0), a => a.Code == "first-business");

        company.CloseBusiness(business.Id);
        company.UnlockAchievements(T0.AddMinutes(1));

        Assert.Contains(company.Achievements, a => a.Code == "first-business");
    }

    [Fact]
    public void Achievements_ManagerAndLevelMetrics()
    {
        var company = NewCompany();
        company.AddCash(1_000_000m);
        var business = company.OpenBusiness(NewEntry(openingCost: 1_000m)).Value;
        company.AutomateBusiness(business.Id, Lucy, T0);
        for (var i = 1; i < 10; i++) company.LevelUpBusiness(business.Id);

        var codes = company.UnlockAchievements(T0).Select(a => a.Code).ToList();

        Assert.Contains("first-manager", codes);
        Assert.Contains("level-10", codes);
        Assert.Equal(10m, company.AchievementMetricValue(AchievementMetric.HighestBusinessLevel));
    }

    [Fact]
    public void AdminReset_ClearsAchievements()
    {
        var company = NewCompany();
        company.AddCash(1_000m);
        company.UnlockAchievements(T0);

        company.AdminReset(T0.AddHours(1));

        Assert.Empty(company.Achievements);
    }

    [Fact]
    public void AchievementCatalog_CodesAreUnique()
    {
        var codes = AchievementCatalog.All.Select(a => a.Code).ToList();
        Assert.Equal(codes.Count, codes.Distinct().Count());
    }

    // -- Admin --------------------------------------------------------------

    [Fact]
    public void AdminSetCash_SetsCash_WithoutCountingAsEarnings()
    {
        var company = NewCompany();
        company.AddCash(100m);

        var result = company.AdminSetCash(50_000m);

        Assert.True(result.IsSuccess);
        Assert.Equal(50_000m, company.Cash);
        Assert.Equal(100m, company.AllTimeEarnings);
        Assert.True(company.CashOverridePending);
    }

    [Fact]
    public void AdminSetCash_Negative_Fails()
    {
        var company = NewCompany();

        var result = company.AdminSetCash(-1m);

        Assert.False(result.IsSuccess);
        Assert.Equal("Cash cannot be negative.", result.Error);
    }

    [Fact]
    public void Sync_AfterAnAdminCashChange_KeepsTheAdminFigure_Once()
    {
        var company = NewCompany();
        company.AdminSetCash(50_000m);

        // The online client still believes it has 120 and reports it.
        company.Sync(120m, T0.AddSeconds(5));

        Assert.Equal(50_000m, company.Cash);
        Assert.False(company.CashOverridePending);

        // The override is spent: normal clamping applies again.
        company.Sync(50_010m, T0.AddSeconds(10));
        Assert.Equal(50_010m, company.Cash);
    }

    [Fact]
    public void AdminReset_GivesAFreshStart_AndKeepsTheCompany()
    {
        var company = NewCompany();
        company.AddCash(40_000m);
        company.OpenBusiness(NewEntry(openingCost: 1_000m));
        company.Prestige(T0);

        company.AdminReset(T0.AddHours(1));

        Assert.Equal(0m, company.Cash);
        Assert.Equal(0m, company.AllTimeEarnings);
        Assert.Empty(company.Businesses);
        Assert.Equal(PrestigeLevel.TheHustle, company.PrestigeLevel);
        Assert.Equal(0, company.PrestigeCount);
        Assert.Equal(GameConstants.BasePassiveIncomePerSecond, company.PassiveIncomePerSecond);
        Assert.Equal("Acme", company.Name);
        Assert.True(company.CashOverridePending);
    }

    [Fact]
    public void Player_SetAdmin_GrantsAndRevokes()
    {
        var player = Player.Create("bilel", "B@X.com", "hash", "TN");

        player.SetAdmin(true);
        Assert.True(player.IsAdmin);
        player.SetAdmin(false);
        Assert.False(player.IsAdmin);
    }

    // -- Manager shifts -----------------------------------------------------

    [Fact]
    public void Offline_HireThenPlayOneHourThenAwaySixHours_PaysBaseFourHours_AndTheBusinessThreeHours()
    {
        // The player's own example: hire at 11:00 (shift until 15:00), play until 12:00,
        // come back at 18:00. The paid window is 12:00-16:00 (4 h cap); the shift covers
        // 12:00-15:00 of it.
        var company = NewCompany(); // LastSyncAt = T0 = "12:00", when the player left
        company.AddCash(5_000m);
        var business = company.OpenBusiness(NewEntry(openingCost: 1_000m, grossIncomePerSecond: 5m)).Value;
        company.AutomateBusiness(business.Id, Lucy, T0.AddHours(-1));
        var cashBefore = company.Cash;

        var earned = company.ApplyOfflineProgress(TimeSpan.FromHours(6));

        Assert.Equal(3m * 4 * 3_600 + 5m * 3 * 3_600, earned); // base 4 h + business 3 h
        Assert.Equal(cashBefore + earned, company.Cash);
    }

    [Fact]
    public void Offline_AfterTheShiftEnded_PaysOnlyBaseIncome()
    {
        var company = NewCompany();
        company.AddCash(5_000m);
        var business = company.OpenBusiness(NewEntry(openingCost: 1_000m, grossIncomePerSecond: 5m)).Value;
        company.AutomateBusiness(business.Id, Lucy, T0.AddHours(-5)); // shift ended 1 h before T0

        var earned = company.ApplyOfflineProgress(TimeSpan.FromHours(2));

        Assert.Equal(3m * 2 * 3_600, earned);
        Assert.False(business.HasManagerAt(T0));
    }

    [Fact]
    public void Offline_ShiftCoveringTheWholeWindow_PaysTheBusinessForAllOfIt()
    {
        var company = NewCompany();
        company.AddCash(5_000m);
        var business = company.OpenBusiness(NewEntry(openingCost: 1_000m, grossIncomePerSecond: 5m)).Value;
        company.AutomateBusiness(business.Id, Lucy, T0);

        var earned = company.ApplyOfflineProgress(TimeSpan.FromHours(1));

        Assert.Equal(8m * 3_600, earned); // (3 + 5) × 1 h
    }

    [Fact]
    public void AutomateBusiness_WhileAShiftRuns_Fails()
    {
        var company = NewCompany();
        company.AddCash(10_000m);
        var business = company.OpenBusiness(NewEntry(openingCost: 1_000m)).Value;
        company.AutomateBusiness(business.Id, Lucy, T0);

        var result = company.AutomateBusiness(business.Id, ManagerName.Create(2, "Milo"), T0.AddHours(3));

        Assert.False(result.IsSuccess);
        Assert.Equal("Business already has a manager.", result.Error);
        Assert.Equal("Lucy", business.ManagerName);
    }

    [Fact]
    public void AutomateBusiness_AfterTheShiftEnds_RehiresForTheSamePrice_WithTheNewName()
    {
        var company = NewCompany();
        company.AddCash(10_000m);
        var business = company.OpenBusiness(NewEntry(openingCost: 1_000m)).Value;
        company.AutomateBusiness(business.Id, Lucy, T0);

        var rehire = T0.AddHours(4);
        var result = company.AutomateBusiness(business.Id, ManagerName.Create(2, "Milo"), rehire);

        Assert.True(result.IsSuccess);
        Assert.Equal("Milo", business.ManagerName);
        Assert.Equal(rehire.AddHours(4), business.ManagerUntil);
        Assert.Equal(5_000m, company.Cash); // 10,000 − 1,000 opening − 2,000 × 2 shifts
    }

    [Fact]
    public void OfflineIncomePerSecondAt_CountsOnlyRunningShifts()
    {
        var company = NewCompany();
        company.AddCash(5_000m);
        var business = company.OpenBusiness(NewEntry(openingCost: 1_000m, grossIncomePerSecond: 5m)).Value;
        company.AutomateBusiness(business.Id, Lucy, T0);

        Assert.Equal(8m, company.OfflineIncomePerSecondAt(T0.AddHours(1)));
        Assert.Equal(3m, company.OfflineIncomePerSecondAt(T0.AddHours(5)));
    }

    // -- Business levels ----------------------------------------------------

    [Fact]
    public void NewBusiness_StartsAtLevelOne_AndTheFirstLevelCostsTheOpeningCost()
    {
        var company = NewCompany();
        company.AddCash(1_000m);
        var business = company.OpenBusiness(NewEntry(openingCost: 1_000m, grossIncomePerSecond: 5m)).Value;

        Assert.Equal(1, business.Level);
        Assert.Equal(1m, business.LevelMultiplier);
        Assert.Equal(1_000m, business.NextLevelCost);
        Assert.Equal(5.5m, business.NextLevelIncomePerSecond);
    }

    [Fact]
    public void LevelUpBusiness_ChargesTheCost_AndBoostsIncomeByTenPercent()
    {
        var company = NewCompany();
        company.AddCash(5_000m);
        var business = company.OpenBusiness(NewEntry(openingCost: 1_000m, grossIncomePerSecond: 5m)).Value;

        var result = company.LevelUpBusiness(business.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, business.Level);
        Assert.Equal(5.5m, business.NetIncomePerSecond);
        Assert.Equal(3_000m, company.Cash);           // 5,000 − 1,000 opening − 1,000 level
        Assert.Equal(1_250m, business.NextLevelCost); // × 1.25
    }

    [Fact]
    public void LevelMultiplier_DoublesAtEachMilestone()
    {
        Assert.Equal(1.8m, Business.MultiplierAt(9));
        Assert.Equal(3.8m, Business.MultiplierAt(10));            // (1 + 0.9) × 2
        Assert.Equal(13.6m, Business.MultiplierAt(25));           // (1 + 2.4) × 4
        Assert.Equal(47.2m, Business.MultiplierAt(50));           // (1 + 4.9) × 8
    }

    [Fact]
    public void LevelUpBusiness_ScalesAssetIncomeToo()
    {
        var company = NewCompany();
        company.AddCash(10_000m);
        var business = company.OpenBusiness(NewEntry(openingCost: 1_000m, grossIncomePerSecond: 5m)).Value;
        business.AddAsset(BusinessAsset.Create(business.Id, "Truck", 500m, 5m)); // gross 10

        company.LevelUpBusiness(business.Id);

        Assert.Equal(11m, business.NetIncomePerSecond);
    }

    [Fact]
    public void LevelUpBusiness_RaisesTheCompanysIncome()
    {
        var company = NewCompany();
        company.AddCash(5_000m);
        var business = company.OpenBusiness(NewEntry(openingCost: 1_000m, grossIncomePerSecond: 5m)).Value;
        var before = company.IncomePerSecond;

        company.LevelUpBusiness(business.Id);

        Assert.Equal(before + 0.5m, company.IncomePerSecond);
    }

    [Fact]
    public void LevelUpBusiness_WithInsufficientFunds_ChangesNothing()
    {
        var company = NewCompany();
        company.AddCash(1_500m);
        var business = company.OpenBusiness(NewEntry(openingCost: 1_000m)).Value;

        var result = company.LevelUpBusiness(business.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal("Insufficient funds.", result.Error);
        Assert.Equal(1, business.Level);
        Assert.Equal(500m, company.Cash);
    }

    [Fact]
    public void LevelUpBusiness_AtMaxLevel_Fails()
    {
        var company = NewCompany();
        company.AddCash(100_000_000_000_000m);
        var business = company.OpenBusiness(NewEntry(openingCost: 1m)).Value;
        for (var i = 1; i < GameConstants.MaxBusinessLevel; i++)
            Assert.True(company.LevelUpBusiness(business.Id).IsSuccess);

        var result = company.LevelUpBusiness(business.Id);

        Assert.Equal(GameConstants.MaxBusinessLevel, business.Level);
        Assert.False(result.IsSuccess);
        Assert.Equal("Business is already at max level.", result.Error);
        Assert.Null(business.NextLevelCost);
        Assert.Null(business.NextLevelIncomePerSecond);
    }

    [Fact]
    public void LevelUpBusiness_DoesNotCountAsEarnings_NorAddToLiquidationValue()
    {
        var company = NewCompany();
        company.AddCash(5_000m);
        var business = company.OpenBusiness(NewEntry(openingCost: 1_000m)).Value;

        company.LevelUpBusiness(business.Id);

        Assert.Equal(5_000m, company.AllTimeEarnings);
        Assert.Equal(1_000m, business.TotalValue);
    }

    [Fact]
    public void LevelUpBusiness_UnknownBusiness_Fails()
    {
        var company = NewCompany();

        var result = company.LevelUpBusiness(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal("Business not found.", result.Error);
    }

    // -- Prestige -----------------------------------------------------------

    [Fact]
    public void CanPrestige_BelowThreshold_Fails()
    {
        var company = NewCompany();

        var result = company.CanPrestige();

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void CanPrestige_DoesNotCountBusinessValue()
    {
        var company = NewCompany();
        company.AddCash(25_000m);
        company.OpenBusiness(NewEntry(openingCost: 1_000m));

        // Net worth is still 25,000, but prestige is paid in cash and only 24,000 is left.
        Assert.Equal(25_000m, company.NetWorth);
        var result = company.CanPrestige();

        Assert.False(result.IsSuccess);
        Assert.Equal("Prestige costs $25,000 in cash.", result.Error);
    }

    [Fact]
    public void Prestige_DeductsThePrice_KeepsTheRest_AdvancesLevel_AndRaisesEvent()
    {
        var company = NewCompany();
        company.AddCash(31_000m);
        company.OpenBusiness(NewEntry(openingCost: 1_000m));

        var result = company.Prestige(T0.AddHours(2));

        Assert.True(result.IsSuccess);
        Assert.Equal(5_000m, company.Cash); // 31,000 − 1,000 business − 25,000 price
        Assert.Single(company.Businesses);
        Assert.Equal(PrestigeLevel.SmallBusiness, company.PrestigeLevel);
        Assert.Equal(1, company.PrestigeCount);
        Assert.Equal(1.18m, company.PrestigeMultiplier);
        Assert.Equal(3m * 1.18m, company.PassiveIncomePerSecond);
        Assert.Contains(company.DomainEvents, e => e is PrestigeTriggeredEvent);
    }

    [Fact]
    public void Prestige_KeepsBusinessesAndTheirAssets()
    {
        var company = NewCompany();
        company.AddCash(40_000m);
        company.OpenBusiness(NewEntry(openingCost: 1_000m));
        var before = company.Businesses.Single();
        var valueBefore = before.TotalValue;

        company.Prestige(T0.AddHours(2));

        var after = company.Businesses.Single();
        Assert.Equal(before.Id, after.Id);
        Assert.Equal(valueBefore, after.TotalValue);
    }

    [Fact]
    public void Prestige_PreservesAllTimeEarnings()
    {
        var company = NewCompany();
        company.AddCash(30_000m);

        var result = company.Prestige(T0.AddHours(2));

        Assert.True(result.IsSuccess);
        // Paying the price is spending, not losing earnings — rank must survive it.
        Assert.Equal(5_000m, company.Cash);
        Assert.Equal(30_000m, company.AllTimeEarnings);
    }

    [Fact]
    public void Prestige_BelowThreshold_ChangesNothing()
    {
        var company = NewCompany();
        company.AddCash(100m);

        var result = company.Prestige(T0.AddHours(2));

        Assert.False(result.IsSuccess);
        Assert.Equal(100m, company.Cash);
        Assert.Equal(0, company.PrestigeCount);
    }

    // -- Passive income upgrade ---------------------------------------------

    [Fact]
    public void UpgradePassiveIncome_RejectsARateThatIsNotAnImprovement()
    {
        var company = NewCompany();
        company.AddCash(50_000m);

        var result = company.UpgradePassiveIncome(cost: 20_000m, newRate: 2m);

        Assert.False(result.IsSuccess);
        Assert.Equal(50_000m, company.Cash);
        Assert.Equal(3m, company.PassiveIncomePerSecond);
    }

    [Fact]
    public void UpgradePassiveIncome_ChargesTheCost_AndRaisesTheRate()
    {
        var company = NewCompany();
        company.AddCash(50_000m);

        var result = company.UpgradePassiveIncome(cost: 20_000m, newRate: 7m);

        Assert.True(result.IsSuccess);
        Assert.Equal(30_000m, company.Cash);
        Assert.Equal(7m, company.PassiveIncomePerSecond);
    }
}

using RichLife.Domain;
using RichLife.Domain.Catalogue;
using RichLife.Domain.Entities;
using RichLife.Domain.Enums;
using RichLife.Domain.Operations;

namespace RichLife.Tests.Domain;

/// <summary>features/014-business-places-and-equipment.md</summary>
public class PlacesAndBreakdownsTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly ManagerName Lucy = ManagerName.Create(1, "Lucy");

    private static Company NewCompany()
    {
        var company = Company.Create(Guid.NewGuid(), "Acme");
        company.AccrueOffline(T0);
        return company;
    }

    /// <summary>Base income 5/s, one asset "washer" (1/s, $500, MTBF 2 h), places park (quiet) and stadium (prime).</summary>
    private static BusinessCatalogueEntry Entry(decimal baseIncome = 5m, int mtbfHours = 2, int capacity = 2, int perLevel = 1)
    {
        var entry = BusinessCatalogueEntry.Create("car-wash", CatalogueFixtures.Details(
            openingCost: 1_000m, baseIncomePerSecond: baseIncome) with
        {
            PlaceBaseCapacity = capacity,
            PlaceCapacityPerLevel = perLevel,
        }).Value;
        entry.AddAsset("washer", CatalogueFixtures.AssetDetails(price: 500m, fixedIncomePerSecond: 1m) with { MtbfHours = mtbfHours });
        entry.AddPlaceKind("stadium", new PlaceKindDetails("Stadium", PlaceTier.Prime, 2_000m, 20));
        entry.AddPlaceKind("park", new PlaceKindDetails("Park", PlaceTier.Quiet, 500m, 10));
        return entry;
    }

    private static (Company Company, Business Business, BusinessCatalogueEntry Entry) Opened(
        decimal cash = 100_000m, decimal baseIncome = 5m, int mtbfHours = 2)
    {
        var company = NewCompany();
        company.AdminSetCash(cash);
        var entry = Entry(baseIncome, mtbfHours);
        var business = company.OpenBusiness(entry).Value;
        return (company, business, entry);
    }

    // -- Places -----------------------------------------------------------------------

    [Fact]
    public void OpenBusiness_ComesWithItsFirstPlace_TheFirstKind_AtNoExtraPrice()
    {
        var (company, business, _) = Opened(cash: 1_000m);

        var place = Assert.Single(business.Places);
        Assert.Equal("park", place.KindId);
        Assert.Equal(PlaceTier.Quiet, place.Tier);
        Assert.Equal(0m, place.PurchasePrice);
        Assert.Equal(2, place.Capacity);
        Assert.Equal(0m, company.Cash);
        Assert.Equal(5m, business.NetIncomePerSecondAt(T0));
        Assert.Equal(1_000m, business.TotalValue);
    }

    [Fact]
    public void OpenBusiness_WithoutPlaceKinds_OpensAQuietMainPlace()
    {
        var company = NewCompany();
        company.AdminSetCash(1_000m);

        var business = company.OpenBusiness(CatalogueFixtures.Entry()).Value;

        var place = Assert.Single(business.Places);
        Assert.Equal("main", place.KindId);
        Assert.Equal(5m, business.NetIncomePerSecondAt(T0));
    }

    [Fact]
    public void OpenPlace_ChargesTheKindsPrice_AndEarnsTheBaseIncomeTimesItsTier()
    {
        var (company, business, entry) = Opened(cash: 3_000m);

        var result = company.OpenPlace(business.Id, entry, "stadium");

        Assert.True(result.IsSuccess);
        Assert.Equal(0m, company.Cash);
        Assert.Equal(2, business.Places.Count);
        Assert.Equal(5m + 5m * 2.2m, business.NetIncomePerSecondAt(T0));
        Assert.Equal(3_000m, business.TotalValue);   // opening + the stadium
    }

    [Fact]
    public void OpenPlace_UnknownKind_OrPastTheLimit_Fails()
    {
        var (company, business, entry) = Opened(cash: 1_000_000m);

        Assert.Equal("Place not available for this business.", company.OpenPlace(business.Id, entry, "moon").Error);

        for (var i = 1; i < GameConstants.MaxPlacesPerBusiness; i++)
            Assert.True(company.OpenPlace(business.Id, entry, "park").IsSuccess);
        Assert.Equal("A business can have at most 10 places.", company.OpenPlace(business.Id, entry, "park").Error);
    }

    [Fact]
    public void BuyUnit_IntoAFullPlace_Fails_UntilThePlaceIsUpgraded()
    {
        var (company, business, entry) = Opened();
        var place = business.Places[0];
        Assert.True(company.BuyUnit(place.Id, entry, "washer", T0).IsSuccess);
        Assert.True(company.BuyUnit(place.Id, entry, "washer", T0).IsSuccess);

        var full = company.BuyUnit(place.Id, entry, "washer", T0);
        Assert.Equal("This place is full. Upgrade it or open another place.", full.Error);

        var cashBefore = company.Cash;
        Assert.True(company.LevelUpPlace(place.Id).IsSuccess);
        Assert.Equal(cashBefore - 125m, company.Cash);       // level 1 → 2 costs 25 % of the kind's price
        Assert.Equal(3, place.Capacity);
        Assert.Equal(168.75m, place.NextLevelCost);           // × 1.35 (Road to Rich)
        Assert.True(company.BuyUnit(place.Id, entry, "washer", T0).IsSuccess);
    }

    [Fact]
    public void ASiteFormat_EarnsItsOwnIncome_AndTheBusinessKeepsItsKind()
    {
        var company = NewCompany();
        company.AdminSetCash(10_000m);
        var entry = BusinessCatalogueEntry.Create("restaurant-chain",
            CatalogueFixtures.Details(openingCost: 1_000m, baseIncomePerSecond: 5m) with { Kind = BusinessKind.Sites }).Value;
        entry.AddPlaceKind("burger-joint", new PlaceKindDetails("Burger Joint", PlaceTier.Quiet, 1_000m, 10, BaseIncomePerSecond: 2m));
        entry.AddPlaceKind("steakhouse", new PlaceKindDetails("Steakhouse", PlaceTier.Quiet, 3_000m, 20, BaseIncomePerSecond: 7m));

        var business = company.OpenBusiness(entry).Value;
        Assert.Equal(BusinessKind.Sites, business.Kind);
        Assert.Equal(2m, business.NetIncomePerSecondAt(T0));   // the Burger Joint's own income, not the entry's 5

        company.OpenPlace(business.Id, entry, "steakhouse");
        Assert.Equal(9m, business.NetIncomePerSecondAt(T0));
    }

    [Fact]
    public void PlaceLevel_AddsAQuarterToEverythingInThePlace()
    {
        var (company, business, entry) = Opened();
        var place = business.Places[0];
        company.BuyUnit(place.Id, entry, "washer", T0);

        company.LevelUpPlace(place.Id);

        Assert.Equal((5m + 1m) * 1.25m, business.NetIncomePerSecondAt(T0));
    }

    [Fact]
    public void BuyAsset_GoesIntoTheFirstPlaceWithRoom()
    {
        var (company, business, entry) = Opened();
        company.OpenPlace(business.Id, entry, "stadium");
        for (var i = 0; i < 3; i++) Assert.True(company.BuyAsset(business.Id, entry, "washer", T0).IsSuccess);

        Assert.Equal(2, business.UnitCountIn(business.Places[0]));
        Assert.Equal(1, business.UnitCountIn(business.Places[1]));
    }

    // -- Breakdowns ---------------------------------------------------------------------

    [Fact]
    public void AUnit_Breaks_AfterHalfToOneAndAHalfTimesItsMtbf_AndThenEarnsNothing()
    {
        var (company, business, entry) = Opened();
        company.BuyUnit(business.Places[0].Id, entry, "washer", T0);
        var unit = Assert.Single(business.Assets);

        Assert.InRange(unit.BreaksAt, T0.AddHours(1), T0.AddHours(3));
        Assert.Equal(UnitStatus.Working, unit.StatusAt(unit.BreaksAt.AddTicks(-1)));
        Assert.Equal(UnitStatus.Broken, unit.StatusAt(unit.BreaksAt));
        Assert.Equal(6m, business.NetIncomePerSecondAt(T0));
        Assert.Equal(5m, business.NetIncomePerSecondAt(unit.BreaksAt));
        Assert.Equal(1, business.BrokenUnitsAt(unit.BreaksAt));
        // The ceiling for a sync still counts it: breakdowns only ever lower income.
        Assert.Equal(6m + GameConstants.BasePassiveIncomePerSecond, company.FullIncomePerSecond);
    }

    [Fact]
    public void TheFault_IsDrawnFromTheBusinessesList_AndIsStable()
    {
        var (company, business, entry) = Opened();
        company.BuyUnit(business.Places[0].Id, entry, "washer", T0);
        var unit = business.Assets[0];

        var fault = business.FaultOf(unit);

        Assert.Contains(fault, FaultCatalog.For("car-wash", BusinessSector.Services));
        Assert.Equal(fault, business.FaultOf(unit));
        Assert.Equal(fault.RepairCost(500m), business.RepairCostOf(unit));
        Assert.Equal(Math.Round(fault.RepairCost(500m) * 2.5m, 2), business.FastRepairCostOf(unit));
    }

    [Fact]
    public void Repair_TakesTheFaultsRepairTime_AndDrawsTheNextFailure()
    {
        var (company, business, entry) = Opened();
        company.BuyUnit(business.Places[0].Id, entry, "washer", T0);
        var unit = business.Assets[0];
        var now = unit.BreaksAt.AddMinutes(10);
        var fault = business.FaultOf(unit);
        var cashBefore = company.Cash;

        var result = company.RepairUnit(unit.Id, fast: false, now);

        Assert.True(result.IsSuccess);
        Assert.Equal(cashBefore - fault.RepairCost(500m), company.Cash);
        Assert.Equal(UnitStatus.Repairing, unit.StatusAt(now));
        Assert.Equal(now.AddHours(fault.RepairHours), unit.WorkingFrom);
        Assert.InRange(unit.BreaksAt, unit.WorkingFrom.AddHours(1), unit.WorkingFrom.AddHours(3));
        Assert.Equal("Already being repaired.", company.RepairUnit(unit.Id, fast: true, now).Error);
    }

    [Fact]
    public void FastRepair_CostsTwoAndAHalfTimes_AndWorksAtOnce()
    {
        var (company, business, entry) = Opened();
        company.BuyUnit(business.Places[0].Id, entry, "washer", T0);
        var unit = business.Assets[0];
        var now = unit.BreaksAt;
        var price = business.FastRepairCostOf(unit);
        var cashBefore = company.Cash;

        Assert.True(company.RepairUnit(unit.Id, fast: true, now).IsSuccess);

        Assert.Equal(cashBefore - price, company.Cash);
        Assert.Equal(UnitStatus.Working, unit.StatusAt(now));
        Assert.Equal(1, unit.RepairCount);
    }

    [Fact]
    public void Repair_AWorkingUnit_OrAnUnknownOne_Fails()
    {
        var (company, business, entry) = Opened();
        company.BuyUnit(business.Places[0].Id, entry, "washer", T0);

        Assert.Equal("This unit is working.", company.RepairUnit(business.Assets[0].Id, false, T0).Error);
        Assert.Equal("Unit not found.", company.RepairUnit(Guid.NewGuid(), false, T0).Error);
    }

    [Fact]
    public void RepairAll_IsAllOrNothing()
    {
        var (company, business, entry) = Opened();
        company.BuyUnit(business.Places[0].Id, entry, "washer", T0);
        company.BuyUnit(business.Places[0].Id, entry, "washer", T0);
        var now = business.Assets.Max(a => a.BreaksAt);
        var total = business.Assets.Sum(business.RepairCostOf);

        company.AdminSetCash(total - 0.01m);
        Assert.Equal("Insufficient funds.", company.RepairAllUnits(business.Id, now).Error);
        Assert.Equal(2, business.BrokenUnitsAt(now));

        company.AdminSetCash(total);
        Assert.True(company.RepairAllUnits(business.Id, now).IsSuccess);
        Assert.Equal(0m, company.Cash);
        Assert.Equal(0, business.BrokenUnitsAt(now));
        Assert.Equal("Nothing to repair.", company.RepairAllUnits(business.Id, now).Error);
    }

    // -- Time away ----------------------------------------------------------------------

    [Fact]
    public void Offline_AUnitEarnsOnlyUntilItBreaks_WhenNobodyCanPayTheRepair()
    {
        // Exactly enough for the business, a manager and one unit: nothing left for repairs.
        var company = NewCompany();
        var entry = Entry(baseIncome: 0m);
        company.AdminSetCash(1_000m + 2_000m + 500m);
        var business = company.OpenBusiness(entry).Value;
        company.AutomateBusiness(business.Id, Lucy, T0);
        company.BuyUnit(business.Places[0].Id, entry, "washer", T0);
        var unit = business.Assets[0];
        var breaksAt = unit.BreaksAt;
        Assert.Equal(0m, company.Cash);

        var run = company.AccrueOfflineRun(T0.AddHours(4));

        var passive = GameConstants.BasePassiveIncomePerSecond * 4 * 3600;
        var unitSeconds = (decimal)(breaksAt - T0).TotalSeconds;
        Assert.Equal(0, run.Repairs);
        Assert.Equal(passive + unitSeconds, run.Earned);
        Assert.Equal(UnitStatus.Broken, unit.StatusAt(T0.AddHours(4)));
    }

    [Fact]
    public void Offline_TheManagerRepairsWhatBreaksDuringTheShift_FromCash()
    {
        var company = NewCompany();
        var entry = Entry(baseIncome: 0m);
        company.AdminSetCash(1_000m + 2_000m + 500m + 10_000m);
        var business = company.OpenBusiness(entry).Value;
        company.AutomateBusiness(business.Id, Lucy, T0);
        company.BuyUnit(business.Places[0].Id, entry, "washer", T0);
        var unit = business.Assets[0];
        var firstBreak = unit.BreaksAt;
        var firstFault = business.FaultOf(unit);
        var cashBefore = company.Cash;

        var run = company.AccrueOfflineRun(T0.AddHours(4));

        Assert.True(run.Repairs >= 1);
        Assert.True(run.RepairsPaid >= firstFault.RepairCost(500m));
        Assert.Equal(cashBefore + run.Earned - run.RepairsPaid, company.Cash);
        Assert.True(unit.RepairCount >= 1);
        // At least what it earned before the first failure, plus the base income.
        var floor = GameConstants.BasePassiveIncomePerSecond * 4 * 3600 + (decimal)(firstBreak - T0).TotalSeconds;
        Assert.True(run.Earned >= floor);
    }

    [Fact]
    public void Online_AManagerOnShiftRepairsBrokenUnits_ButOnlyThen()
    {
        var (company, business, entry) = Opened();
        company.BuyUnit(business.Places[0].Id, entry, "washer", T0);
        var unit = business.Assets[0];
        var now = unit.BreaksAt.AddMinutes(1);

        Assert.Equal((0, 0m), company.RunManagerRepairs(now));   // no manager

        company.AutomateBusiness(business.Id, Lucy, now);
        var cost = business.RepairCostOf(unit);
        var (repairs, spent) = company.RunManagerRepairs(now);

        Assert.Equal(1, repairs);
        Assert.Equal(cost, spent);
        Assert.Equal(UnitStatus.Repairing, unit.StatusAt(now));
    }

    // -- Catalogue ----------------------------------------------------------------------

    [Fact]
    public void PlaceKinds_AreValidated_AndListedByDisplayOrder()
    {
        var entry = Entry();

        Assert.Equal(["park", "stadium"], entry.PlaceKinds.Select(k => k.Id));
        Assert.Equal("Place already exists.",
            entry.AddPlaceKind("park", new PlaceKindDetails("Park", PlaceTier.Quiet, 1m, 0)).Error);
        Assert.Equal("Place price must be positive.",
            entry.AddPlaceKind("beach", new PlaceKindDetails("Beach", PlaceTier.Busy, 0m, 0)).Error);
        Assert.Equal("Unknown place tier.",
            entry.AddPlaceKind("beach", new PlaceKindDetails("Beach", (PlaceTier)9, 1m, 0)).Error);
        Assert.True(entry.RemovePlaceKind("stadium").IsSuccess);
        Assert.Equal("Place not found.", entry.RemovePlaceKind("stadium").Error);
    }

    [Fact]
    public void AssetMtbf_IsValidated()
    {
        var entry = Entry();

        Assert.Equal("Time between failures must be between 1 and 10000 hours.",
            entry.AddAsset("dryer", CatalogueFixtures.AssetDetails() with { MtbfHours = 0 }).Error);
    }

    [Fact]
    public void AFaultId_NeverMeansTwoDifferentFaults()
    {
        var all = FaultCatalog.All.ToList();
        Assert.Equal(all.Count, all.Select(f => f.Id).Distinct().Count());
        Assert.All(all, f => Assert.True(f.RepairHours >= 1 && f.Weight > 0));
    }
}

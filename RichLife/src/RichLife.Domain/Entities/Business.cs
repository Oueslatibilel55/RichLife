using RichLife.Domain.Common;
using RichLife.Domain.Enums;

namespace RichLife.Domain.Entities;

public class Business : BaseEntity
{
    public Guid CompanyId { get; private set; }

    /// <summary>
    /// Id of the <see cref="Catalogue.BusinessCatalogueEntry"/> this was opened from (e.g. "food-cart").
    /// The numbers below are a copy taken at opening, not a live link to the entry.
    /// </summary>
    public string CatalogueId { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;
    public BusinessSector Sector { get; private set; }
    public PrestigeLevel RequiredPrestige { get; private set; }
    public decimal OpeningCost { get; private set; }
    public decimal MonthlySalaryCost { get; private set; }
    /// <summary>
    /// End of the current (or last) manager shift, UTC. A manager works
    /// <see cref="GameConstants.ManagerShift"/> from the moment they are hired; the business
    /// earns offline only while a shift runs. Null if no manager was ever hired.
    /// </summary>
    public DateTime? ManagerUntil { get; private set; }

    /// <summary>
    /// The current or last manager, by id into <see cref="Catalogue.ManagerName"/>, plus a copy
    /// of the name — the same id-plus-copy shape as <see cref="CatalogueId"/>/<see cref="Name"/>.
    /// Both null until a manager is first hired; kept after the shift ends.
    /// </summary>
    public int? ManagerNameId { get; private set; }
    public string? ManagerName { get; private set; }
    public bool IsForSale { get; private set; }
    public decimal? AskingPrice { get; private set; }
    public int EmployeeCount { get; private set; }

    // Places (features/014-business-places-and-equipment.md): every business has at least one.
    private readonly List<BusinessPlace> _places = [];
    public IReadOnlyList<BusinessPlace> Places =>
        _places.OrderBy(p => p.CreatedAt).ThenBy(p => p.Id).ToList();

    // Units owned by this business (trucks, washers, rooms…), each standing in one place.
    private readonly List<BusinessAsset> _assets = [];
    public IReadOnlyList<BusinessAsset> Assets => _assets.AsReadOnly();

    /// <summary>Starts at 1; raised by paying <see cref="NextLevelCost"/>.</summary>
    public int Level { get; private set; } = 1;

    // Salaries are quoted monthly and amortized over a 30-day month.
    public decimal SalaryCostPerSecond => MonthlySalaryCost / GameConstants.SecondsPerSalaryMonth;

    /// <summary>
    /// Gross income at <paramref name="atUtc"/>: each place's own income plus its working units,
    /// × the place's multiplier. A broken or repairing unit earns nothing.
    /// </summary>
    public decimal GrossIncomePerSecondAt(DateTime atUtc) =>
        _places.Sum(p => (p.BaseIncomePerSecond
                          + _assets.Where(a => a.PlaceId == p.Id && a.EarnsAt(atUtc)).Sum(a => a.IncomePerSecond))
                         * p.Multiplier);

    /// <summary>Gross income with every unit working — the most the business can earn.</summary>
    public decimal FullGrossIncomePerSecond =>
        _places.Sum(p => (p.BaseIncomePerSecond
                          + _assets.Where(a => a.PlaceId == p.Id).Sum(a => a.IncomePerSecond))
                         * p.Multiplier);

    /// <summary>Gross income scaled by the level, minus salaries (not scaled).</summary>
    public decimal NetIncomePerSecondAt(DateTime atUtc) => NetAt(GrossIncomePerSecondAt(atUtc), Level);

    /// <summary><see cref="NetIncomePerSecondAt"/> with every unit working.</summary>
    public decimal FullNetIncomePerSecond => NetAt(FullGrossIncomePerSecond, Level);

    /// <summary>What a unit adds to <see cref="NetIncomePerSecondAt"/> while it works.</summary>
    public decimal UnitContributionPerSecond(BusinessAsset unit) =>
        unit.IncomePerSecond * (PlaceOf(unit)?.Multiplier ?? 1m) * LevelMultiplier;

    /// <summary>What a place adds to <see cref="NetIncomePerSecondAt"/> (before salaries).</summary>
    public decimal PlaceIncomePerSecondAt(BusinessPlace place, DateTime atUtc) =>
        (place.BaseIncomePerSecond
         + _assets.Where(a => a.PlaceId == place.Id && a.EarnsAt(atUtc)).Sum(a => a.IncomePerSecond))
        * place.Multiplier * LevelMultiplier;

    public BusinessPlace? PlaceOf(BusinessAsset unit) => _places.FirstOrDefault(p => p.Id == unit.PlaceId);

    public IEnumerable<BusinessAsset> UnitsIn(BusinessPlace place) =>
        _assets.Where(a => a.PlaceId == place.Id).OrderBy(a => a.CreatedAt).ThenBy(a => a.Id);

    /// <summary>What can break in this business, in a fixed order.</summary>
    public IReadOnlyList<Operations.FaultDefinition> Faults => Operations.FaultCatalog.For(CatalogueId, Sector);

    public Operations.FaultDefinition FaultOf(BusinessAsset unit) => unit.FaultFrom(Faults);

    /// <summary>Standard price of fixing <paramref name="unit"/>'s fault.</summary>
    public decimal RepairCostOf(BusinessAsset unit) => FaultOf(unit).RepairCost(unit.PurchasePrice);

    public decimal FastRepairCostOf(BusinessAsset unit) =>
        Math.Round(RepairCostOf(unit) * GameConstants.FastRepairMultiplier, 2, MidpointRounding.AwayFromZero);

    public int BrokenUnitsAt(DateTime atUtc) => _assets.Count(a => a.StatusAt(atUtc) == UnitStatus.Broken);

    public decimal LevelMultiplier => MultiplierAt(Level);

    public bool IsMaxLevel => Level >= GameConstants.MaxBusinessLevel;

    /// <summary>Price of the next level: opening cost × growth^(level − 1). Null at max.</summary>
    public decimal? NextLevelCost
    {
        get
        {
            if (IsMaxLevel) return null;
            var cost = OpeningCost;
            for (var i = 1; i < Level; i++) cost *= GameConstants.LevelCostGrowth;
            return cost;
        }
    }

    /// <summary>What <see cref="NetIncomePerSecondAt"/> becomes after the next level. Null at max.</summary>
    public decimal? NextLevelIncomePerSecondAt(DateTime atUtc) =>
        IsMaxLevel ? null : NetAt(GrossIncomePerSecondAt(atUtc), Level + 1);

    private decimal NetAt(decimal gross, int level) => gross * MultiplierAt(level) - SalaryCostPerSecond;

    /// <summary>(1 + bonus × (level − 1)), doubled once for every milestone reached.</summary>
    public static decimal MultiplierAt(int level)
    {
        var multiplier = 1m + GameConstants.LevelIncomeBonus * (level - 1);
        foreach (var milestone in GameConstants.LevelMilestones)
            if (level >= milestone) multiplier *= 2m;
        return multiplier;
    }

    /// <summary>Liquidation value: what was paid to open it, its extra places and every unit bought for it.</summary>
    public decimal TotalValue =>
        OpeningCost + _places.Sum(p => p.PurchasePrice) + _assets.Sum(a => a.CurrentValue);

    /// <summary>Price of hiring a manager, which automates the business.</summary>
    public decimal ManagerCost => OpeningCost * GameConstants.ManagerCostMultiplier;

    // -- Taxes (features/013-business-taxes.md) -------------------------------------

    /// <summary>Start of the current tax period; one bill per <see cref="GameConstants.TaxPeriod"/> from the opening.</summary>
    public DateTime TaxPeriodStart { get; private set; } = DateTime.UtcNow;

    /// <summary>What this business has earned (prestige and boost included) since <see cref="TaxPeriodStart"/>.</summary>
    public decimal TaxableEarnings { get; private set; }

    /// <summary>Billed and not yet paid. Bills add up until the player pays — never taken automatically.</summary>
    public decimal TaxDue { get; private set; }

    public DateTime TaxPeriodEndsAt => TaxPeriodStart + GameConstants.TaxPeriod;

    /// <summary>The bill the current period would produce if it ended now.</summary>
    public decimal TaxAccruing => RoundTax(TaxableEarnings * GameConstants.TaxRate);

    internal void RecordEarnings(decimal amount)
    {
        if (amount <= 0m) return;
        TaxableEarnings += amount;
        MarkUpdated();
    }

    /// <summary>
    /// Bills every period that has ended by <paramref name="nowUtc"/>. Several periods at once
    /// (after time away) make one bill, since earnings are only recorded when credited.
    /// Returns the amount billed.
    /// </summary>
    internal decimal AssessTaxes(DateTime nowUtc)
    {
        if (nowUtc < TaxPeriodEndsAt) return 0m;

        var periods = (nowUtc - TaxPeriodStart).Ticks / GameConstants.TaxPeriod.Ticks;
        var bill = TaxAccruing;
        TaxDue += bill;
        TaxableEarnings = 0m;
        TaxPeriodStart += TimeSpan.FromTicks(GameConstants.TaxPeriod.Ticks * periods);
        MarkUpdated();
        return bill;
    }

    /// <summary>Clears the bill and returns what it was. Cash is checked and taken by the aggregate.</summary>
    internal decimal PayTaxes()
    {
        var paid = TaxDue;
        TaxDue = 0m;
        MarkUpdated();
        return paid;
    }

    private static decimal RoundTax(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);

    private Business() { }

    /// <summary>
    /// A new business with its first place, which comes with it (no extra price). The place
    /// earns <paramref name="baseIncomePerSecond"/> on its own; without
    /// <paramref name="firstPlace"/> it is a quiet "Main" place.
    /// </summary>
    public static Business Create(
        Guid companyId, string catalogueId, string name, BusinessSector sector,
        PrestigeLevel requiredPrestige, decimal openingCost,
        decimal baseIncomePerSecond, decimal monthlySalaryCost,
        int employeeCount, PlaceSpec? firstPlace = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogueId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var business = new Business
        {
            CompanyId         = companyId,
            CatalogueId       = catalogueId,
            Name              = name,
            Sector            = sector,
            RequiredPrestige  = requiredPrestige,
            OpeningCost       = openingCost,
            MonthlySalaryCost = monthlySalaryCost,
            EmployeeCount     = employeeCount
        };
        var spec = firstPlace ?? PlaceSpec.Default(openingCost);
        business._places.Add(BusinessPlace.Create(
            business.Id, spec.KindId, spec.Name, spec.Tier, spec.Price, purchasePrice: 0m,
            baseIncomePerSecond, spec.BaseCapacity, spec.CapacityPerLevel));
        return business;
    }

    /// <summary>A further place; price and the place limit are checked by the aggregate.</summary>
    internal BusinessPlace AddPlace(PlaceSpec spec, decimal baseIncomePerSecond)
    {
        var place = BusinessPlace.Create(
            Id, spec.KindId, spec.Name, spec.Tier, spec.Price, purchasePrice: spec.Price,
            baseIncomePerSecond, spec.BaseCapacity, spec.CapacityPerLevel);
        _places.Add(place);
        MarkUpdated();
        return place;
    }

    /// <summary>The first place (in opening order) with room for one more unit.</summary>
    public BusinessPlace? FirstPlaceWithRoom() =>
        Places.FirstOrDefault(p => _assets.Count(a => a.PlaceId == p.Id) < p.Capacity);

    public int UnitCountIn(BusinessPlace place) => _assets.Count(a => a.PlaceId == place.Id);

    /// <summary>True while a manager shift is running at <paramref name="atUtc"/>.</summary>
    public bool HasManagerAt(DateTime atUtc) => ManagerUntil > atUtc;

    /// <summary>
    /// Seconds of the window [<paramref name="fromUtc"/>, <paramref name="toUtc"/>] covered by
    /// the manager's shift — the only part of time away this business is paid for.
    /// </summary>
    public decimal ManagedSecondsWithin(DateTime fromUtc, DateTime toUtc)
    {
        if (ManagerUntil is not { } until) return 0m;
        var shiftStart = until - GameConstants.ManagerShift;
        var start = fromUtc > shiftStart ? fromUtc : shiftStart;
        var end   = toUtc < until ? toUtc : until;
        return end > start ? (decimal)(end - start).TotalSeconds : 0m;
    }

    /// <summary>Starts a new shift. Price and "no shift running" are checked by the aggregate.</summary>
    public void HireManager(Catalogue.ManagerName manager, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ManagerUntil  = nowUtc + GameConstants.ManagerShift;
        ManagerNameId = manager.Id;
        ManagerName   = manager.Name;
        MarkUpdated();
    }

    /// <summary>Raises the level by one. Price and the max are checked by the aggregate.</summary>
    internal void LevelUp()
    {
        Level++;
        MarkUpdated();
    }

    /// <summary>Puts a unit in one of this business's places. Room and price are checked by the aggregate.</summary>
    public void AddAsset(BusinessAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        if (_places.All(p => p.Id != asset.PlaceId))
            throw new ArgumentException("The unit's place does not belong to this business.", nameof(asset));
        _assets.Add(asset);
        MarkUpdated();
    }

    // -- Breakdowns (features/014-business-places-and-equipment.md) ------------------

    /// <summary>
    /// Net earnings (before prestige) of time away in [<paramref name="fromUtc"/>, <paramref name="toUtc"/>):
    /// only the part the manager's shift covers counts, each unit only while it works, and the
    /// part before <paramref name="boostEndUtc"/> counts <see cref="GameConstants.BoostMultiplier"/>
    /// times. A unit that breaks during the shift is repaired by the manager at the standard
    /// price when <paramref name="tryPay"/> accepts it, and earns again once fixed.
    /// </summary>
    internal OfflineRun EarnOffline(
        DateTime fromUtc, DateTime toUtc, DateTime? boostEndUtc, Func<decimal, bool> tryPay)
    {
        if (ManagerUntil is not { } until) return default;
        var shiftStart = until - GameConstants.ManagerShift;
        var ms = fromUtc > shiftStart ? fromUtc : shiftStart;
        var me = toUtc < until ? toUtc : until;
        if (me <= ms) return default;

        var extra = GameConstants.BoostMultiplier - 1m;
        var be = boostEndUtc is { } b && b > ms ? (b < me ? b : me) : ms;
        decimal Weight(DateTime a1, DateTime a2) =>
            Common.Time.Overlap(a1, a2, ms, me) + extra * Common.Time.Overlap(a1, a2, ms, be);

        var gross = _places.Sum(p => p.BaseIncomePerSecond * p.Multiplier) * Weight(ms, me);
        var repairs = 0;
        var spent = 0m;
        foreach (var unit in _assets.OrderBy(a => a.BreaksAt))
        {
            var multiplier = PlaceOf(unit)?.Multiplier ?? 1m;
            // Each repair moves the unit's window past the last one, so this always ends.
            for (var guard = 0; guard < 100; guard++)
            {
                gross += unit.IncomePerSecond * multiplier * Weight(unit.WorkingFrom, unit.BreaksAt);
                if (unit.BreaksAt >= me || unit.WorkingFrom > unit.BreaksAt) break;

                var fault = FaultOf(unit);
                var cost = fault.RepairCost(unit.PurchasePrice);
                if (!tryPay(cost)) break;

                var at = unit.BreaksAt > ms ? unit.BreaksAt : ms;
                unit.Repair(at, TimeSpan.FromHours(fault.RepairHours));
                repairs++;
                spent += cost;
            }
        }

        var net = gross * LevelMultiplier - SalaryCostPerSecond * Weight(ms, me);
        return new OfflineRun(net, repairs, spent);
    }

    /// <summary>
    /// While a shift runs, the manager repairs every broken unit at the standard price, as far as
    /// <paramref name="tryPay"/> allows. Returns how many and what they cost.
    /// </summary>
    internal (int Repairs, decimal Spent) RepairUnderManager(DateTime nowUtc, Func<decimal, bool> tryPay)
    {
        if (!HasManagerAt(nowUtc)) return (0, 0m);
        var repairs = 0;
        var spent = 0m;
        foreach (var unit in _assets.Where(a => a.StatusAt(nowUtc) == UnitStatus.Broken).OrderBy(a => a.BreaksAt))
        {
            var fault = FaultOf(unit);
            var cost = fault.RepairCost(unit.PurchasePrice);
            if (!tryPay(cost)) continue;
            unit.Repair(nowUtc, TimeSpan.FromHours(fault.RepairHours));
            repairs++;
            spent += cost;
        }
        if (repairs > 0) MarkUpdated();
        return (repairs, spent);
    }

    /// <summary>Starts a repair: back after the fault's repair time, or at once when <paramref name="fast"/>.</summary>
    internal void RepairUnit(BusinessAsset unit, bool fast, DateTime nowUtc)
    {
        var hours = fast ? 0 : FaultOf(unit).RepairHours;
        unit.Repair(nowUtc, TimeSpan.FromHours(hours));
        MarkUpdated();
    }

    public void ListForSale(decimal askingPrice)
    {
        IsForSale   = true;
        AskingPrice = askingPrice;
        MarkUpdated();
    }

    public void CancelSaleListing()
    {
        IsForSale   = false;
        AskingPrice = null;
        MarkUpdated();
    }
}

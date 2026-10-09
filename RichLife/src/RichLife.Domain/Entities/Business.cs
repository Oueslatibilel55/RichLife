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
    public decimal GrossIncomePerSecond { get; private set; }
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

    // Assets owned by this business (trucks, cars, rooms, etc.)
    private readonly List<BusinessAsset> _assets = [];
    public IReadOnlyList<BusinessAsset> Assets => _assets.AsReadOnly();

    /// <summary>Starts at 1; raised by paying <see cref="NextLevelCost"/>.</summary>
    public int Level { get; private set; } = 1;

    // Salaries are quoted monthly and amortized over a 30-day month.
    public decimal SalaryCostPerSecond => MonthlySalaryCost / GameConstants.SecondsPerSalaryMonth;

    /// <summary>Gross income (base + assets) scaled by the level; salary is not scaled.</summary>
    public decimal NetIncomePerSecond => IncomeAt(Level);

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

    /// <summary>What <see cref="NetIncomePerSecond"/> becomes after the next level. Null at max.</summary>
    public decimal? NextLevelIncomePerSecond => IsMaxLevel ? null : IncomeAt(Level + 1);

    private decimal IncomeAt(int level) => GrossIncomePerSecond * MultiplierAt(level) - SalaryCostPerSecond;

    /// <summary>(1 + bonus × (level − 1)), doubled once for every milestone reached.</summary>
    public static decimal MultiplierAt(int level)
    {
        var multiplier = 1m + GameConstants.LevelIncomeBonus * (level - 1);
        foreach (var milestone in GameConstants.LevelMilestones)
            if (level >= milestone) multiplier *= 2m;
        return multiplier;
    }

    /// <summary>Liquidation value: what was paid to open it plus every asset bought for it.</summary>
    public decimal TotalValue => OpeningCost + _assets.Sum(a => a.CurrentValue);

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

    public static Business Create(
        Guid companyId, string catalogueId, string name, BusinessSector sector,
        PrestigeLevel requiredPrestige, decimal openingCost,
        decimal grossIncomePerSecond, decimal monthlySalaryCost,
        int employeeCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogueId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Business
        {
            CompanyId            = companyId,
            CatalogueId          = catalogueId,
            Name                 = name,
            Sector               = sector,
            RequiredPrestige     = requiredPrestige,
            OpeningCost          = openingCost,
            GrossIncomePerSecond = grossIncomePerSecond,
            MonthlySalaryCost    = monthlySalaryCost,
            EmployeeCount        = employeeCount
        };
    }

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

    public void AddAsset(BusinessAsset asset)
    {
        _assets.Add(asset);
        GrossIncomePerSecond += asset.IncomePerSecond;
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

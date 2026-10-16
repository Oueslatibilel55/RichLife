using RichLife.Domain.Common;
using RichLife.Domain.Enums;
using RichLife.Domain.Operations;

namespace RichLife.Domain.Entities;

/// <summary>
/// A unit owned by a business, standing in one of its places — a scooter, a washer, a
/// treadmill, a room (features/014-business-places-and-equipment.md; the API calls it a
/// "unit"). Name, price and income are a copy taken when it was bought.
/// </summary>
/// <remarks>
/// A unit earns only while <see cref="WorkingFrom"/> ≤ t &lt; <see cref="BreaksAt"/>. Both
/// instants are known in advance — the next failure is drawn when the unit is bought or
/// repaired, from its id and <see cref="RepairCount"/> — so the client can stop counting a
/// unit's income on its own, and offline income is integrated exactly. Which fault it is is
/// drawn the same way and never stored.
/// </remarks>
public class BusinessAsset : BaseEntity
{
    public Guid BusinessId { get; private set; }
    public Guid PlaceId { get; private set; }

    /// <summary>Catalogue asset id (e.g. "speed-queen-washer"); null for units bought before 2026-10-16.</summary>
    public string? CatalogueId { get; private set; }

    public string Name { get; private set; } = string.Empty;
    public decimal PurchasePrice { get; private set; }
    public decimal CurrentValue { get; private set; }
    public decimal IncomePerSecond { get; private set; }

    /// <summary>Mean time between failures, in hours, copied from the catalogue.</summary>
    public int MtbfHours { get; private set; }

    /// <summary>Back in service from this instant (in the past unless a repair is running).</summary>
    public DateTime WorkingFrom { get; private set; }

    /// <summary>The next failure. From then on the unit earns nothing until repaired.</summary>
    public DateTime BreaksAt { get; private set; }

    public int RepairCount { get; private set; }

    private BusinessAsset() { }

    public static BusinessAsset Create(
        Guid businessId, Guid placeId, string? catalogueId, string name,
        decimal purchasePrice, decimal incomePerSecond, int mtbfHours, DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfLessThan(mtbfHours, 1);

        var unit = new BusinessAsset
        {
            BusinessId      = businessId,
            PlaceId         = placeId,
            CatalogueId     = catalogueId,
            Name            = name,
            PurchasePrice   = purchasePrice,
            CurrentValue    = purchasePrice,
            IncomePerSecond = incomePerSecond,
            MtbfHours       = mtbfHours,
            WorkingFrom     = nowUtc,
        };
        unit.BreaksAt = nowUtc + unit.DrawInterval();
        return unit;
    }

    public UnitStatus StatusAt(DateTime atUtc) =>
        atUtc < WorkingFrom ? UnitStatus.Repairing
        : atUtc >= BreaksAt ? UnitStatus.Broken
        : UnitStatus.Working;

    public bool EarnsAt(DateTime atUtc) => StatusAt(atUtc) == UnitStatus.Working;

    /// <summary>Seconds of [<paramref name="fromUtc"/>, <paramref name="toUtc"/>) the unit works.</summary>
    public decimal WorkingSecondsWithin(DateTime fromUtc, DateTime toUtc) =>
        Time.Overlap(WorkingFrom, BreaksAt, fromUtc, toUtc);

    /// <summary>The fault the unit has (or will have at <see cref="BreaksAt"/>).</summary>
    public FaultDefinition FaultFrom(IReadOnlyList<FaultDefinition> faults)
    {
        var rng = Rng();
        rng.NextUnit();   // the first draw is the interval
        var roll = rng.Next(faults.Sum(f => f.Weight));
        foreach (var f in faults)
        {
            if (roll < f.Weight) return f;
            roll -= f.Weight;
        }
        return faults[^1];
    }

    /// <summary>
    /// Back in service after <paramref name="duration"/> from <paramref name="atUtc"/>, with a
    /// fresh failure drawn from then. Broken-ness and price are checked by the aggregate.
    /// </summary>
    internal void Repair(DateTime atUtc, TimeSpan duration)
    {
        RepairCount++;
        WorkingFrom = atUtc + duration;
        BreaksAt = WorkingFrom + DrawInterval();
        MarkUpdated();
    }

    private TimeSpan DrawInterval()
    {
        var u = Rng().NextUnit();
        var factor = GameConstants.BreakdownDrawMin
                     + u * (GameConstants.BreakdownDrawMax - GameConstants.BreakdownDrawMin);
        return TimeSpan.FromHours((double)(MtbfHours * factor));
    }

    private SeededRandom Rng() => new(SeededRandom.Seed(Id, RepairCount));
}

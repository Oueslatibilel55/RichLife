using RichLife.Domain.Common;
using RichLife.Domain.Enums;

namespace RichLife.Domain.Entities;

/// <summary>A place kind's numbers, as copied into a new <see cref="BusinessPlace"/>.</summary>
public sealed record PlaceSpec(
    string KindId, string Name, PlaceTier Tier, decimal Price, int BaseCapacity, int CapacityPerLevel,
    decimal? BaseIncomePerSecond = null)
{
    /// <summary>For a business whose catalogue entry lists no place kind.</summary>
    public static PlaceSpec Default(decimal price) =>
        new("main", "Main", PlaceTier.Quiet, price, GameConstants.DefaultPlaceCapacity, GameConstants.DefaultPlaceCapacityPerLevel);
}

/// <summary>Net earnings of a business for a stretch of time away, and the repairs its manager paid.</summary>
public readonly record struct OfflineRun(decimal Earned, int Repairs, decimal RepairsPaid);

/// <summary>
/// One location of a business — "24 Oak Street" — holding its units
/// (features/014-business-places-and-equipment.md). Its numbers are a copy of the catalogue's
/// place kind taken at opening, like a business copies its catalogue entry.
/// </summary>
public class BusinessPlace : BaseEntity
{
    public Guid BusinessId { get; private set; }

    /// <summary>Catalogue place kind (e.g. "airport"); the kind's numbers are copied below.</summary>
    public string KindId { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Street { get; private set; } = string.Empty;
    public PlaceTier Tier { get; private set; }

    /// <summary>1..<see cref="GameConstants.MaxPlaceLevel"/>. Raises capacity and income.</summary>
    public int Level { get; private set; } = 1;

    /// <summary>What the place earns on its own, before its multiplier — it never breaks.</summary>
    public decimal BaseIncomePerSecond { get; private set; }

    public int BaseCapacity { get; private set; }
    public int CapacityPerLevel { get; private set; }

    /// <summary>The kind's price: level 1 → 2 costs <see cref="GameConstants.PlaceLevelCostShare"/> of it.</summary>
    public decimal Price { get; private set; }

    /// <summary>What was paid to open it — 0 for the first place, which comes with the business.</summary>
    public decimal PurchasePrice { get; private set; }

    private BusinessPlace() { }

    public static BusinessPlace Create(
        Guid businessId, string kindId, string name, PlaceTier tier, decimal price, decimal purchasePrice,
        decimal baseIncomePerSecond, int baseCapacity, int capacityPerLevel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kindId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var place = new BusinessPlace
        {
            BusinessId          = businessId,
            KindId              = kindId,
            Name                = name,
            Tier                = tier,
            Price               = price,
            PurchasePrice       = purchasePrice,
            BaseIncomePerSecond = baseIncomePerSecond,
            BaseCapacity        = Math.Max(baseCapacity, 1),
            CapacityPerLevel    = Math.Max(capacityPerLevel, 0),
        };
        place.Street = Operations.StreetNames.For(place.Id);
        return place;
    }

    /// <summary>Every income of the place (its own and its units') is × this.</summary>
    public decimal Multiplier =>
        GameConstants.PlaceTierMultiplier(Tier) * (1m + GameConstants.PlaceLevelIncomeBonus * (Level - 1));

    public int Capacity => CapacityAt(Level);

    public int CapacityAt(int level) => BaseCapacity + CapacityPerLevel * (level - 1);

    public bool IsMaxLevel => Level >= GameConstants.MaxPlaceLevel;

    /// <summary>
    /// Next level's price: <see cref="GameConstants.PlaceLevelCostShare"/> of the place and the units
    /// in it (<paramref name="unitsValue"/>), × growth^(level − 1). Null at max. Use
    /// <see cref="Business.NextLevelCostOf"/>, which knows the units.
    /// </summary>
    internal decimal? NextLevelCostFor(decimal unitsValue)
    {
        if (IsMaxLevel) return null;
        var cost = (Price + unitsValue) * GameConstants.PlaceLevelCostShare;
        for (var i = 1; i < Level; i++) cost *= GameConstants.PlaceLevelCostGrowth;
        return Math.Round(cost, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Price and the max are checked by the aggregate.</summary>
    internal void LevelUp()
    {
        Level++;
        MarkUpdated();
    }
}

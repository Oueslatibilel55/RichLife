using RichLife.Domain.Entities;
using RichLife.Domain.Enums;

namespace RichLife.Domain.Catalogue;

/// <summary>Editable fields of a catalogue place kind — everything except its id.</summary>
public record PlaceKindDetails(string Name, PlaceTier Tier, decimal Price, int DisplayOrder);

/// <summary>
/// Somewhere a business can open a place — "Park", "Airport" (features/014-business-places-and-equipment.md).
/// Part of the <see cref="BusinessCatalogueEntry"/> aggregate: its id is unique within that
/// business only. The kind with the lowest display order is the one a business opens with.
/// </summary>
public sealed class PlaceKindEntry
{
    public string Id { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public PlaceTier Tier { get; private set; }
    public decimal Price { get; private set; }
    public int DisplayOrder { get; private set; }

    private PlaceKindEntry() { }

    internal static PlaceKindEntry Create(string id, PlaceKindDetails details)
    {
        var kind = new PlaceKindEntry { Id = id };
        kind.Apply(details);
        return kind;
    }

    internal void Apply(PlaceKindDetails d)
    {
        Name         = d.Name.Trim();
        Tier         = d.Tier;
        Price        = d.Price;
        DisplayOrder = d.DisplayOrder;
    }

    /// <summary>The numbers a new place of this kind copies, with the business's capacity rule.</summary>
    public PlaceSpec ToSpec(int baseCapacity, int capacityPerLevel) =>
        new(Id, Name, Tier, Price, baseCapacity, capacityPerLevel);

    internal static string? Validate(PlaceKindDetails d)
    {
        if (string.IsNullOrWhiteSpace(d.Name) || d.Name.Trim().Length > BusinessCatalogueEntry.MaxNameLength)
            return $"Place name is required and must be at most {BusinessCatalogueEntry.MaxNameLength} characters.";
        if (!Enum.IsDefined(d.Tier)) return "Unknown place tier.";
        if (d.Price <= 0m) return "Place price must be positive.";
        return null;
    }
}

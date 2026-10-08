namespace RichLife.Domain.Catalogue;

/// <summary>Editable fields of a catalogue asset — everything except its id.</summary>
public record AssetCatalogueDetails(
    string Name,
    decimal Price,
    int UnlockAtAssetCount,
    decimal? FixedIncomePerSecond,
    int DisplayOrder);

/// <summary>
/// An asset that can be bought for one catalogue business. Part of the
/// <see cref="BusinessCatalogueEntry"/> aggregate: its id is unique within that business only.
/// </summary>
public sealed class AssetCatalogueEntry
{
    public string Id { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public decimal Price { get; private set; }

    /// <summary>How many assets the business must already own before this one can be bought.</summary>
    public int UnlockAtAssetCount { get; private set; }

    /// <summary>When null, income is derived from <see cref="Price"/> by the sector ratio.</summary>
    public decimal? FixedIncomePerSecond { get; private set; }

    public int DisplayOrder { get; private set; }

    private AssetCatalogueEntry() { }

    internal static AssetCatalogueEntry Create(string id, AssetCatalogueDetails details)
    {
        var asset = new AssetCatalogueEntry { Id = id };
        asset.Apply(details);
        return asset;
    }

    internal void Apply(AssetCatalogueDetails d)
    {
        Name                 = d.Name.Trim();
        Price                = d.Price;
        UnlockAtAssetCount   = d.UnlockAtAssetCount;
        FixedIncomePerSecond = d.FixedIncomePerSecond;
        DisplayOrder         = d.DisplayOrder;
    }

    internal static string? Validate(AssetCatalogueDetails d)
    {
        if (string.IsNullOrWhiteSpace(d.Name) || d.Name.Trim().Length > BusinessCatalogueEntry.MaxNameLength)
            return $"Asset name is required and must be at most {BusinessCatalogueEntry.MaxNameLength} characters.";
        if (d.Price <= 0m) return "Asset price must be positive.";
        if (d.UnlockAtAssetCount < 0) return "Unlock count cannot be negative.";
        if (d.FixedIncomePerSecond < 0m) return "Asset income cannot be negative.";
        return null;
    }
}

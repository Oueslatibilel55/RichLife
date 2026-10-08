using System.Text.RegularExpressions;
using RichLife.Domain.Common;
using RichLife.Domain.Entities;
using RichLife.Domain.Enums;

namespace RichLife.Domain.Catalogue;

/// <summary>Editable fields of a catalogue business — everything except its id and assets.</summary>
public record BusinessCatalogueDetails(
    string Name,
    BusinessSector Sector,
    PrestigeLevel RequiredPrestige,
    decimal OpeningCost,
    decimal BaseIncomePerSecond,
    decimal MonthlySalaryCost,
    int BaseEmployeeCount,
    string Description,
    int DisplayOrder);

/// <summary>
/// A business players can open: game content, edited by admins and stored in the database.
/// Its own aggregate — a <see cref="Business"/> refers to it by <see cref="Id"/> only and
/// copies its numbers when opened, so editing an entry never changes a business someone
/// already owns.
/// </summary>
public sealed partial class BusinessCatalogueEntry
{
    public const int MaxIdLength = 60;
    public const int MaxNameLength = 80;
    public const int MaxDescriptionLength = 500;

    /// <summary>Slug (e.g. "food-cart"). Immutable: owned businesses reference it.</summary>
    public string Id { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;
    public BusinessSector Sector { get; private set; }
    public PrestigeLevel RequiredPrestige { get; private set; }
    public decimal OpeningCost { get; private set; }
    public decimal BaseIncomePerSecond { get; private set; }
    public decimal MonthlySalaryCost { get; private set; }
    public int BaseEmployeeCount { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public int DisplayOrder { get; private set; }

    /// <summary>
    /// Retired entries cannot be opened any more, but stay in the database because owned
    /// businesses still reference them. There is no delete.
    /// </summary>
    public bool IsActive { get; private set; } = true;

    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    private readonly List<AssetCatalogueEntry> _availableAssets = [];

    public IReadOnlyList<AssetCatalogueEntry> AvailableAssets =>
        _availableAssets.OrderBy(a => a.DisplayOrder).ThenBy(a => a.Id, StringComparer.Ordinal).ToList();

    private BusinessCatalogueEntry() { }

    public static Result<BusinessCatalogueEntry> Create(string id, BusinessCatalogueDetails details)
    {
        if (!IsValidSlug(id)) return Result.Fail<BusinessCatalogueEntry>(InvalidSlugMessage("Catalogue id"));

        var invalid = Validate(details);
        if (invalid is not null) return Result.Fail<BusinessCatalogueEntry>(invalid);

        var entry = new BusinessCatalogueEntry { Id = id };
        entry.Apply(details);
        return Result.Ok(entry);
    }

    // -- Admin --------------------------------------------------------------

    public Result Update(BusinessCatalogueDetails details, bool isActive)
    {
        var invalid = Validate(details);
        if (invalid is not null) return Result.Fail(invalid);

        Apply(details);
        IsActive = isActive;
        MarkUpdated();
        return Result.Ok();
    }

    public Result AddAsset(string assetId, AssetCatalogueDetails details)
    {
        if (!IsValidSlug(assetId)) return Result.Fail(InvalidSlugMessage("Asset id"));
        if (FindAsset(assetId) is not null) return Result.Fail("Asset already exists.");

        var invalid = AssetCatalogueEntry.Validate(details);
        if (invalid is not null) return Result.Fail(invalid);

        _availableAssets.Add(AssetCatalogueEntry.Create(assetId, details));
        MarkUpdated();
        return Result.Ok();
    }

    public Result UpdateAsset(string assetId, AssetCatalogueDetails details)
    {
        var asset = FindAsset(assetId);
        if (asset is null) return Result.Fail("Asset not found.");

        var invalid = AssetCatalogueEntry.Validate(details);
        if (invalid is not null) return Result.Fail(invalid);

        asset.Apply(details);
        MarkUpdated();
        return Result.Ok();
    }

    /// <summary>
    /// Safe for players who already bought it: a <see cref="BusinessAsset"/> keeps its own
    /// copy of name, price and income and does not reference the catalogue.
    /// </summary>
    public Result RemoveAsset(string assetId)
    {
        var asset = FindAsset(assetId);
        if (asset is null) return Result.Fail("Asset not found.");

        _availableAssets.Remove(asset);
        MarkUpdated();
        return Result.Ok();
    }

    // -- Game ---------------------------------------------------------------

    public AssetCatalogueEntry? FindAsset(string assetId)
        => _availableAssets.FirstOrDefault(a => a.Id == assetId);

    /// <summary>
    /// Income per second an asset yields: its fixed value when set, otherwise derived from
    /// its price at this business's sector ratio.
    /// </summary>
    public decimal IncomeFor(AssetCatalogueEntry asset)
        => asset.FixedIncomePerSecond
           ?? asset.Price * (Sector == BusinessSector.Transport
               ? GameConstants.AssetIncomeRatio
               : GameConstants.RentalIncomeRatio);

    /// <summary>A new business stamped with this entry's current numbers.</summary>
    public Business CreateBusiness(Guid companyId)
        => Business.Create(
            companyId, Id, Name, Sector, RequiredPrestige,
            OpeningCost, BaseIncomePerSecond, MonthlySalaryCost, BaseEmployeeCount);

    // -- Internals ----------------------------------------------------------

    private void Apply(BusinessCatalogueDetails d)
    {
        Name                = d.Name.Trim();
        Sector              = d.Sector;
        RequiredPrestige    = d.RequiredPrestige;
        OpeningCost         = d.OpeningCost;
        BaseIncomePerSecond = d.BaseIncomePerSecond;
        MonthlySalaryCost   = d.MonthlySalaryCost;
        BaseEmployeeCount   = d.BaseEmployeeCount;
        Description         = d.Description.Trim();
        DisplayOrder        = d.DisplayOrder;
    }

    private void MarkUpdated() => UpdatedAt = DateTime.UtcNow;

    private static string? Validate(BusinessCatalogueDetails d)
    {
        if (string.IsNullOrWhiteSpace(d.Name) || d.Name.Trim().Length > MaxNameLength)
            return $"Name is required and must be at most {MaxNameLength} characters.";
        if (!Enum.IsDefined(d.Sector)) return "Unknown sector.";
        if (!Enum.IsDefined(d.RequiredPrestige)) return "Unknown prestige level.";
        if (d.OpeningCost <= 0m) return "Opening cost must be positive.";
        if (d.BaseIncomePerSecond < 0m) return "Base income cannot be negative.";
        if (d.MonthlySalaryCost < 0m) return "Monthly salary cost cannot be negative.";
        if (d.BaseEmployeeCount < 0) return "Employee count cannot be negative.";
        if (d.Description is null || d.Description.Trim().Length > MaxDescriptionLength)
            return $"Description must be at most {MaxDescriptionLength} characters.";
        return null;
    }

    internal static bool IsValidSlug(string? id)
        => id is not null && id.Length <= MaxIdLength && SlugPattern().IsMatch(id);

    private static string InvalidSlugMessage(string what)
        => $"{what} must be lowercase letters, digits and single dashes, at most {MaxIdLength} characters.";

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();
}

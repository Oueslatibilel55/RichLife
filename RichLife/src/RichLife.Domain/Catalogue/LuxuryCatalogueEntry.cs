using System.Text.RegularExpressions;
using RichLife.Domain.Common;
using RichLife.Domain.Enums;

namespace RichLife.Domain.Catalogue;

/// <summary>
/// A luxury item players can buy for status (watch, car, yacht, island…). Game content,
/// seeded by migration into <c>luxury_catalogue</c>, like the business catalogue. Keyed by an
/// immutable slug; a bought item copies what it needs, so later edits do not rewrite history.
/// </summary>
public sealed partial class LuxuryCatalogueEntry
{
    public string Id { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public LuxuryCategory Category { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public PrestigeLevel RequiredPrestige { get; private set; }

    /// <summary>Path on the frontend origin, e.g. "/luxury/superyacht.jpg".</summary>
    public string ImageUrl { get; private set; } = string.Empty;

    /// <summary>"Author · License" — shown with the photo, as the license requires.</summary>
    public string ImageCredit { get; private set; } = string.Empty;
    public string ImageSourceUrl { get; private set; } = string.Empty;

    public int DisplayOrder { get; private set; }
    public bool IsActive { get; private set; } = true;

    public const int MaxIdLength = 60;
    public const int MaxNameLength = 80;
    public const int MaxDescriptionLength = 300;
    public const int MaxImageUrlLength = 300;
    public const int MaxImageCreditLength = 200;
    public const int MaxImageSourceUrlLength = 500;

    private LuxuryCatalogueEntry() { }

    // -- Admin (contract §7d) ---------------------------------------------------

    /// <summary>A new item from the admin editor. Starts active.</summary>
    public static Result<LuxuryCatalogueEntry> CreateNew(string id, LuxuryItemDetails details)
    {
        if (id is null || id.Length > MaxIdLength || !SlugPattern().IsMatch(id))
            return Result.Fail<LuxuryCatalogueEntry>(
                $"Item id must be lowercase letters, digits and single dashes, at most {MaxIdLength} characters.");
        if (Validate(details) is { } invalid) return Result.Fail<LuxuryCatalogueEntry>(invalid);

        var entry = new LuxuryCatalogueEntry { Id = id };
        entry.Apply(details);
        return Result.Ok(entry);
    }

    /// <summary>Edits apply to later purchases only — a bought item copied what it needed.</summary>
    public Result Update(LuxuryItemDetails details, bool isActive)
    {
        if (Validate(details) is { } invalid) return Result.Fail(invalid);
        Apply(details);
        IsActive = isActive;
        return Result.Ok();
    }

    private void Apply(LuxuryItemDetails d)
    {
        Name = d.Name.Trim();
        Category = d.Category;
        Description = (d.Description ?? string.Empty).Trim();
        Price = d.Price;
        RequiredPrestige = d.RequiredPrestige;
        ImageUrl = d.ImageUrl.Trim();
        ImageCredit = (d.ImageCredit ?? string.Empty).Trim();
        ImageSourceUrl = (d.ImageSourceUrl ?? string.Empty).Trim();
        DisplayOrder = d.DisplayOrder;
    }

    private static string? Validate(LuxuryItemDetails d)
    {
        if (string.IsNullOrWhiteSpace(d.Name) || d.Name.Trim().Length > MaxNameLength)
            return $"Name is required and must be at most {MaxNameLength} characters.";
        if ((d.Description ?? string.Empty).Trim().Length > MaxDescriptionLength)
            return $"Description must be at most {MaxDescriptionLength} characters.";
        if (d.Price <= 0m) return "Price must be positive.";
        if (!Enum.IsDefined(d.Category)) return "Unknown category.";
        if (!Enum.IsDefined(d.RequiredPrestige)) return "Unknown prestige level.";
        var url = d.ImageUrl?.Trim() ?? string.Empty;
        if (url.Length == 0 || url.Length > MaxImageUrlLength
            || !(url.StartsWith('/') || url.StartsWith("https://", StringComparison.Ordinal)))
            return $"Image URL is required, at most {MaxImageUrlLength} characters, and must start with / or https://.";
        if ((d.ImageCredit ?? string.Empty).Trim().Length > MaxImageCreditLength)
            return $"Image credit must be at most {MaxImageCreditLength} characters.";
        if ((d.ImageSourceUrl ?? string.Empty).Trim().Length > MaxImageSourceUrlLength)
            return $"Image source URL must be at most {MaxImageSourceUrlLength} characters.";
        return null;
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();

    /// <summary>Seeded rows come from the migration; this exists for tests and fixtures.</summary>
    public static LuxuryCatalogueEntry Create(
        string id, string name, LuxuryCategory category, decimal price, PrestigeLevel requiredPrestige,
        string imageUrl = "", string imageCredit = "", string description = "", bool isActive = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (price <= 0m) throw new ArgumentOutOfRangeException(nameof(price), "Price must be positive.");
        return new LuxuryCatalogueEntry
        {
            Id = id, Name = name, Category = category, Price = price, RequiredPrestige = requiredPrestige,
            ImageUrl = imageUrl, ImageCredit = imageCredit, Description = description, IsActive = isActive,
        };
    }
}

/// <summary>The editable fields of a luxury item (everything but its id and active flag).</summary>
public sealed record LuxuryItemDetails(
    string Name,
    LuxuryCategory Category,
    string? Description,
    decimal Price,
    PrestigeLevel RequiredPrestige,
    string ImageUrl,
    string? ImageCredit,
    string? ImageSourceUrl,
    int DisplayOrder);

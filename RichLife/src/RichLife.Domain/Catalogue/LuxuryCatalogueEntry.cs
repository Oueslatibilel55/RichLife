using RichLife.Domain.Enums;

namespace RichLife.Domain.Catalogue;

/// <summary>
/// A luxury item players can buy for status (watch, car, yacht, island…). Game content,
/// seeded by migration into <c>luxury_catalogue</c>, like the business catalogue. Keyed by an
/// immutable slug; a bought item copies what it needs, so later edits do not rewrite history.
/// </summary>
public sealed class LuxuryCatalogueEntry
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

    private LuxuryCatalogueEntry() { }

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

using RichLife.Domain.Common;
using RichLife.Domain.Enums;

namespace RichLife.Domain.Entities;

/// <summary>
/// A luxury item the company bought. Status only — it earns nothing — but it counts toward
/// net worth at <see cref="Cost"/>. Copies the catalogue's name, price and photo at purchase
/// (the same id-plus-copy shape as <see cref="Business"/>). Bought on <see cref="BaseEntity.CreatedAt"/>.
/// </summary>
public class LuxuryAsset : BaseEntity
{
    public Guid CompanyId { get; private set; }

    /// <summary>Id of the <see cref="Catalogue.LuxuryCatalogueEntry"/> it was bought from.</summary>
    public string CatalogueId { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;
    public LuxuryCategory Category { get; private set; }
    public decimal Cost { get; private set; }
    public string ImageUrl { get; private set; } = string.Empty;
    public string ImageCredit { get; private set; } = string.Empty;

    /// <summary>Unused (always 0): luxury is status, not income. Kept from the original model.</summary>
    public decimal IncomeMultiplierBonus { get; private set; }

    private LuxuryAsset() { }

    public static LuxuryAsset Create(Guid companyId, Catalogue.LuxuryCatalogueEntry item, DateTime boughtAtUtc)
        => new()
        {
            CompanyId = companyId,
            CatalogueId = item.Id,
            Name = item.Name,
            Category = item.Category,
            Cost = item.Price,
            ImageUrl = item.ImageUrl,
            ImageCredit = item.ImageCredit,
            CreatedAt = boughtAtUtc,
            UpdatedAt = boughtAtUtc,
        };
}

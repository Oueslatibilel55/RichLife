using RichLife.Domain.Enums;

namespace RichLife.Application.DTOs;

// -- Player view: filtered by prestige, flagged against the caller's company ----------

public record BusinessCatalogueDto(
    string Id,
    string Name,
    string Sector,
    string RequiredPrestige,
    decimal OpeningCost,
    decimal BaseIncomePerSecond,
    decimal MonthlySalaryCost,
    int BaseEmployeeCount,
    string Description,
    bool CanAfford,
    bool IsOwned,
    IReadOnlyList<AssetCatalogueDto> AvailableAssets
);

public record AssetCatalogueDto(
    string Id,
    string Name,
    decimal Price,
    decimal IncomePerSecond,
    int UnlockAtAssetCount,
    bool IsUnlocked
);

// -- Admin view: every entry, with the raw fields an editor needs ---------------------

public record AdminCatalogueEntryDto(
    string Id,
    string Name,
    BusinessSector Sector,
    PrestigeLevel RequiredPrestige,
    decimal OpeningCost,
    decimal BaseIncomePerSecond,
    decimal MonthlySalaryCost,
    int BaseEmployeeCount,
    string Description,
    int DisplayOrder,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<AdminCatalogueAssetDto> AvailableAssets
);

public record AdminCatalogueAssetDto(
    string Id,
    string Name,
    decimal Price,
    int UnlockAtAssetCount,
    decimal? FixedIncomePerSecond,
    decimal IncomePerSecond,
    int DisplayOrder
);

public record CreateCatalogueEntryRequest(
    string Id,
    string Name,
    BusinessSector Sector,
    PrestigeLevel RequiredPrestige,
    decimal OpeningCost,
    decimal BaseIncomePerSecond,
    decimal MonthlySalaryCost,
    int BaseEmployeeCount,
    string Description,
    int DisplayOrder,
    IReadOnlyList<CreateCatalogueAssetRequest>? AvailableAssets
);

public record UpdateCatalogueEntryRequest(
    string Name,
    BusinessSector Sector,
    PrestigeLevel RequiredPrestige,
    decimal OpeningCost,
    decimal BaseIncomePerSecond,
    decimal MonthlySalaryCost,
    int BaseEmployeeCount,
    string Description,
    int DisplayOrder,
    bool IsActive
);

public record CreateCatalogueAssetRequest(
    string Id,
    string Name,
    decimal Price,
    int UnlockAtAssetCount,
    decimal? FixedIncomePerSecond,
    int DisplayOrder
);

public record UpdateCatalogueAssetRequest(
    string Name,
    decimal Price,
    int UnlockAtAssetCount,
    decimal? FixedIncomePerSecond,
    int DisplayOrder
);

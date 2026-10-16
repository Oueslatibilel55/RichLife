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
    IReadOnlyList<AssetCatalogueDto> AvailableAssets,
    int PlaceBaseCapacity,
    int PlaceCapacityPerLevel,
    IReadOnlyList<PlaceKindDto> PlaceKinds,
    string Kind
);

public record AssetCatalogueDto(
    string Id,
    string Name,
    decimal Price,
    decimal IncomePerSecond,
    int UnlockAtAssetCount,
    bool IsUnlocked,
    int MtbfHours
);

/// <summary>Where a business can open a place. The first one is where it opens.</summary>
public record PlaceKindDto(
    string Id,
    string Name,
    string Tier,
    decimal Price,
    decimal IncomeMultiplier,
    bool CanAfford,
    decimal BaseIncomePerSecond
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
    IReadOnlyList<AdminCatalogueAssetDto> AvailableAssets,
    int PlaceBaseCapacity,
    int PlaceCapacityPerLevel,
    IReadOnlyList<AdminPlaceKindDto> PlaceKinds,
    BusinessKind Kind
);

public record AdminCatalogueAssetDto(
    string Id,
    string Name,
    decimal Price,
    int UnlockAtAssetCount,
    decimal? FixedIncomePerSecond,
    decimal IncomePerSecond,
    int DisplayOrder,
    int MtbfHours
);

public record AdminPlaceKindDto(
    string Id,
    string Name,
    PlaceTier Tier,
    decimal Price,
    int DisplayOrder,
    decimal? BaseIncomePerSecond
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
    IReadOnlyList<CreateCatalogueAssetRequest>? AvailableAssets,
    int? PlaceBaseCapacity = null,
    int? PlaceCapacityPerLevel = null,
    IReadOnlyList<CreatePlaceKindRequest>? PlaceKinds = null,
    BusinessKind? Kind = null
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
    bool IsActive,
    int? PlaceBaseCapacity = null,
    int? PlaceCapacityPerLevel = null,
    BusinessKind? Kind = null
);

public record CreateCatalogueAssetRequest(
    string Id,
    string Name,
    decimal Price,
    int UnlockAtAssetCount,
    decimal? FixedIncomePerSecond,
    int DisplayOrder,
    int? MtbfHours = null
);

public record UpdateCatalogueAssetRequest(
    string Name,
    decimal Price,
    int UnlockAtAssetCount,
    decimal? FixedIncomePerSecond,
    int DisplayOrder,
    int? MtbfHours = null
);

public record CreatePlaceKindRequest(
    string Id,
    string Name,
    PlaceTier Tier,
    decimal Price,
    int DisplayOrder,
    decimal? BaseIncomePerSecond = null
);

public record UpdatePlaceKindRequest(
    string Name,
    PlaceTier Tier,
    decimal Price,
    int DisplayOrder,
    decimal? BaseIncomePerSecond = null
);

using RichLife.Application.DTOs;
using RichLife.Domain;
using RichLife.Domain.Catalogue;
using RichLife.Domain.Entities;

namespace RichLife.Application.Mapping;

/// <summary>Turns catalogue entries into the player and admin contracts, and requests back into domain details.</summary>
public static class CatalogueMapper
{
    public static BusinessCatalogueDto ToPlayerDto(
        BusinessCatalogueEntry e, decimal playerCash, Business? owned) => new(
        e.Id, e.Name, e.Sector.ToString(), e.RequiredPrestige.ToString(),
        e.OpeningCost, e.BaseIncomePerSecond, e.MonthlySalaryCost,
        e.BaseEmployeeCount, e.Description,
        CanAfford: playerCash >= e.OpeningCost,
        IsOwned: owned is not null,
        e.AvailableAssets
            .Select(a => new AssetCatalogueDto(
                a.Id, a.Name, a.Price, e.IncomeFor(a), a.UnlockAtAssetCount,
                IsUnlocked: (owned?.Assets.Count ?? 0) >= a.UnlockAtAssetCount,
                a.MtbfHours))
            .ToList(),
        e.PlaceBaseCapacity,
        e.PlaceCapacityPerLevel,
        e.PlaceKinds
            .Select(k => new PlaceKindDto(
                k.Id, k.Name, k.Tier.ToString(), k.Price, GameConstants.PlaceTierMultiplier(k.Tier),
                CanAfford: playerCash >= k.Price,
                BaseIncomePerSecond: k.BaseIncomePerSecond ?? e.BaseIncomePerSecond))
            .ToList(),
        e.Kind.ToString());

    public static AdminCatalogueEntryDto ToAdminDto(BusinessCatalogueEntry e) => new(
        e.Id, e.Name, e.Sector, e.RequiredPrestige,
        e.OpeningCost, e.BaseIncomePerSecond, e.MonthlySalaryCost,
        e.BaseEmployeeCount, e.Description, e.DisplayOrder, e.IsActive,
        e.CreatedAt, e.UpdatedAt,
        e.AvailableAssets
            .Select(a => new AdminCatalogueAssetDto(
                a.Id, a.Name, a.Price, a.UnlockAtAssetCount,
                a.FixedIncomePerSecond, e.IncomeFor(a), a.DisplayOrder, a.MtbfHours))
            .ToList(),
        e.PlaceBaseCapacity,
        e.PlaceCapacityPerLevel,
        e.PlaceKinds
            .Select(k => new AdminPlaceKindDto(k.Id, k.Name, k.Tier, k.Price, k.DisplayOrder, k.BaseIncomePerSecond))
            .ToList(),
        e.Kind);

    public static BusinessCatalogueDetails ToDetails(CreateCatalogueEntryRequest r) => new(
        r.Name, r.Sector, r.RequiredPrestige, r.OpeningCost, r.BaseIncomePerSecond,
        r.MonthlySalaryCost, r.BaseEmployeeCount, r.Description ?? string.Empty, r.DisplayOrder,
        r.PlaceBaseCapacity ?? GameConstants.DefaultPlaceCapacity,
        r.PlaceCapacityPerLevel ?? GameConstants.DefaultPlaceCapacityPerLevel,
        r.Kind ?? Domain.Enums.BusinessKind.Equipment);

    /// <summary>Capacity fields left out keep the entry's current values.</summary>
    public static BusinessCatalogueDetails ToDetails(UpdateCatalogueEntryRequest r, BusinessCatalogueEntry current) => new(
        r.Name, r.Sector, r.RequiredPrestige, r.OpeningCost, r.BaseIncomePerSecond,
        r.MonthlySalaryCost, r.BaseEmployeeCount, r.Description ?? string.Empty, r.DisplayOrder,
        r.PlaceBaseCapacity ?? current.PlaceBaseCapacity,
        r.PlaceCapacityPerLevel ?? current.PlaceCapacityPerLevel,
        r.Kind ?? current.Kind);

    public static AssetCatalogueDetails ToDetails(CreateCatalogueAssetRequest r) => new(
        r.Name, r.Price, r.UnlockAtAssetCount, r.FixedIncomePerSecond, r.DisplayOrder,
        r.MtbfHours ?? GameConstants.DefaultMtbfHours);

    /// <summary>A missing <c>mtbfHours</c> keeps the asset's current value.</summary>
    public static AssetCatalogueDetails ToDetails(UpdateCatalogueAssetRequest r, AssetCatalogueEntry? current) => new(
        r.Name, r.Price, r.UnlockAtAssetCount, r.FixedIncomePerSecond, r.DisplayOrder,
        r.MtbfHours ?? current?.MtbfHours ?? GameConstants.DefaultMtbfHours);

    public static PlaceKindDetails ToDetails(CreatePlaceKindRequest r) => new(r.Name ?? string.Empty, r.Tier, r.Price, r.DisplayOrder, r.BaseIncomePerSecond);

    public static PlaceKindDetails ToDetails(UpdatePlaceKindRequest r) => new(r.Name ?? string.Empty, r.Tier, r.Price, r.DisplayOrder, r.BaseIncomePerSecond);
}

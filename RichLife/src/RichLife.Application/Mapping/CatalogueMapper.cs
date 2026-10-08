using RichLife.Application.DTOs;
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
                IsUnlocked: (owned?.Assets.Count ?? 0) >= a.UnlockAtAssetCount))
            .ToList());

    public static AdminCatalogueEntryDto ToAdminDto(BusinessCatalogueEntry e) => new(
        e.Id, e.Name, e.Sector, e.RequiredPrestige,
        e.OpeningCost, e.BaseIncomePerSecond, e.MonthlySalaryCost,
        e.BaseEmployeeCount, e.Description, e.DisplayOrder, e.IsActive,
        e.CreatedAt, e.UpdatedAt,
        e.AvailableAssets
            .Select(a => new AdminCatalogueAssetDto(
                a.Id, a.Name, a.Price, a.UnlockAtAssetCount,
                a.FixedIncomePerSecond, e.IncomeFor(a), a.DisplayOrder))
            .ToList());

    public static BusinessCatalogueDetails ToDetails(CreateCatalogueEntryRequest r) => new(
        r.Name, r.Sector, r.RequiredPrestige, r.OpeningCost, r.BaseIncomePerSecond,
        r.MonthlySalaryCost, r.BaseEmployeeCount, r.Description ?? string.Empty, r.DisplayOrder);

    public static BusinessCatalogueDetails ToDetails(UpdateCatalogueEntryRequest r) => new(
        r.Name, r.Sector, r.RequiredPrestige, r.OpeningCost, r.BaseIncomePerSecond,
        r.MonthlySalaryCost, r.BaseEmployeeCount, r.Description ?? string.Empty, r.DisplayOrder);

    public static AssetCatalogueDetails ToDetails(CreateCatalogueAssetRequest r) => new(
        r.Name, r.Price, r.UnlockAtAssetCount, r.FixedIncomePerSecond, r.DisplayOrder);

    public static AssetCatalogueDetails ToDetails(UpdateCatalogueAssetRequest r) => new(
        r.Name, r.Price, r.UnlockAtAssetCount, r.FixedIncomePerSecond, r.DisplayOrder);
}

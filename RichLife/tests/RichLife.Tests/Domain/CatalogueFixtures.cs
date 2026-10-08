using RichLife.Domain.Catalogue;
using RichLife.Domain.Enums;

namespace RichLife.Tests.Domain;

/// <summary>Builds catalogue entries in memory — the domain tests never touch the database.</summary>
internal static class CatalogueFixtures
{
    public static BusinessCatalogueDetails Details(
        decimal openingCost = 1_000m,
        decimal baseIncomePerSecond = 5m,
        decimal monthlySalaryCost = 0m,
        BusinessSector sector = BusinessSector.Hospitality,
        PrestigeLevel requiredPrestige = PrestigeLevel.TheHustle,
        string name = "Food Cart") =>
        new(name, sector, requiredPrestige, openingCost, baseIncomePerSecond,
            monthlySalaryCost, BaseEmployeeCount: 0, Description: "A test business.", DisplayOrder: 0);

    public static AssetCatalogueDetails AssetDetails(
        decimal price = 500m,
        int unlockAtAssetCount = 0,
        decimal? fixedIncomePerSecond = 1m,
        string name = "Menu item") =>
        new(name, price, unlockAtAssetCount, fixedIncomePerSecond, DisplayOrder: 0);

    public static BusinessCatalogueEntry Entry(
        string id = "food-cart",
        decimal openingCost = 1_000m,
        decimal baseIncomePerSecond = 5m,
        decimal monthlySalaryCost = 0m,
        BusinessSector sector = BusinessSector.Hospitality,
        PrestigeLevel requiredPrestige = PrestigeLevel.TheHustle)
        => BusinessCatalogueEntry.Create(
            id, Details(openingCost, baseIncomePerSecond, monthlySalaryCost, sector, requiredPrestige)).Value;
}

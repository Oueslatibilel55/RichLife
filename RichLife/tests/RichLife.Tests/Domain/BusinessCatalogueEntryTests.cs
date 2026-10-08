using RichLife.Domain;
using RichLife.Domain.Catalogue;
using RichLife.Domain.Enums;

namespace RichLife.Tests.Domain;

public class BusinessCatalogueEntryTests
{
    // -- Create -------------------------------------------------------------

    [Fact]
    public void Create_WithValidDetails_IsActiveAndHasNoAssets()
    {
        var result = BusinessCatalogueEntry.Create("food-cart", CatalogueFixtures.Details());

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsActive);
        Assert.Empty(result.Value.AvailableAssets);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Food-Cart")]
    [InlineData("food cart")]
    [InlineData("food--cart")]
    [InlineData("-food-cart")]
    [InlineData("food-cart-")]
    public void Create_RejectsAnIdThatIsNotASlug(string id)
    {
        var result = BusinessCatalogueEntry.Create(id, CatalogueFixtures.Details());

        Assert.False(result.IsSuccess);
        Assert.StartsWith("Catalogue id must be", result.Error);
    }

    [Fact]
    public void Create_RejectsAnIdLongerThanTheColumn()
        => Assert.False(BusinessCatalogueEntry.Create(new string('a', 61), CatalogueFixtures.Details()).IsSuccess);

    [Fact]
    public void Create_RejectsANonPositiveOpeningCost()
    {
        var result = BusinessCatalogueEntry.Create("food-cart", CatalogueFixtures.Details(openingCost: 0m));

        Assert.False(result.IsSuccess);
        Assert.Equal("Opening cost must be positive.", result.Error);
    }

    [Fact]
    public void Create_RejectsNegativeIncome()
        => Assert.Equal("Base income cannot be negative.",
            BusinessCatalogueEntry.Create("food-cart", CatalogueFixtures.Details(baseIncomePerSecond: -1m)).Error);

    [Fact]
    public void Create_RejectsAnUndefinedSector()
        => Assert.Equal("Unknown sector.",
            BusinessCatalogueEntry.Create("food-cart", CatalogueFixtures.Details(sector: (BusinessSector)99)).Error);

    [Fact]
    public void Create_RejectsABlankName()
        => Assert.False(BusinessCatalogueEntry.Create("food-cart", CatalogueFixtures.Details(name: "  ")).IsSuccess);

    // -- Update -------------------------------------------------------------

    [Fact]
    public void Update_ReplacesTheFields_AndCanRetireTheEntry()
    {
        var entry = CatalogueFixtures.Entry();

        var result = entry.Update(CatalogueFixtures.Details(openingCost: 2_000m, name: "Food Truck"), isActive: false);

        Assert.True(result.IsSuccess);
        Assert.Equal("Food Truck", entry.Name);
        Assert.Equal(2_000m, entry.OpeningCost);
        Assert.False(entry.IsActive);
    }

    [Fact]
    public void Update_WithInvalidDetails_ChangesNothing()
    {
        var entry = CatalogueFixtures.Entry(openingCost: 1_000m);

        var result = entry.Update(CatalogueFixtures.Details(openingCost: -5m), isActive: false);

        Assert.False(result.IsSuccess);
        Assert.Equal(1_000m, entry.OpeningCost);
        Assert.True(entry.IsActive);
    }

    // -- Assets -------------------------------------------------------------

    [Fact]
    public void AddAsset_RejectsADuplicateId()
    {
        var entry = CatalogueFixtures.Entry();
        entry.AddAsset("menu-item", CatalogueFixtures.AssetDetails());

        var result = entry.AddAsset("menu-item", CatalogueFixtures.AssetDetails());

        Assert.False(result.IsSuccess);
        Assert.Equal("Asset already exists.", result.Error);
        Assert.Single(entry.AvailableAssets);
    }

    [Fact]
    public void AddAsset_RejectsANonPositivePrice()
        => Assert.Equal("Asset price must be positive.",
            CatalogueFixtures.Entry().AddAsset("menu-item", CatalogueFixtures.AssetDetails(price: 0m)).Error);

    [Fact]
    public void AddAsset_RejectsAnIdThatIsNotASlug()
        => Assert.StartsWith("Asset id must be",
            CatalogueFixtures.Entry().AddAsset("Menu Item", CatalogueFixtures.AssetDetails()).Error);

    [Fact]
    public void AvailableAssets_AreOrderedByDisplayOrder()
    {
        var entry = CatalogueFixtures.Entry();
        entry.AddAsset("second", CatalogueFixtures.AssetDetails() with { DisplayOrder = 20 });
        entry.AddAsset("first", CatalogueFixtures.AssetDetails() with { DisplayOrder = 10 });

        Assert.Equal(["first", "second"], entry.AvailableAssets.Select(a => a.Id));
    }

    [Fact]
    public void UpdateAsset_ReplacesItsFields()
    {
        var entry = CatalogueFixtures.Entry();
        entry.AddAsset("menu-item", CatalogueFixtures.AssetDetails(price: 500m));

        var result = entry.UpdateAsset("menu-item", CatalogueFixtures.AssetDetails(price: 750m, unlockAtAssetCount: 3));

        Assert.True(result.IsSuccess);
        Assert.Equal(750m, entry.FindAsset("menu-item")!.Price);
        Assert.Equal(3, entry.FindAsset("menu-item")!.UnlockAtAssetCount);
    }

    [Fact]
    public void UpdateAsset_Unknown_Fails()
        => Assert.Equal("Asset not found.",
            CatalogueFixtures.Entry().UpdateAsset("nope", CatalogueFixtures.AssetDetails()).Error);

    [Fact]
    public void RemoveAsset_TakesItOffSale()
    {
        var entry = CatalogueFixtures.Entry();
        entry.AddAsset("menu-item", CatalogueFixtures.AssetDetails());

        Assert.True(entry.RemoveAsset("menu-item").IsSuccess);
        Assert.Null(entry.FindAsset("menu-item"));
        Assert.Equal("Asset not found.", entry.RemoveAsset("menu-item").Error);
    }

    // -- Income -------------------------------------------------------------

    [Fact]
    public void IncomeFor_PrefersTheAssetsFixedIncome()
    {
        var entry = CatalogueFixtures.Entry();
        entry.AddAsset("menu-item", CatalogueFixtures.AssetDetails(price: 10_000m, fixedIncomePerSecond: 2m));

        Assert.Equal(2m, entry.IncomeFor(entry.FindAsset("menu-item")!));
    }

    [Fact]
    public void IncomeFor_WithoutAFixedIncome_UsesTheTransportRatio()
    {
        var entry = CatalogueFixtures.Entry(id: "transport-company", sector: BusinessSector.Transport);
        entry.AddAsset("truck", CatalogueFixtures.AssetDetails(price: 100_000m, fixedIncomePerSecond: null));

        Assert.Equal(100_000m * GameConstants.AssetIncomeRatio, entry.IncomeFor(entry.FindAsset("truck")!));
    }

    [Fact]
    public void IncomeFor_WithoutAFixedIncome_UsesTheRentalRatioOutsideTransport()
    {
        var entry = CatalogueFixtures.Entry(sector: BusinessSector.RealEstate);
        entry.AddAsset("room", CatalogueFixtures.AssetDetails(price: 8_000m, fixedIncomePerSecond: null));

        Assert.Equal(8_000m * GameConstants.RentalIncomeRatio, entry.IncomeFor(entry.FindAsset("room")!));
    }

    // -- CreateBusiness -----------------------------------------------------

    [Fact]
    public void CreateBusiness_CopiesTheEntrysNumbers()
    {
        var entry = CatalogueFixtures.Entry(
            id: "taxi-fleet", openingCost: 50_000m, baseIncomePerSecond: 30m, monthlySalaryCost: 4_800m,
            sector: BusinessSector.Transport, requiredPrestige: PrestigeLevel.SmallBusiness);
        var companyId = Guid.NewGuid();

        var business = entry.CreateBusiness(companyId);

        Assert.Equal(companyId, business.CompanyId);
        Assert.Equal("taxi-fleet", business.CatalogueId);
        Assert.Equal(BusinessSector.Transport, business.Sector);
        Assert.Equal(PrestigeLevel.SmallBusiness, business.RequiredPrestige);
        Assert.Equal(50_000m, business.OpeningCost);
        Assert.Equal(30m, business.GrossIncomePerSecond);
        Assert.Equal(4_800m, business.MonthlySalaryCost);
    }
}

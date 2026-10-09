using RichLife.Domain.Catalogue;
using RichLife.Domain.Enums;

namespace RichLife.Tests.Domain;

public class LuxuryCatalogueEntryTests
{
    private static LuxuryItemDetails Details(
        string name = "Rolex Submariner", decimal price = 150_000m, string imageUrl = "/luxury/rolex.jpg") =>
        new(name, LuxuryCategory.Watch, "A diver's watch.", price, PrestigeLevel.SmallBusiness,
            imageUrl, "Author · CC BY-SA 4.0", "https://commons.wikimedia.org/x", 10);

    [Fact]
    public void CreateNew_WithValidDetails_StartsActive()
    {
        var result = LuxuryCatalogueEntry.CreateNew("rolex-submariner", Details());

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsActive);
        Assert.Equal(150_000m, result.Value.Price);
    }

    [Theory]
    [InlineData("Rolex")]
    [InlineData("rolex--sub")]
    [InlineData("rolex sub")]
    [InlineData("")]
    public void CreateNew_WithABadSlug_Fails(string id)
    {
        Assert.False(LuxuryCatalogueEntry.CreateNew(id, Details()).IsSuccess);
    }

    [Theory]
    [InlineData("", 1, "/x.jpg", "Name is required and must be at most 80 characters.")]
    [InlineData("Watch", 0, "/x.jpg", "Price must be positive.")]
    [InlineData("Watch", 1, "", "Image URL is required, at most 300 characters, and must start with / or https://.")]
    [InlineData("Watch", 1, "http://x.jpg", "Image URL is required, at most 300 characters, and must start with / or https://.")]
    public void CreateNew_WithInvalidDetails_ReturnsTheMessage(string name, decimal price, string url, string error)
    {
        Assert.Equal(error, LuxuryCatalogueEntry.CreateNew("item", Details(name, price, url)).Error);
    }

    [Fact]
    public void Update_ChangesTheFields_AndCanRetire()
    {
        var item = LuxuryCatalogueEntry.CreateNew("rolex-submariner", Details()).Value;

        var result = item.Update(Details(price: 200_000m), isActive: false);

        Assert.True(result.IsSuccess);
        Assert.Equal(200_000m, item.Price);
        Assert.False(item.IsActive);
    }

    [Fact]
    public void Update_WithInvalidDetails_ChangesNothing()
    {
        var item = LuxuryCatalogueEntry.CreateNew("rolex-submariner", Details()).Value;

        Assert.False(item.Update(Details(price: -5m), isActive: false).IsSuccess);
        Assert.Equal(150_000m, item.Price);
        Assert.True(item.IsActive);
    }
}

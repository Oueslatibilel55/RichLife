using RichLife.Domain;
using RichLife.Domain.Catalogue;
using RichLife.Domain.Entities;
using RichLife.Domain.Enums;

namespace RichLife.Tests.Domain;

public class BusinessTests
{
    private static Business NewBusiness(
        decimal openingCost = 1_000m,
        decimal grossIncomePerSecond = 5m,
        decimal monthlySalaryCost = 0m) =>
        Business.Create(
            Guid.NewGuid(), "food-cart", "Food Cart", BusinessSector.Hospitality,
            PrestigeLevel.TheHustle, openingCost, grossIncomePerSecond, monthlySalaryCost, 0);

    [Fact]
    public void NetIncome_SubtractsSalaryAmortizedOverAThirtyDayMonth()
    {
        var business = NewBusiness(grossIncomePerSecond: 5m, monthlySalaryCost: 2_592_000m);

        // A monthly salary equal to the seconds in the month costs exactly $1/s.
        Assert.Equal(1m, business.SalaryCostPerSecond);
        Assert.Equal(4m, business.NetIncomePerSecond);
    }

    [Fact]
    public void NetIncome_CanGoNegativeWhenSalariesOutrunRevenue()
    {
        var business = NewBusiness(grossIncomePerSecond: 1m, monthlySalaryCost: 2_592_000m * 3m);

        Assert.Equal(-2m, business.NetIncomePerSecond);
    }

    [Fact]
    public void TotalValue_IsOpeningCostPlusAssets()
    {
        var business = NewBusiness(openingCost: 1_000m);
        business.AddAsset(BusinessAsset.Create(business.Id, "Truck", 500m, 0.15m));
        business.AddAsset(BusinessAsset.Create(business.Id, "Van", 250m, 0.08m));

        Assert.Equal(1_750m, business.TotalValue);
    }

    [Fact]
    public void AddAsset_RaisesGrossIncomeByTheAssetsYield()
    {
        var business = NewBusiness(grossIncomePerSecond: 5m);

        business.AddAsset(BusinessAsset.Create(business.Id, "Truck", 500m, 0.15m));

        Assert.Equal(5.15m, business.GrossIncomePerSecond);
        Assert.Single(business.Assets);
    }

    [Fact]
    public void HireManager_StartsAFourHourShift_AndRecordsTheManager()
    {
        var business = NewBusiness();
        var now = new DateTime(2026, 1, 1, 11, 0, 0, DateTimeKind.Utc);

        Assert.False(business.HasManagerAt(now));
        Assert.Null(business.ManagerName);
        business.HireManager(ManagerName.Create(7, "Lucy"), now);
        Assert.True(business.HasManagerAt(now));
        Assert.Equal(now.AddHours(4), business.ManagerUntil);
        Assert.True(business.HasManagerAt(now.AddHours(4).AddSeconds(-1)));
        Assert.False(business.HasManagerAt(now.AddHours(4)));
        Assert.Equal(7, business.ManagerNameId);
        Assert.Equal("Lucy", business.ManagerName);
    }

    [Fact]
    public void ListForSale_ThenCancel_ClearsTheAskingPrice()
    {
        var business = NewBusiness();

        business.ListForSale(5_000m);
        Assert.True(business.IsForSale);
        Assert.Equal(5_000m, business.AskingPrice);

        business.CancelSaleListing();
        Assert.False(business.IsForSale);
        Assert.Null(business.AskingPrice);
    }

    [Fact]
    public void Create_RequiresACatalogueId()
        => Assert.Throws<ArgumentException>(() => Business.Create(
            Guid.NewGuid(), "  ", "Food Cart", BusinessSector.Hospitality,
            PrestigeLevel.TheHustle, 1_000m, 5m, 0m, 0));

    [Fact]
    public void SalaryAmortization_UsesTheSharedConstant()
        => Assert.Equal(2_592_000m, GameConstants.SecondsPerSalaryMonth);
}

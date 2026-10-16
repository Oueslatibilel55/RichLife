using RichLife.Domain;
using RichLife.Domain.Entities;
using RichLife.Domain.Market;

namespace RichLife.Tests.Domain;

/// <summary>features/017-bourse.md</summary>
public class MarketTests
{
    private static readonly DateTime T0 = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly StockDefinition Nova = StockCatalog.Find("NOVA")!;

    private static Company NewCompany(decimal cash)
    {
        var company = Company.Create(Guid.NewGuid(), "Acme", cash);
        company.AccrueOffline(T0);
        return company;
    }

    [Fact]
    public void Prices_AreTheSameForEveryoneAndEveryCall()
    {
        Assert.Equal(StockMarket.PriceAt(Nova, T0), StockMarket.PriceAt(Nova, T0));
        Assert.NotEqual(StockMarket.PriceAt(Nova, T0), StockMarket.PriceAt(Nova, T0.AddHours(3)));
    }

    [Fact]
    public void Prices_StayAroundTheBasePrice_AndMoveSmoothly()
    {
        foreach (var stock in StockCatalog.All)
        {
            var points = StockMarket.History(stock, T0, TimeSpan.FromHours(1), 24 * 60);
            var bound = Math.Exp(stock.Volatility * 2.2);
            Assert.All(points, p =>
                Assert.InRange((double)p.Price, (double)stock.BasePrice / bound, (double)stock.BasePrice * bound));

            // A minute never jumps more than a fraction of a percent.
            var a = StockMarket.PriceAt(stock, T0);
            var b = StockMarket.PriceAt(stock, T0.AddMinutes(1));
            Assert.True(Math.Abs(b - a) / a < 0.01m, $"{stock.Ticker} jumped {a} → {b}");
        }
    }

    [Fact]
    public void Prices_StepEveryThirtySeconds_OnTheSameTickForEveryone()
    {
        var start = StockMarket.TickStart(T0.AddSeconds(7));
        Assert.Equal(T0, start);                                       // T0 is on a tick boundary
        Assert.Equal(T0.AddSeconds(30), StockMarket.NextTickAt(T0.AddSeconds(7)));

        // Same price anywhere inside a tick, a new one on the next.
        Assert.Equal(StockMarket.PriceAt(Nova, T0), StockMarket.PriceAt(Nova, T0.AddSeconds(29)));
        Assert.Equal(StockMarket.PriceAt(Nova, T0.AddSeconds(30)), StockMarket.PriceAt(Nova, T0.AddSeconds(59)));
        Assert.True(StockMarket.Volume24h(Nova, T0) > 0);
    }

    [Fact]
    public void Tickers_AreUnique_AndFoundWhateverTheCase()
    {
        Assert.Equal(StockCatalog.All.Count, StockCatalog.All.Select(s => s.Ticker).Distinct().Count());
        Assert.Same(Nova, StockCatalog.Find("nova"));
        Assert.Null(StockCatalog.Find("XXXX"));
    }

    [Fact]
    public void Buy_PaysThePricePlusTheFee_AndHoldsTheShares()
    {
        var company = NewCompany(5_000m);
        var price = StockMarket.PriceAt(Nova, T0);

        Assert.True(company.BuyStock(Nova, 10, T0).IsSuccess);

        var cost = Math.Round(price * 10 * (1m + GameConstants.StockFeeRate), 2);
        Assert.Equal(5_000m - cost, company.Cash);
        var holding = Assert.Single(company.Stocks);
        Assert.Equal(10, holding.Shares);
        Assert.Equal(cost, holding.CostBasis);
        Assert.Equal(cost, company.InvestedInStocks);
        Assert.Equal(5_000m, company.NetWorth);   // held shares count at cost
    }

    [Fact]
    public void Buy_RefusesNoShares_TooLittleCash_AndPastTheLimit()
    {
        var poor = NewCompany(10m);
        Assert.Equal("Choose at least 1 share.", poor.BuyStock(Nova, 0, T0).Error);
        Assert.Equal("Insufficient funds.", poor.BuyStock(Nova, 1, T0).Error);

        var rich = NewCompany(1_000_000m);
        Assert.Equal(GameConstants.StockMinInvestLimit, rich.StockInvestLimit);   // no income yet: the floor
        Assert.Equal("Investing limit reached: you can hold at most one day of income on the Bourse.",
            rich.BuyStock(Nova, 1_000, T0).Error);
        Assert.Equal(1_000_000m, rich.Cash);
    }

    [Fact]
    public void Sell_PaysThePriceMinusTheFee_AndAProfitCountsAsEarnings()
    {
        var company = NewCompany(9_000m);
        company.BuyStock(Nova, 20, T0);
        var basis = company.InvestedInStocks;

        // Find a later minute where the price is higher, to sell at a profit.
        var later = Enumerable.Range(1, 2_000).Select(m => T0.AddMinutes(m * 7))
            .First(t => StockMarket.PriceAt(Nova, t) * 0.99m > StockMarket.PriceAt(Nova, T0) * 1.02m);
        var cashBefore = company.Cash;
        var proceeds = Math.Round(StockMarket.PriceAt(Nova, later) * 20 * (1m - GameConstants.StockFeeRate), 2);

        Assert.True(company.SellStock(Nova, 20, later).IsSuccess);

        Assert.Equal(cashBefore + proceeds, company.Cash);
        Assert.Empty(company.Stocks);
        Assert.Equal(proceeds - basis, company.AllTimeEarnings);
    }

    [Fact]
    public void Sell_Part_KeepsTheRestAtItsShareOfTheCost_AndALossIsNotTakenFromEarnings()
    {
        var company = NewCompany(9_000m);
        company.BuyStock(Nova, 10, T0);
        var basis = company.InvestedInStocks;

        Assert.True(company.SellStock(Nova, 4, T0).IsSuccess);   // same minute: a loss of the fees

        Assert.Equal(6, company.Stocks[0].Shares);
        Assert.Equal(basis - Math.Round(basis * 4 / 10, 2), company.Stocks[0].CostBasis);
        Assert.Equal(0m, company.AllTimeEarnings);
        Assert.Equal("You do not own that many shares.", company.SellStock(Nova, 7, T0).Error);
    }

    [Fact]
    public void AdminReset_SellsNothing_ButClearsTheHoldings()
    {
        var company = NewCompany(5_000m);
        company.BuyStock(Nova, 5, T0);

        company.AdminReset(T0);

        Assert.Empty(company.Stocks);
        Assert.Equal(GameConstants.StartingCash, company.Cash);
    }
}

using RichLife.Application.DTOs;
using RichLife.Application.Interfaces;
using RichLife.Domain;
using RichLife.Domain.Common;
using RichLife.Domain.Entities;
using RichLife.Domain.Market;

namespace RichLife.Application.Services;

/// <summary>The Bourse (contract §6f). Prices are a function of time (<see cref="StockMarket"/>); the rules are on Company.</summary>
public class MarketService(
    ICompanyRepository companyRepo,
    IUnitOfWork uow,
    TimeProvider clock)
{
    private DateTime UtcNow => clock.GetUtcNow().UtcDateTime;

    public async Task<Result<MarketDto>> GetAsync(Guid playerId, CancellationToken ct = default)
    {
        var company = await companyRepo.GetByPlayerIdAsync(playerId, ct);
        return company is null
            ? Result.Fail<MarketDto>("Company not found.")
            : Result.Ok(ToDto(company, UtcNow));
    }

    public Result<StockHistoryDto> History(string ticker, string? range)
    {
        var stock = StockCatalog.Find(ticker);
        if (stock is null) return Result.Fail<StockHistoryDto>("Stock not found.");

        var (name, step, count) = (range ?? "day").ToLowerInvariant() switch
        {
            "week"  => ("week", TimeSpan.FromHours(1), 168),
            "month" => ("month", TimeSpan.FromHours(4), 180),
            _       => ("day", TimeSpan.FromMinutes(15), 96),
        };
        var points = StockMarket.History(stock, UtcNow, step, count)
            .Select(p => new PricePointDto(p.At, p.Price))
            .ToList();
        return Result.Ok(new StockHistoryDto(stock.Ticker, name, points));
    }

    public Task<Result<MarketDto>> BuyAsync(Guid playerId, string ticker, long shares, CancellationToken ct = default)
        => TradeAsync(playerId, ticker, (c, s, now) => c.BuyStock(s, shares, now), ct);

    public Task<Result<MarketDto>> SellAsync(Guid playerId, string ticker, long shares, CancellationToken ct = default)
        => TradeAsync(playerId, ticker, (c, s, now) => c.SellStock(s, shares, now), ct);

    private async Task<Result<MarketDto>> TradeAsync(
        Guid playerId, string ticker, Func<Company, StockDefinition, DateTime, Result> trade, CancellationToken ct)
    {
        var company = await companyRepo.GetByPlayerIdAsync(playerId, ct);
        if (company is null) return Result.Fail<MarketDto>("Company not found.");

        var stock = StockCatalog.Find(ticker);
        if (stock is null) return Result.Fail<MarketDto>("Stock not found.");

        var now = UtcNow;
        var result = trade(company, stock, now);
        if (!result.IsSuccess) return Result.Fail<MarketDto>(result.Error!);

        companyRepo.Update(company);
        await uow.CommitAsync(ct);
        return Result.Ok(ToDto(company, now));
    }

    private static MarketDto ToDto(Company c, DateTime now)
    {
        var tick = StockMarket.TickStart(now);
        var stocks = StockCatalog.All.Select(s =>
        {
            var price = StockMarket.PriceAt(s, tick);
            var previous = StockMarket.PriceAt(s, tick.AddHours(-24));
            var day = StockMarket.History(s, tick, TimeSpan.FromMinutes(15), 97).Select(p => p.Price).ToList();
            var held = c.Stocks.FirstOrDefault(h => h.Ticker == s.Ticker);
            var spark = StockMarket.History(s, tick, TimeSpan.FromHours(1), 24).Select(p => p.Price).ToList();
            var shares = held?.Shares ?? 0;
            return new StockDto(
                s.Ticker, s.Name, s.Sector, s.Icon, price, StockMarket.Change24h(s, tick), spark,
                shares, held?.CostBasis ?? 0m, price * shares,
                Change: price - previous, PreviousClose: previous,
                DayHigh: day.Max(), DayLow: day.Min(), Volume: StockMarket.Volume24h(s, tick));
        }).ToList();

        // RLX: the average of every stock against its base price, × 1,000.
        var index = StockCatalog.All.Average(s => StockMarket.PriceAt(s, tick) / s.BasePrice) * 1_000m;
        var indexBefore = StockCatalog.All.Average(s => StockMarket.PriceAt(s, tick.AddHours(-24)) / s.BasePrice) * 1_000m;

        return new MarketDto(
            c.Cash, GameConstants.StockFeeRate, c.InvestedInStocks, c.StockInvestLimit,
            stocks.Sum(s => s.Value), tick, stocks,
            NextUpdateAt: StockMarket.NextTickAt(now),
            TickSeconds: GameConstants.StockTickSeconds,
            IndexValue: Math.Round(index, 2),
            IndexChange24h: indexBefore == 0m ? 0m : Math.Round((index - indexBefore) / indexBefore, 4));
    }
}

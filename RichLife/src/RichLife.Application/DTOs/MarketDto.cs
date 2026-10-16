namespace RichLife.Application.DTOs;

/// <summary>The Bourse for one player (contract §6f, features/017-bourse.md).</summary>
public record MarketDto(
    decimal Cash,
    decimal FeeRate,
    decimal Invested,
    decimal InvestLimit,
    decimal PortfolioValue,
    DateTime PricedAt,
    IReadOnlyList<StockDto> Stocks
);

/// <summary>
/// One stock: price now, change over 24 h (a fraction), 24 hourly prices for a sparkline, and
/// what the player holds (<c>shares</c> 0 when none; <c>costBasis</c> is what they cost, fees included).
/// </summary>
public record StockDto(
    string Ticker,
    string Name,
    string Sector,
    string Icon,
    decimal Price,
    decimal Change24h,
    IReadOnlyList<decimal> Spark,
    long Shares,
    decimal CostBasis,
    decimal Value
);

public record StockHistoryDto(string Ticker, string Range, IReadOnlyList<PricePointDto> Points);

public record PricePointDto(DateTime At, decimal Price);

public record TradeStockRequest(long Shares);

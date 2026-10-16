using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using RichLife.Application.DTOs;
using RichLife.Application.Services;

namespace RichLife.Api.Endpoints;

/// <summary>The Bourse: prices, charts, buy and sell (contract §6f). Players only.</summary>
public static class MarketEndpoints
{
    public static void MapMarketEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/api/game/market")
            .WithTags("Bourse")
            .RequireAuthorization(AuthPolicies.Player)
            .RequireRateLimiting(RateLimitPolicies.GameActions);

        group.MapGet("/", (ClaimsPrincipal user, MarketService svc, CancellationToken ct) =>
            Run(user, id => svc.GetAsync(id, ct)))
        .WithName("GetMarket")
        .WithSummary("Every stock with its price, 24 h change and sparkline, and what the player holds");

        group.MapGet("/{ticker}/history", Results<Ok<StockHistoryDto>, BadRequest<string>> (
            string ticker, string? range, MarketService svc) =>
        {
            var result = svc.History(ticker, range);
            return result.IsSuccess ? TypedResults.Ok(result.Value) : TypedResults.BadRequest(result.Error!);
        })
        .WithName("GetStockHistory")
        .WithSummary("Prices over a day (15 min), a week (hourly) or a month (4 h)");

        group.MapPost("/{ticker}/buy", (string ticker, TradeStockRequest req, ClaimsPrincipal user, MarketService svc, CancellationToken ct) =>
            Run(user, id => svc.BuyAsync(id, ticker, req.Shares, ct)))
        .WithName("BuyStock")
        .WithSummary("Buy shares at the price of the minute, plus the fee");

        group.MapPost("/{ticker}/sell", (string ticker, TradeStockRequest req, ClaimsPrincipal user, MarketService svc, CancellationToken ct) =>
            Run(user, id => svc.SellAsync(id, ticker, req.Shares, ct)))
        .WithName("SellStock")
        .WithSummary("Sell shares at the price of the minute, minus the fee");
    }

    private static async Task<Results<Ok<MarketDto>, BadRequest<string>, UnauthorizedHttpResult>> Run(
        ClaimsPrincipal user, Func<Guid, Task<Domain.Common.Result<MarketDto>>> action)
    {
        if (user.GetPlayerId() is not { } playerId) return TypedResults.Unauthorized();
        var result = await action(playerId);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : TypedResults.BadRequest(result.Error!);
    }
}

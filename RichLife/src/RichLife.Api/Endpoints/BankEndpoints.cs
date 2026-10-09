using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using RichLife.Application.DTOs;
using RichLife.Application.Services;

namespace RichLife.Api.Endpoints;

/// <summary>The bank (contract §6d). Players only.</summary>
public static class BankEndpoints
{
    public static void MapBankEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/api/game/bank")
            .WithTags("Bank")
            .RequireAuthorization(AuthPolicies.Player)
            .RequireRateLimiting(RateLimitPolicies.GameActions);

        group.MapGet("/", async Task<Results<Ok<BankDto>, BadRequest<string>, UnauthorizedHttpResult>> (
            ClaimsPrincipal user, BankService svc, CancellationToken ct) =>
        {
            if (user.GetPlayerId() is not { } playerId) return TypedResults.Unauthorized();
            var result = await svc.GetAsync(playerId, ct);
            return result.IsSuccess ? TypedResults.Ok(result.Value) : TypedResults.BadRequest(result.Error!);
        })
        .WithName("GetBank")
        .WithSummary("Current loan offers for the player's prestige, the active loan and recent history");

        group.MapPost("/loans/{offerId}", async Task<Results<Ok<BankDto>, BadRequest<string>, UnauthorizedHttpResult>> (
            string offerId, ClaimsPrincipal user, BankService svc, CancellationToken ct) =>
        {
            if (user.GetPlayerId() is not { } playerId) return TypedResults.Unauthorized();
            var result = await svc.TakeLoanAsync(playerId, offerId, ct);
            return result.IsSuccess ? TypedResults.Ok(result.Value) : TypedResults.BadRequest(result.Error!);
        })
        .WithName("TakeLoan")
        .WithSummary("Take one of the current offers (one loan at a time)");

        group.MapPost("/repay", async Task<Results<Ok<BankDto>, BadRequest<string>, UnauthorizedHttpResult>> (
            ClaimsPrincipal user, BankService svc, CancellationToken ct) =>
        {
            if (user.GetPlayerId() is not { } playerId) return TypedResults.Unauthorized();
            var result = await svc.RepayAsync(playerId, ct);
            return result.IsSuccess ? TypedResults.Ok(result.Value) : TypedResults.BadRequest(result.Error!);
        })
        .WithName("RepayLoan")
        .WithSummary("Repay everything still owed on the active loan");
    }
}

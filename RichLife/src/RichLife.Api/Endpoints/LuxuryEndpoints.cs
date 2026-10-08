using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using RichLife.Application.DTOs;
using RichLife.Application.Services;

namespace RichLife.Api.Endpoints;

/// <summary>The luxury shop (contract §6c). Players only.</summary>
public static class LuxuryEndpoints
{
    public static void MapLuxuryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/api/game/luxury")
            .WithTags("Luxury")
            .RequireAuthorization(AuthPolicies.Player)
            .RequireRateLimiting(RateLimitPolicies.GameActions);

        group.MapGet("/", async Task<Results<Ok<IReadOnlyList<LuxuryItemDto>>, BadRequest<string>, UnauthorizedHttpResult>> (
            ClaimsPrincipal user, LuxuryService svc, CancellationToken ct) =>
        {
            if (user.GetPlayerId() is not { } playerId) return TypedResults.Unauthorized();
            var result = await svc.GetShopAsync(playerId, ct);
            return result.IsSuccess ? TypedResults.Ok(result.Value) : TypedResults.BadRequest(result.Error!);
        })
        .WithName("GetLuxuryShop")
        .WithSummary("Every luxury item, with owned / unlocked / affordable flags");

        group.MapPost("/{id}", async Task<Results<Ok<OwnedLuxuryDto>, BadRequest<string>, UnauthorizedHttpResult>> (
            string id, ClaimsPrincipal user, LuxuryService svc, CancellationToken ct) =>
        {
            if (user.GetPlayerId() is not { } playerId) return TypedResults.Unauthorized();
            var result = await svc.BuyAsync(playerId, id, ct);
            return result.IsSuccess ? TypedResults.Ok(result.Value) : TypedResults.BadRequest(result.Error!);
        })
        .WithName("BuyLuxury")
        .WithSummary("Buy a luxury item (status; counts toward net worth)");
    }
}

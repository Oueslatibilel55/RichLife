using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using RichLife.Application.DTOs;
using RichLife.Application.Services;

namespace RichLife.Api.Endpoints;

/// <summary>The store: diamonds, boosts, the offline double, badges (contract §6e). Players only.</summary>
public static class StoreEndpoints
{
    public static void MapStoreEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/api/game/store")
            .WithTags("Store")
            .RequireAuthorization(AuthPolicies.Player)
            .RequireRateLimiting(RateLimitPolicies.GameActions);

        group.MapGet("/", (ClaimsPrincipal user, StoreService svc, CancellationToken ct) =>
            Run(user, id => svc.GetAsync(id, ct)))
        .WithName("GetStore")
        .WithSummary("Diamonds, boosts, the offline double offer, badges and the diamond history");

        group.MapPost("/boosts/{hours:int}", (int hours, ClaimsPrincipal user, StoreService svc, CancellationToken ct) =>
            Run(user, id => svc.BuyBoostAsync(id, hours, ct)))
        .WithName("BuyBoost")
        .WithSummary("Buy hours of doubled income (adds to a running boost)");

        group.MapPost("/double-offline", (ClaimsPrincipal user, StoreService svc, CancellationToken ct) =>
            Run(user, id => svc.DoubleOfflineAsync(id, ct)))
        .WithName("DoubleOffline")
        .WithSummary("Pay the last offline earnings again, for diamonds");

        group.MapPost("/exchange", (ExchangeDiamondsRequest req, ClaimsPrincipal user, StoreService svc, CancellationToken ct) =>
            Run(user, id => svc.ExchangeAsync(id, req.Diamonds, ct)))
        .WithName("ExchangeDiamonds")
        .WithSummary("Turn diamonds into cash (one way)");

        group.MapPost("/badges/{badgeId}", (string badgeId, ClaimsPrincipal user, StoreService svc, CancellationToken ct) =>
            Run(user, id => svc.BuyBadgeAsync(id, badgeId, ct)))
        .WithName("BuyBadge")
        .WithSummary("Buy a profile badge");

        group.MapPut("/featured-badge", (FeatureBadgeRequest req, ClaimsPrincipal user, StoreService svc, CancellationToken ct) =>
            Run(user, id => svc.FeatureBadgeAsync(id, req.BadgeId, ct)))
        .WithName("FeatureBadge")
        .WithSummary("Choose the badge shown next to the name");

        group.MapPost("/avatars/{avatarId}", (string avatarId, ClaimsPrincipal user, StoreService svc, CancellationToken ct) =>
            Run(user, id => svc.BuyAvatarAsync(id, avatarId, ct)))
        .WithName("BuyAvatar")
        .WithSummary("Buy a profile avatar (and put it on)");

        group.MapPut("/avatar", (SelectAvatarRequest req, ClaimsPrincipal user, StoreService svc, CancellationToken ct) =>
            Run(user, id => svc.SelectAvatarAsync(id, req.AvatarId, ct)))
        .WithName("SelectAvatar")
        .WithSummary("Choose the avatar in use (null for the initial)");
    }

    private static async Task<Results<Ok<StoreDto>, BadRequest<string>, UnauthorizedHttpResult>> Run(
        ClaimsPrincipal user, Func<Guid, Task<Domain.Common.Result<StoreDto>>> action)
    {
        if (user.GetPlayerId() is not { } playerId) return TypedResults.Unauthorized();
        var result = await action(playerId);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : TypedResults.BadRequest(result.Error!);
    }
}

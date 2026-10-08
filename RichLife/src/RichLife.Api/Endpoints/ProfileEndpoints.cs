using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using RichLife.Application.DTOs;
using RichLife.Application.Services;

namespace RichLife.Api.Endpoints;

/// <summary>The player's own profile (contract §6b). Players only — admins have no game profile.</summary>
public static class ProfileEndpoints
{
    public static void MapProfileEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/api/profile")
            .WithTags("Profile")
            .RequireAuthorization(AuthPolicies.Player)
            .RequireRateLimiting(RateLimitPolicies.GameActions);

        group.MapGet("/", async Task<Results<Ok<ProfileDto>, UnauthorizedHttpResult>> (
            ClaimsPrincipal user, ProfileService svc, CancellationToken ct) =>
        {
            if (user.GetPlayerId() is not { } playerId) return TypedResults.Unauthorized();
            var result = await svc.GetAsync(playerId, ct);
            // A valid token for a deleted player: treat as signed out.
            return result.IsSuccess ? TypedResults.Ok(result.Value) : TypedResults.Unauthorized();
        })
        .WithName("GetProfile")
        .WithSummary("Your account, company summary, leaderboard rank and achievements");
    }
}

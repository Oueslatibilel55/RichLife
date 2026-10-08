using Microsoft.AspNetCore.Http.HttpResults;
using RichLife.Application.DTOs;
using RichLife.Application.Services;

namespace RichLife.Api.Endpoints;

public static class LeaderboardEndpoints
{
    public static void MapLeaderboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/api/leaderboard")
            .WithTags("Leaderboard")
            .RequireRateLimiting(RateLimitPolicies.GameActions);

        group.MapGet("/", async Task<Ok<IReadOnlyList<LeaderboardEntryDto>>> (
            LeaderboardService svc, CancellationToken ct, int take = 50) =>
            TypedResults.Ok(await svc.GetTopAsync(take, ct)))
        .WithName("GetLeaderboard")
        .WithSummary("Top players by all-time earnings");
    }
}

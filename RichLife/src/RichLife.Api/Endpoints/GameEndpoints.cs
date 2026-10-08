using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using RichLife.Application.DTOs;
using RichLife.Application.Services;

namespace RichLife.Api.Endpoints;

public static class GameEndpoints
{
    public static void MapGameEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/api/game")
            .WithTags("Game")
            .RequireAuthorization(AuthPolicies.Player)
            .RequireRateLimiting(RateLimitPolicies.GameActions);

        // GET /api/game/state — load company and credit offline earnings
        group.MapGet("/state", async Task<Results<Ok<OfflineEarningsDto>, NotFound<string>, UnauthorizedHttpResult>> (
            ClaimsPrincipal user, CompanyService svc, CancellationToken ct) =>
        {
            if (user.GetPlayerId() is not { } playerId) return TypedResults.Unauthorized();

            var result = await svc.LoadWithOfflineProgressAsync(playerId, ct);
            return result.IsSuccess
                ? TypedResults.Ok(result.Value)
                : TypedResults.NotFound(result.Error!);
        })
        .WithName("GetGameState")
        .WithSummary("Load company state and apply offline earnings");

        // POST /api/game/prestige
        group.MapPost("/prestige", async Task<Results<Ok<CompanyDto>, BadRequest<string>, UnauthorizedHttpResult>> (
            ClaimsPrincipal user, CompanyService svc, CancellationToken ct) =>
        {
            if (user.GetPlayerId() is not { } playerId) return TypedResults.Unauthorized();

            var result = await svc.PrestigeAsync(playerId, ct);
            return result.IsSuccess
                ? TypedResults.Ok(result.Value)
                : TypedResults.BadRequest(result.Error!);
        })
        .WithName("Prestige")
        .WithSummary("Trigger prestige: surrender the current run for a permanent multiplier");

        // POST /api/game/company
        group.MapPost("/company", async Task<Results<Ok<CompanyDto>, BadRequest<string>, UnauthorizedHttpResult>> (
            CreateCompanyRequest req, ClaimsPrincipal user, CompanyService svc, CancellationToken ct) =>
        {
            if (user.GetPlayerId() is not { } playerId) return TypedResults.Unauthorized();

            var result = await svc.GetOrCreateAsync(playerId, req.CompanyName, ct);
            return result.IsSuccess
                ? TypedResults.Ok(result.Value)
                : TypedResults.BadRequest(result.Error!);
        })
        .WithName("CreateCompany")
        .WithSummary("Create or get the player company");

        // GET /api/game/company
        group.MapGet("/company", async Task<Results<Ok<CompanyDto>, NotFound<string>, UnauthorizedHttpResult>> (
            ClaimsPrincipal user, CompanyService svc, CancellationToken ct) =>
        {
            if (user.GetPlayerId() is not { } playerId) return TypedResults.Unauthorized();

            var company = await svc.GetByPlayerIdAsync(playerId, ct);
            return company is not null
                ? TypedResults.Ok(company)
                : TypedResults.NotFound("Company not found.");
        })
        .WithName("GetCompany")
        .WithSummary("Get current player company");

        // POST /api/game/sync
        group.MapPost("/sync", async Task<Results<Ok<SyncResultDto>, BadRequest<string>, UnauthorizedHttpResult>> (
            SyncRequest req, ClaimsPrincipal user, CompanyService svc, CancellationToken ct) =>
        {
            if (user.GetPlayerId() is not { } playerId) return TypedResults.Unauthorized();

            var result = await svc.SyncAsync(playerId, req.Cash, ct);
            return result.IsSuccess
                ? TypedResults.Ok(result.Value)
                : TypedResults.BadRequest(result.Error!);
        })
        .WithName("SyncGame")
        .WithSummary("Report client-simulated cash; the server clamps it to a plausible ceiling");

        // POST /api/game/sync-beacon — navigator.sendBeacon sends text/plain, so the
        // body is read manually rather than through JSON model binding.
        group.MapPost("/sync-beacon", async Task<Results<Ok, BadRequest<string>, UnauthorizedHttpResult>> (
            HttpRequest request, ClaimsPrincipal user, CompanyService svc, CancellationToken ct) =>
        {
            if (user.GetPlayerId() is not { } playerId) return TypedResults.Unauthorized();

            SyncRequest? req;
            try
            {
                req = await System.Text.Json.JsonSerializer.DeserializeAsync<SyncRequest>(
                    request.Body,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                    ct);
            }
            catch (System.Text.Json.JsonException)
            {
                return TypedResults.BadRequest("Malformed sync payload.");
            }

            if (req is null) return TypedResults.BadRequest("Empty sync payload.");

            var result = await svc.SyncAsync(playerId, req.Cash, ct);
            return result.IsSuccess
                ? TypedResults.Ok()
                : TypedResults.BadRequest(result.Error!);
        })
        .WithName("SyncBeacon")
        .WithSummary("Fire-and-forget sync used on tab close");
    }

    public record CreateCompanyRequest(string CompanyName);
    public record SyncRequest(decimal Cash);
}

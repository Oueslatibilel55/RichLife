using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using RichLife.Application.DTOs;
using RichLife.Application.Services;

namespace RichLife.Api.Endpoints;

/// <summary>Admin panel: stats, players, manager names (contract §7b). The catalogue is §7.</summary>
public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        // The admin policy is the gate: no token → 401, a token without the role → 403.
        var group = app
            .MapGroup("/api/admin")
            .WithTags("Admin")
            .RequireAuthorization(AuthPolicies.Admin)
            .RequireRateLimiting(RateLimitPolicies.GameActions);

        group.MapGet("/stats", async Task<Ok<AdminStatsDto>> (AdminService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.GetStatsAsync(ct)))
        .WithName("AdminStats")
        .WithSummary("Game-wide numbers for the admin dashboard");

        // -- Players ------------------------------------------------------------

        group.MapGet("/players", async Task<Ok<IReadOnlyList<AdminPlayerDto>>> (
            string? search, AdminService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.GetPlayersAsync(search, ct)))
        .WithName("AdminListPlayers")
        .WithSummary("Players with their company summary, newest first");

        group.MapPut("/players/{id:guid}/role", async Task<Results<Ok<AdminPlayerDto>, BadRequest<string>, UnauthorizedHttpResult>> (
            Guid id, SetAdminRoleRequest req, ClaimsPrincipal user, AdminService svc, CancellationToken ct) =>
        {
            if (user.GetPlayerId() is not { } actorId) return TypedResults.Unauthorized();
            var result = await svc.SetAdminAsync(actorId, id, req.IsAdmin, ct);
            return result.IsSuccess ? TypedResults.Ok(result.Value) : TypedResults.BadRequest(result.Error!);
        })
        .WithName("AdminSetRole")
        .WithSummary("Grant or revoke the admin role");

        group.MapPut("/players/{id:guid}/cash", async Task<Results<Ok<AdminPlayerDto>, BadRequest<string>>> (
            Guid id, SetCashRequest req, AdminService svc, CancellationToken ct) =>
        {
            var result = await svc.SetCashAsync(id, req.Cash, ct);
            return result.IsSuccess ? TypedResults.Ok(result.Value) : TypedResults.BadRequest(result.Error!);
        })
        .WithName("AdminSetCash")
        .WithSummary("Set a player's cash (wins over their next sync)");

        group.MapPost("/players/{id:guid}/reset", async Task<Results<Ok<AdminPlayerDto>, BadRequest<string>>> (
            Guid id, AdminService svc, CancellationToken ct) =>
        {
            var result = await svc.ResetAsync(id, ct);
            return result.IsSuccess ? TypedResults.Ok(result.Value) : TypedResults.BadRequest(result.Error!);
        })
        .WithName("AdminResetPlayer")
        .WithSummary("Fresh start for a player's company");

        group.MapDelete("/players/{id:guid}", async Task<Results<Ok, BadRequest<string>, UnauthorizedHttpResult>> (
            Guid id, ClaimsPrincipal user, AdminService svc, CancellationToken ct) =>
        {
            if (user.GetPlayerId() is not { } actorId) return TypedResults.Unauthorized();
            var result = await svc.DeleteAsync(actorId, id, ct);
            return result.IsSuccess ? TypedResults.Ok() : TypedResults.BadRequest(result.Error!);
        })
        .WithName("AdminDeletePlayer")
        .WithSummary("Delete a player and everything they own");

        group.MapPost("/players/{id:guid}/diamonds", async Task<Results<Ok<AdminPlayerDto>, BadRequest<string>>> (
            Guid id, AdjustDiamondsRequest req, AdminService svc, CancellationToken ct) =>
        {
            var result = await svc.AdjustDiamondsAsync(id, req.Amount, req.Reason, ct);
            return result.IsSuccess ? TypedResults.Ok(result.Value) : TypedResults.BadRequest(result.Error!);
        })
        .WithName("AdminAdjustDiamonds")
        .WithSummary("Give or take away diamonds, with a note for the player's ledger");

        group.MapPost("/players/{id:guid}/forgive-loan", async Task<Results<Ok<AdminPlayerDto>, BadRequest<string>>> (
            Guid id, AdminService svc, CancellationToken ct) =>
        {
            var result = await svc.ForgiveLoanAsync(id, ct);
            return result.IsSuccess ? TypedResults.Ok(result.Value) : TypedResults.BadRequest(result.Error!);
        })
        .WithName("AdminForgiveLoan")
        .WithSummary("Cancel what a player still owes on their active loan");

        // Bank — contract §7c. Banks are rules in code: read-only here.
        group.MapGet("/loans", async Task<Ok<IReadOnlyList<AdminLoanDto>>> (
            AdminService svc, CancellationToken ct, bool active = true) =>
            TypedResults.Ok(await svc.GetLoansAsync(active, ct)))
        .WithName("AdminListLoans")
        .WithSummary("Loans, newest first (active only by default)");

        group.MapGet("/banks", async Task<Ok<IReadOnlyList<AdminBankDto>>> (AdminService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.GetBanksAsync(ct)))
        .WithName("AdminListBanks")
        .WithSummary("The 20 banks with their rate bands and usage");

        group.MapGet("/manager-names", async Task<Ok<IReadOnlyList<ManagerNameDto>>> (
            AdminService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.GetManagerNamesAsync(ct)))
        .WithName("AdminListManagerNames")
        .WithSummary("Every manager name, with how many businesses use it");

        group.MapPost("/manager-names", async Task<Results<Created<ManagerNameDto>, BadRequest<string>>> (
            CreateManagerNameRequest req, AdminService svc, CancellationToken ct) =>
        {
            var result = await svc.AddManagerNameAsync(req.Name, ct);
            return result.IsSuccess
                ? TypedResults.Created($"/api/admin/manager-names/{result.Value.Id}", result.Value)
                : TypedResults.BadRequest(result.Error!);
        })
        .WithName("AdminAddManagerName")
        .WithSummary("Add a manager name to the random pool");

        group.MapDelete("/manager-names/{id:int}", async Task<Results<Ok, BadRequest<string>>> (
            int id, AdminService svc, CancellationToken ct) =>
        {
            var result = await svc.DeleteManagerNameAsync(id, ct);
            return result.IsSuccess ? TypedResults.Ok() : TypedResults.BadRequest(result.Error!);
        })
        .WithName("AdminDeleteManagerName")
        .WithSummary("Remove an unused manager name");
    }
}

using Microsoft.AspNetCore.Http.HttpResults;
using RichLife.Application.DTOs;
using RichLife.Application.Services;

namespace RichLife.Api.Endpoints;

/// <summary>The luxury catalogue editor (contract §7d). Admins only.</summary>
public static class AdminLuxuryEndpoints
{
    public static void MapAdminLuxuryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/api/admin/luxury")
            .WithTags("Admin")
            .RequireAuthorization(AuthPolicies.Admin)
            .RequireRateLimiting(RateLimitPolicies.GameActions);

        group.MapGet("/", async Task<Ok<IReadOnlyList<AdminLuxuryItemDto>>> (LuxuryAdminService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.GetAllAsync(ct)))
        .WithName("AdminListLuxury")
        .WithSummary("Every luxury item, retired ones included");

        group.MapGet("/{id}", async Task<Results<Ok<AdminLuxuryItemDto>, NotFound<string>>> (
            string id, LuxuryAdminService svc, CancellationToken ct) =>
            await svc.GetAsync(id, ct) is { } item ? TypedResults.Ok(item) : TypedResults.NotFound("Item not found."))
        .WithName("AdminGetLuxury");

        group.MapPost("/", async Task<Results<Created<AdminLuxuryItemDto>, BadRequest<string>>> (
            CreateLuxuryItemRequest req, LuxuryAdminService svc, CancellationToken ct) =>
        {
            var result = await svc.CreateAsync(req, ct);
            return result.IsSuccess
                ? TypedResults.Created($"/api/admin/luxury/{result.Value.Id}", result.Value)
                : TypedResults.BadRequest(result.Error!);
        })
        .WithName("AdminCreateLuxury");

        group.MapPut("/{id}", async Task<Results<Ok<AdminLuxuryItemDto>, BadRequest<string>>> (
            string id, UpdateLuxuryItemRequest req, LuxuryAdminService svc, CancellationToken ct) =>
        {
            var result = await svc.UpdateAsync(id, req, ct);
            return result.IsSuccess ? TypedResults.Ok(result.Value) : TypedResults.BadRequest(result.Error!);
        })
        .WithName("AdminUpdateLuxury")
        .WithSummary("Edit a luxury item (applies to later purchases); isActive=false retires it");
    }
}

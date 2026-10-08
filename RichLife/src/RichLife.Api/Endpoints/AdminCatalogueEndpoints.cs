using Microsoft.AspNetCore.Http.HttpResults;
using RichLife.Application.DTOs;
using RichLife.Application.Services;
using RichLife.Domain.Common;

namespace RichLife.Api.Endpoints;

public static class AdminCatalogueEndpoints
{
    public static void MapAdminCatalogueEndpoints(this IEndpointRouteBuilder app)
    {
        // Authorization is the whole gate here — no handler reads the player id. A valid
        // token without the admin role claim is a 403 from the middleware.
        var group = app
            .MapGroup("/api/admin/catalogue")
            .WithTags("Admin — Catalogue")
            .RequireAuthorization(AuthPolicies.Admin)
            .RequireRateLimiting(RateLimitPolicies.GameActions);

        // GET /api/admin/catalogue
        group.MapGet("/", async Task<Ok<IReadOnlyList<AdminCatalogueEntryDto>>> (
            CatalogueAdminService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.GetAllAsync(ct)))
        .WithName("AdminListCatalogue")
        .WithSummary("Every catalogue entry, retired ones included");

        // GET /api/admin/catalogue/{id}
        group.MapGet("/{id}", async Task<Results<Ok<AdminCatalogueEntryDto>, NotFound<string>>> (
            string id, CatalogueAdminService svc, CancellationToken ct) =>
            await svc.GetAsync(id, ct) is { } entry
                ? TypedResults.Ok(entry)
                : TypedResults.NotFound("Catalogue entry not found."))
        .WithName("AdminGetCatalogueEntry")
        .WithSummary("One catalogue entry");

        // POST /api/admin/catalogue
        group.MapPost("/", async Task<Results<Created<AdminCatalogueEntryDto>, BadRequest<string>>> (
            CreateCatalogueEntryRequest req, CatalogueAdminService svc, CancellationToken ct) =>
        {
            var result = await svc.CreateAsync(req, ct);
            return result.IsSuccess
                ? TypedResults.Created($"/api/admin/catalogue/{result.Value.Id}", result.Value)
                : TypedResults.BadRequest(result.Error!);
        })
        .WithName("AdminCreateCatalogueEntry")
        .WithSummary("Add a business to the catalogue, optionally with its assets");

        // PUT /api/admin/catalogue/{id}
        group.MapPut("/{id}", async Task<Results<Ok<AdminCatalogueEntryDto>, BadRequest<string>>> (
            string id, UpdateCatalogueEntryRequest req, CatalogueAdminService svc, CancellationToken ct) =>
            ToResult(await svc.UpdateAsync(id, req, ct)))
        .WithName("AdminUpdateCatalogueEntry")
        .WithSummary("Replace a business's fields, including retiring it (isActive: false)");

        // POST /api/admin/catalogue/{id}/assets
        group.MapPost("/{id}/assets", async Task<Results<Ok<AdminCatalogueEntryDto>, BadRequest<string>>> (
            string id, CreateCatalogueAssetRequest req, CatalogueAdminService svc, CancellationToken ct) =>
            ToResult(await svc.AddAssetAsync(id, req, ct)))
        .WithName("AdminAddCatalogueAsset")
        .WithSummary("Add an asset to a catalogue business");

        // PUT /api/admin/catalogue/{id}/assets/{assetId}
        group.MapPut("/{id}/assets/{assetId}", async Task<Results<Ok<AdminCatalogueEntryDto>, BadRequest<string>>> (
            string id, string assetId, UpdateCatalogueAssetRequest req, CatalogueAdminService svc, CancellationToken ct) =>
            ToResult(await svc.UpdateAssetAsync(id, assetId, req, ct)))
        .WithName("AdminUpdateCatalogueAsset")
        .WithSummary("Replace an asset's fields");

        // DELETE /api/admin/catalogue/{id}/assets/{assetId}
        group.MapDelete("/{id}/assets/{assetId}", async Task<Results<Ok<AdminCatalogueEntryDto>, BadRequest<string>>> (
            string id, string assetId, CatalogueAdminService svc, CancellationToken ct) =>
            ToResult(await svc.RemoveAssetAsync(id, assetId, ct)))
        .WithName("AdminRemoveCatalogueAsset")
        .WithSummary("Remove an asset from sale; copies players already bought are untouched");
    }

    private static Results<Ok<AdminCatalogueEntryDto>, BadRequest<string>> ToResult(
        Result<AdminCatalogueEntryDto> result)
        => result.IsSuccess
            ? TypedResults.Ok(result.Value)
            : TypedResults.BadRequest(result.Error!);
}

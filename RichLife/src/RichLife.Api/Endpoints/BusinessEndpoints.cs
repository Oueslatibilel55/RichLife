using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using RichLife.Application.DTOs;
using RichLife.Application.Services;

namespace RichLife.Api.Endpoints;

public static class BusinessEndpoints
{
    public static void MapBusinessEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/api/game/businesses")
            .WithTags("Businesses")
            .RequireAuthorization(AuthPolicies.Player)
            .RequireRateLimiting(RateLimitPolicies.GameActions);

        // GET /api/game/businesses/catalogue
        group.MapGet("/catalogue", async Task<Results<Ok<IReadOnlyList<BusinessCatalogueDto>>, BadRequest<string>, UnauthorizedHttpResult>> (
            ClaimsPrincipal user, BusinessService svc, CancellationToken ct) =>
        {
            if (user.GetPlayerId() is not { } playerId) return TypedResults.Unauthorized();

            var result = await svc.GetCatalogueAsync(playerId, ct);
            return result.IsSuccess
                ? TypedResults.Ok(result.Value)
                : TypedResults.BadRequest(result.Error!);
        })
        .WithName("GetCatalogue")
        .WithSummary("Get all available businesses for current prestige");

        // POST /api/game/businesses/{catalogueId}
        group.MapPost("/{catalogueId}", async Task<Results<Ok<BusinessDto>, BadRequest<string>, UnauthorizedHttpResult>> (
            string catalogueId, ClaimsPrincipal user, BusinessService svc, CancellationToken ct) =>
        {
            if (user.GetPlayerId() is not { } playerId) return TypedResults.Unauthorized();

            var result = await svc.OpenBusinessAsync(playerId, catalogueId, ct);
            return result.IsSuccess
                ? TypedResults.Ok(result.Value)
                : TypedResults.BadRequest(result.Error!);
        })
        .WithName("OpenBusiness")
        .WithSummary("Open a new business (one per catalogue entry)");

        // POST /api/game/businesses/{businessId}/assets/{assetCatalogueId}
        group.MapPost("/{businessId:guid}/assets/{assetCatalogueId}", async Task<Results<Ok<BusinessDto>, BadRequest<string>, UnauthorizedHttpResult>> (
            Guid businessId, string assetCatalogueId,
            ClaimsPrincipal user, BusinessService svc, CancellationToken ct) =>
        {
            if (user.GetPlayerId() is not { } playerId) return TypedResults.Unauthorized();

            var result = await svc.BuyAssetAsync(playerId, businessId, assetCatalogueId, ct);
            return result.IsSuccess
                ? TypedResults.Ok(result.Value)
                : TypedResults.BadRequest(result.Error!);
        })
        .WithName("BuyAsset")
        .WithSummary("Buy an asset for a business (truck, car, equipment...)");

        // POST /api/game/businesses/{businessId}/automate
        group.MapPost("/{businessId:guid}/automate", async Task<Results<Ok<BusinessDto>, BadRequest<string>, UnauthorizedHttpResult>> (
            Guid businessId, ClaimsPrincipal user, BusinessService svc, CancellationToken ct) =>
        {
            if (user.GetPlayerId() is not { } playerId) return TypedResults.Unauthorized();

            var result = await svc.AutomateBusinessAsync(playerId, businessId, ct);
            return result.IsSuccess
                ? TypedResults.Ok(result.Value)
                : TypedResults.BadRequest(result.Error!);
        })
        .WithName("AutomateBusiness")
        .WithSummary("Hire a manager so the business keeps earning offline");

        // POST /api/game/businesses/{businessId}/level-up
        group.MapPost("/{businessId:guid}/level-up", async Task<Results<Ok<BusinessDto>, BadRequest<string>, UnauthorizedHttpResult>> (
            Guid businessId, ClaimsPrincipal user, BusinessService svc, CancellationToken ct) =>
        {
            if (user.GetPlayerId() is not { } playerId) return TypedResults.Unauthorized();

            var result = await svc.LevelUpBusinessAsync(playerId, businessId, ct);
            return result.IsSuccess
                ? TypedResults.Ok(result.Value)
                : TypedResults.BadRequest(result.Error!);
        })
        .WithName("LevelUpBusiness")
        .WithSummary("Pay to raise a business one level and boost its income");

        // POST /api/game/businesses/{businessId}/pay-taxes
        group.MapPost("/{businessId:guid}/pay-taxes", async Task<Results<Ok<TaxPaymentDto>, BadRequest<string>, UnauthorizedHttpResult>> (
            Guid businessId, ClaimsPrincipal user, BusinessService svc, CancellationToken ct) =>
        {
            if (user.GetPlayerId() is not { } playerId) return TypedResults.Unauthorized();

            var result = await svc.PayTaxesAsync(playerId, businessId, ct);
            return result.IsSuccess
                ? TypedResults.Ok(result.Value)
                : TypedResults.BadRequest(result.Error!);
        })
        .WithName("PayBusinessTaxes")
        .WithSummary("Pay the tax a business owes");

        // POST /api/game/businesses/taxes/pay — two segments, so it never shadows a catalogue id
        group.MapPost("/taxes/pay", async Task<Results<Ok<TaxPaymentDto>, BadRequest<string>, UnauthorizedHttpResult>> (
            ClaimsPrincipal user, BusinessService svc, CancellationToken ct) =>
        {
            if (user.GetPlayerId() is not { } playerId) return TypedResults.Unauthorized();

            var result = await svc.PayAllTaxesAsync(playerId, ct);
            return result.IsSuccess
                ? TypedResults.Ok(result.Value)
                : TypedResults.BadRequest(result.Error!);
        })
        .WithName("PayAllTaxes")
        .WithSummary("Pay every business's taxes at once");

        // DELETE /api/game/businesses/{businessId}?emergency=false
        group.MapDelete("/{businessId:guid}", async Task<Results<Ok, BadRequest<string>, UnauthorizedHttpResult>> (
            Guid businessId, bool emergency,
            ClaimsPrincipal user, BusinessService svc, CancellationToken ct) =>
        {
            if (user.GetPlayerId() is not { } playerId) return TypedResults.Unauthorized();

            var result = await svc.CloseBusinessAsync(playerId, businessId, emergency, ct);
            return result.IsSuccess
                ? TypedResults.Ok()
                : TypedResults.BadRequest(result.Error!);
        })
        .WithName("CloseBusiness")
        .WithSummary("Close a business and refund its value minus the closing fee");
    }
}

using Microsoft.AspNetCore.Http.HttpResults;
using RichLife.Application.DTOs;
using RichLife.Application.Services;

namespace RichLife.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/api/auth")
            .WithTags("Auth")
            .RequireRateLimiting(RateLimitPolicies.Auth);

        group.MapPost("/register", async Task<Results<Ok<AuthResponse>, BadRequest<string>>> (
            RegisterRequest req, AuthService svc, CancellationToken ct) =>
        {
            var result = await svc.RegisterAsync(req, ct);
            return result.IsSuccess
                ? TypedResults.Ok(result.Value)
                : TypedResults.BadRequest(result.Error!);
        })
        .WithName("Register")
        .WithSummary("Register a new player");

        group.MapPost("/login", async Task<Results<Ok<AuthResponse>, UnauthorizedHttpResult>> (
            LoginRequest req, AuthService svc, CancellationToken ct) =>
        {
            var result = await svc.LoginAsync(req, ct);
            return result.IsSuccess
                ? TypedResults.Ok(result.Value)
                : TypedResults.Unauthorized();
        })
        .WithName("Login")
        .WithSummary("Login and receive JWT tokens");

        group.MapPost("/refresh", async Task<Results<Ok<AuthResponse>, UnauthorizedHttpResult>> (
            RefreshRequest req, AuthService svc, CancellationToken ct) =>
        {
            var result = await svc.RefreshAsync(req.RefreshToken, ct);
            return result.IsSuccess
                ? TypedResults.Ok(result.Value)
                : TypedResults.Unauthorized();
        })
        .WithName("Refresh")
        .WithSummary("Refresh access token")
        .AllowAnonymous();
    }

    public record RefreshRequest(string RefreshToken);
}

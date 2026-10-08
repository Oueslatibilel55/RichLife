using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RichLife.Api.Endpoints;
using RichLife.Application.Extensions;
using RichLife.Application.Services;
using RichLife.Infrastructure.Extensions;
using RichLife.Infrastructure.Persistence;
using RichLife.ServiceDefaults;
using Scalar.AspNetCore;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// -- Service defaults (Aspire: OpenTelemetry, health checks, resilience) -------
builder.AddServiceDefaults();

// -- Application & Infrastructure ---------------------------------------------
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// -- Auth ----------------------------------------------------------------------
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        var cfg = builder.Configuration;
        var jwtSecret = cfg["Jwt:Secret"]
            ?? throw new InvalidOperationException(
                "Jwt:Secret is not configured. In development run: " +
                "dotnet user-secrets set \"Jwt:Secret\" <value> --project src/RichLife.Api. " +
                "In production set the Jwt__Secret environment variable.");

        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = cfg["Jwt:Issuer"],
            ValidAudience            = cfg["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthPolicies.Admin, p => p.RequireRole(Roles.Admin))
    .AddPolicy(AuthPolicies.Player, p => p
        .RequireAuthenticatedUser()
        .RequireAssertion(ctx => !ctx.User.IsInRole(Roles.Admin)));

// -- OpenAPI -------------------------------------------------------------------
builder.Services.AddOpenApi();

// -- CORS ----------------------------------------------------------------------
builder.Services.AddCors(opt =>
    opt.AddPolicy("angular", p => p
        .WithOrigins("http://localhost:4200")
        .AllowAnyHeader()
        .AllowAnyMethod()));

// -- Rate limiting -------------------------------------------------------------
// Every policy is partitioned. A single unpartitioned limiter would be shared by the
// whole player base, so a handful of clients on the 5s /sync cadence would exhaust the
// budget and everyone else would start seeing 429s.
builder.Services.AddRateLimiter(opt =>
{
    opt.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // One budget per player. Falls back to the caller's IP when the request carries no
    // usable token, so an anonymous caller cannot spend somebody else's allowance.
    opt.AddPolicy(RateLimitPolicies.GameActions, context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            PartitionKey(context, "game"),
            _ => new SlidingWindowRateLimiterOptions
            {
                Window            = TimeSpan.FromSeconds(10),
                PermitLimit       = 20,
                SegmentsPerWindow = 5,
            }));

    // Credential endpoints are cheap to spam and expensive to verify (BCrypt). The caller
    // has no identity yet by definition, so these partition on IP.
    opt.AddPolicy(RateLimitPolicies.Auth, context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            PartitionKey(context, "auth"),
            _ => new SlidingWindowRateLimiterOptions
            {
                Window            = TimeSpan.FromMinutes(1),
                PermitLimit       = 10,
                SegmentsPerWindow = 6,
            }));
});

// Declared here rather than inline so both policies key their partitions identically.
static string PartitionKey(HttpContext context, string prefix)
    => context.User.GetPlayerId() is { } playerId
        ? $"{prefix}:player:{playerId}"
        : $"{prefix}:ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

// -- Forwarded headers -----------------------------------------------------------
// Deployed, a request reaches the API through two proxies: Netlify (which proxies
// /api/*) and Render's load balancer. Without this every caller shares the proxy's IP,
// so the whole player base would share one auth rate-limit budget. Each proxy appends
// to X-Forwarded-For; reading only the last two entries takes the address Netlify saw
// and ignores anything a client wrote into the header itself. The proxies' addresses
// are not fixed, hence no KnownProxies.
builder.Services.Configure<ForwardedHeadersOptions>(opt =>
{
    opt.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    opt.ForwardLimit = 2;
    opt.KnownIPNetworks.Clear();
    opt.KnownProxies.Clear();
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var app = builder.Build();

// -- Database migrations ---------------------------------------------------------
// Opt-in (Database__MigrateOnStartup=true on the deployed service): locally migrations
// stay an explicit `dotnet ef database update`, as documented in CLAUDE.md.
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<GameDbContext>().Database.MigrateAsync();
}

// -- Middleware ----------------------------------------------------------------
app.UseForwardedHeaders();
// Order matters. Rate limiting sits between authentication and authorization: after
// UseAuthentication so HttpContext.User is populated and the per-player partition key
// resolves, and before UseAuthorization so a caller rejected by a policy still counts
// against a budget. Every Map* call below sits behind the whole pipeline.
app.UseCors("angular");
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// -- Endpoints -----------------------------------------------------------------
app.MapDefaultEndpoints(); // Aspire health endpoints
app.MapAuthEndpoints();
app.MapGameEndpoints();
app.MapBusinessEndpoints();
app.MapLeaderboardEndpoints();
app.MapProfileEndpoints();
app.MapLuxuryEndpoints();
app.MapAdminCatalogueEndpoints();
app.MapAdminEndpoints();

app.Run();

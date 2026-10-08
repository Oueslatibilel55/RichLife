using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RichLife.Application.Interfaces;
using RichLife.Infrastructure.Events;
using RichLife.Infrastructure.Persistence;
using RichLife.Infrastructure.Repositories;

namespace RichLife.Infrastructure.Extensions;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        // Under Aspire, `WithReference(richlifeDb)` injects ConnectionStrings__richlife —
        // named after the database resource, not "DefaultConnection". Prefer it so the
        // orchestrated run actually uses the database Aspire started, and fall back to
        // DefaultConnection for docker-compose, design-time tooling and production.
        var connectionString =
            config.GetConnectionString("richlife")
            ?? config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "No database connection string found. Set it with " +
                "'dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" <value> " +
                "--project src/RichLife.Api' in development, or the " +
                "ConnectionStrings__DefaultConnection environment variable in production.");

        services.AddScoped<CatalogueCacheInvalidator>();
        services.AddDbContext<GameDbContext>((sp, opt) => opt
            .UseNpgsql(connectionString,
                npg => npg.MigrationsAssembly(typeof(GameDbContext).Assembly.GetName().Name))
            .AddInterceptors(sp.GetRequiredService<CatalogueCacheInvalidator>()));

        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<ICatalogueRepository, CatalogueRepository>();
        services.AddScoped<IManagerNameRepository, ManagerNameRepository>();
        services.AddScoped<IAdminReadRepository, AdminReadRepository>();
        services.AddScoped<ILuxuryCatalogueRepository, LuxuryCatalogueRepository>();
        services.AddScoped<IPlayerRepository, PlayerRepository>();
        services.AddScoped<ILeaderboardRepository, LeaderboardRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddMemoryCache();

        return services;
    }
}

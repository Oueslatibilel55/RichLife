using Microsoft.Extensions.DependencyInjection;
using RichLife.Application.Services;

namespace RichLife.Application.Extensions;

public static class ApplicationExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Injected rather than calling DateTime.UtcNow, so income accrual and the
        // sync ceiling can be tested without sleeping.
        services.TryAddSingletonTimeProvider();

        services.AddScoped<CompanyService>();
        services.AddScoped<AuthService>();
        services.AddScoped<BusinessService>();
        services.AddScoped<LeaderboardService>();
        services.AddScoped<CatalogueAdminService>();
        services.AddScoped<AdminService>();
        services.AddScoped<ProfileService>();
        services.AddScoped<LuxuryService>();
        services.AddScoped<BankService>();

        return services;
    }

    private static void TryAddSingletonTimeProvider(this IServiceCollection services)
    {
        if (services.Any(d => d.ServiceType == typeof(TimeProvider))) return;
        services.AddSingleton(TimeProvider.System);
    }
}

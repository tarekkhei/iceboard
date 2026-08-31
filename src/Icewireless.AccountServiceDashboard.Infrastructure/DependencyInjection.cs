using Icewireless.AccountServiceDashboard.Application.Configuration;
using Icewireless.AccountServiceDashboard.Application.Interfaces;
using Icewireless.AccountServiceDashboard.Infrastructure.Oracle;
using Icewireless.AccountServiceDashboard.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Icewireless.AccountServiceDashboard.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string? contentRootPath = null)
    {
        services.AddSingleton<IOracleConnectionFactory, OracleConnectionFactory>();
        services.AddScoped<IAccountServiceRepository, AccountServiceRepository>();
        services.AddScoped<IStatusRepository, StatusRepository>();
        services.AddScoped<IAnalysisRepository, AnalysisRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddSingleton<IKpiQueryCatalog, Services.KpiQueryCatalog>();

        services.AddSingleton<JsonAccountExclusionRepository>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<DashboardSettings>>().Value;
            var relative = string.IsNullOrWhiteSpace(settings.AccountExclusionRulesPath)
                ? "App_Data/account-exclusion-rules.json"
                : settings.AccountExclusionRulesPath;
            var root = contentRootPath
                       ?? AppContext.BaseDirectory
                       ?? Directory.GetCurrentDirectory();
            var path = Path.IsPathRooted(relative)
                ? relative
                : Path.GetFullPath(Path.Combine(root, relative));
            return new JsonAccountExclusionRepository(
                path,
                sp.GetRequiredService<IOracleConnectionFactory>(),
                sp.GetRequiredService<IOptions<DashboardSettings>>(),
                sp.GetRequiredService<ILogger<JsonAccountExclusionRepository>>());
        });
        services.AddSingleton<IAccountExclusionRepository>(sp => sp.GetRequiredService<JsonAccountExclusionRepository>());
        services.AddSingleton<IAccountExclusionPatternProvider>(sp => sp.GetRequiredService<JsonAccountExclusionRepository>());

        return services;
    }
}

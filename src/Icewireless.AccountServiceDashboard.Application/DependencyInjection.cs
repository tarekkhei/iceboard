using Icewireless.AccountServiceDashboard.Application.Configuration;
using Icewireless.AccountServiceDashboard.Application.Interfaces;
using Icewireless.AccountServiceDashboard.Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Icewireless.AccountServiceDashboard.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DashboardSettings>(configuration.GetSection(DashboardSettings.SectionName));
        services.Configure<Auth0Settings>(configuration.GetSection(Auth0Settings.SectionName));

        services.AddSingleton<IStatusMappingService, StatusMappingService>();
        services.AddSingleton<IMetricDateFieldService, MetricDateFieldService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IStatusService, StatusService>();
        services.AddScoped<IAnalysisService, AnalysisService>();
        services.AddScoped<IExportService, ExportService>();
        services.AddScoped<IKpiLinesService, KpiLinesService>();
        services.AddScoped<IAccountExclusionService, AccountExclusionService>();
        return services;
    }
}

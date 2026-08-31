using Icewireless.AccountServiceDashboard.Application.Configuration;
using Icewireless.AccountServiceDashboard.Application.DTOs;
using Icewireless.AccountServiceDashboard.Application.Interfaces;
using Icewireless.AccountServiceDashboard.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Icewireless.AccountServiceDashboard.Web.Controllers;

public sealed class DashboardController : Controller
{
    private readonly IDashboardService _dashboardService;
    private readonly IAnalysisRepository _analysisRepository;
    private readonly IKpiQueryCatalog _kpiQueries;
    private readonly IOracleConnectionFactory _oracle;
    private readonly DashboardSettings _settings;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        IDashboardService dashboardService,
        IAnalysisRepository analysisRepository,
        IKpiQueryCatalog kpiQueries,
        IOracleConnectionFactory oracle,
        IOptions<DashboardSettings> settings,
        ILogger<DashboardController> logger)
    {
        _dashboardService = dashboardService;
        _analysisRepository = analysisRepository;
        _kpiQueries = kpiQueries;
        _oracle = oracle;
        _settings = settings.Value;
        _logger = logger;
    }

    [HttpGet("/")]
    [HttpGet("/dashboard")]
    public async Task<IActionResult> Index([FromQuery] FilterQuery query, CancellationToken cancellationToken)
    {
        try
        {
            _oracle.EnsureConfigured();
            var filter = query.ToFilter(_settings.PageSize);
            var (kpis, charts) = await _dashboardService.GetDashboardAsync(filter, cancellationToken);
            var options = await SafeFilterOptions(filter, cancellationToken);
            return View(new DashboardPageViewModel
            {
                Title = "Dashboard",
                ActiveNavKey = "dashboard",
                Breadcrumbs = [new BreadcrumbItem("Home", "/"), new BreadcrumbItem("Dashboard")],
                Filter = filter,
                FilterOptions = options,
                Kpis = kpis,
                Charts = charts,
                KpiQueries = _kpiQueries.GetDashboardKpiQueries(filter)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dashboard failed for provider {Provider}", query.ProviderCode);
            return View("~/Views/Shared/Error.cshtml", new ErrorViewModel
            {
                Message = DescribeDashboardError(ex),
                Detail = ex.GetType().Name
            });
        }
    }

    private static string DescribeDashboardError(Exception ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        if (message.Contains("IceWirelessOracle", StringComparison.OrdinalIgnoreCase)
            || message.Contains("YOUR_USER", StringComparison.OrdinalIgnoreCase)
            || message.Contains("ORA-", StringComparison.OrdinalIgnoreCase)
            || message.Contains("connection", StringComparison.OrdinalIgnoreCase))
        {
            return message
                + " If this is an upgrade, restore the previous appsettings.Production.json (Oracle connection string) and retry.";
        }

        return string.IsNullOrWhiteSpace(message)
            ? "An unexpected error occurred. Please try again or contact support."
            : message;
    }

    private async Task<FilterOptionsDto> SafeFilterOptions(DashboardFilter filter, CancellationToken ct)
    {
        try { return await _analysisRepository.GetFilterOptionsAsync(filter, ct); }
        catch { return new FilterOptionsDto([], [], [], []); }
    }
}

public sealed class FilterQuery
{
    public string? ProviderCode { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public string? Region { get; set; }
    public string? Product { get; set; }
    public string? ServiceType { get; set; }
    public string? Status { get; set; }
    public string? Search { get; set; }
    public bool IncludeTestAccounts { get; set; }
    public string? SortColumn { get; set; }
    public bool SortDescending { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; }

    public DashboardFilter ToFilter(int defaultPageSize) => new DashboardFilter
    {
        ProviderCode = ProviderCode,
        FromDate = FromDate,
        ToDate = ToDate,
        Region = Region,
        Product = Product,
        ServiceType = ServiceType,
        Status = Status,
        Search = Search,
        IncludeTestAccounts = IncludeTestAccounts,
        SortColumn = SortColumn,
        SortDescending = SortDescending,
        Page = Page < 1 ? 1 : Page,
        PageSize = PageSize < 1 ? defaultPageSize : PageSize
    }.Normalize(defaultPageSize);
}

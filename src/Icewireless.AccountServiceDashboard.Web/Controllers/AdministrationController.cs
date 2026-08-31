using Icewireless.AccountServiceDashboard.Application.Configuration;
using Icewireless.AccountServiceDashboard.Application.DTOs;
using Icewireless.AccountServiceDashboard.Application.Interfaces;
using Icewireless.AccountServiceDashboard.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Icewireless.AccountServiceDashboard.Web.Controllers;

public sealed class AdministrationController : Controller
{
    private readonly IOracleConnectionFactory _oracleConnectionFactory;
    private readonly IStatusService _statusService;
    private readonly IAccountExclusionService _exclusionService;
    private readonly DashboardSettings _settings;

    public AdministrationController(
        IOracleConnectionFactory oracleConnectionFactory,
        IStatusService statusService,
        IAccountExclusionService exclusionService,
        IOptions<DashboardSettings> settings)
    {
        _oracleConnectionFactory = oracleConnectionFactory;
        _statusService = statusService;
        _exclusionService = exclusionService;
        _settings = settings.Value;
    }

    [HttpGet("/admin/database-status")]
    public async Task<IActionResult> DatabaseStatus(CancellationToken cancellationToken)
    {
        var configured = _oracleConnectionFactory.HasConnectionString;
        var reachable = configured && await _oracleConnectionFactory.CanConnectAsync(cancellationToken);
        var message = !configured
            ? "ConnectionStrings:IceWirelessOracle is not configured."
            : reachable
                ? "Oracle connectivity check succeeded."
                : "Oracle connectivity check failed. Verify network, credentials, and TNS/EZConnect settings.";

        return View(new DatabaseStatusPageViewModel
        {
            Title = "Database Status",
            ActiveNavKey = "admin-database",
            Breadcrumbs =
            [
                new BreadcrumbItem("Home", "/"),
                new BreadcrumbItem("Administration"),
                new BreadcrumbItem("Database Status")
            ],
            Status = new DatabaseStatusDto(configured, reachable, message, DateTimeOffset.UtcNow)
        });
    }

    [HttpGet("/admin/query-diagnostics")]
    public IActionResult QueryDiagnostics() =>
        Placeholder("Query Diagnostics", "admin-diagnostics",
            "Query diagnostics placeholder. Bind-variable traces and timing will be added with live queries.");

    [HttpGet("/admin/status-mapping")]
    public IActionResult StatusMapping()
    {
        return View(new StatusMappingPageViewModel
        {
            Title = "Status Mapping",
            ActiveNavKey = "admin-status-mapping",
            Breadcrumbs =
            [
                new BreadcrumbItem("Home", "/"),
                new BreadcrumbItem("Administration"),
                new BreadcrumbItem("Status Mapping")
            ],
            Mappings = _statusService.GetMappings()
        });
    }

    [HttpGet("/admin/account-exclusions")]
    public async Task<IActionResult> AccountExclusions([FromQuery] string? search, [FromQuery] bool? isActive, CancellationToken cancellationToken)
    {
        PagedResult<AccountExclusionRuleDto> page;
        string? error = null;
        try
        {
            page = await _exclusionService.SearchAsync(new AccountExclusionSearchDto(search, isActive, 1, 200), cancellationToken);
        }
        catch (Exception ex)
        {
            page = new PagedResult<AccountExclusionRuleDto>([], 0, 1, 200);
            error = "Could not load exclusion rules from JSON file. " + ex.Message;
        }

        return View(new AccountExclusionsPageViewModel
        {
            Title = "Account Exclusion Rules",
            ActiveNavKey = "admin-exclusions",
            Breadcrumbs =
            [
                new BreadcrumbItem("Home", "/"),
                new BreadcrumbItem("Administration"),
                new BreadcrumbItem("Account Exclusion Rules")
            ],
            Data = page,
            Search = search,
            ActiveOnly = isActive,
            ErrorMessage = error
        });
    }

    [HttpPost("/admin/account-exclusions")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateExclusion(
        [FromForm] string pattern,
        [FromForm] string? description,
        [FromForm] bool isActive = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _exclusionService.CreateAsync(new AccountExclusionRuleWriteDto(
                pattern, "CONTAINS", isActive, description, User.Identity?.Name ?? "DASHBOARD"), cancellationToken);
        }
        catch (Exception ex)
        {
            TempData["ExclusionError"] = ex.Message;
        }

        return RedirectToAction(nameof(AccountExclusions));
    }

    [HttpPost("/admin/account-exclusions/{id:long}/toggle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleExclusion(long id, [FromForm] bool enable, CancellationToken cancellationToken)
    {
        if (enable) await _exclusionService.EnableAsync(id, cancellationToken);
        else await _exclusionService.DisableAsync(id, cancellationToken);
        return RedirectToAction(nameof(AccountExclusions));
    }

    [HttpPost("/admin/account-exclusions/{id:long}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteExclusion(long id, CancellationToken cancellationToken)
    {
        await _exclusionService.DeleteAsync(id, cancellationToken);
        return RedirectToAction(nameof(AccountExclusions));
    }

    [HttpGet("/admin/settings")]
    public IActionResult Settings() =>
        Placeholder("Settings", "admin-settings",
            $"ProviderCode={_settings.ProviderCode}; PageSize={_settings.PageSize}; CommandTimeout={_settings.CommandTimeoutSeconds}s. Auth0 is prepared but not enabled.");

    private IActionResult Placeholder(string title, string navKey, string description) =>
        View("~/Views/Shared/Placeholder.cshtml", new PageViewModel
        {
            Title = title,
            Section = "Administration",
            Description = description,
            ActiveNavKey = navKey,
            QueriesImplemented = false,
            Breadcrumbs =
            [
                new BreadcrumbItem("Home", "/"),
                new BreadcrumbItem("Administration"),
                new BreadcrumbItem(title)
            ]
        });
}

using Icewireless.AccountServiceDashboard.Application.Configuration;
using Icewireless.AccountServiceDashboard.Application.DTOs;
using Icewireless.AccountServiceDashboard.Application.Interfaces;
using Icewireless.AccountServiceDashboard.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Icewireless.AccountServiceDashboard.Web.Controllers;

public sealed class ReportsController : Controller
{
    private readonly IStatusService _statusService;
    private readonly IAccountRepository _accountRepository;
    private readonly IExportService _exportService;
    private readonly IAnalysisRepository _analysisRepository;
    private readonly DashboardSettings _settings;

    public ReportsController(
        IStatusService statusService,
        IAccountRepository accountRepository,
        IExportService exportService,
        IAnalysisRepository analysisRepository,
        IOptions<DashboardSettings> settings)
    {
        _statusService = statusService;
        _accountRepository = accountRepository;
        _exportService = exportService;
        _analysisRepository = analysisRepository;
        _settings = settings.Value;
    }

    [HttpGet("/reports/account-details")]
    public async Task<IActionResult> AccountDetails([FromQuery] FilterQuery q, CancellationToken ct)
    {
        var filter = q.ToFilter(_settings.PageSize);
        var data = await _accountRepository.GetAccountDetailsAsync(filter, ct);
        return View("~/Views/StatusEvents/Index.cshtml", await Table("Account Details", "report-accounts", "history", filter, data, ct));
    }

    [HttpGet("/reports/service-details")]
    public async Task<IActionResult> ServiceDetails([FromQuery] FilterQuery q, CancellationToken ct)
    {
        var filter = q.ToFilter(_settings.PageSize);
        var data = await _statusService.GetCurrentActiveLinesAsync(filter, ct);
        return View("~/Views/LineDetails/CurrentActiveLines.cshtml", new CurrentActiveLinesPageViewModel
        {
            Title = "Service Details",
            ActiveNavKey = "report-services",
            Breadcrumbs = [new("Home", "/"), new("Reports"), new("Service Details")],
            Filter = filter,
            FilterOptions = await SafeOptions(filter, ct),
            Data = data
        });
    }

    [HttpGet("/reports/status-history")]
    public async Task<IActionResult> StatusHistory([FromQuery] FilterQuery q, CancellationToken ct)
    {
        var filter = q.ToFilter(_settings.PageSize);
        var data = await _statusService.GetStatusHistoryAsync(filter, ct);
        return View("~/Views/StatusEvents/Index.cshtml", await Table("Status History", "report-status-history", "history", filter, data, ct));
    }

    [HttpGet("/reports/export")]
    public async Task<IActionResult> ExportData([FromQuery] FilterQuery q, CancellationToken ct)
    {
        var filter = q.ToFilter(_settings.PageSize);
        return View("ExportData", new ExportPageViewModel
        {
            Title = "Export Data",
            ActiveNavKey = "report-export",
            Breadcrumbs = [new("Home", "/"), new("Reports"), new("Export Data")],
            Filter = filter,
            FilterOptions = await SafeOptions(filter, ct)
        });
    }

    [HttpGet("/reports/export/csv/{reportKey}")]
    public async Task<IActionResult> ExportCsv(string reportKey, [FromQuery] FilterQuery q, CancellationToken ct)
    {
        var result = await _exportService.ExportCsvAsync(reportKey, q.ToFilter(_settings.PageSize), ct);
        return File(result.Content, result.ContentType, result.FileName);
    }

    [HttpGet("/reports/export/excel/{reportKey}")]
    public async Task<IActionResult> ExportExcel(string reportKey, [FromQuery] FilterQuery q, CancellationToken ct)
    {
        var result = await _exportService.ExportExcelAsync(reportKey, q.ToFilter(_settings.PageSize), ct);
        return File(result.Content, result.ContentType, result.FileName.Replace(".csv", ".xlsx"));
    }

    private async Task<StatusEventsPageViewModel> Table(string title, string nav, string eventKey, DashboardFilter filter, PagedResult<StatusEventDto> data, CancellationToken ct) =>
        new()
        {
            Title = title,
            ActiveNavKey = nav,
            EventKey = eventKey,
            Breadcrumbs = [new("Home", "/"), new("Reports"), new(title)],
            Filter = filter,
            FilterOptions = await SafeOptions(filter, ct),
            Data = data,
            DateFieldUsed = "ACCTSTATUS.FROMDATE",
            StatusEndField = "ACCTSTATUS.TODATE"
        };

    private async Task<FilterOptionsDto> SafeOptions(DashboardFilter filter, CancellationToken ct)
    {
        try { return await _analysisRepository.GetFilterOptionsAsync(filter, ct); }
        catch { return new FilterOptionsDto([], [], [], []); }
    }
}

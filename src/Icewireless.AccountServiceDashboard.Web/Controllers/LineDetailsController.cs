using Icewireless.AccountServiceDashboard.Application.Configuration;
using Icewireless.AccountServiceDashboard.Application.DTOs;
using Icewireless.AccountServiceDashboard.Application.Interfaces;
using Icewireless.AccountServiceDashboard.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Icewireless.AccountServiceDashboard.Web.Controllers;

public sealed class LineDetailsController : Controller
{
    private readonly IStatusService _statusService;
    private readonly IExportService _exportService;
    private readonly IAnalysisRepository _analysisRepository;
    private readonly DashboardSettings _settings;

    public LineDetailsController(
        IStatusService statusService,
        IExportService exportService,
        IAnalysisRepository analysisRepository,
        IOptions<DashboardSettings> settings)
    {
        _statusService = statusService;
        _exportService = exportService;
        _analysisRepository = analysisRepository;
        _settings = settings.Value;
    }

    [HttpGet("/line-details/new-active")]
    public async Task<IActionResult> NewActiveLines([FromQuery] FilterQuery query, CancellationToken ct)
    {
        var filter = query.ToFilter(_settings.PageSize);
        var page = await _statusService.GetNewActiveLinesAsync(filter, ct);
        return View("NewActiveLines", new NewActiveLinesPageViewModel
        {
            Title = "New Active Lines",
            ActiveNavKey = "line-new-active",
            Breadcrumbs =
            [
                new BreadcrumbItem("Home", "/"),
                new BreadcrumbItem("Line Details"),
                new BreadcrumbItem("New Active Lines")
            ],
            Filter = filter,
            FilterOptions = await SafeOptions(filter, ct),
            Data = page,
            DateFieldUsed = "ACCOUNTS.REGISTRATIONDATE"
        });
    }

    [HttpGet("/line-details/current-active")]
    public async Task<IActionResult> CurrentActiveLines([FromQuery] FilterQuery query, CancellationToken ct)
    {
        var filter = query.ToFilter(_settings.PageSize);
        var page = await _statusService.GetCurrentActiveLinesAsync(filter, ct);
        return View("CurrentActiveLines", new CurrentActiveLinesPageViewModel
        {
            Title = "Current Active Lines",
            ActiveNavKey = "line-current-active",
            Breadcrumbs =
            [
                new BreadcrumbItem("Home", "/"),
                new BreadcrumbItem("Line Details"),
                new BreadcrumbItem("Current Active Lines")
            ],
            Filter = filter,
            FilterOptions = await SafeOptions(filter, ct),
            Data = page
        });
    }

    [HttpGet("/line-details/activated-during-period")]
    public async Task<IActionResult> ActivatedDuringPeriod([FromQuery] FilterQuery query, CancellationToken ct)
    {
        var filter = query.ToFilter(_settings.PageSize);
        var page = await _statusService.GetActivatedDuringPeriodAsync(filter, ct);
        return View("ActivatedDuringPeriod", new ActivatedDuringPeriodPageViewModel
        {
            Title = "Activated During Selected Period",
            ActiveNavKey = "line-activated-period",
            Breadcrumbs =
            [
                new BreadcrumbItem("Home", "/"),
                new BreadcrumbItem("Line Details"),
                new BreadcrumbItem("Activated During Selected Period")
            ],
            Filter = filter,
            FilterOptions = await SafeOptions(filter, ct),
            Data = page,
            DateFieldUsed = "ACCOUNTS.ACTIVATIONDATE"
        });
    }

    [HttpGet("/line-details/new-active/export/csv")]
    public async Task<IActionResult> ExportNewCsv([FromQuery] FilterQuery query, CancellationToken ct)
    {
        var result = await _exportService.ExportNewActiveLinesCsvAsync(query.ToFilter(_settings.PageSize), ct);
        return File(result.Content, result.ContentType, result.FileName);
    }

    [HttpGet("/line-details/new-active/export/excel")]
    public async Task<IActionResult> ExportNewExcel([FromQuery] FilterQuery query, CancellationToken ct)
    {
        var result = await _exportService.ExportNewActiveLinesExcelAsync(query.ToFilter(_settings.PageSize), ct);
        return File(result.Content, result.ContentType, result.FileName.Replace(".csv", ".xlsx"));
    }

    [HttpGet("/line-details/current-active/export/csv")]
    public async Task<IActionResult> ExportCurrentCsv([FromQuery] FilterQuery query, CancellationToken ct)
    {
        var result = await _exportService.ExportCurrentActiveLinesCsvAsync(query.ToFilter(_settings.PageSize), ct);
        return File(result.Content, result.ContentType, result.FileName);
    }

    [HttpGet("/line-details/current-active/export/excel")]
    public async Task<IActionResult> ExportCurrentExcel([FromQuery] FilterQuery query, CancellationToken ct)
    {
        var result = await _exportService.ExportCurrentActiveLinesExcelAsync(query.ToFilter(_settings.PageSize), ct);
        return File(result.Content, result.ContentType, result.FileName.Replace(".csv", ".xlsx"));
    }

    [HttpGet("/line-details/activated-during-period/export/csv")]
    public async Task<IActionResult> ExportActivatedCsv([FromQuery] FilterQuery query, CancellationToken ct)
    {
        var result = await _exportService.ExportActivatedDuringPeriodCsvAsync(query.ToFilter(_settings.PageSize), ct);
        return File(result.Content, result.ContentType, result.FileName);
    }

    [HttpGet("/line-details/activated-during-period/export/excel")]
    public async Task<IActionResult> ExportActivatedExcel([FromQuery] FilterQuery query, CancellationToken ct)
    {
        var result = await _exportService.ExportActivatedDuringPeriodExcelAsync(query.ToFilter(_settings.PageSize), ct);
        return File(result.Content, result.ContentType, result.FileName.Replace(".csv", ".xlsx"));
    }

    private async Task<FilterOptionsDto> SafeOptions(DashboardFilter filter, CancellationToken ct)
    {
        try { return await _analysisRepository.GetFilterOptionsAsync(filter, ct); }
        catch { return new FilterOptionsDto([], [], [], []); }
    }
}

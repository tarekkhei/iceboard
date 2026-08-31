using Icewireless.AccountServiceDashboard.Application.Configuration;
using Icewireless.AccountServiceDashboard.Application.DTOs;
using Icewireless.AccountServiceDashboard.Application.Interfaces;
using Icewireless.AccountServiceDashboard.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Icewireless.AccountServiceDashboard.Web.Controllers;

public sealed class StatusEventsController : Controller
{
    private readonly IStatusService _statusService;
    private readonly IExportService _exportService;
    private readonly IAnalysisRepository _analysisRepository;
    private readonly IMetricDateFieldService _dateFields;
    private readonly DashboardSettings _settings;

    public StatusEventsController(
        IStatusService statusService,
        IExportService exportService,
        IAnalysisRepository analysisRepository,
        IMetricDateFieldService dateFields,
        IOptions<DashboardSettings> settings)
    {
        _statusService = statusService;
        _exportService = exportService;
        _analysisRepository = analysisRepository;
        _dateFields = dateFields;
        _settings = settings.Value;
    }

    [HttpGet("/status-events/active")]
    public Task<IActionResult> Active([FromQuery] FilterQuery q, CancellationToken ct) => Page("active", "status-active", "Active Status Events", q, ct);

    [HttpGet("/status-events/suspended")]
    public Task<IActionResult> Suspended([FromQuery] FilterQuery q, CancellationToken ct) => Page("suspended", "status-suspended", "Suspended Events", q, ct);

    [HttpGet("/status-events/pending-cancellation")]
    public Task<IActionResult> PendingCancellation([FromQuery] FilterQuery q, CancellationToken ct) => Page("pending-cancellation", "status-pending-cancellation", "Pending Cancellation", q, ct);

    [HttpGet("/status-events/cancellation")]
    public Task<IActionResult> Cancellation([FromQuery] FilterQuery q, CancellationToken ct) => Page("cancellation", "status-cancellation", "Cancellation Events", q, ct);

    [HttpGet("/status-events/permanently-closed")]
    public Task<IActionResult> PermanentlyClosed([FromQuery] FilterQuery q, CancellationToken ct) => Page("permanently-closed", "status-permanently-closed", "Permanent Closed", q, ct);

    [HttpGet("/status-events/old")]
    public Task<IActionResult> Old([FromQuery] FilterQuery q, CancellationToken ct) => Page("old", "status-old", "Old Status Events", q, ct);

    [HttpGet("/status-events/archived")]
    public Task<IActionResult> Archived([FromQuery] FilterQuery q, CancellationToken ct) => Page("archived", "status-archived", "Archived Status Events", q, ct);

    [HttpGet("/status-events/{eventKey}/export/csv")]
    public async Task<IActionResult> ExportCsv(string eventKey, [FromQuery] FilterQuery q, CancellationToken ct)
    {
        var result = await _exportService.ExportStatusEventsCsvAsync(eventKey, q.ToFilter(_settings.PageSize), ct);
        return File(result.Content, result.ContentType, result.FileName);
    }

    [HttpGet("/status-events/{eventKey}/export/excel")]
    public async Task<IActionResult> ExportExcel(string eventKey, [FromQuery] FilterQuery q, CancellationToken ct)
    {
        var result = await _exportService.ExportStatusEventsExcelAsync(eventKey, q.ToFilter(_settings.PageSize), ct);
        return File(result.Content, result.ContentType, result.FileName.Replace(".csv", ".xlsx"));
    }

    private async Task<IActionResult> Page(string key, string navKey, string title, FilterQuery q, CancellationToken ct)
    {
        var filter = q.ToFilter(_settings.PageSize);
        var data = await _statusService.GetStatusEventsAsync(key, filter, ct);
        FilterOptionsDto options;
        try { options = await _analysisRepository.GetFilterOptionsAsync(filter, ct); }
        catch { options = new FilterOptionsDto([], [], [], []); }

        return View("~/Views/StatusEvents/Index.cshtml", new StatusEventsPageViewModel
        {
            Title = title,
            ActiveNavKey = navKey,
            EventKey = key,
            Breadcrumbs =
            [
                new BreadcrumbItem("Home", "/"),
                new BreadcrumbItem("Status Event Details"),
                new BreadcrumbItem(title)
            ],
            Filter = filter,
            FilterOptions = options,
            Data = data,
            DateFieldUsed = key switch
            {
                "suspended" => _dateFields.ResolveDateField("suspended-events"),
                "pending-cancellation" => _dateFields.ResolveDateField("pending-cancellation"),
                "cancellation" => _dateFields.ResolveDateField("cancellation-events"),
                "permanently-closed" => _dateFields.ResolveDateField("permanently-closed"),
                _ => _dateFields.ResolveDateField("status-events")
            },
            StatusEndField = _dateFields.ResolveDateField("status-end")
        });
    }
}

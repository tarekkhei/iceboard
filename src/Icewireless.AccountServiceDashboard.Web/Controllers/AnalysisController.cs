using Icewireless.AccountServiceDashboard.Application.Configuration;
using Icewireless.AccountServiceDashboard.Application.DTOs;
using Icewireless.AccountServiceDashboard.Application.Interfaces;
using Icewireless.AccountServiceDashboard.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Icewireless.AccountServiceDashboard.Web.Controllers;

public sealed class AnalysisController : Controller
{
    private readonly IAnalysisService _analysisService;
    private readonly IAnalysisRepository _analysisRepository;
    private readonly DashboardSettings _settings;

    public AnalysisController(
        IAnalysisService analysisService,
        IAnalysisRepository analysisRepository,
        IOptions<DashboardSettings> settings)
    {
        _analysisService = analysisService;
        _analysisRepository = analysisRepository;
        _settings = settings.Value;
    }

    [HttpGet("/analysis/region")]
    public Task<IActionResult> Region([FromQuery] FilterQuery q, CancellationToken ct) =>
        Breakdown("Region Analysis", "analysis-region", q, ct, f => _analysisService.GetRegionAnalysisAsync(f, ct));

    [HttpGet("/analysis/product")]
    [HttpGet("/analysis/service")]
    public Task<IActionResult> Product([FromQuery] FilterQuery q, CancellationToken ct) =>
        Breakdown("Product Analysis", "analysis-product", q, ct, f => _analysisService.GetProductAnalysisAsync(f, ct));

    [HttpGet("/analysis/cancellation-reason")]
    public Task<IActionResult> CancellationReason([FromQuery] FilterQuery q, CancellationToken ct) =>
        Breakdown("Cancellation by Reason", "analysis-cancellation-reason", q, ct, f => _analysisService.GetCancellationByReasonAsync(f, ct));

    [HttpGet("/analysis/activation-trends")]
    public Task<IActionResult> ActivationTrends([FromQuery] FilterQuery q, CancellationToken ct) => Trends("Activation Trends", "analysis-activation-trends", q, ct);

    [HttpGet("/analysis/cancellation-trends")]
    public Task<IActionResult> CancellationTrends([FromQuery] FilterQuery q, CancellationToken ct) => Trends("Cancellation Trends", "analysis-cancellation-trends", q, ct);

    [HttpGet("/analysis/net-growth")]
    public Task<IActionResult> NetGrowth([FromQuery] FilterQuery q, CancellationToken ct) => Trends("Net Growth", "analysis-net-growth", q, ct);

    [HttpGet("/analysis/churn")]
    public async Task<IActionResult> Churn([FromQuery] FilterQuery q, CancellationToken ct)
    {
        var filter = q.ToFilter(_settings.PageSize);
        var (activations, cancellations, _) = await _analysisService.GetTrendsAsync(filter, ct);
        return View("~/Views/Analysis/Trends.cshtml", new AnalysisTrendsPageViewModel
        {
            Title = "Churn Analysis",
            ActiveNavKey = "analysis-churn",
            Breadcrumbs = [new("Home", "/"), new("Analysis"), new("Churn Analysis")],
            Filter = filter,
            FilterOptions = await SafeOptions(filter, ct),
            Activations = activations,
            Cancellations = cancellations,
            NetGrowth = [],
            Note = "Churn Rate = Cancellation Events (unique T-status accounts, ACCTSTATUS.FROMDATE) ÷ Opening Active Lines."
        });
    }

    private async Task<IActionResult> Breakdown(
        string title,
        string navKey,
        FilterQuery q,
        CancellationToken ct,
        Func<DashboardFilter, Task<IReadOnlyList<BreakdownItemDto>>> loader)
    {
        var filter = q.ToFilter(_settings.PageSize);
        var items = await loader(filter);
        return View("~/Views/Analysis/Breakdown.cshtml", new AnalysisBreakdownPageViewModel
        {
            Title = title,
            ActiveNavKey = navKey,
            Breadcrumbs = [new("Home", "/"), new("Analysis"), new(title)],
            Filter = filter,
            FilterOptions = await SafeOptions(filter, ct),
            Items = items
        });
    }

    private async Task<IActionResult> Trends(string title, string navKey, FilterQuery q, CancellationToken ct)
    {
        var filter = q.ToFilter(_settings.PageSize);
        var (activations, cancellations, net) = await _analysisService.GetTrendsAsync(filter, ct);
        return View("~/Views/Analysis/Trends.cshtml", new AnalysisTrendsPageViewModel
        {
            Title = title,
            ActiveNavKey = navKey,
            Breadcrumbs = [new("Home", "/"), new("Analysis"), new(title)],
            Filter = filter,
            FilterOptions = await SafeOptions(filter, ct),
            Activations = activations,
            Cancellations = cancellations,
            NetGrowth = net,
            Note = "Activations use ACCOUNTS.REGISTRATIONDATE. Cancellations use ACCTSTATUS.FROMDATE."
        });
    }

    private async Task<FilterOptionsDto> SafeOptions(DashboardFilter filter, CancellationToken ct)
    {
        try { return await _analysisRepository.GetFilterOptionsAsync(filter, ct); }
        catch { return new FilterOptionsDto([], [], [], []); }
    }
}

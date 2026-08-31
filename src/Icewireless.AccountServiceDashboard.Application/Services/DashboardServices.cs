using Icewireless.AccountServiceDashboard.Application.DTOs;
using Icewireless.AccountServiceDashboard.Application.Interfaces;
using Icewireless.AccountServiceDashboard.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Icewireless.AccountServiceDashboard.Application.Services;

public sealed class DashboardService : IDashboardService
{
    private readonly IAccountServiceRepository _accountServices;
    private readonly IStatusRepository _status;
    private readonly IAnalysisRepository _analysis;
    private readonly IAccountExclusionService _exclusions;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(
        IAccountServiceRepository accountServices,
        IStatusRepository status,
        IAnalysisRepository analysis,
        IAccountExclusionService exclusions,
        ILogger<DashboardService> logger)
    {
        _accountServices = accountServices;
        _status = status;
        _analysis = analysis;
        _exclusions = exclusions;
        _logger = logger;
    }

    public async Task<(DashboardKpisDto Kpis, DashboardChartsDto Charts)> GetDashboardAsync(
        DashboardFilter filter,
        CancellationToken cancellationToken = default)
    {
        var previousFilter = BuildPreviousPeriodFilter(filter);

        // Sequential Oracle loads: parallel current+previous snapshots were causing
        // connection bursts that surface as intermittent ORA-12570 / connection resets.
        var current = await LoadKpiSnapshotAsync(filter, cancellationToken);
        var previous = await LoadKpiSnapshotAsync(previousFilter, cancellationToken);

        // Chart/trend queries are best-effort: a cancel (ORA-01013) or slow Oracle
        // failure must not turn a successful KPI load into an HTTP 500.
        var activationsTrend = await SafeChartAsync(
            "ActivationTrend", () => _analysis.GetActivationTrendAsync(filter, cancellationToken), [], cancellationToken);
        var cancellationsTrend = await SafeChartAsync(
            "CompletedCancellationsTrend", () => _status.GetCompletedCancellationsTrendAsync(filter, cancellationToken), [], cancellationToken);
        var pendingTrend = await SafeChartAsync(
            "PendingCancellationsTrend", () => _status.GetNewPendingCancellationsTrendAsync(filter, cancellationToken), [], cancellationToken);
        var suspendedTrend = await SafeChartAsync(
            "SuspendedEventsTrend", () => _status.GetSuspendedEventsTrendAsync(filter, cancellationToken), [], cancellationToken);
        var netTrend = BuildNetTrend(activationsTrend, cancellationsTrend);
        var churnRateTrend = BuildChurnRateTrend(cancellationsTrend, current.Opening);

        await LogExclusionImpactAsync(filter, current.CurrentActive, cancellationToken);

        _logger.LogInformation(
            "Dashboard KPIs NewActive={NewActive} CurrentActive={Current} Cancellations={Cancel} NetGrowth={Net} IncludeTestAccounts={IncludeTest}",
            current.NewActive, current.CurrentActive, current.Cancellations, current.NetGrowth, filter.IncludeTestAccounts);

        var charts = new DashboardChartsDto(
            activationsTrend,
            cancellationsTrend,
            suspendedTrend,
            netTrend,
            churnRateTrend,
            await SafeChartAsync("StatusDistribution", () => _analysis.GetStatusDistributionAsync(filter, cancellationToken), [], cancellationToken),
            await SafeChartAsync("RegionAnalysis", () => _analysis.GetRegionAnalysisAsync(filter, cancellationToken), [], cancellationToken),
            await SafeChartAsync("ProductAnalysis", () => _analysis.GetProductAnalysisAsync(filter, cancellationToken), [], cancellationToken),
            await SafeChartAsync("CancellationByReason", () => _analysis.GetCancellationByReasonAsync(filter, cancellationToken), [], cancellationToken));

        var prevFrom = previousFilter.FromDate;
        var prevTo = previousFilter.ToDate;
        var activationSpark = ToSparkline(activationsTrend);
        var cancelSpark = ToSparkline(cancellationsTrend);
        var pendingSpark = ToSparkline(pendingTrend);
        var suspendedSpark = ToSparkline(suspendedTrend);
        var netSpark = ToSparkline(netTrend);
        var churnSpark = ToSparkline(churnRateTrend);

        var kpis = new DashboardKpisDto(
            current.NewActive,
            current.CurrentActive,
            0,
            current.Suspended,
            current.Pending,
            current.Cancellations,
            current.Permanent,
            current.NetGrowth,
            current.Churn,
            current.ChurnReason,
            filter.FromDate!.Value,
            filter.ToDate!.Value,
            BuildTrend(current.NewActive, previous.NewActive, activationSpark, prevFrom, prevTo),
            BuildTrend(current.CurrentActive, previous.CurrentActive, [], prevFrom, prevTo),
            BuildTrend(current.Suspended, previous.Suspended, suspendedSpark, prevFrom, prevTo),
            BuildTrend(current.Pending, previous.Pending, pendingSpark, prevFrom, prevTo),
            BuildTrend(current.Cancellations, previous.Cancellations, cancelSpark, prevFrom, prevTo),
            BuildTrend(current.Permanent, previous.Permanent, cancelSpark, prevFrom, prevTo),
            BuildTrend(current.NetGrowth, previous.NetGrowth, netSpark, prevFrom, prevTo),
            BuildTrend(current.Churn, previous.Churn, churnSpark, prevFrom, prevTo));

        return (kpis, charts);
    }

    private async Task<KpiSnapshot> LoadKpiSnapshotAsync(DashboardFilter filter, CancellationToken cancellationToken)
    {
        var newActive = await _accountServices.CountNewActiveLinesAsync(filter, cancellationToken);
        var currentActive = await _accountServices.CountCurrentActiveLinesAsync(filter, cancellationToken);
        var suspended = await _status.CountStatusEventsAsync(StatusCodes.Suspended, filter, cancellationToken);
        var pending = await _status.CountPendingCancellationAccountsAsync(filter, cancellationToken);
        var cancellations = await _status.CountCancellationAccountsAsync(filter, cancellationToken);
        var permanent = await _status.CountPermanentClosedAccountsAsync(filter, cancellationToken);
        var opening = await _accountServices.CountOpeningActiveLinesAsync(filter, cancellationToken);
        var netGrowth = newActive - cancellations;

        double? churn = null;
        string? churnReason = null;
        if (opening <= 0)
            churnReason = "Opening active lines could not be derived for the selected period.";
        else
            churn = Math.Round(cancellations * 100.0 / opening, 2);

        return new KpiSnapshot(newActive, currentActive, suspended, pending, cancellations, permanent, netGrowth, opening, churn, churnReason);
    }

    private static DashboardFilter BuildPreviousPeriodFilter(DashboardFilter filter)
    {
        var from = filter.FromDate!.Value;
        var to = filter.ToDate!.Value;
        var days = to.DayNumber - from.DayNumber + 1;
        if (days < 1) days = 1;
        var prevTo = from.AddDays(-1);
        var prevFrom = prevTo.AddDays(-(days - 1));
        return CloneWithDates(filter, prevFrom, prevTo);
    }

    private static DashboardFilter CloneWithDates(DashboardFilter filter, DateOnly from, DateOnly to) =>
        new()
        {
            ProviderCode = filter.ProviderCode,
            FromDate = from,
            ToDate = to,
            Region = filter.Region,
            Product = filter.Product,
            ServiceType = filter.ServiceType,
            Status = filter.Status,
            Search = filter.Search,
            IncludeTestAccounts = filter.IncludeTestAccounts,
            SortColumn = filter.SortColumn,
            SortDescending = filter.SortDescending,
            Page = filter.Page,
            PageSize = filter.PageSize
        };

    private static KpiTrendDto BuildTrend(
        double? current,
        double? previous,
        IReadOnlyList<double> sparkline,
        DateOnly? prevFrom,
        DateOnly? prevTo)
    {
        if (current is null && previous is null)
            return KpiTrendDto.Empty;

        double? percent = null;
        var direction = "na";
        if (current is not null && previous is not null)
        {
            if (previous.Value == 0 && current.Value == 0)
            {
                percent = 0;
                direction = "flat";
            }
            else if (previous.Value == 0)
            {
                percent = null;
                direction = current.Value > 0 ? "up" : current.Value < 0 ? "down" : "flat";
            }
            else
            {
                percent = Math.Round((current.Value - previous.Value) / Math.Abs(previous.Value) * 100.0, 1);
                direction = percent > 0 ? "up" : percent < 0 ? "down" : "flat";
            }
        }

        return new KpiTrendDto(previous, percent, direction, sparkline, prevFrom, prevTo);
    }

    private static KpiTrendDto BuildTrend(
        long current,
        long previous,
        IReadOnlyList<double> sparkline,
        DateOnly? prevFrom,
        DateOnly? prevTo) =>
        BuildTrend((double)current, (double)previous, sparkline, prevFrom, prevTo);

    private static IReadOnlyList<double> ToSparkline(IReadOnlyList<TrendPointDto> points) =>
        points.OrderBy(p => p.PeriodStart).Select(p => (double)p.Value).TakeLast(8).ToList();

    private static IReadOnlyList<double> ToSparkline(IReadOnlyList<RatePointDto> points) =>
        points.OrderBy(p => p.PeriodStart).Select(p => p.Value).TakeLast(8).ToList();

    private async Task<T> SafeChartAsync<T>(
        string name,
        Func<Task<T>> work,
        T fallback,
        CancellationToken cancellationToken)
    {
        try
        {
            return await work();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // ORA-01013 and similar cancels/timeouts during chart load should not 500 the page.
            _logger.LogWarning(ex, "Dashboard chart/query {Name} failed; continuing with empty data.", name);
            return fallback;
        }
    }

    private async Task LogExclusionImpactAsync(DashboardFilter filter, long finalCurrentActive, CancellationToken cancellationToken)
    {
        try
        {
            if (filter.IncludeTestAccounts)
            {
                _logger.LogInformation(
                    "Dashboard query returned {Final} current active accounts (Include Test / Demo Accounts enabled — exclusion bypassed).",
                    finalCurrentActive);
                return;
            }

            var excluded = await _exclusions.CountExcludedAccountsAsync(filter, cancellationToken);
            _logger.LogInformation(
                "Dashboard query returned {Final} current active accounts after excluding {Excluded} test/demo accounts.",
                finalCurrentActive, excluded);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Could not log exclusion impact for dashboard.");
        }
    }

    private static IReadOnlyList<RatePointDto> BuildChurnRateTrend(
        IReadOnlyList<TrendPointDto> cancellations,
        long openingActive)
    {
        if (openingActive <= 0)
            return [];

        return cancellations
            .OrderBy(p => p.PeriodStart)
            .Select(p => new RatePointDto(
                p.PeriodStart,
                p.PeriodLabel,
                Math.Round(p.Value * 100.0 / openingActive, 2)))
            .ToList();
    }

    private static IReadOnlyList<TrendPointDto> BuildNetTrend(
        IReadOnlyList<TrendPointDto> activations,
        IReadOnlyList<TrendPointDto> cancellations)
    {
        var map = new Dictionary<DateOnly, (long Act, long Cancel)>();
        foreach (var a in activations)
            map[a.PeriodStart] = (a.Value, map.GetValueOrDefault(a.PeriodStart).Cancel);
        foreach (var c in cancellations)
            map[c.PeriodStart] = (map.GetValueOrDefault(c.PeriodStart).Act, c.Value);

        return map.OrderBy(kv => kv.Key)
            .Select(kv => new TrendPointDto(kv.Key, $"W/C {kv.Key:MMM dd}", kv.Value.Act - kv.Value.Cancel))
            .ToList();
    }

    private sealed record KpiSnapshot(
        long NewActive,
        long CurrentActive,
        long Suspended,
        long Pending,
        long Cancellations,
        long Permanent,
        long NetGrowth,
        long Opening,
        double? Churn,
        string? ChurnReason);
}

public sealed class StatusService : IStatusService
{
    private readonly IAccountServiceRepository _accountServices;
    private readonly IStatusRepository _statusRepository;
    private readonly IStatusMappingService _statusMapping;

    public StatusService(
        IAccountServiceRepository accountServices,
        IStatusRepository statusRepository,
        IStatusMappingService statusMapping)
    {
        _accountServices = accountServices;
        _statusRepository = statusRepository;
        _statusMapping = statusMapping;
    }

    public async Task<PagedResult<NewActiveLineDto>> GetNewActiveLinesAsync(DashboardFilter filter, CancellationToken cancellationToken = default)
    {
        var page = await _accountServices.GetNewActiveLinesAsync(filter, cancellationToken);
        return page;
    }

    public async Task<PagedResult<CurrentActiveLineDto>> GetCurrentActiveLinesAsync(DashboardFilter filter, CancellationToken cancellationToken = default)
    {
        var page = await _accountServices.GetCurrentActiveLinesAsync(filter, cancellationToken);
        var items = page.Items.Select(i => i with { StatusLabel = _statusMapping.GetDisplayLabel(i.StatusCode) }).ToList();
        return new PagedResult<CurrentActiveLineDto>(items, page.TotalCount, page.Page, page.PageSize);
    }

    public async Task<PagedResult<ActivatedDuringPeriodDto>> GetActivatedDuringPeriodAsync(DashboardFilter filter, CancellationToken cancellationToken = default)
    {
        var page = await _accountServices.GetActivatedDuringPeriodAsync(filter, cancellationToken);
        var items = page.Items.Select(i => i with { StatusLabel = _statusMapping.GetDisplayLabel(i.StatusCode) }).ToList();
        return new PagedResult<ActivatedDuringPeriodDto>(items, page.TotalCount, page.Page, page.PageSize);
    }

    public Task<PagedResult<StatusEventDto>> GetStatusEventsAsync(string eventKey, DashboardFilter filter, CancellationToken cancellationToken = default)
    {
        if (string.Equals(eventKey, "pending-cancellation", StringComparison.OrdinalIgnoreCase))
            return _statusRepository.GetPendingCancellationAccountsAsync(filter, cancellationToken);

        if (string.Equals(eventKey, "cancellation", StringComparison.OrdinalIgnoreCase))
            return _statusRepository.GetCancellationAccountsAsync(filter, cancellationToken);

        if (string.Equals(eventKey, "permanently-closed", StringComparison.OrdinalIgnoreCase))
            return _statusRepository.GetPermanentClosedAccountsAsync(filter, cancellationToken);

        return _statusRepository.GetStatusEventsAsync(ResolveStatusCode(eventKey), filter, cancellationToken);
    }

    public Task<PagedResult<StatusEventDto>> GetStatusHistoryAsync(DashboardFilter filter, CancellationToken cancellationToken = default) =>
        _statusRepository.GetStatusHistoryAsync(filter, cancellationToken);

    public IReadOnlyList<StatusMappingDto> GetMappings() => _statusMapping.GetAllMappings();

    public string ResolveStatusCode(string eventKey) => eventKey.Trim().ToLowerInvariant() switch
    {
        "active" => StatusCodes.Active,
        "suspended" => StatusCodes.Suspended,
        "pending-cancellation" => StatusCodes.PendingClose,
        "cancellation" => StatusCodes.PermanentClosed,
        "permanently-closed" => StatusCodes.PermanentClosed,
        "old" => StatusCodes.Old,
        "archived" => StatusCodes.Archived,
        _ => throw new ArgumentOutOfRangeException(nameof(eventKey), eventKey, "Unknown status event key.")
    };
}

public sealed class AnalysisService : IAnalysisService
{
    private readonly IAnalysisRepository _analysis;

    public AnalysisService(IAnalysisRepository analysis)
    {
        _analysis = analysis;
    }

    public Task<IReadOnlyList<BreakdownItemDto>> GetRegionAnalysisAsync(DashboardFilter filter, CancellationToken cancellationToken = default) =>
        _analysis.GetRegionAnalysisAsync(filter, cancellationToken);

    public Task<IReadOnlyList<BreakdownItemDto>> GetProductAnalysisAsync(DashboardFilter filter, CancellationToken cancellationToken = default) =>
        _analysis.GetProductAnalysisAsync(filter, cancellationToken);

    public Task<IReadOnlyList<BreakdownItemDto>> GetCancellationByReasonAsync(DashboardFilter filter, CancellationToken cancellationToken = default) =>
        _analysis.GetCancellationByReasonAsync(filter, cancellationToken);

    public async Task<(IReadOnlyList<TrendPointDto> Activations, IReadOnlyList<TrendPointDto> Cancellations, IReadOnlyList<TrendPointDto> NetGrowth)> GetTrendsAsync(
        DashboardFilter filter,
        CancellationToken cancellationToken = default)
    {
        var activations = await _analysis.GetActivationTrendAsync(filter, cancellationToken);
        var cancellations = await _analysis.GetCancellationTrendAsync(filter, cancellationToken);
        var periods = activations.Select(a => a.PeriodStart).Union(cancellations.Select(c => c.PeriodStart)).OrderBy(x => x);
        var net = periods.Select(p =>
        {
            var a = activations.FirstOrDefault(x => x.PeriodStart == p)?.Value ?? 0;
            var c = cancellations.FirstOrDefault(x => x.PeriodStart == p)?.Value ?? 0;
            return new TrendPointDto(p, $"W/C {p:MMM dd}", a - c);
        }).ToList();
        return (activations, cancellations, net);
    }
}

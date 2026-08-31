namespace Icewireless.AccountServiceDashboard.Application.DTOs;

public sealed class DashboardFilter
{
    public string? ProviderCode { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public string? Region { get; set; }
    public string? Product { get; set; }
    public string? ServiceType { get; set; }
    public string? Status { get; set; }
    public string? Search { get; set; }
    /// <summary>
    /// When false (default), active ACCOUNT_EXCLUSION_RULES hide matching accountname patterns.
    /// When true, all accounts are included regardless of exclusion rules.
    /// </summary>
    public bool IncludeTestAccounts { get; set; }
    public string? SortColumn { get; set; }
    public bool SortDescending { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;

    public DashboardFilter Normalize(int defaultPageSize, int maxPageSize = 200)
    {
        var page = Page < 1 ? 1 : Page;
        var size = PageSize < 1 ? defaultPageSize : Math.Min(PageSize, maxPageSize);
        var to = ToDate ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var from = FromDate ?? to.AddDays(-27);
        if (from > to)
        {
            (from, to) = (to, from);
        }

        return new DashboardFilter
        {
            ProviderCode = ResolveProvider(ProviderCode),
            FromDate = from,
            ToDate = to,
            Region = NullIfEmpty(Region),
            Product = NullIfEmpty(Product),
            ServiceType = NullIfEmpty(ServiceType),
            Status = NullIfEmpty(Status),
            Search = NullIfEmpty(Search),
            IncludeTestAccounts = IncludeTestAccounts,
            SortColumn = NullIfEmpty(SortColumn),
            SortDescending = SortDescending,
            Page = page,
            PageSize = size
        };
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string ResolveProvider(string? value)
    {
        var code = value?.Trim();
        if (string.Equals(code, "IRISWV", StringComparison.OrdinalIgnoreCase))
            return "IRISWV";
        return "ICENP";
    }
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

public sealed record NewActiveLineDto(
    long ServiceId,
    DateTime RegistrationDate,
    string? AccountCode,
    string? AccountName,
    string? Product,
    string? Service,
    string? ServiceType,
    decimal? Quantity,
    string? Customer,
    string? Region,
    string? Email);

public sealed record CurrentActiveLineDto(
    long AccountId,
    string? AccountCode,
    string? AccountName,
    string? StatusCode,
    string? StatusLabel,
    string? Customer,
    string? Region,
    string? Email,
    DateTime? ActivationDate,
    string? Product,
    string? Service,
    string? ServiceType);

public sealed record ActivatedDuringPeriodDto(
    long AccountId,
    string? AccountCode,
    string? AccountName,
    string? StatusCode,
    string? StatusLabel,
    string? Customer,
    string? Region,
    string? Email,
    DateTime ActivationDate,
    string? Product,
    string? Service,
    string? ServiceType);

public sealed record StatusEventDto(
    long AccountId,
    long? ServiceId,
    string? AccountCode,
    string? AccountName,
    string StatusCode,
    string StatusLabel,
    DateTime StatusFromDate,
    DateTime? StatusEndDate,
    string? Reason,
    string? Product,
    string? Service,
    string? ServiceType,
    string? Region,
    string? Customer);

public sealed record BreakdownItemDto(string Label, long Value);

public sealed record TrendPointDto(DateOnly PeriodStart, string PeriodLabel, long Value);

public sealed record RatePointDto(DateOnly PeriodStart, string PeriodLabel, double Value);

public sealed record DashboardKpisDto(
    long NewActiveLines,
    long CurrentActiveLines,
    long ActivatedDuringSelectedPeriod,
    long SuspendedEvents,
    long PendingCancellationEvents,
    long CancellationEvents,
    long PermanentClosedEvents,
    long NetGrowth,
    double? ChurnRate,
    string? ChurnUnavailableReason,
    DateOnly FromDate,
    DateOnly ToDate,
    KpiTrendDto NewActiveTrend,
    KpiTrendDto CurrentActiveTrend,
    KpiTrendDto SuspendedTrend,
    KpiTrendDto PendingCancellationTrend,
    KpiTrendDto CancellationTrend,
    KpiTrendDto PermanentClosedTrend,
    KpiTrendDto NetGrowthTrend,
    KpiTrendDto ChurnTrend);

/// <summary>
/// Period-over-period comparison for a KPI card (previous equivalent window).
/// </summary>
public sealed record KpiTrendDto(
    double? PreviousValue,
    double? PercentChange,
    string Direction,
    IReadOnlyList<double> Sparkline,
    DateOnly? PreviousFromDate,
    DateOnly? PreviousToDate)
{
    public static KpiTrendDto Empty { get; } = new(null, null, "na", [], null, null);
}

public sealed record DashboardChartsDto(
    IReadOnlyList<TrendPointDto> ActivationsTrend,
    IReadOnlyList<TrendPointDto> CancellationsTrend,
    IReadOnlyList<TrendPointDto> SuspendedTrend,
    IReadOnlyList<TrendPointDto> NetGrowthTrend,
    IReadOnlyList<RatePointDto> ChurnRateTrend,
    IReadOnlyList<BreakdownItemDto> StatusDistribution,
    IReadOnlyList<BreakdownItemDto> RegionAnalysis,
    IReadOnlyList<BreakdownItemDto> ProductAnalysis,
    IReadOnlyList<BreakdownItemDto> CancellationReasons);

public sealed record FilterOptionsDto(
    IReadOnlyList<string> Regions,
    IReadOnlyList<string> Products,
    IReadOnlyList<string> ServiceTypes,
    IReadOnlyList<StatusMappingDto> Statuses);

public sealed record ExportResultDto(
    string FileName,
    string ContentType,
    byte[] Content);

public sealed record AccountExclusionRuleDto(
    long Id,
    string Pattern,
    string MatchType,
    bool IsActive,
    string? Description,
    string? CreatedBy,
    DateTime CreatedDate,
    DateTime LastUpdated);

public sealed record AccountExclusionRuleWriteDto(
    string Pattern,
    string MatchType,
    bool IsActive,
    string? Description,
    string? CreatedBy);

public sealed record AccountExclusionSearchDto(
    string? Search,
    bool? IsActive,
    int Page = 1,
    int PageSize = 50);

public sealed record KpiLinesPageDto(
    string Key,
    string Title,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<string?>> Rows,
    int TotalCount,
    int Page,
    int PageSize,
    string CsvExportPath);

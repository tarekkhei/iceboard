using Icewireless.AccountServiceDashboard.Application.DTOs;

namespace Icewireless.AccountServiceDashboard.Web.ViewModels;

public sealed record BreadcrumbItem(string Text, string? Url = null);

public class PageChromeViewModel
{
    public required string Title { get; init; }
    public required string ActiveNavKey { get; init; }
    public IReadOnlyList<BreadcrumbItem> Breadcrumbs { get; init; } = Array.Empty<BreadcrumbItem>();
    public DashboardFilter? Filter { get; init; }
    public FilterOptionsDto? FilterOptions { get; init; }
}

public sealed class DashboardPageViewModel : PageChromeViewModel
{
    public required DashboardKpisDto Kpis { get; init; }
    public required DashboardChartsDto Charts { get; init; }
    public IReadOnlyDictionary<string, KpiQueryDto> KpiQueries { get; init; } =
        new Dictionary<string, KpiQueryDto>();
}

public sealed class KpiCardViewModel
{
    public required string Title { get; init; }
    public required string Value { get; init; }
    public required string Footnote { get; init; }
    public required string QueryKey { get; init; }
    public string? ValueCss { get; init; }
    public string? LinesKey { get; init; }
    public bool ShowChart { get; init; } = true;
    public KpiTrendDto? Trend { get; init; }
    public string? PreviousDisplay { get; init; }
}

public sealed class NewActiveLinesPageViewModel : PageChromeViewModel
{
    public required PagedResult<NewActiveLineDto> Data { get; init; }
    public required string DateFieldUsed { get; init; }
}

public sealed class CurrentActiveLinesPageViewModel : PageChromeViewModel
{
    public required PagedResult<CurrentActiveLineDto> Data { get; init; }
}

public sealed class ActivatedDuringPeriodPageViewModel : PageChromeViewModel
{
    public required PagedResult<ActivatedDuringPeriodDto> Data { get; init; }
    public required string DateFieldUsed { get; init; }
}

public sealed class StatusEventsPageViewModel : PageChromeViewModel
{
    public required string EventKey { get; init; }
    public required PagedResult<StatusEventDto> Data { get; init; }
    public required string DateFieldUsed { get; init; }
    public required string StatusEndField { get; init; }
}

public sealed class AnalysisBreakdownPageViewModel : PageChromeViewModel
{
    public required IReadOnlyList<BreakdownItemDto> Items { get; init; }
}

public sealed class AnalysisTrendsPageViewModel : PageChromeViewModel
{
    public required IReadOnlyList<TrendPointDto> Activations { get; init; }
    public required IReadOnlyList<TrendPointDto> Cancellations { get; init; }
    public required IReadOnlyList<TrendPointDto> NetGrowth { get; init; }
    public string? Note { get; init; }
}

public sealed class ExportPageViewModel : PageChromeViewModel;

public sealed class StatusMappingPageViewModel : PageChromeViewModel
{
    public IReadOnlyList<StatusMappingDto> Mappings { get; init; } = Array.Empty<StatusMappingDto>();
}

public sealed class DatabaseStatusPageViewModel : PageChromeViewModel
{
    public required DatabaseStatusDto Status { get; init; }
}

public sealed class AccountExclusionsPageViewModel : PageChromeViewModel
{
    public required PagedResult<AccountExclusionRuleDto> Data { get; init; }
    public string? Search { get; init; }
    public bool? ActiveOnly { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed class ErrorViewModel
{
    public string Message { get; init; } = "An unexpected error occurred. Please try again or contact support.";
    public string? Detail { get; init; }
}

/// <summary>Legacy placeholder model kept for admin settings pages.</summary>
public sealed class PageViewModel : PageChromeViewModel
{
    public string Section { get; init; } = "";
    public string Description { get; init; } = "";
    public bool QueriesImplemented { get; init; }
}

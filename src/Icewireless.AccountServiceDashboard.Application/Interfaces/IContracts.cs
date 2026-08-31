using Icewireless.AccountServiceDashboard.Application.DTOs;
using Icewireless.AccountServiceDashboard.Domain.Enums;

namespace Icewireless.AccountServiceDashboard.Application.Interfaces;

public interface IOracleConnectionFactory
{
    Task<System.Data.Common.DbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default);
    Task<bool> CanConnectAsync(CancellationToken cancellationToken = default);
    bool HasConnectionString { get; }
    void EnsureConfigured();
}

public interface IAccountServiceRepository
{
    Task<long> CountNewActiveLinesAsync(DashboardFilter filter, CancellationToken cancellationToken = default);
    Task<PagedResult<NewActiveLineDto>> GetNewActiveLinesAsync(DashboardFilter filter, CancellationToken cancellationToken = default);

    Task<long> CountCurrentActiveLinesAsync(DashboardFilter filter, CancellationToken cancellationToken = default);
    Task<PagedResult<CurrentActiveLineDto>> GetCurrentActiveLinesAsync(DashboardFilter filter, CancellationToken cancellationToken = default);

    Task<long> CountActivatedDuringPeriodAsync(DashboardFilter filter, CancellationToken cancellationToken = default);
    Task<PagedResult<ActivatedDuringPeriodDto>> GetActivatedDuringPeriodAsync(DashboardFilter filter, CancellationToken cancellationToken = default);

    Task<long> CountOpeningActiveLinesAsync(DashboardFilter filter, CancellationToken cancellationToken = default);
}

public interface IStatusRepository
{
    Task<long> CountStatusEventsAsync(string statusCode, DashboardFilter filter, CancellationToken cancellationToken = default);
    Task<PagedResult<StatusEventDto>> GetStatusEventsAsync(string statusCode, DashboardFilter filter, CancellationToken cancellationToken = default);
    Task<PagedResult<StatusEventDto>> GetStatusHistoryAsync(DashboardFilter filter, CancellationToken cancellationToken = default);

    Task<long> CountPendingCancellationAccountsAsync(DashboardFilter filter, CancellationToken cancellationToken = default);
    Task<PagedResult<StatusEventDto>> GetPendingCancellationAccountsAsync(DashboardFilter filter, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrendPointDto>> GetNewPendingCancellationsTrendAsync(DashboardFilter filter, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrendPointDto>> GetSuspendedEventsTrendAsync(DashboardFilter filter, CancellationToken cancellationToken = default);

    Task<long> CountCancellationAccountsAsync(DashboardFilter filter, CancellationToken cancellationToken = default);
    Task<PagedResult<StatusEventDto>> GetCancellationAccountsAsync(DashboardFilter filter, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrendPointDto>> GetCompletedCancellationsTrendAsync(DashboardFilter filter, CancellationToken cancellationToken = default);

    Task<long> CountPermanentClosedAccountsAsync(DashboardFilter filter, CancellationToken cancellationToken = default);
    Task<PagedResult<StatusEventDto>> GetPermanentClosedAccountsAsync(DashboardFilter filter, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrendPointDto>> GetPermanentClosedTrendAsync(DashboardFilter filter, CancellationToken cancellationToken = default);
}

public interface IAnalysisRepository
{
    Task<IReadOnlyList<TrendPointDto>> GetActivationTrendAsync(DashboardFilter filter, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrendPointDto>> GetCancellationTrendAsync(DashboardFilter filter, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BreakdownItemDto>> GetStatusDistributionAsync(DashboardFilter filter, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BreakdownItemDto>> GetRegionAnalysisAsync(DashboardFilter filter, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BreakdownItemDto>> GetProductAnalysisAsync(DashboardFilter filter, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BreakdownItemDto>> GetCancellationByReasonAsync(DashboardFilter filter, CancellationToken cancellationToken = default);
    Task<FilterOptionsDto> GetFilterOptionsAsync(DashboardFilter filter, CancellationToken cancellationToken = default);
}

public interface IAccountRepository
{
    Task<PagedResult<StatusEventDto>> GetAccountDetailsAsync(DashboardFilter filter, CancellationToken cancellationToken = default);
}

public interface IStatusMappingService
{
    ServiceStatus Map(string? statusCode);
    string GetDisplayLabel(ServiceStatus status);
    string GetDisplayLabel(string? statusCode);
    string? GetCode(ServiceStatus status);
    IReadOnlyList<StatusMappingDto> GetAllMappings();
}

public interface IMetricDateFieldService
{
    string ResolveDateField(string metricKey);
}

public interface IDashboardService
{
    Task<(DashboardKpisDto Kpis, DashboardChartsDto Charts)> GetDashboardAsync(DashboardFilter filter, CancellationToken cancellationToken = default);
}

public interface IStatusService
{
    Task<PagedResult<NewActiveLineDto>> GetNewActiveLinesAsync(DashboardFilter filter, CancellationToken cancellationToken = default);
    Task<PagedResult<CurrentActiveLineDto>> GetCurrentActiveLinesAsync(DashboardFilter filter, CancellationToken cancellationToken = default);
    Task<PagedResult<ActivatedDuringPeriodDto>> GetActivatedDuringPeriodAsync(DashboardFilter filter, CancellationToken cancellationToken = default);
    Task<PagedResult<StatusEventDto>> GetStatusEventsAsync(string eventKey, DashboardFilter filter, CancellationToken cancellationToken = default);
    Task<PagedResult<StatusEventDto>> GetStatusHistoryAsync(DashboardFilter filter, CancellationToken cancellationToken = default);
    IReadOnlyList<StatusMappingDto> GetMappings();
    string ResolveStatusCode(string eventKey);
}

public interface IAnalysisService
{
    Task<IReadOnlyList<BreakdownItemDto>> GetRegionAnalysisAsync(DashboardFilter filter, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BreakdownItemDto>> GetProductAnalysisAsync(DashboardFilter filter, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BreakdownItemDto>> GetCancellationByReasonAsync(DashboardFilter filter, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<TrendPointDto> Activations, IReadOnlyList<TrendPointDto> Cancellations, IReadOnlyList<TrendPointDto> NetGrowth)> GetTrendsAsync(DashboardFilter filter, CancellationToken cancellationToken = default);
}

public interface IKpiLinesService
{
    Task<KpiLinesPageDto> GetLinesAsync(string kpiKey, DashboardFilter filter, CancellationToken cancellationToken = default);
    bool SupportsKey(string kpiKey);
}

public interface IExportService
{
    Task<ExportResultDto> ExportNewActiveLinesCsvAsync(DashboardFilter filter, CancellationToken cancellationToken = default);
    Task<ExportResultDto> ExportNewActiveLinesExcelAsync(DashboardFilter filter, CancellationToken cancellationToken = default);
    Task<ExportResultDto> ExportCurrentActiveLinesCsvAsync(DashboardFilter filter, CancellationToken cancellationToken = default);
    Task<ExportResultDto> ExportCurrentActiveLinesExcelAsync(DashboardFilter filter, CancellationToken cancellationToken = default);
    Task<ExportResultDto> ExportActivatedDuringPeriodCsvAsync(DashboardFilter filter, CancellationToken cancellationToken = default);
    Task<ExportResultDto> ExportActivatedDuringPeriodExcelAsync(DashboardFilter filter, CancellationToken cancellationToken = default);
    Task<ExportResultDto> ExportStatusEventsCsvAsync(string eventKey, DashboardFilter filter, CancellationToken cancellationToken = default);
    Task<ExportResultDto> ExportStatusEventsExcelAsync(string eventKey, DashboardFilter filter, CancellationToken cancellationToken = default);
    Task<ExportResultDto> ExportCsvAsync(string reportKey, DashboardFilter filter, CancellationToken cancellationToken = default);
    Task<ExportResultDto> ExportExcelAsync(string reportKey, DashboardFilter filter, CancellationToken cancellationToken = default);
}

public interface IKpiQueryCatalog
{
    IReadOnlyDictionary<string, KpiQueryDto> GetDashboardKpiQueries(DashboardFilter filter);
}

public interface IAccountExclusionRepository
{
    Task<PagedResult<AccountExclusionRuleDto>> SearchAsync(AccountExclusionSearchDto search, CancellationToken cancellationToken = default);
    Task<AccountExclusionRuleDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<AccountExclusionRuleDto> CreateAsync(AccountExclusionRuleWriteDto rule, CancellationToken cancellationToken = default);
    Task<AccountExclusionRuleDto?> UpdateAsync(long id, AccountExclusionRuleWriteDto rule, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default);
    Task<AccountExclusionRuleDto?> SetActiveAsync(long id, bool isActive, CancellationToken cancellationToken = default);
    Task<long> CountExcludedAccountsAsync(DashboardFilter filter, CancellationToken cancellationToken = default);
}

public interface IAccountExclusionService
{
    Task<PagedResult<AccountExclusionRuleDto>> SearchAsync(AccountExclusionSearchDto search, CancellationToken cancellationToken = default);
    Task<AccountExclusionRuleDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<AccountExclusionRuleDto> CreateAsync(AccountExclusionRuleWriteDto rule, CancellationToken cancellationToken = default);
    Task<AccountExclusionRuleDto?> UpdateAsync(long id, AccountExclusionRuleWriteDto rule, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default);
    Task<AccountExclusionRuleDto?> EnableAsync(long id, CancellationToken cancellationToken = default);
    Task<AccountExclusionRuleDto?> DisableAsync(long id, CancellationToken cancellationToken = default);
    Task<long> CountExcludedAccountsAsync(DashboardFilter filter, CancellationToken cancellationToken = default);
    IReadOnlyList<string> GetActiveContainsPatterns();
    /// <summary>Human-readable SQL-style preview of the exclusion filter for the current checkbox state.</summary>
    string GetExclusionFilterPreview(bool includeTestAccounts);
}

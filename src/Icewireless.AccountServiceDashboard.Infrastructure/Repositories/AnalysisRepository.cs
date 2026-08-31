using System.Data;
using Icewireless.AccountServiceDashboard.Application.Configuration;
using Icewireless.AccountServiceDashboard.Application.DTOs;
using Icewireless.AccountServiceDashboard.Application.Interfaces;
using Icewireless.AccountServiceDashboard.Domain.Enums;
using Icewireless.AccountServiceDashboard.Infrastructure.Oracle;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Icewireless.AccountServiceDashboard.Infrastructure.Repositories;

public sealed class AnalysisRepository : IAnalysisRepository
{
    private readonly IOracleConnectionFactory _connections;
    private readonly DashboardSettings _settings;
    private readonly IStatusMappingService _statusMapping;
    private readonly IAccountExclusionPatternProvider _exclusionPatterns;
    private readonly ILogger<AnalysisRepository> _logger;

    public AnalysisRepository(
        IOracleConnectionFactory connections,
        IOptions<DashboardSettings> settings,
        IStatusMappingService statusMapping,
        IAccountExclusionPatternProvider exclusionPatterns,
        ILogger<AnalysisRepository> logger)
    {
        _connections = connections;
        _settings = settings.Value;
        _statusMapping = statusMapping;
        _exclusionPatterns = exclusionPatterns;
        _logger = logger;
    }


    private Task<T> WithRetryAsync<T>(string operation, Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken) =>
        OracleTransientRetry.RunAsync(work, _logger, cancellationToken, operation);

    public Task<IReadOnlyList<TrendPointDto>> GetActivationTrendAsync(DashboardFilter filter, CancellationToken cancellationToken = default) =>
        WithRetryAsync("GetActivationTrendAsync", async ct =>
        {
            _connections.EnsureConfigured();
            filter = filter.Normalize(_settings.PageSize);
            await using var conn = await _connections.CreateOpenConnectionAsync(ct);
            await using var cmd = OracleCommandFactory.Create(conn, OracleSql.ActivationTrend, _settings);
            BindTrend(cmd, filter);
            return await ReadTrendAsync(cmd, ct);
        }, cancellationToken);

    public Task<IReadOnlyList<TrendPointDto>> GetCancellationTrendAsync(DashboardFilter filter, CancellationToken cancellationToken = default) =>
        WithRetryAsync("GetCancellationTrendAsync", async ct =>
        {
            _connections.EnsureConfigured();
            filter = filter.Normalize(_settings.PageSize);
            await using var conn = await _connections.CreateOpenConnectionAsync(ct);
            await using var cmd = OracleCommandFactory.Create(conn, OracleSql.NewPendingCancellationsTrend, _settings);
            BindTrend(cmd, filter);
            OracleCommandFactory.Add(cmd, "p_status", StatusCodes.PendingClose, DbType.String);
            return await ReadTrendAsync(cmd, ct);
        }, cancellationToken);

    public Task<IReadOnlyList<BreakdownItemDto>> GetStatusDistributionAsync(DashboardFilter filter, CancellationToken cancellationToken = default) =>
        WithRetryAsync("GetStatusDistributionAsync", async ct =>
        {
            _connections.EnsureConfigured();
            filter = filter.Normalize(_settings.PageSize);
            await using var conn = await _connections.CreateOpenConnectionAsync(ct);
            await using var cmd = OracleCommandFactory.Create(conn, OracleSql.StatusDistribution, _settings);
            OracleCommandFactory.AddProvider(cmd, _settings, filter);
            OracleCommandFactory.Add(cmd, "p_from", filter.FromDate!.Value.ToDateTime(TimeOnly.MinValue), DbType.Date);
            OracleCommandFactory.Add(cmd, "p_to", filter.ToDate!.Value.ToDateTime(TimeOnly.MinValue), DbType.Date);
            OracleCommandFactory.Add(cmd, "p_exclude_product", OracleSql.ExcludedProductCode, DbType.String);
            OracleCommandFactory.ApplyExclusionSql(cmd, filter, _exclusionPatterns.GetActiveContainsPatterns());
            var items = await ReadBreakdownAsync(cmd, ct);
            return (IReadOnlyList<BreakdownItemDto>)items.Select(i => new BreakdownItemDto(_statusMapping.GetDisplayLabel(i.Label), i.Value)).ToList();
        }, cancellationToken);

    public Task<IReadOnlyList<BreakdownItemDto>> GetRegionAnalysisAsync(DashboardFilter filter, CancellationToken cancellationToken = default) =>
        QueryBreakdownAsync(OracleSql.RegionAnalysis, filter, cancellationToken);

    public Task<IReadOnlyList<BreakdownItemDto>> GetProductAnalysisAsync(DashboardFilter filter, CancellationToken cancellationToken = default) =>
        QueryBreakdownAsync(OracleSql.ProductAnalysis, filter, cancellationToken);

    public Task<IReadOnlyList<BreakdownItemDto>> GetCancellationByReasonAsync(DashboardFilter filter, CancellationToken cancellationToken = default) =>
        WithRetryAsync("GetCancellationByReasonAsync", async ct =>
        {
            _connections.EnsureConfigured();
            filter = filter.Normalize(_settings.PageSize);
            await using var conn = await _connections.CreateOpenConnectionAsync(ct);
            await using var cmd = OracleCommandFactory.Create(conn, OracleSql.CancellationByReason, _settings);
            OracleCommandFactory.AddProvider(cmd, _settings, filter);
            OracleCommandFactory.Add(cmd, "p_from", filter.FromDate!.Value.ToDateTime(TimeOnly.MinValue), DbType.Date);
            OracleCommandFactory.Add(cmd, "p_to", filter.ToDate!.Value.ToDateTime(TimeOnly.MinValue), DbType.Date);
            OracleCommandFactory.Add(cmd, "p_status", StatusCodes.PendingClose, DbType.String);
            OracleCommandFactory.Add(cmd, "p_exclude_product", OracleSql.ExcludedProductCode, DbType.String);
            OracleCommandFactory.ApplyExclusionSql(cmd, filter, _exclusionPatterns.GetActiveContainsPatterns());
            return await ReadBreakdownAsync(cmd, ct);
        }, cancellationToken);

    public async Task<FilterOptionsDto> GetFilterOptionsAsync(DashboardFilter filter, CancellationToken cancellationToken = default)
    {
        return await WithRetryAsync("GetFilterOptionsAsync", async ct =>
        {
            _connections.EnsureConfigured();
            filter = filter.Normalize(_settings.PageSize);
            await using var conn = await _connections.CreateOpenConnectionAsync(ct);
            var regions = await ReadValuesAsync(conn, OracleSql.FilterRegions, filter, ct);
            var products = await ReadValuesAsync(conn, OracleSql.FilterProducts, filter, ct);
            var types = await ReadValuesAsync(conn, OracleSql.FilterServiceTypes, filter, ct);
            return new FilterOptionsDto(regions, products, types, _statusMapping.GetAllMappings());
        }, cancellationToken);
    }

    private Task<IReadOnlyList<BreakdownItemDto>> QueryBreakdownAsync(string sql, DashboardFilter filter, CancellationToken ct) =>
        WithRetryAsync("QueryBreakdownAsync", async retryCt =>
        {
            _connections.EnsureConfigured();
            filter = filter.Normalize(_settings.PageSize);
            await using var conn = await _connections.CreateOpenConnectionAsync(retryCt);
            await using var cmd = OracleCommandFactory.Create(conn, sql, _settings);
            BindTrend(cmd, filter);
            return await ReadBreakdownAsync(cmd, retryCt);
        }, ct);

    private void BindTrend(global::Oracle.ManagedDataAccess.Client.OracleCommand cmd, DashboardFilter filter)
    {
        OracleCommandFactory.AddProvider(cmd, _settings, filter);
        OracleCommandFactory.Add(cmd, "p_from", filter.FromDate!.Value.ToDateTime(TimeOnly.MinValue), DbType.Date);
        if (cmd.CommandText.Contains(":p_to_exclusive", StringComparison.OrdinalIgnoreCase))
        {
            OracleCommandFactory.Add(cmd, "p_to_exclusive", filter.ToDate!.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), DbType.Date);
        }

        if (System.Text.RegularExpressions.Regex.IsMatch(
                cmd.CommandText,
                @":p_to(?![A-Za-z0-9_])",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            OracleCommandFactory.Add(cmd, "p_to", filter.ToDate!.Value.ToDateTime(TimeOnly.MinValue), DbType.Date);
        }

        if (cmd.CommandText.Contains(":p_region", StringComparison.Ordinal)
            || cmd.CommandText.Contains(":p_product", StringComparison.Ordinal)
            || cmd.CommandText.Contains(":p_exclude_product", StringComparison.Ordinal))
        {
            if (cmd.CommandText.Contains(":p_region", StringComparison.Ordinal))
                OracleCommandFactory.Add(cmd, "p_region", filter.Region, DbType.String);
            if (cmd.CommandText.Contains(":p_product", StringComparison.Ordinal))
                OracleCommandFactory.Add(cmd, "p_product", filter.Product, DbType.String);
            if (cmd.CommandText.Contains(":p_exclude_product", StringComparison.Ordinal))
                OracleCommandFactory.Add(cmd, "p_exclude_product", OracleSql.ExcludedProductCode, DbType.String);
            if (cmd.CommandText.Contains(":p_service_type", StringComparison.Ordinal))
                OracleCommandFactory.Add(cmd, "p_service_type", filter.ServiceType, DbType.String);
        }

        OracleCommandFactory.ApplyExclusionSql(cmd, filter, _exclusionPatterns.GetActiveContainsPatterns());
    }

    private static async Task<IReadOnlyList<TrendPointDto>> ReadTrendAsync(global::Oracle.ManagedDataAccess.Client.OracleCommand cmd, CancellationToken ct)
    {
        var items = new List<TrendPointDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var dt = reader.GetDateTime(reader.GetOrdinal("PERIOD_START"));
            var period = DateOnly.FromDateTime(dt);
            items.Add(new TrendPointDto(period, $"W/C {period:MMM dd}", Convert.ToInt64(reader.GetValue(reader.GetOrdinal("VALUE")))));
        }

        return items;
    }

    private static async Task<IReadOnlyList<BreakdownItemDto>> ReadBreakdownAsync(global::Oracle.ManagedDataAccess.Client.OracleCommand cmd, CancellationToken ct)
    {
        var items = new List<BreakdownItemDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(new BreakdownItemDto(
                Convert.ToString(reader.GetValue(reader.GetOrdinal("LABEL"))) ?? "Unknown",
                Convert.ToInt64(reader.GetValue(reader.GetOrdinal("VALUE")))));
        }

        return items;
    }

    private async Task<IReadOnlyList<string>> ReadValuesAsync(System.Data.Common.DbConnection conn, string sql, DashboardFilter filter, CancellationToken ct)
    {
        await using var cmd = OracleCommandFactory.Create(conn, sql, _settings);
        OracleCommandFactory.AddProvider(cmd, _settings, filter);
        var list = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var v = Convert.ToString(reader.GetValue(0));
            if (!string.IsNullOrWhiteSpace(v))
            {
                list.Add(v);
            }
        }

        _logger.LogDebug("Loaded {Count} filter values", list.Count);
        return list;
    }
}

public sealed class AccountRepository : IAccountRepository
{
    private readonly IStatusRepository _statusRepository;

    public AccountRepository(IStatusRepository statusRepository)
    {
        _statusRepository = statusRepository;
    }

    public Task<PagedResult<StatusEventDto>> GetAccountDetailsAsync(DashboardFilter filter, CancellationToken cancellationToken = default) =>
        _statusRepository.GetStatusHistoryAsync(filter, cancellationToken);
}

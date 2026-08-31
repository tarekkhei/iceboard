using Icewireless.AccountServiceDashboard.Application.Configuration;
using Icewireless.AccountServiceDashboard.Application.DTOs;
using Icewireless.AccountServiceDashboard.Application.Interfaces;
using Icewireless.AccountServiceDashboard.Infrastructure.Oracle;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Icewireless.AccountServiceDashboard.Infrastructure.Repositories;

public sealed class StatusRepository : IStatusRepository
{
    private readonly IOracleConnectionFactory _connections;
    private readonly DashboardSettings _settings;
    private readonly IStatusMappingService _statusMapping;
    private readonly IAccountExclusionPatternProvider _exclusionPatterns;
    private readonly ILogger<StatusRepository> _logger;

    public StatusRepository(
        IOracleConnectionFactory connections,
        IOptions<DashboardSettings> settings,
        IStatusMappingService statusMapping,
        IAccountExclusionPatternProvider exclusionPatterns,
        ILogger<StatusRepository> logger)
    {
        _connections = connections;
        _settings = settings.Value;
        _statusMapping = statusMapping;
        _exclusionPatterns = exclusionPatterns;
        _logger = logger;
    }


    private Task<T> WithRetryAsync<T>(string operation, Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken) =>
        OracleTransientRetry.RunAsync(work, _logger, cancellationToken, operation);

    public async Task<long> CountStatusEventsAsync(string statusCode, DashboardFilter filter, CancellationToken cancellationToken = default)
    {
        return await WithRetryAsync("CountStatusEventsAsync", async ct =>
        {
            _connections.EnsureConfigured();
            filter = filter.Normalize(_settings.PageSize);
            var sql = IsSuspended(statusCode) ? OracleSql.CountSuspendedAccounts : OracleSql.CountStatusEvents;
            await using var conn = await _connections.CreateOpenConnectionAsync(ct);
            await using var cmd = OracleCommandFactory.Create(conn, sql, _settings);
            OracleCommandFactory.BindProviderAndFilters(cmd, _settings, filter, includeDates: true, includePagination: false, statusCode: statusCode, exclusionPatterns: _exclusionPatterns.GetActiveContainsPatterns());
            var count = await OracleCommandFactory.ExecuteCountAsync(cmd, ct);
            if (IsSuspended(statusCode))
                _logger.LogDebug("Suspended accounts count={Count} (DISTINCT a.id, ACCTSTATUS.FROMDATE)", count);
            else
                _logger.LogDebug("Status events count={Count} for {Status} using ACCTSTATUS.FROMDATE", count, statusCode);
            return count;
        }, cancellationToken);
    }

    public async Task<PagedResult<StatusEventDto>> GetStatusEventsAsync(string statusCode, DashboardFilter filter, CancellationToken cancellationToken = default)
    {
        filter = filter.Normalize(_settings.PageSize);
        _connections.EnsureConfigured();
        var total = await CountStatusEventsAsync(statusCode, filter, cancellationToken);
        var sql = IsSuspended(statusCode) ? OracleSql.SelectSuspendedAccounts : OracleSql.SelectStatusEvents;
        await using var conn = await _connections.CreateOpenConnectionAsync(cancellationToken);
        await using var cmd = OracleCommandFactory.Create(conn, sql, _settings);
        OracleCommandFactory.BindProviderAndFilters(cmd, _settings, filter, includeDates: true, includePagination: true, statusCode: statusCode, exclusionPatterns: _exclusionPatterns.GetActiveContainsPatterns());
        return new PagedResult<StatusEventDto>(await ReadEventsAsync(cmd, cancellationToken), (int)total, filter.Page, filter.PageSize);
    }

    public async Task<long> CountPendingCancellationAccountsAsync(DashboardFilter filter, CancellationToken cancellationToken = default)
    {
        return await WithRetryAsync("CountPendingCancellationAccountsAsync", async ct =>
        {
            _connections.EnsureConfigured();
            filter = filter.Normalize(_settings.PageSize);
            await using var conn = await _connections.CreateOpenConnectionAsync(ct);
            await using var cmd = OracleCommandFactory.Create(conn, OracleSql.CountPendingCancellationAccounts, _settings);
            OracleCommandFactory.BindProviderAndFilters(
                cmd, _settings, filter, includeDates: true, includePagination: false,
                statusCode: Domain.Enums.StatusCodes.PendingClose,
                exclusionPatterns: _exclusionPatterns.GetActiveContainsPatterns());
            var count = await OracleCommandFactory.ExecuteCountAsync(cmd, ct);
            _logger.LogDebug("Pending cancellation accounts count={Count} (DISTINCT a.id, ACCTSTATUS.FROMDATE)", count);
            return count;
        }, cancellationToken);
    }

    public async Task<PagedResult<StatusEventDto>> GetPendingCancellationAccountsAsync(DashboardFilter filter, CancellationToken cancellationToken = default)
    {
        filter = filter.Normalize(_settings.PageSize);
        _connections.EnsureConfigured();
        var total = await CountPendingCancellationAccountsAsync(filter, cancellationToken);
        await using var conn = await _connections.CreateOpenConnectionAsync(cancellationToken);
        await using var cmd = OracleCommandFactory.Create(conn, OracleSql.SelectPendingCancellationAccounts, _settings);
        OracleCommandFactory.BindProviderAndFilters(
            cmd, _settings, filter, includeDates: true, includePagination: true,
            statusCode: Domain.Enums.StatusCodes.PendingClose,
            exclusionPatterns: _exclusionPatterns.GetActiveContainsPatterns());
        return new PagedResult<StatusEventDto>(await ReadEventsAsync(cmd, cancellationToken), (int)total, filter.Page, filter.PageSize);
    }

    public Task<IReadOnlyList<TrendPointDto>> GetNewPendingCancellationsTrendAsync(DashboardFilter filter, CancellationToken cancellationToken = default) =>
        WithRetryAsync("GetNewPendingCancellationsTrendAsync", async ct =>
        {
            _connections.EnsureConfigured();
            filter = filter.Normalize(_settings.PageSize);
            await using var conn = await _connections.CreateOpenConnectionAsync(ct);
            await using var cmd = OracleCommandFactory.Create(conn, OracleSql.NewPendingCancellationsTrend, _settings);
            OracleCommandFactory.BindProviderAndFilters(
                cmd, _settings, filter, includeDates: true, includePagination: false,
                statusCode: Domain.Enums.StatusCodes.PendingClose,
                exclusionPatterns: _exclusionPatterns.GetActiveContainsPatterns());
            var items = new List<TrendPointDto>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var dt = reader.GetDateTime(reader.GetOrdinal("PERIOD_START"));
                var period = DateOnly.FromDateTime(dt);
                items.Add(new TrendPointDto(period, $"W/C {period:MMM dd}", Convert.ToInt64(reader.GetValue(reader.GetOrdinal("VALUE")))));
            }

            return (IReadOnlyList<TrendPointDto>)items;
        }, cancellationToken);

    public Task<IReadOnlyList<TrendPointDto>> GetSuspendedEventsTrendAsync(DashboardFilter filter, CancellationToken cancellationToken = default) =>
        WithRetryAsync("GetSuspendedEventsTrendAsync", async ct =>
        {
            _connections.EnsureConfigured();
            filter = filter.Normalize(_settings.PageSize);
            await using var conn = await _connections.CreateOpenConnectionAsync(ct);
            await using var cmd = OracleCommandFactory.Create(conn, OracleSql.SuspendedEventsTrend, _settings);
            OracleCommandFactory.BindProviderAndFilters(
                cmd, _settings, filter, includeDates: true, includePagination: false,
                statusCode: Domain.Enums.StatusCodes.Suspended,
                exclusionPatterns: _exclusionPatterns.GetActiveContainsPatterns());
            var items = new List<TrendPointDto>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var dt = reader.GetDateTime(reader.GetOrdinal("PERIOD_START"));
                var period = DateOnly.FromDateTime(dt);
                items.Add(new TrendPointDto(period, $"W/C {period:MMM dd}", Convert.ToInt64(reader.GetValue(reader.GetOrdinal("VALUE")))));
            }

            return (IReadOnlyList<TrendPointDto>)items;
        }, cancellationToken);

    public async Task<long> CountCancellationAccountsAsync(DashboardFilter filter, CancellationToken cancellationToken = default)
    {
        return await WithRetryAsync("CountCancellationAccountsAsync", async ct =>
        {
            _connections.EnsureConfigured();
            filter = filter.Normalize(_settings.PageSize);
            await using var conn = await _connections.CreateOpenConnectionAsync(ct);
            await using var cmd = OracleCommandFactory.Create(conn, OracleSql.CountCancellationAccounts, _settings);
            OracleCommandFactory.BindProviderAndFilters(
                cmd, _settings, filter, includeDates: true, includePagination: false,
                statusCode: Domain.Enums.StatusCodes.PermanentClosed,
                exclusionPatterns: _exclusionPatterns.GetActiveContainsPatterns());
            var count = await OracleCommandFactory.ExecuteCountAsync(cmd, ct);
            _logger.LogDebug("Cancellation accounts count={Count} (DISTINCT a.id, ACCTSTATUS.FROMDATE)", count);
            return count;
        }, cancellationToken);
    }

    public async Task<PagedResult<StatusEventDto>> GetCancellationAccountsAsync(DashboardFilter filter, CancellationToken cancellationToken = default)
    {
        filter = filter.Normalize(_settings.PageSize);
        _connections.EnsureConfigured();
        var total = await CountCancellationAccountsAsync(filter, cancellationToken);
        await using var conn = await _connections.CreateOpenConnectionAsync(cancellationToken);
        await using var cmd = OracleCommandFactory.Create(conn, OracleSql.SelectCancellationAccounts, _settings);
        OracleCommandFactory.BindProviderAndFilters(
            cmd, _settings, filter, includeDates: true, includePagination: true,
            statusCode: Domain.Enums.StatusCodes.PermanentClosed,
            exclusionPatterns: _exclusionPatterns.GetActiveContainsPatterns());
        return new PagedResult<StatusEventDto>(await ReadEventsAsync(cmd, cancellationToken), (int)total, filter.Page, filter.PageSize);
    }

    public Task<IReadOnlyList<TrendPointDto>> GetCompletedCancellationsTrendAsync(DashboardFilter filter, CancellationToken cancellationToken = default) =>
        WithRetryAsync("GetCompletedCancellationsTrendAsync", async ct =>
        {
            _connections.EnsureConfigured();
            filter = filter.Normalize(_settings.PageSize);
            await using var conn = await _connections.CreateOpenConnectionAsync(ct);
            await using var cmd = OracleCommandFactory.Create(conn, OracleSql.CompletedCancellationsTrend, _settings);
            OracleCommandFactory.BindProviderAndFilters(
                cmd, _settings, filter, includeDates: true, includePagination: false,
                statusCode: Domain.Enums.StatusCodes.PermanentClosed,
                exclusionPatterns: _exclusionPatterns.GetActiveContainsPatterns());
            var items = new List<TrendPointDto>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var dt = reader.GetDateTime(reader.GetOrdinal("PERIOD_START"));
                var period = DateOnly.FromDateTime(dt);
                items.Add(new TrendPointDto(period, $"W/C {period:MMM dd}", Convert.ToInt64(reader.GetValue(reader.GetOrdinal("VALUE")))));
            }

            return (IReadOnlyList<TrendPointDto>)items;
        }, cancellationToken);

    public Task<long> CountPermanentClosedAccountsAsync(DashboardFilter filter, CancellationToken cancellationToken = default) =>
        CountPermanentClosedCoreAsync(filter, cancellationToken);

    public async Task<PagedResult<StatusEventDto>> GetPermanentClosedAccountsAsync(DashboardFilter filter, CancellationToken cancellationToken = default)
    {
        filter = filter.Normalize(_settings.PageSize);
        _connections.EnsureConfigured();
        var total = await CountPermanentClosedCoreAsync(filter, cancellationToken);
        await using var conn = await _connections.CreateOpenConnectionAsync(cancellationToken);
        await using var cmd = OracleCommandFactory.Create(conn, OracleSql.SelectPermanentClosedAccounts, _settings);
        OracleCommandFactory.BindProviderAndFilters(
            cmd, _settings, filter, includeDates: true, includePagination: true,
            statusCode: Domain.Enums.StatusCodes.PermanentClosed,
            exclusionPatterns: _exclusionPatterns.GetActiveContainsPatterns());
        return new PagedResult<StatusEventDto>(await ReadEventsAsync(cmd, cancellationToken), (int)total, filter.Page, filter.PageSize);
    }

    public Task<IReadOnlyList<TrendPointDto>> GetPermanentClosedTrendAsync(DashboardFilter filter, CancellationToken cancellationToken = default) =>
        WithRetryAsync("GetPermanentClosedTrendAsync", async ct =>
        {
            _connections.EnsureConfigured();
            filter = filter.Normalize(_settings.PageSize);
            await using var conn = await _connections.CreateOpenConnectionAsync(ct);
            await using var cmd = OracleCommandFactory.Create(conn, OracleSql.PermanentClosedAccountsTrend, _settings);
            OracleCommandFactory.BindProviderAndFilters(
                cmd, _settings, filter, includeDates: true, includePagination: false,
                statusCode: Domain.Enums.StatusCodes.PermanentClosed,
                exclusionPatterns: _exclusionPatterns.GetActiveContainsPatterns());
            var items = new List<TrendPointDto>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var dt = reader.GetDateTime(reader.GetOrdinal("PERIOD_START"));
                var period = DateOnly.FromDateTime(dt);
                items.Add(new TrendPointDto(period, $"W/C {period:MMM dd}", Convert.ToInt64(reader.GetValue(reader.GetOrdinal("VALUE")))));
            }

            return (IReadOnlyList<TrendPointDto>)items;
        }, cancellationToken);

    private async Task<long> CountPermanentClosedCoreAsync(DashboardFilter filter, CancellationToken cancellationToken)
    {
        return await WithRetryAsync("CountPermanentClosedCoreAsync", async ct =>
        {
            _connections.EnsureConfigured();
            filter = filter.Normalize(_settings.PageSize);
            await using var conn = await _connections.CreateOpenConnectionAsync(ct);
            await using var cmd = OracleCommandFactory.Create(conn, OracleSql.CountPermanentClosedAccounts, _settings);
            OracleCommandFactory.BindProviderAndFilters(
                cmd, _settings, filter, includeDates: true, includePagination: false,
                statusCode: Domain.Enums.StatusCodes.PermanentClosed,
                exclusionPatterns: _exclusionPatterns.GetActiveContainsPatterns());
            var count = await OracleCommandFactory.ExecuteCountAsync(cmd, ct);
            _logger.LogDebug("Permanent closed accounts count={Count} (DISTINCT a.id, ACCTSTATUS.FROMDATE)", count);
            return count;
        }, cancellationToken);
    }

    private static bool IsSuspended(string statusCode) =>
        string.Equals(statusCode, Domain.Enums.StatusCodes.Suspended, StringComparison.OrdinalIgnoreCase);

    public async Task<PagedResult<StatusEventDto>> GetStatusHistoryAsync(DashboardFilter filter, CancellationToken cancellationToken = default)
    {
        filter = filter.Normalize(_settings.PageSize);
        _connections.EnsureConfigured();

        await using var conn = await _connections.CreateOpenConnectionAsync(cancellationToken);
        await using var countCmd = OracleCommandFactory.Create(conn, OracleSql.CountStatusHistory, _settings);
        OracleCommandFactory.BindProviderAndFilters(countCmd, _settings, filter, includeDates: true, includePagination: false, exclusionPatterns: _exclusionPatterns.GetActiveContainsPatterns());
        var total = await OracleCommandFactory.ExecuteCountAsync(countCmd, cancellationToken);

        await using var cmd = OracleCommandFactory.Create(conn, OracleSql.SelectStatusHistory, _settings);
        OracleCommandFactory.BindProviderAndFilters(cmd, _settings, filter, includeDates: true, includePagination: true, exclusionPatterns: _exclusionPatterns.GetActiveContainsPatterns());
        return new PagedResult<StatusEventDto>(await ReadEventsAsync(cmd, cancellationToken), (int)total, filter.Page, filter.PageSize);
    }

    private async Task<IReadOnlyList<StatusEventDto>> ReadEventsAsync(global::Oracle.ManagedDataAccess.Client.OracleCommand cmd, CancellationToken ct)
    {
        var items = new List<StatusEventDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var code = OracleCommandFactory.GetString(reader, "STATUS_CODE") ?? "";
            items.Add(new StatusEventDto(
                OracleCommandFactory.GetInt64(reader, "ACCOUNT_ID") ?? 0,
                OracleCommandFactory.GetInt64(reader, "SERVICE_ID"),
                OracleCommandFactory.GetString(reader, "ACCOUNT_CODE"),
                OracleCommandFactory.GetString(reader, "ACCOUNT_NAME"),
                code,
                _statusMapping.GetDisplayLabel(code),
                OracleCommandFactory.GetDate(reader, "STATUS_FROM_DATE") ?? DateTime.MinValue,
                OracleCommandFactory.GetDate(reader, "STATUS_END_DATE"),
                OracleCommandFactory.GetString(reader, "REASON"),
                OracleCommandFactory.GetString(reader, "PRODUCT"),
                OracleCommandFactory.GetString(reader, "SERVICE_CODE"),
                OracleCommandFactory.GetString(reader, "SERVICE_TYPE"),
                OracleCommandFactory.GetString(reader, "REGION"),
                OracleCommandFactory.GetString(reader, "CUSTOMER")));
        }

        return items;
    }
}

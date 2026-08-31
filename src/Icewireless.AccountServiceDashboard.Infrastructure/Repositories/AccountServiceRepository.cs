using System.Data;
using Icewireless.AccountServiceDashboard.Application.Configuration;
using Icewireless.AccountServiceDashboard.Application.DTOs;
using Icewireless.AccountServiceDashboard.Application.Interfaces;
using Icewireless.AccountServiceDashboard.Domain.Enums;
using Icewireless.AccountServiceDashboard.Infrastructure.Oracle;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Icewireless.AccountServiceDashboard.Infrastructure.Repositories;

public sealed class AccountServiceRepository : IAccountServiceRepository
{
    private readonly IOracleConnectionFactory _connections;
    private readonly DashboardSettings _settings;
    private readonly IAccountExclusionPatternProvider _exclusionPatterns;
    private readonly ILogger<AccountServiceRepository> _logger;

    public AccountServiceRepository(
        IOracleConnectionFactory connections,
        IOptions<DashboardSettings> settings,
        IAccountExclusionPatternProvider exclusionPatterns,
        ILogger<AccountServiceRepository> logger)
    {
        _connections = connections;
        _settings = settings.Value;
        _exclusionPatterns = exclusionPatterns;
        _logger = logger;
    }


    private Task<T> WithRetryAsync<T>(string operation, Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken) =>
        OracleTransientRetry.RunAsync(work, _logger, cancellationToken, operation);

    public async Task<long> CountNewActiveLinesAsync(DashboardFilter filter, CancellationToken cancellationToken = default)
    {
        return await WithRetryAsync("CountNewActiveLinesAsync", async ct =>
        {
            _connections.EnsureConfigured();
            filter = filter.Normalize(_settings.PageSize);
            await using var conn = await _connections.CreateOpenConnectionAsync(ct);
            await using var cmd = OracleCommandFactory.Create(conn, OracleSql.CountNewActiveLines, _settings);
            OracleCommandFactory.BindProviderAndFilters(cmd, _settings, filter, includeDates: true, includePagination: false, exclusionPatterns: _exclusionPatterns.GetActiveContainsPatterns());
            var count = await OracleCommandFactory.ExecuteCountAsync(cmd, ct);
            _logger.LogDebug("New Active Lines count={Count} using ACCOUNTS.REGISTRATIONDATE", count);
            return count;
        }, cancellationToken);
    }

    public async Task<PagedResult<NewActiveLineDto>> GetNewActiveLinesAsync(DashboardFilter filter, CancellationToken cancellationToken = default)
    {
        filter = filter.Normalize(_settings.PageSize);
        _connections.EnsureConfigured();
        var total = await CountNewActiveLinesAsync(filter, cancellationToken);
        await using var conn = await _connections.CreateOpenConnectionAsync(cancellationToken);
        await using var cmd = OracleCommandFactory.Create(conn, OracleSql.SelectNewActiveLines, _settings);
        OracleCommandFactory.BindProviderAndFilters(cmd, _settings, filter, includeDates: true, includePagination: true, exclusionPatterns: _exclusionPatterns.GetActiveContainsPatterns());

        var items = new List<NewActiveLineDto>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new NewActiveLineDto(
                OracleCommandFactory.GetInt64(reader, "SERVICE_ID") ?? 0,
                OracleCommandFactory.GetDate(reader, "REGISTRATION_DATE") ?? DateTime.MinValue,
                OracleCommandFactory.GetString(reader, "ACCOUNT_CODE"),
                OracleCommandFactory.GetString(reader, "ACCOUNT_NAME"),
                OracleCommandFactory.GetString(reader, "PRODUCT"),
                OracleCommandFactory.GetString(reader, "SERVICE_CODE"),
                OracleCommandFactory.GetString(reader, "SERVICE_TYPE"),
                OracleCommandFactory.GetDecimal(reader, "QUANTITY"),
                OracleCommandFactory.GetString(reader, "CUSTOMER"),
                OracleCommandFactory.GetString(reader, "REGION"),
                OracleCommandFactory.GetString(reader, "EMAIL")));
        }

        return new PagedResult<NewActiveLineDto>(items, (int)total, filter.Page, filter.PageSize);
    }

    public async Task<long> CountCurrentActiveLinesAsync(DashboardFilter filter, CancellationToken cancellationToken = default)
    {
        return await WithRetryAsync("CountCurrentActiveLinesAsync", async ct =>
        {
            _connections.EnsureConfigured();
            filter = filter.Normalize(_settings.PageSize);
            await using var conn = await _connections.CreateOpenConnectionAsync(ct);
            await using var cmd = OracleCommandFactory.Create(conn, OracleSql.CountCurrentActiveLines, _settings);
            OracleCommandFactory.BindProviderAndFilters(cmd, _settings, filter, includeDates: false, includePagination: false, activeStatus: StatusCodes.Active, exclusionPatterns: _exclusionPatterns.GetActiveContainsPatterns());
            return await OracleCommandFactory.ExecuteCountAsync(cmd, ct);
        }, cancellationToken);
    }

    public async Task<PagedResult<CurrentActiveLineDto>> GetCurrentActiveLinesAsync(DashboardFilter filter, CancellationToken cancellationToken = default)
    {
        filter = filter.Normalize(_settings.PageSize);
        _connections.EnsureConfigured();
        var total = await CountCurrentActiveLinesAsync(filter, cancellationToken);
        await using var conn = await _connections.CreateOpenConnectionAsync(cancellationToken);
        await using var cmd = OracleCommandFactory.Create(conn, OracleSql.SelectCurrentActiveLines, _settings);
        OracleCommandFactory.BindProviderAndFilters(cmd, _settings, filter, includeDates: false, includePagination: true, activeStatus: StatusCodes.Active, exclusionPatterns: _exclusionPatterns.GetActiveContainsPatterns());

        var items = new List<CurrentActiveLineDto>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var code = OracleCommandFactory.GetString(reader, "STATUS_CODE");
            items.Add(new CurrentActiveLineDto(
                OracleCommandFactory.GetInt64(reader, "ACCOUNT_ID") ?? 0,
                OracleCommandFactory.GetString(reader, "ACCOUNT_CODE"),
                OracleCommandFactory.GetString(reader, "ACCOUNT_NAME"),
                code,
                null,
                OracleCommandFactory.GetString(reader, "CUSTOMER"),
                OracleCommandFactory.GetString(reader, "REGION"),
                OracleCommandFactory.GetString(reader, "EMAIL"),
                OracleCommandFactory.GetDate(reader, "ACTIVATION_DATE"),
                OracleCommandFactory.GetString(reader, "PRODUCT"),
                OracleCommandFactory.GetString(reader, "SERVICE_CODE"),
                OracleCommandFactory.GetString(reader, "SERVICE_TYPE")));
        }

        return new PagedResult<CurrentActiveLineDto>(items, (int)total, filter.Page, filter.PageSize);
    }

    public async Task<long> CountActivatedDuringPeriodAsync(DashboardFilter filter, CancellationToken cancellationToken = default)
    {
        return await WithRetryAsync("CountActivatedDuringPeriodAsync", async ct =>
        {
            _connections.EnsureConfigured();
            filter = filter.Normalize(_settings.PageSize);
            await using var conn = await _connections.CreateOpenConnectionAsync(ct);
            await using var cmd = OracleCommandFactory.Create(conn, OracleSql.CountActivatedDuringPeriod, _settings);
            OracleCommandFactory.BindProviderAndFilters(cmd, _settings, filter, includeDates: true, includePagination: false, exclusionPatterns: _exclusionPatterns.GetActiveContainsPatterns());
            return await OracleCommandFactory.ExecuteCountAsync(cmd, ct);
        }, cancellationToken);
    }

    public async Task<PagedResult<ActivatedDuringPeriodDto>> GetActivatedDuringPeriodAsync(DashboardFilter filter, CancellationToken cancellationToken = default)
    {
        filter = filter.Normalize(_settings.PageSize);
        _connections.EnsureConfigured();
        var total = await CountActivatedDuringPeriodAsync(filter, cancellationToken);
        await using var conn = await _connections.CreateOpenConnectionAsync(cancellationToken);
        await using var cmd = OracleCommandFactory.Create(conn, OracleSql.SelectActivatedDuringPeriod, _settings);
        OracleCommandFactory.BindProviderAndFilters(cmd, _settings, filter, includeDates: true, includePagination: true, exclusionPatterns: _exclusionPatterns.GetActiveContainsPatterns());

        var items = new List<ActivatedDuringPeriodDto>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var code = OracleCommandFactory.GetString(reader, "STATUS_CODE");
            items.Add(new ActivatedDuringPeriodDto(
                OracleCommandFactory.GetInt64(reader, "ACCOUNT_ID") ?? 0,
                OracleCommandFactory.GetString(reader, "ACCOUNT_CODE"),
                OracleCommandFactory.GetString(reader, "ACCOUNT_NAME"),
                code,
                null,
                OracleCommandFactory.GetString(reader, "CUSTOMER"),
                OracleCommandFactory.GetString(reader, "REGION"),
                OracleCommandFactory.GetString(reader, "EMAIL"),
                OracleCommandFactory.GetDate(reader, "ACTIVATION_DATE") ?? DateTime.MinValue,
                OracleCommandFactory.GetString(reader, "PRODUCT"),
                OracleCommandFactory.GetString(reader, "SERVICE_CODE"),
                OracleCommandFactory.GetString(reader, "SERVICE_TYPE")));
        }

        return new PagedResult<ActivatedDuringPeriodDto>(items, (int)total, filter.Page, filter.PageSize);
    }

    public async Task<long> CountOpeningActiveLinesAsync(DashboardFilter filter, CancellationToken cancellationToken = default)
    {
        return await WithRetryAsync("CountOpeningActiveLinesAsync", async ct =>
        {
            _connections.EnsureConfigured();
            filter = filter.Normalize(_settings.PageSize);
            await using var conn = await _connections.CreateOpenConnectionAsync(ct);
            await using var cmd = OracleCommandFactory.Create(conn, OracleSql.CountOpeningActiveLines, _settings);
            OracleCommandFactory.AddProvider(cmd, _settings, filter);
            OracleCommandFactory.Add(cmd, "p_active_status", StatusCodes.Active, DbType.String);
            OracleCommandFactory.Add(cmd, "p_from", filter.FromDate!.Value.ToDateTime(TimeOnly.MinValue), DbType.Date);
            OracleCommandFactory.Add(cmd, "p_region", filter.Region, DbType.String);
            OracleCommandFactory.Add(cmd, "p_product", filter.Product, DbType.String);
            OracleCommandFactory.Add(cmd, "p_exclude_product", OracleSql.ExcludedProductCode, DbType.String);
            OracleCommandFactory.Add(cmd, "p_service_type", filter.ServiceType, DbType.String);
            OracleCommandFactory.ApplyExclusionSql(cmd, filter, _exclusionPatterns.GetActiveContainsPatterns());
            return await OracleCommandFactory.ExecuteCountAsync(cmd, ct);
        }, cancellationToken);
    }
}

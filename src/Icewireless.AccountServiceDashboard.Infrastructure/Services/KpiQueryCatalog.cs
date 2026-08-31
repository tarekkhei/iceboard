using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Icewireless.AccountServiceDashboard.Application.Configuration;
using Icewireless.AccountServiceDashboard.Application.DTOs;
using Icewireless.AccountServiceDashboard.Application.Interfaces;
using Icewireless.AccountServiceDashboard.Domain.Enums;
using Icewireless.AccountServiceDashboard.Infrastructure.Oracle;
using Microsoft.Extensions.Options;

namespace Icewireless.AccountServiceDashboard.Infrastructure.Services;

/// <summary>
/// Exposes the live Oracle KPI SQL used by the dashboard for operator inspection.
/// Display SQL substitutes current UI filter values into bind placeholders.
/// </summary>
public sealed class KpiQueryCatalog : IKpiQueryCatalog
{
    private readonly DashboardSettings _settings;

    public KpiQueryCatalog(IOptions<DashboardSettings> settings)
    {
        _settings = settings.Value;
    }

    public IReadOnlyDictionary<string, KpiQueryDto> GetDashboardKpiQueries(DashboardFilter filter)
    {
        var statusSql = Normalize(OracleSql.CountStatusEvents);
        var suspendedSql = Normalize(OracleSql.CountSuspendedAccounts);
        var pendingSql = Normalize(OracleSql.CountPendingCancellationAccounts);
        var cancellationSql = Normalize(OracleSql.CountCancellationAccounts);
        var permanentClosedSql = Normalize(OracleSql.CountPermanentClosedAccounts);
        var newActive = Normalize(OracleSql.CountNewActiveLines);
        var current = Normalize(OracleSql.CountCurrentActiveLines);
        var opening = Normalize(OracleSql.CountOpeningActiveLines);

        return new Dictionary<string, KpiQueryDto>(StringComparer.OrdinalIgnoreCase)
        {
            ["new-active"] = Create(
                "new-active",
                "New Active Lines",
                newActive,
                filter,
                statusOverride: null,
                activeStatus: null,
                "Date field: ACCOUNTS.REGISTRATIONDATE. Metric: COUNT(DISTINCT a.id). Never uses ACTIVATIONDATE / FIRSTACTIVATIONDATE."),
            ["current-active"] = Create(
                "current-active",
                "Current Active Lines",
                current,
                filter,
                statusOverride: null,
                activeStatus: StatusCodes.Active,
                "Account-level current snapshot. FROM accounts a · COUNT(DISTINCT a.id) · activationdate IS NOT NULL AND ownstatus = 'A'. No dashboard registration-date range. Excludes S/C/T/N/O/R. Does not count services."),
            ["suspended"] = Create(
                "suspended",
                "Suspended Events",
                suspendedSql,
                filter,
                statusOverride: StatusCodes.Suspended,
                activeStatus: null,
                "Unique accounts that entered Suspended (S) in the selected period. FROM accounts a · COUNT(DISTINCT a.id) · ACCTSTATUS status='S' with FROMDATE as the event date (fromdate >= :p_from AND fromdate < :p_to_exclusive). TODATE is status end only. Not a status-row count."),
            ["pending-cancellation"] = Create(
                "pending-cancellation",
                "Pending Cancellation",
                pendingSql,
                filter,
                statusOverride: StatusCodes.PendingClose,
                activeStatus: null,
                "Unique accounts that entered Pending to Close (C) in the selected period. FROM accounts a · COUNT(DISTINCT a.id) · ACCTSTATUS status='C' with FROMDATE as the event date (fromdate >= :p_from AND fromdate < :p_to_exclusive). TODATE is status end only. Not a status-row or service count."),
            ["cancellation"] = Create(
                "cancellation",
                "Cancellation Events",
                cancellationSql,
                filter,
                statusOverride: StatusCodes.PermanentClosed,
                activeStatus: null,
                "Unique accounts that entered Permanently Closed / Completed Cancellation (T) in the selected period. FROM accounts a · COUNT(DISTINCT a.id) · ACCTSTATUS status='T' with FROMDATE as the event date (fromdate >= :p_from AND fromdate < :p_to_exclusive). TODATE is status end only. Not a status-row or service count."),
            ["permanent-closed"] = Create(
                "permanent-closed",
                "Permanent Closed",
                permanentClosedSql,
                filter,
                statusOverride: StatusCodes.PermanentClosed,
                activeStatus: null,
                "Unique accounts that entered Permanently Closed (T) in the selected period. FROM accounts a · COUNT(DISTINCT a.id) · ACCTSTATUS status='T' with FROMDATE as the event date (fromdate >= :p_from AND fromdate < :p_to_exclusive). TODATE is status end only. Same definition as Cancellation Events. Not a status-row or service count."),
            ["net-growth"] = Create(
                "net-growth",
                "Net Growth",
                JoinSections(
                    "-- Net Growth = New Active Lines − Cancellation Events",
                    "-- [1] New Active Lines",
                    newActive,
                    "-- [2] Cancellation Events (unique T-status accounts, FROMDATE entry)",
                    cancellationSql),
                filter,
                statusOverride: StatusCodes.PermanentClosed,
                activeStatus: null,
                "Formula: New Active Lines − Cancellation Events."),
            ["churn"] = Create(
                "churn",
                "Churn Rate",
                JoinSections(
                    "-- Churn Rate = Cancellation Events / Opening Active Lines * 100",
                    "-- [1] Cancellation Events (numerator — unique T-status accounts, FROMDATE entry)",
                    cancellationSql,
                    "-- [2] Opening Active Lines (denominator)",
                    opening),
                filter,
                statusOverride: StatusCodes.PermanentClosed,
                activeStatus: StatusCodes.Active,
                "Formula: Cancellation Events ÷ Opening Active Lines × 100. Opening uses Active status covering period start.")
        };
    }

    private KpiQueryDto Create(
        string key,
        string title,
        string sql,
        DashboardFilter filter,
        string? statusOverride,
        string? activeStatus,
        string notes)
    {
        var parameters = BuildParameters(sql, filter, statusOverride, activeStatus);
        var header = BuildBindParameterHeader(parameters);
        var displaySql = Substitute(sql, parameters);
        return new KpiQueryDto(key, title, header + "\n\n" + displaySql, notes, parameters);
    }

    private List<KpiQueryParameterDto> BuildParameters(
        string sql,
        DashboardFilter filter,
        string? statusOverride,
        string? activeStatus)
    {
        var parameters = new List<KpiQueryParameterDto>
        {
            new("p_provider", Quote(_settings.ResolveProvider(filter.ProviderCode))),
            new("p_from", ToOracleDate(filter.FromDate)),
            new("p_to", ToOracleDate(filter.ToDate)),
            new("p_to_exclusive", ToOracleDate(filter.ToDate?.AddDays(1))),
            new("p_region", ToSqlLiteral(filter.Region)),
            new("p_product", ToSqlLiteral(filter.Product)),
            new("p_exclude_product", Quote(OracleSql.ExcludedProductCode)),
            new("p_service_type", ToSqlLiteral(filter.ServiceType)),
            new("p_search", ToSqlLiteral(filter.Search)),
            new("p_include_test_accounts", filter.IncludeTestAccounts ? "1" : "0"),
            new("ui_status", string.IsNullOrWhiteSpace(filter.Status) ? "NULL" : Quote(filter.Status.Trim())),
        };

        if (sql.Contains(":p_status", StringComparison.OrdinalIgnoreCase))
        {
            parameters.Add(new(
                "p_status",
                Quote(statusOverride ?? filter.Status ?? StatusCodes.PendingClose)));
        }

        if (sql.Contains(":p_active_status", StringComparison.OrdinalIgnoreCase))
        {
            parameters.Add(new("p_active_status", Quote(activeStatus ?? StatusCodes.Active)));
        }

        return parameters;
    }

    private static string BuildBindParameterHeader(IReadOnlyList<KpiQueryParameterDto> parameters)
    {
        var sb = new StringBuilder();
        sb.AppendLine("-- Bind parameters (from current UI filters)");
        foreach (var p in parameters)
        {
            var label = p.Name == "ui_status" ? "UI Status" : ":" + p.Name;
            sb.AppendLine($"--   {label,-16} = {p.Value}");
        }

        return sb.ToString().TrimEnd();
    }

    private static string Substitute(string sql, IReadOnlyList<KpiQueryParameterDto> parameters)
    {
        foreach (var p in parameters
                     .Where(p => p.Name != "ui_status")
                     .OrderByDescending(p => p.Name.Length))
        {
            var token = ":" + p.Name;
            var pattern = Regex.Escape(token) + @"(?![A-Za-z0-9_])";
            sql = Regex.Replace(sql, pattern, p.Value, RegexOptions.IgnoreCase);
        }

        return sql;
    }

    private static string JoinSections(string heading, string section1Title, string section1, string section2Title, string section2)
    {
        var sb = new StringBuilder();
        sb.AppendLine(heading);
        sb.AppendLine();
        sb.AppendLine(section1Title);
        sb.AppendLine(section1);
        sb.AppendLine();
        sb.AppendLine(section2Title);
        sb.Append(section2);
        return sb.ToString();
    }

    private static string ToOracleDate(DateOnly? value) =>
        value is null
            ? "NULL"
            : $"DATE '{value.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}'";

    private static string ToSqlLiteral(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "NULL" : Quote(value.Trim());

    private static string Quote(string value) =>
        "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    private static string Normalize(string sql) =>
        string.Join('\n', sql.Replace("\r\n", "\n").Split('\n').Select(l => l.TrimEnd())).Trim();
}

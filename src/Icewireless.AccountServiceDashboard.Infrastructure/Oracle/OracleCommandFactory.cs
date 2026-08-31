using System.Data;
using System.Data.Common;
using Icewireless.AccountServiceDashboard.Application.Configuration;
using Icewireless.AccountServiceDashboard.Application.DTOs;
using Oracle.ManagedDataAccess.Client;

namespace Icewireless.AccountServiceDashboard.Infrastructure.Oracle;

internal static class OracleCommandFactory
{
    public static OracleCommand Create(DbConnection connection, string sql, DashboardSettings settings)
    {
        return new OracleCommand(sql, (OracleConnection)connection)
        {
            BindByName = true,
            CommandTimeout = settings.CommandTimeoutSeconds
        };
    }

    public static void Add(OracleCommand command, string name, object? value, DbType dbType)
    {
        var p = command.CreateParameter();
        p.ParameterName = name;
        p.DbType = dbType;
        p.Value = value ?? DBNull.Value;
        command.Parameters.Add(p);
    }

    public static void AddProvider(OracleCommand command, DashboardSettings settings, DashboardFilter filter)
    {
        Add(command, "p_provider", settings.ResolveProvider(filter.ProviderCode), DbType.String);
    }

    public static void BindProviderAndFilters(
        OracleCommand command,
        DashboardSettings settings,
        DashboardFilter filter,
        bool includeDates,
        bool includePagination,
        string? statusCode = null,
        string? activeStatus = null,
        IReadOnlyList<string>? exclusionPatterns = null)
    {
        ApplyExclusionSql(command, filter, exclusionPatterns ?? []);

        AddProvider(command, settings, filter);
        if (includeDates)
        {
            Add(command, "p_from", filter.FromDate!.Value.ToDateTime(TimeOnly.MinValue), DbType.Date);
            if (command.CommandText.Contains(":p_to_exclusive", StringComparison.OrdinalIgnoreCase))
            {
                Add(command, "p_to_exclusive", filter.ToDate!.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), DbType.Date);
            }

            if (command.CommandText.Contains(":p_to", StringComparison.OrdinalIgnoreCase))
            {
                var needsPTo = System.Text.RegularExpressions.Regex.IsMatch(
                    command.CommandText,
                    @":p_to(?![A-Za-z0-9_])",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (needsPTo)
                {
                    Add(command, "p_to", filter.ToDate!.Value.ToDateTime(TimeOnly.MinValue), DbType.Date);
                }
            }
        }

        Add(command, "p_region", filter.Region, DbType.String);
        Add(command, "p_product", filter.Product, DbType.String);
        Add(command, "p_exclude_product", OracleSql.ExcludedProductCode, DbType.String);
        Add(command, "p_service_type", filter.ServiceType, DbType.String);
        Add(command, "p_search", filter.Search, DbType.String);

        if (statusCode is not null)
        {
            Add(command, "p_status", statusCode, DbType.String);
        }
        else if (command.CommandText.Contains(":p_status", StringComparison.Ordinal))
        {
            Add(command, "p_status", filter.Status, DbType.String);
        }

        if (activeStatus is not null)
        {
            Add(command, "p_active_status", activeStatus, DbType.String);
        }

        if (includePagination)
        {
            Add(command, "p_offset", (filter.Page - 1) * filter.PageSize, DbType.Int32);
            Add(command, "p_page_size", filter.PageSize, DbType.Int32);
        }
    }

    /// <summary>
    /// Expands JSON-driven exclusion markers and binds include-test + pattern parameters.
    /// </summary>
    public static void ApplyExclusionSql(
        OracleCommand command,
        DashboardFilter filter,
        IReadOnlyList<string> exclusionPatterns)
    {
        var effective = filter.IncludeTestAccounts
            ? Array.Empty<string>()
            : exclusionPatterns;

        if (command.CommandText.Contains(AccountExclusionQueryBuilder.Marker, StringComparison.Ordinal))
        {
            command.CommandText = AccountExclusionQueryBuilder.Expand(command.CommandText, effective);
        }

        if (command.CommandText.Contains(":" + AccountExclusionQueryBuilder.ParameterName, StringComparison.OrdinalIgnoreCase))
        {
            Add(command, AccountExclusionQueryBuilder.ParameterName, filter.IncludeTestAccounts ? 1 : 0, DbType.Int32);
        }

        BindExclusionPatterns(command, effective);
    }

    public static void BindExclusionPatterns(OracleCommand command, IReadOnlyList<string> patterns)
    {
        for (var i = 0; i < patterns.Count; i++)
        {
            var name = AccountExclusionQueryBuilder.PatternParameterPrefix + i;
            if (!HasBindPlaceholder(command.CommandText, name))
                continue;
            Add(command, name, patterns[i], DbType.String);
        }
    }

    private static bool HasBindPlaceholder(string sql, string name)
    {
        var token = ":" + name;
        var idx = 0;
        while ((idx = sql.IndexOf(token, idx, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            var end = idx + token.Length;
            if (end >= sql.Length || (!char.IsLetterOrDigit(sql[end]) && sql[end] != '_'))
                return true;
            idx = end;
        }

        return false;
    }

    public static async Task<long> ExecuteCountAsync(OracleCommand command, CancellationToken ct)
    {
        var result = await command.ExecuteScalarAsync(ct);
        return result is null or DBNull ? 0 : Convert.ToInt64(result);
    }

    public static string? GetString(DbDataReader reader, string name)
    {
        var i = reader.GetOrdinal(name);
        return reader.IsDBNull(i) ? null : Convert.ToString(reader.GetValue(i));
    }

    public static DateTime? GetDate(DbDataReader reader, string name)
    {
        var i = reader.GetOrdinal(name);
        return reader.IsDBNull(i) ? null : reader.GetDateTime(i);
    }

    public static long? GetInt64(DbDataReader reader, string name)
    {
        var i = reader.GetOrdinal(name);
        return reader.IsDBNull(i) ? null : Convert.ToInt64(reader.GetValue(i));
    }

    public static decimal? GetDecimal(DbDataReader reader, string name)
    {
        var i = reader.GetOrdinal(name);
        return reader.IsDBNull(i) ? null : Convert.ToDecimal(reader.GetValue(i));
    }
}

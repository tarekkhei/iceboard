using System.Text;
using Icewireless.AccountServiceDashboard.Domain.Enums;

namespace Icewireless.AccountServiceDashboard.Infrastructure.Oracle;

/// <summary>
/// Centralized account exclusion SQL. Active CONTAINS patterns come from the JSON rules file
/// (not an Oracle table, not hardcoded in queries).
/// Bind :p_include_test_accounts = 1 to bypass; = 0 to apply patterns against accounts.accountname.
/// </summary>
public static class AccountExclusionQueryBuilder
{
    public const string ParameterName = "p_include_test_accounts";
    public const string Marker = "/*__ACCOUNT_EXCLUSION__*/";
    public const string PatternParameterPrefix = "p_excl_";

    /// <summary>
    /// Inserts a marker so runtime Expand can inject the current JSON patterns.
    /// </summary>
    public static string Apply(string sql, string accountAlias = "a")
    {
        if (string.IsNullOrWhiteSpace(sql))
            return sql;

        if (sql.Contains(Marker, StringComparison.Ordinal) ||
            sql.Contains(":" + ParameterName, StringComparison.OrdinalIgnoreCase))
            return sql;

        // Alias is recorded in the marker for Expand.
        var placeholder = $"\n  {Marker}:{accountAlias}\n";
        var insertAt = FindInsertIndex(sql);
        return insertAt >= 0
            ? sql.Insert(insertAt, placeholder)
            : sql.TrimEnd() + placeholder;
    }

    /// <summary>
    /// Replaces exclusion markers with a bind-parameterized CONTAINS predicate from JSON patterns.
    /// </summary>
    public static string Expand(string sql, IReadOnlyList<string> activeContainsPatterns)
    {
        if (string.IsNullOrWhiteSpace(sql) || !sql.Contains(Marker, StringComparison.Ordinal))
            return sql;

        // Support one or more markers with optional :alias suffix.
        var result = sql;
        while (true)
        {
            var start = result.IndexOf(Marker, StringComparison.Ordinal);
            if (start < 0)
                break;

            var alias = "a";
            var end = start + Marker.Length;
            if (end < result.Length && result[end] == ':')
            {
                var aliasStart = end + 1;
                var aliasEnd = aliasStart;
                while (aliasEnd < result.Length && (char.IsLetterOrDigit(result[aliasEnd]) || result[aliasEnd] == '_'))
                    aliasEnd++;
                alias = result[aliasStart..aliasEnd];
                end = aliasEnd;
            }

            var predicate = BuildPredicate(activeContainsPatterns, alias);
            result = result[..start] + predicate + result[end..];
        }

        return result;
    }

    public static string BuildPredicate(IReadOnlyList<string> activeContainsPatterns, string accountAlias = "a")
    {
        var patterns = activeContainsPatterns
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (patterns.Count == 0)
        {
            // No active rules — never exclude.
            return $@"
  AND (NVL(:{ParameterName}, 0) = 1 OR 1 = 1)";
        }

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("  AND (");
        sb.AppendLine($"        NVL(:{ParameterName}, 0) = 1");
        sb.AppendLine("        OR NOT (");
        for (var i = 0; i < patterns.Count; i++)
        {
            if (i > 0)
                sb.AppendLine("             OR");
            sb.AppendLine($"            {PatternMatchClause(PatternParameterPrefix + i, accountAlias)}");
        }

        sb.AppendLine("        )");
        sb.Append("      )");
        return sb.ToString();
    }

    public static string BuildExcludedAccountsCountSql(IReadOnlyList<string> activeContainsPatterns)
    {
        var patterns = activeContainsPatterns
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (patterns.Count == 0)
            return "SELECT 0 AS CNT FROM dual";

        var likes = string.Join("\n             OR ",
            patterns.Select((_, i) => PatternMatchClause(PatternParameterPrefix + i, "a")));

        return $@"
SELECT COUNT(DISTINCT a.id) AS CNT
FROM accounts a
WHERE a.providercode = :p_provider
  AND (
             {likes}
      )";
    }

    public static IReadOnlyList<string> NormalizeContainsPatterns(IEnumerable<(string Pattern, string MatchType, bool IsActive)> rules) =>
        rules
            .Where(r => r.IsActive)
            .Where(r => ExclusionMatchTypes.IsSupported(r.MatchType) ||
                        string.Equals(r.MatchType, ExclusionMatchTypes.Contains, StringComparison.OrdinalIgnoreCase))
            .Where(r => !string.IsNullOrWhiteSpace(r.Pattern))
            .Select(r => r.Pattern.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// CONTAINS match against account name, account code, and contact (customer) name.
    /// Person-name patterns like "Dharmesh" live on ACCTCONTACTS, not ACCOUNTNAME.
    /// </summary>
    private static string PatternMatchClause(string parameterName, string accountAlias) =>
        $@"(
              UPPER(NVL({accountAlias}.accountname, CHR(0))) LIKE '%' || UPPER(:{parameterName}) || '%'
              OR UPPER(NVL({accountAlias}.code, CHR(0))) LIKE '%' || UPPER(:{parameterName}) || '%'
              OR EXISTS (
                  SELECT 1
                  FROM acctcontacts cx
                  WHERE cx.accountid = {accountAlias}.id
                    AND UPPER(TRIM(NVL(cx.firstname, '') || ' ' || NVL(cx.middlename, '') || ' ' || NVL(cx.lastname, '')))
                        LIKE '%' || UPPER(:{parameterName}) || '%'
              )
            )";

    private static int FindInsertIndex(string sql)
    {
        var ranked = IndexOfLineStart(sql, ") ranked");
        if (ranked >= 0)
            return ranked;

        var groupBy = IndexOfLineStart(sql, "GROUP BY");
        if (groupBy >= 0)
            return groupBy;

        var orderBy = IndexOfLineStart(sql, "ORDER BY");
        if (orderBy >= 0)
            return orderBy;

        return -1;
    }

    private static int IndexOfLineStart(string sql, string token)
    {
        var idx = sql.LastIndexOf(token, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return -1;

        while (idx > 0 && sql[idx - 1] is ' ' or '\t')
            idx--;

        return idx;
    }
}

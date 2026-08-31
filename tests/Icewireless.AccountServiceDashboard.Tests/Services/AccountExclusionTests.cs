using Icewireless.AccountServiceDashboard.Application.DTOs;
using Icewireless.AccountServiceDashboard.Domain.Enums;
using Icewireless.AccountServiceDashboard.Infrastructure.Oracle;

namespace Icewireless.AccountServiceDashboard.Tests.Services;

public class AccountExclusionQueryBuilderTests
{
    [Fact]
    public void Predicate_UsesBoundPatterns_NotOracleTable()
    {
        var sql = AccountExclusionQueryBuilder.BuildPredicate(["TEST", "DEMO"]);
        Assert.Contains(":p_include_test_accounts", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(":p_excl_0", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(":p_excl_1", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("a.accountname", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("account_exclusion_rules", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NOT EXISTS", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Apply_InsertsMarker_AndExpandIsIdempotentOnMarker()
    {
        var raw = @"
FROM accounts a
WHERE a.id = 1
    ) ranked
ORDER BY 1";
        var marked = AccountExclusionQueryBuilder.Apply(raw);
        Assert.Contains(AccountExclusionQueryBuilder.Marker, marked, StringComparison.Ordinal);
        var once = AccountExclusionQueryBuilder.Expand(marked, ["TEST"]);
        var twice = AccountExclusionQueryBuilder.Expand(once, ["TEST"]);
        Assert.DoesNotContain(AccountExclusionQueryBuilder.Marker, once, StringComparison.Ordinal);
        Assert.Equal(once, twice);
        Assert.Contains(":p_excl_0", once, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Expand_WithNoPatterns_DoesNotExclude()
    {
        var marked = AccountExclusionQueryBuilder.Apply("FROM accounts a WHERE 1=1");
        var sql = AccountExclusionQueryBuilder.Expand(marked, []);
        Assert.Contains("OR 1 = 1", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(":p_excl_0", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Expand_InsertsBeforeGroupBy()
    {
        var marked = AccountExclusionQueryBuilder.Apply(@"
FROM accounts a
WHERE a.providercode = :p_provider
GROUP BY a.id
ORDER BY 1");
        var sql = AccountExclusionQueryBuilder.Expand(marked, ["QA"]);
        Assert.True(sql.IndexOf(":p_excl_0", StringComparison.OrdinalIgnoreCase)
                    < sql.IndexOf("GROUP BY", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DashboardFilter_IncludeTestAccounts_DefaultsFalse(bool include)
    {
        var filter = new DashboardFilter { IncludeTestAccounts = include }.Normalize(50);
        Assert.Equal(include, filter.IncludeTestAccounts);
    }

    [Fact]
    public void MatchType_OnlyContainsSupportedInitially()
    {
        Assert.True(ExclusionMatchTypes.IsSupported("CONTAINS"));
        Assert.True(ExclusionMatchTypes.IsSupported("contains"));
        Assert.False(ExclusionMatchTypes.IsSupported("REGEX"));
        Assert.False(ExclusionMatchTypes.IsSupported("STARTS_WITH"));
    }

    [Fact]
    public void DisabledRules_AreIgnoredByNormalizer()
    {
        var patterns = AccountExclusionQueryBuilder.NormalizeContainsPatterns(
        [
            ("TEST", "CONTAINS", true),
            ("DEMO", "CONTAINS", false),
            ("QA", "CONTAINS", true)
        ]);
        Assert.Equal(2, patterns.Count);
        Assert.Contains("TEST", patterns);
        Assert.Contains("QA", patterns);
        Assert.DoesNotContain("DEMO", patterns);
    }
}

public class AccountExclusionSqlIntegrationTests
{
    [Fact]
    public void AllAccountKpis_IncludeCentralizedExclusionMarker()
    {
        string[] sqls =
        [
            OracleSql.CountNewActiveLines,
            OracleSql.CountCurrentActiveLines,
            OracleSql.CountActivatedDuringPeriod,
            OracleSql.CountOpeningActiveLines,
            OracleSql.CountStatusEvents,
            OracleSql.CountSuspendedAccounts,
            OracleSql.CountPendingCancellationAccounts,
            OracleSql.CountCancellationAccounts,
            OracleSql.CountPermanentClosedAccounts,
            OracleSql.CompletedCancellationsTrend,
            OracleSql.SuspendedEventsTrend,
            OracleSql.ActivationTrend,
            OracleSql.CancellationTrend,
            OracleSql.StatusDistribution,
            OracleSql.RegionAnalysis,
            OracleSql.ProductAnalysis,
            OracleSql.SelectNewActiveLines,
            OracleSql.SelectCurrentActiveLines
        ];

        foreach (var sql in sqls)
        {
            Assert.Contains(AccountExclusionQueryBuilder.Marker, sql, StringComparison.Ordinal);
            Assert.DoesNotContain("account_exclusion_rules", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void FilterOptionQueries_DoNotApplyExclusion()
    {
        Assert.DoesNotContain(AccountExclusionQueryBuilder.Marker, OracleSql.FilterRegions, StringComparison.Ordinal);
        Assert.DoesNotContain(AccountExclusionQueryBuilder.Marker, OracleSql.FilterProducts, StringComparison.Ordinal);
        Assert.DoesNotContain(AccountExclusionQueryBuilder.Marker, OracleSql.FilterServiceTypes, StringComparison.Ordinal);
    }

    [Fact]
    public void IncludeFlag_BypassesViaOrShortCircuit()
    {
        var predicate = AccountExclusionQueryBuilder.BuildPredicate(["TEST"]);
        Assert.Contains("NVL(:p_include_test_accounts, 0) = 1", predicate, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OR NOT (", predicate, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MultipleRules_CombineWithOrInsideNot()
    {
        var predicate = AccountExclusionQueryBuilder.BuildPredicate(["TEST", "DEMO", "QA"]);
        Assert.Contains(":p_excl_0", predicate, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(":p_excl_1", predicate, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(":p_excl_2", predicate, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PatternMatch_IncludesAccountNameCodeAndContact()
    {
        var predicate = AccountExclusionQueryBuilder.BuildPredicate(["Dharmesh"]);
        Assert.Contains("accountname", predicate, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(".code", predicate, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("acctcontacts", predicate, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("firstname", predicate, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("lastname", predicate, StringComparison.OrdinalIgnoreCase);
    }
}

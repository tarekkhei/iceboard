using Icewireless.AccountServiceDashboard.Domain.Enums;
using Icewireless.AccountServiceDashboard.Infrastructure.Oracle;

namespace Icewireless.AccountServiceDashboard.Tests.Services;

/// <summary>
/// Business-rule proofs for Permanent Closed (status T, FROMDATE-entry KPI).
/// </summary>
public class PermanentClosedBusinessRuleTests
{
    [Fact]
    public void Count_UsesDistinctAccountId_NotStatusOrServiceCombinations()
    {
        var sql = OracleSql.CountPermanentClosedAccounts;
        Assert.Contains("COUNT(DISTINCT a.id)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("COUNT(DISTINCT acs.id)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("|| '|'", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("YYYYMMDDHH24MISS", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Count_FoundationIsAccounts_NotAcctStatus()
    {
        var normalized = OracleSql.CountPermanentClosedAccounts.Replace("\r\n", "\n");
        Assert.Contains("FROM accounts a", normalized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INNER JOIN acctstatus", normalized, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("EXISTS", normalized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Count_UsesPermanentClosedStatusFromAcctStatus()
    {
        Assert.Contains("ast.status = :p_status", OracleSql.PermanentClosedAccountsBaseFrom, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FROM acctstatus ast", OracleSql.PermanentClosedAccountsBaseFrom, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(StatusCodes.PermanentClosed, "T");
    }

    [Fact]
    public void Count_UsesFromDateEntry_NotInclusiveOverlap()
    {
        var sql = OracleSql.PermanentClosedAccountsBaseFrom;
        Assert.Contains("ast.fromdate >= :p_from", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ast.fromdate < :p_to_exclusive", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ast.todate IS NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ast.todate >= :p_from", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ast.fromdate <= :p_to", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Filters_UseExists_ToAvoidFanOut()
    {
        var sql = OracleSql.PermanentClosedAccountsBaseFrom;
        Assert.Contains("FROM acctcontacts", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FROM accountservices", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(":p_region IS NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(":p_product IS NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(":p_service_type IS NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(":p_search IS NULL", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExclusionMarker_IsPresent_AndIncludeFlagBypasses()
    {
        Assert.Contains(AccountExclusionQueryBuilder.Marker, OracleSql.CountPermanentClosedAccounts, StringComparison.Ordinal);
        Assert.Contains(AccountExclusionQueryBuilder.Marker, OracleSql.SelectPermanentClosedAccounts, StringComparison.Ordinal);
        Assert.Contains(AccountExclusionQueryBuilder.Marker, OracleSql.PermanentClosedAccountsTrend, StringComparison.Ordinal);
        var predicate = AccountExclusionQueryBuilder.BuildPredicate(["TEST"]);
        Assert.Contains("NVL(:p_include_test_accounts, 0) = 1", predicate, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DrillDown_OneRowPerAccount_MostRecentFromDateInPeriod()
    {
        var sql = OracleSql.SelectPermanentClosedAccounts;
        Assert.Contains("PARTITION BY ast.accountid", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY ast.fromdate DESC", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("qc.rn = 1", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FROM accounts a", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ast.fromdate >= :p_from", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ast.fromdate < :p_to_exclusive", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ast.todate >= :p_from", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Chart_UsesFromDateEntry_DistinctAccounts()
    {
        var sql = OracleSql.PermanentClosedAccountsTrend;
        Assert.Contains("COUNT(DISTINCT ast.accountid)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ast.fromdate >= :p_from", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ast.fromdate < :p_to_exclusive", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ast.todate >= :p_from", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SharesFromDateSqlWithCancellationEvents()
    {
        Assert.Same(OracleSql.CountCancellationAccounts, OracleSql.CountPermanentClosedAccounts);
        Assert.Equal(OracleSql.CancellationAccountsBaseFrom, OracleSql.PermanentClosedAccountsBaseFrom);
        Assert.Equal(StatusCodes.PendingClose, "C");
        Assert.Equal(StatusCodes.PermanentClosed, "T");
        Assert.Contains("COUNT(DISTINCT a.id)", OracleSql.CountPendingCancellationAccounts, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NewActiveAndCurrentActive_RemainUnchangedPatterns()
    {
        Assert.Contains("a.registrationdate", OracleSql.CountNewActiveLines, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ACTIVATIONDATE", OracleSql.CountNewActiveLines, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("a.ownstatus = :p_active_status", OracleSql.CurrentActiveBaseFrom, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(":p_from", OracleSql.CurrentActiveBaseFrom, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExcludedProduct_IsBoundInPermanentClosedQueries()
    {
        Assert.Contains(":p_exclude_product", OracleSql.CountPermanentClosedAccounts, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("ICENP_MASTER", OracleSql.ExcludedProductCode);
    }
}

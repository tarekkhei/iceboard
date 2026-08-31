using Icewireless.AccountServiceDashboard.Infrastructure.Oracle;

namespace Icewireless.AccountServiceDashboard.Tests.Services;

/// <summary>
/// Business-rule proofs for Pending Cancellation FROMDATE-entry KPI.
/// </summary>
public class PendingCancellationBusinessRuleTests
{
    [Fact]
    public void Count_UsesDistinctAccountId_NotStatusOrServiceCombinations()
    {
        var sql = OracleSql.CountPendingCancellationAccounts;
        Assert.Contains("COUNT(DISTINCT a.id)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("COUNT(DISTINCT acs.id)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("|| '|'", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("YYYYMMDDHH24MISS", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Count_FoundationIsAccounts_NotAcctStatus()
    {
        var normalized = OracleSql.CountPendingCancellationAccounts.Replace("\r\n", "\n");
        Assert.Contains("FROM accounts a", normalized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INNER JOIN acctstatus", normalized, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("EXISTS", normalized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Count_UsesPendingCloseStatusFromAcctStatus()
    {
        Assert.Contains("ast.status = :p_status", OracleSql.PendingCancellationAccountsBaseFrom, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FROM acctstatus ast", OracleSql.PendingCancellationAccountsBaseFrom, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Count_UsesFromDateEntry_NotInclusiveOverlap()
    {
        var sql = OracleSql.PendingCancellationAccountsBaseFrom;
        Assert.Contains("ast.fromdate >= :p_from", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ast.fromdate < :p_to_exclusive", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ast.todate IS NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ast.todate >= :p_from", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ast.fromdate <= :p_to", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Filters_UseExists_ToAvoidFanOut()
    {
        var sql = OracleSql.PendingCancellationAccountsBaseFrom;
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
        Assert.Contains(AccountExclusionQueryBuilder.Marker, OracleSql.CountPendingCancellationAccounts, StringComparison.Ordinal);
        var predicate = AccountExclusionQueryBuilder.BuildPredicate(["TEST"]);
        Assert.Contains("NVL(:p_include_test_accounts, 0) = 1", predicate, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OR NOT (", predicate, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DrillDown_OneRowPerAccount_MostRecentFromDateInPeriod()
    {
        var sql = OracleSql.SelectPendingCancellationAccounts;
        Assert.Contains("PARTITION BY ast.accountid", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY ast.fromdate DESC", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("qpc.rn = 1", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FROM accounts a", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ast.fromdate >= :p_from", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ast.fromdate < :p_to_exclusive", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ast.todate >= :p_from", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Chart_UsesFromDateEntry_DistinctAccounts()
    {
        var sql = OracleSql.NewPendingCancellationsTrend;
        Assert.Contains("COUNT(DISTINCT ast.accountid)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ast.fromdate >= :p_from", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ast.fromdate < :p_to_exclusive", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ast.todate >= :p_from", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SuspendedPendingCancellationAndPermanentClosed_ShareFromDateEntry()
    {
        Assert.Contains("COUNT(DISTINCT a.id)", OracleSql.CountSuspendedAccounts, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ast.fromdate >= :p_from", OracleSql.SuspendedAccountsBaseFrom, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ast.fromdate < :p_to_exclusive", OracleSql.SuspendedAccountsBaseFrom, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ast.todate >= :p_from", OracleSql.SuspendedAccountsBaseFrom, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(OracleSql.SuspendedAccountsBaseFrom, OracleSql.PendingCancellationAccountsBaseFrom);
        Assert.Equal(OracleSql.SuspendedAccountsBaseFrom, OracleSql.CancellationAccountsBaseFrom);
        Assert.Equal(OracleSql.CancellationAccountsBaseFrom, OracleSql.PermanentClosedAccountsBaseFrom);
    }
}

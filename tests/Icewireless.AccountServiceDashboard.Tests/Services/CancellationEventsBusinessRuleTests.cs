using Icewireless.AccountServiceDashboard.Domain.Enums;
using Icewireless.AccountServiceDashboard.Infrastructure.Oracle;

namespace Icewireless.AccountServiceDashboard.Tests.Services;

/// <summary>
/// Business-rule proofs for Cancellation Events (status T, FROMDATE-entry KPI).
/// </summary>
public class CancellationEventsBusinessRuleTests
{
    [Fact]
    public void Count_UsesDistinctAccountId_NotStatusOrServiceCombinations()
    {
        var sql = OracleSql.CountCancellationAccounts;
        Assert.Contains("COUNT(DISTINCT a.id)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("COUNT(DISTINCT acs.id)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("|| '|'", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("YYYYMMDDHH24MISS", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("COUNT(*)", OracleSql.CancellationAccountsBaseFrom, StringComparison.Ordinal);
    }

    [Fact]
    public void Count_FoundationIsAccounts_NotAcctStatus()
    {
        var normalized = OracleSql.CountCancellationAccounts.Replace("\r\n", "\n");
        Assert.Contains("FROM accounts a", normalized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INNER JOIN acctstatus", normalized, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("EXISTS", normalized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Count_UsesPermanentClosedStatusFromAcctStatus()
    {
        Assert.Contains("ast.status = :p_status", OracleSql.CancellationAccountsBaseFrom, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FROM acctstatus ast", OracleSql.CancellationAccountsBaseFrom, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(StatusCodes.PermanentClosed, "T");
    }

    [Fact]
    public void Count_UsesFromDateEntry_NotInclusiveOverlap()
    {
        var sql = OracleSql.CancellationAccountsBaseFrom;
        Assert.Contains("ast.fromdate >= :p_from", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ast.fromdate < :p_to_exclusive", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ast.todate IS NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ast.todate >= :p_from", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ast.fromdate <= :p_to", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Filters_UseExists_ToAvoidFanOut()
    {
        var sql = OracleSql.CancellationAccountsBaseFrom;
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
        Assert.Contains(AccountExclusionQueryBuilder.Marker, OracleSql.CountCancellationAccounts, StringComparison.Ordinal);
        Assert.Contains(AccountExclusionQueryBuilder.Marker, OracleSql.SelectCancellationAccounts, StringComparison.Ordinal);
        Assert.Contains(AccountExclusionQueryBuilder.Marker, OracleSql.CompletedCancellationsTrend, StringComparison.Ordinal);
        var predicate = AccountExclusionQueryBuilder.BuildPredicate(["TEST"]);
        Assert.Contains("NVL(:p_include_test_accounts, 0) = 1", predicate, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OR NOT (", predicate, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DrillDown_OneRowPerAccount_MostRecentFromDateInPeriod()
    {
        var sql = OracleSql.SelectCancellationAccounts;
        Assert.Contains("PARTITION BY ast.accountid", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY ast.fromdate DESC", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("qc.rn = 1", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FROM accounts a", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ast.fromdate >= :p_from", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ast.fromdate < :p_to_exclusive", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ast.todate >= :p_from", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INNER JOIN accountservices acs ON", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Chart_UsesFromDateEntry_DistinctAccounts()
    {
        var sql = OracleSql.CompletedCancellationsTrend;
        Assert.Contains("COUNT(DISTINCT ast.accountid)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ast.fromdate >= :p_from", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ast.fromdate < :p_to_exclusive", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ast.todate >= :p_from", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PendingCancellation_RemainsStatusC_AndCancellationAndPermanentClosedShareFromDate()
    {
        Assert.Contains("COUNT(DISTINCT a.id)", OracleSql.CountPendingCancellationAccounts, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(OracleSql.SuspendedAccountsBaseFrom, OracleSql.PendingCancellationAccountsBaseFrom);
        Assert.Equal(OracleSql.SuspendedAccountsBaseFrom, OracleSql.CancellationAccountsBaseFrom);
        Assert.Equal(OracleSql.CancellationAccountsBaseFrom, OracleSql.PermanentClosedAccountsBaseFrom);
        Assert.Same(OracleSql.CountCancellationAccounts, OracleSql.CountPermanentClosedAccounts);
        Assert.Equal(StatusCodes.PendingClose, "C");
        Assert.Equal(StatusCodes.PermanentClosed, "T");
        Assert.NotEqual(StatusCodes.PendingClose, StatusCodes.PermanentClosed);
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
    public void ExcludedProduct_IsBoundInCancellationQueries()
    {
        Assert.Contains(":p_exclude_product", OracleSql.CountCancellationAccounts, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("ICENP_MASTER", OracleSql.ExcludedProductCode);
    }
}

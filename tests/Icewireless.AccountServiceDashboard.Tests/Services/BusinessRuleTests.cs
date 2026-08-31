using Icewireless.AccountServiceDashboard.Application.Services;
using Icewireless.AccountServiceDashboard.Infrastructure.Oracle;

namespace Icewireless.AccountServiceDashboard.Tests.Services;

public class MetricDateFieldServiceTests
{
    private readonly MetricDateFieldService _sut = new();

    [Theory]
    [InlineData(MetricDateFieldService.NewActiveLines, "ACCOUNTS.REGISTRATIONDATE")]
    [InlineData("activations", "ACCOUNTS.REGISTRATIONDATE")]
    [InlineData(MetricDateFieldService.CurrentActiveLines, "Snapshot")]
    [InlineData(MetricDateFieldService.ActivatedDuringPeriod, "ACCOUNTS.ACTIVATIONDATE")]
    [InlineData(MetricDateFieldService.StatusEvents, "ACCTSTATUS.FROMDATE")]
    [InlineData("suspended-events", "ACCTSTATUS.FROMDATE")]
    [InlineData("pending-cancellation", "ACCTSTATUS.FROMDATE")]
    [InlineData("cancellation", "ACCTSTATUS.FROMDATE")]
    [InlineData("permanent-closed", "ACCTSTATUS.FROMDATE")]
    [InlineData(MetricDateFieldService.StatusEnd, "ACCTSTATUS.TODATE")]
    public void ResolveDateField_ReturnsMandatoryField(string key, string expected)
    {
        Assert.Equal(expected, _sut.ResolveDateField(key));
    }

    [Fact]
    public void ResolveDateField_Unknown_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _sut.ResolveDateField("unknown-metric"));
    }
}

public class BusinessRuleSqlTests
{
    [Fact]
    public void NewActiveLines_UsesAccountsFoundation_AndRegistrationDate()
    {
        var sql = OracleSql.CountNewActiveLines + OracleSql.SelectNewActiveLines + OracleSql.ActivationTrend + OracleSql.NewActiveBaseFrom;
        Assert.Contains("FROM accounts a", OracleSql.CountNewActiveLines, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("COUNT(DISTINCT a.id)", OracleSql.CountNewActiveLines, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("a.registrationdate", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(":p_to_exclusive", OracleSql.CountNewActiveLines, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("acs.registrationdate", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("COUNT(DISTINCT acs.id)", OracleSql.CountNewActiveLines, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ACTIVATIONDATE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FIRSTACTIVATIONDATE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("a.activationdate", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CurrentActiveLines_UsesAccountSnapshot_NoPeriodDateFilter()
    {
        Assert.Contains("FROM accounts a", OracleSql.CountCurrentActiveLines, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("COUNT(DISTINCT a.id)", OracleSql.CountCurrentActiveLines, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("a.activationdate IS NOT NULL", OracleSql.CurrentActiveBaseFrom, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("a.ownstatus = :p_active_status", OracleSql.CurrentActiveBaseFrom, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("registrationdate >=", OracleSql.CurrentActiveBaseFrom, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("activationdate >=", OracleSql.CurrentActiveBaseFrom, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fromdate >=", OracleSql.CurrentActiveBaseFrom, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(":p_from", OracleSql.CurrentActiveBaseFrom, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(":p_to", OracleSql.CurrentActiveBaseFrom, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("COUNT(DISTINCT acs.id)", OracleSql.CountCurrentActiveLines, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INNER JOIN accountservices", OracleSql.CountCurrentActiveLines, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ActivatedDuringPeriod_UsesActivationDateRange_OnAccounts()
    {
        Assert.Contains("FROM accounts a", OracleSql.CountActivatedDuringPeriod, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("COUNT(DISTINCT a.id)", OracleSql.CountActivatedDuringPeriod, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("a.activationdate >= :p_from", OracleSql.ActivatedDuringPeriodBaseFrom, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("a.activationdate < :p_to_exclusive", OracleSql.ActivatedDuringPeriodBaseFrom, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("COUNT(DISTINCT acs.id)", OracleSql.CountActivatedDuringPeriod, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StatusEvents_UseFromDate_AsEventDate_NotToDate()
    {
        var sql = OracleSql.StatusEventBaseFrom + OracleSql.CountStatusEvents + OracleSql.SelectStatusEvents
                  + OracleSql.CancellationTrend + OracleSql.StatusDistribution;
        Assert.Contains("ast.fromdate >= :p_from", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ast.fromdate <= :p_to", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ast.todate >= :p_from", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ast.todate <= :p_to", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ast.todate AS status_end_date", OracleSql.SelectStatusEvents, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SuspendedEvents_CountsDistinctAccounts_WithFromDateEntry()
    {
        Assert.Contains("FROM accounts a", OracleSql.CountSuspendedAccounts, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("COUNT(DISTINCT a.id)", OracleSql.CountSuspendedAccounts, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("EXISTS", OracleSql.SuspendedAccountsBaseFrom, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("acctstatus", OracleSql.SuspendedAccountsBaseFrom, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ast.fromdate >= :p_from", OracleSql.SuspendedAccountsBaseFrom, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ast.fromdate < :p_to_exclusive", OracleSql.SuspendedAccountsBaseFrom, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ast.todate IS NULL", OracleSql.SuspendedAccountsBaseFrom, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ast.todate >= :p_from", OracleSql.SuspendedAccountsBaseFrom, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("COUNT(DISTINCT acs.id)", OracleSql.CountSuspendedAccounts, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("accountid ||", OracleSql.CountSuspendedAccounts, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("COUNT(DISTINCT a.id)", OracleSql.CountSuspendedAccounts, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FROM accounts a", OracleSql.CountSuspendedAccounts, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INNER JOIN acctstatus", OracleSql.CountSuspendedAccounts, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ast.fromdate >= :p_from", OracleSql.SelectSuspendedAccounts, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ast.fromdate < :p_to_exclusive", OracleSql.SelectSuspendedAccounts, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ast.todate >= :p_from", OracleSql.SelectSuspendedAccounts, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TRUNC(ast.fromdate, 'IW')", OracleSql.SuspendedEventsTrend, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("COUNT(DISTINCT ast.accountid)", OracleSql.SuspendedEventsTrend, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ast.fromdate >= :p_from", OracleSql.SuspendedEventsTrend, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ast.fromdate < :p_to_exclusive", OracleSql.SuspendedEventsTrend, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PendingCancellation_CountsDistinctAccounts_WithFromDateEntry()
    {
        Assert.Contains("FROM accounts a", OracleSql.CountPendingCancellationAccounts, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("COUNT(DISTINCT a.id)", OracleSql.CountPendingCancellationAccounts, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("EXISTS", OracleSql.PendingCancellationAccountsBaseFrom, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("acctstatus", OracleSql.PendingCancellationAccountsBaseFrom, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ast.fromdate >= :p_from", OracleSql.PendingCancellationAccountsBaseFrom, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ast.fromdate < :p_to_exclusive", OracleSql.PendingCancellationAccountsBaseFrom, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ast.todate IS NULL", OracleSql.PendingCancellationAccountsBaseFrom, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ast.todate >= :p_from", OracleSql.PendingCancellationAccountsBaseFrom, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("COUNT(DISTINCT acs.id)", OracleSql.CountPendingCancellationAccounts, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("accountid ||", OracleSql.CountPendingCancellationAccounts, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INNER JOIN acctstatus", OracleSql.CountPendingCancellationAccounts, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PARTITION BY ast.accountid", OracleSql.SelectPendingCancellationAccounts, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ast.fromdate >= :p_from", OracleSql.SelectPendingCancellationAccounts, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ast.fromdate < :p_to_exclusive", OracleSql.SelectPendingCancellationAccounts, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ast.todate >= :p_from", OracleSql.SelectPendingCancellationAccounts, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("COUNT(DISTINCT ast.accountid)", OracleSql.NewPendingCancellationsTrend, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ast.fromdate >= :p_from", OracleSql.NewPendingCancellationsTrend, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ast.fromdate < :p_to_exclusive", OracleSql.NewPendingCancellationsTrend, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NewActiveAndCurrentActive_RemainAccountCentric_UnchangedByPendingCancellation()
    {
        Assert.Contains("a.registrationdate", OracleSql.CountNewActiveLines, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("COUNT(DISTINCT a.id)", OracleSql.CountNewActiveLines, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("a.ownstatus = :p_active_status", OracleSql.CountCurrentActiveLines, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("COUNT(DISTINCT a.id)", OracleSql.CountCurrentActiveLines, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ast.status = :p_status", OracleSql.CountNewActiveLines, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ast.status = :p_status", OracleSql.CountCurrentActiveLines, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NewActiveAndStatusSql_PreventDuplicateFanOut()
    {
        Assert.Contains("EXISTS", OracleSql.CountNewActiveLines, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("COUNT(DISTINCT a.id)", OracleSql.CountNewActiveLines, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ROW_NUMBER()", OracleSql.SelectNewActiveLines, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PARTITION BY a.id", OracleSql.SelectNewActiveLines, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ROW_NUMBER()", OracleSql.SelectStatusEvents, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EntireOracleSql_NewActivePaths_NeverUseForbiddenActivationFields()
    {
        Assert.Contains("a.registrationdate", OracleSql.ActivationTrend, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("COUNT(DISTINCT a.id)", OracleSql.ActivationTrend, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FROM accounts a", OracleSql.ActivationTrend, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("acs.registrationdate", OracleSql.ActivationTrend, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ACTIVATIONDATE", OracleSql.ActivationTrend, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FIRSTACTIVATIONDATE", OracleSql.ActivationTrend, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ACTIVATIONDATE", OracleSql.CountNewActiveLines, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FIRSTACTIVATIONDATE", OracleSql.SelectNewActiveLines, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExportableTables_DisplayProductCatalogName_AndSearchIt()
    {
        string[] selects =
        [
            OracleSql.SelectNewActiveLines,
            OracleSql.SelectCurrentActiveLines,
            OracleSql.SelectActivatedDuringPeriod,
            OracleSql.SelectSuspendedAccounts,
            OracleSql.SelectStatusEvents,
            OracleSql.SelectStatusHistory,
            OracleSql.SelectPendingCancellationAccounts,
            OracleSql.SelectCancellationAccounts
        ];

        foreach (var sql in selects)
        {
            Assert.Contains("NVL(p.description", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("LEFT JOIN products p", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("acs.productcode AS product", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("q.productcode", sql, StringComparison.Ordinal);
        }

        Assert.Contains("UPPER(p.description)", OracleSql.NewActiveBaseFrom, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UPPER(p.description)", OracleSql.CurrentActiveBaseFrom, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UPPER(px.description)", OracleSql.SelectNewActiveLines, StringComparison.OrdinalIgnoreCase);
    }
}

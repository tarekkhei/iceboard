using Icewireless.AccountServiceDashboard.Application.DTOs;
using Icewireless.AccountServiceDashboard.Application.Interfaces;
using Icewireless.AccountServiceDashboard.Application.Services;
using Icewireless.AccountServiceDashboard.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Icewireless.AccountServiceDashboard.Tests.Services;

public class DashboardServiceTests
{
    [Fact]
    public async Task GetDashboardAsync_ComputesNetGrowthAndChurn()
    {
        var accounts = new Mock<IAccountServiceRepository>();
        var status = new Mock<IStatusRepository>();
        var analysis = new Mock<IAnalysisRepository>();
        var exclusions = new Mock<IAccountExclusionService>();

        var filter = new DashboardFilter
        {
            FromDate = new DateOnly(2026, 1, 1),
            ToDate = new DateOnly(2026, 1, 28),
            Page = 1,
            PageSize = 50
        }.Normalize(50);

        accounts.Setup(a => a.CountNewActiveLinesAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync(100);
        accounts.Setup(a => a.CountCurrentActiveLinesAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync(500);
        accounts.Setup(a => a.CountOpeningActiveLinesAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync(200);
        exclusions.Setup(e => e.CountExcludedAccountsAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync(38);
        status.Setup(s => s.CountStatusEventsAsync(StatusCodes.Suspended, It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync(10);
        status.Setup(s => s.CountPendingCancellationAccountsAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync(15);
        status.Setup(s => s.CountCancellationAccountsAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync(20);
        status.Setup(s => s.CountPermanentClosedAccountsAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync(5);
        status.Setup(s => s.GetCompletedCancellationsTrendAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        status.Setup(s => s.GetNewPendingCancellationsTrendAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        status.Setup(s => s.GetSuspendedEventsTrendAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        analysis.Setup(a => a.GetActivationTrendAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        analysis.Setup(a => a.GetStatusDistributionAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        analysis.Setup(a => a.GetRegionAnalysisAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        analysis.Setup(a => a.GetProductAnalysisAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        analysis.Setup(a => a.GetCancellationByReasonAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var sut = new DashboardService(accounts.Object, status.Object, analysis.Object, exclusions.Object, NullLogger<DashboardService>.Instance);
        var (kpis, charts) = await sut.GetDashboardAsync(filter);

        Assert.Equal(100, kpis.NewActiveLines);
        Assert.Equal(500, kpis.CurrentActiveLines);
        Assert.Equal(15, kpis.PendingCancellationEvents);
        Assert.Equal(20, kpis.CancellationEvents);
        Assert.Equal(5, kpis.PermanentClosedEvents);
        Assert.Equal(80, kpis.NetGrowth);
        Assert.Equal(10.0, kpis.ChurnRate);
        Assert.Empty(charts.ChurnRateTrend);
        Assert.Equal("flat", kpis.NewActiveTrend.Direction);
        Assert.Equal(0, kpis.NewActiveTrend.PercentChange);
        Assert.Equal(100, kpis.NewActiveTrend.PreviousValue);
        Assert.Equal(new DateOnly(2025, 12, 4), kpis.NewActiveTrend.PreviousFromDate);
        Assert.Equal(new DateOnly(2025, 12, 31), kpis.NewActiveTrend.PreviousToDate);
        exclusions.Verify(e => e.CountExcludedAccountsAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>()), Times.Once);
        accounts.Verify(a => a.CountActivatedDuringPeriodAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>()), Times.Never);
        status.Verify(s => s.CountPendingCancellationAccountsAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        status.Verify(s => s.CountCancellationAccountsAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        status.Verify(s => s.CountPermanentClosedAccountsAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        analysis.Verify(a => a.GetCancellationTrendAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetDashboardAsync_ComputesPercentChangeAgainstPreviousPeriod()
    {
        var accounts = new Mock<IAccountServiceRepository>();
        var status = new Mock<IStatusRepository>();
        var analysis = new Mock<IAnalysisRepository>();
        var exclusions = new Mock<IAccountExclusionService>();

        var filter = new DashboardFilter
        {
            FromDate = new DateOnly(2026, 1, 15),
            ToDate = new DateOnly(2026, 1, 28),
            Page = 1,
            PageSize = 50
        }.Normalize(50);

        accounts.Setup(a => a.CountNewActiveLinesAsync(
                It.Is<DashboardFilter>(f => f.FromDate == new DateOnly(2026, 1, 15)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1428);
        accounts.Setup(a => a.CountNewActiveLinesAsync(
                It.Is<DashboardFilter>(f => f.FromDate == new DateOnly(2026, 1, 1)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1317);
        accounts.Setup(a => a.CountCurrentActiveLinesAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync(500);
        accounts.Setup(a => a.CountOpeningActiveLinesAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync(200);
        exclusions.Setup(e => e.CountExcludedAccountsAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
        status.Setup(s => s.CountStatusEventsAsync(StatusCodes.Suspended, It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
        status.Setup(s => s.CountPendingCancellationAccountsAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
        status.Setup(s => s.CountCancellationAccountsAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
        status.Setup(s => s.CountPermanentClosedAccountsAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
        status.Setup(s => s.GetCompletedCancellationsTrendAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        status.Setup(s => s.GetNewPendingCancellationsTrendAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        status.Setup(s => s.GetSuspendedEventsTrendAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        analysis.Setup(a => a.GetActivationTrendAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        analysis.Setup(a => a.GetStatusDistributionAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        analysis.Setup(a => a.GetRegionAnalysisAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        analysis.Setup(a => a.GetProductAnalysisAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        analysis.Setup(a => a.GetCancellationByReasonAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var sut = new DashboardService(accounts.Object, status.Object, analysis.Object, exclusions.Object, NullLogger<DashboardService>.Instance);
        var (kpis, _) = await sut.GetDashboardAsync(filter);

        Assert.Equal(1428, kpis.NewActiveLines);
        Assert.Equal(1317, kpis.NewActiveTrend.PreviousValue);
        Assert.Equal("up", kpis.NewActiveTrend.Direction);
        Assert.Equal(8.4, kpis.NewActiveTrend.PercentChange);
        Assert.Equal(new DateOnly(2026, 1, 1), kpis.NewActiveTrend.PreviousFromDate);
        Assert.Equal(new DateOnly(2026, 1, 14), kpis.NewActiveTrend.PreviousToDate);
    }

    [Fact]
    public async Task GetDashboardAsync_BuildsWeeklyChurnFromCancellationsAndOpening()
    {
        var accounts = new Mock<IAccountServiceRepository>();
        var status = new Mock<IStatusRepository>();
        var analysis = new Mock<IAnalysisRepository>();
        var exclusions = new Mock<IAccountExclusionService>();

        var filter = new DashboardFilter
        {
            FromDate = new DateOnly(2026, 1, 1),
            ToDate = new DateOnly(2026, 1, 28),
            Page = 1,
            PageSize = 50
        }.Normalize(50);

        var week1 = new DateOnly(2026, 1, 5);
        var week2 = new DateOnly(2026, 1, 12);
        IReadOnlyList<TrendPointDto> weeklyCancels =
        [
            new(week1, "W/C Jan 05", 4),
            new(week2, "W/C Jan 12", 6)
        ];

        accounts.Setup(a => a.CountNewActiveLinesAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
        accounts.Setup(a => a.CountCurrentActiveLinesAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync(200);
        accounts.Setup(a => a.CountOpeningActiveLinesAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync(200);
        exclusions.Setup(e => e.CountExcludedAccountsAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
        status.Setup(s => s.CountStatusEventsAsync(StatusCodes.Suspended, It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
        status.Setup(s => s.CountPendingCancellationAccountsAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
        status.Setup(s => s.CountCancellationAccountsAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync(10);
        status.Setup(s => s.CountPermanentClosedAccountsAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync(10);
        status.Setup(s => s.GetCompletedCancellationsTrendAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync(weeklyCancels);
        status.Setup(s => s.GetNewPendingCancellationsTrendAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        status.Setup(s => s.GetSuspendedEventsTrendAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        analysis.Setup(a => a.GetActivationTrendAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        analysis.Setup(a => a.GetStatusDistributionAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        analysis.Setup(a => a.GetRegionAnalysisAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        analysis.Setup(a => a.GetProductAnalysisAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        analysis.Setup(a => a.GetCancellationByReasonAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var sut = new DashboardService(accounts.Object, status.Object, analysis.Object, exclusions.Object, NullLogger<DashboardService>.Instance);
        var (kpis, charts) = await sut.GetDashboardAsync(filter);

        Assert.Equal(5.0, kpis.ChurnRate);
        Assert.Equal(2, charts.ChurnRateTrend.Count);
        Assert.Equal(2.0, charts.ChurnRateTrend[0].Value);
        Assert.Equal(3.0, charts.ChurnRateTrend[1].Value);
        Assert.Equal("W/C Jan 05", charts.ChurnRateTrend[0].PeriodLabel);
        Assert.Equal("W/C Jan 12", charts.ChurnRateTrend[1].PeriodLabel);
        Assert.Equal([2.0, 3.0], kpis.ChurnTrend.Sparkline);
    }
}

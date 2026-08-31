using Icewireless.AccountServiceDashboard.Application.DTOs;
using Icewireless.AccountServiceDashboard.Application.Interfaces;
using Icewireless.AccountServiceDashboard.Infrastructure.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Icewireless.AccountServiceDashboard.Tests.Repositories;

public class RepositoryGuardTests
{
    [Fact]
    public async Task AccountRepository_DelegatesToStatusHistory()
    {
        var status = new Mock<IStatusRepository>();
        status.Setup(s => s.GetStatusHistoryAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<StatusEventDto>([], 0, 1, 50));

        var repo = new AccountRepository(status.Object);
        var result = await repo.GetAccountDetailsAsync(new DashboardFilter { Page = 1, PageSize = 50 });
        Assert.Empty(result.Items);
        status.Verify(s => s.GetStatusHistoryAsync(It.IsAny<DashboardFilter>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AccountServiceRepository_WithoutConnection_ThrowsInsteadOfZeros()
    {
        var factory = new Mock<IOracleConnectionFactory>();
        factory.SetupGet(f => f.HasConnectionString).Returns(false);
        factory.Setup(f => f.EnsureConfigured())
            .Throws(new InvalidOperationException("Oracle is not configured."));
        var settings = Microsoft.Extensions.Options.Options.Create(
            new Icewireless.AccountServiceDashboard.Application.Configuration.DashboardSettings
            {
                ProviderCode = "ICENP",
                PageSize = 50
            });

        var patterns = new Mock<IAccountExclusionPatternProvider>();
        patterns.Setup(p => p.GetActiveContainsPatterns()).Returns([]);

        var repo = new AccountServiceRepository(factory.Object, settings, patterns.Object, NullLogger<AccountServiceRepository>.Instance);
        var filter = new DashboardFilter { FromDate = new DateOnly(2026, 1, 1), ToDate = new DateOnly(2026, 1, 28) }.Normalize(50);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.CountNewActiveLinesAsync(filter));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.GetNewActiveLinesAsync(filter));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.CountCurrentActiveLinesAsync(filter));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.CountActivatedDuringPeriodAsync(filter));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.GetActivatedDuringPeriodAsync(filter));
    }
}

using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Icewireless.AccountServiceDashboard.Tests.Controllers;

public class SmokeControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public SmokeControllerTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:IceWirelessOracle", "");
            builder.UseSetting("DashboardSettings:ProviderCode", "ICENP");
            builder.UseSetting("Auth0:Enabled", "false");
        }).CreateClient();
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/dashboard")]
    [InlineData("/line-details/new-active")]
    [InlineData("/line-details/current-active")]
    [InlineData("/status-events/active")]
    [InlineData("/status-events/suspended")]
    [InlineData("/status-events/pending-cancellation")]
    [InlineData("/status-events/cancellation")]
    [InlineData("/status-events/permanently-closed")]
    [InlineData("/status-events/old")]
    [InlineData("/status-events/archived")]
    [InlineData("/analysis/region")]
    [InlineData("/analysis/service")]
    [InlineData("/analysis/cancellation-reason")]
    [InlineData("/analysis/activation-trends")]
    [InlineData("/analysis/cancellation-trends")]
    [InlineData("/analysis/net-growth")]
    [InlineData("/analysis/churn")]
    [InlineData("/reports/account-details")]
    [InlineData("/reports/service-details")]
    [InlineData("/reports/status-history")]
    [InlineData("/reports/export")]
    [InlineData("/admin/database-status")]
    [InlineData("/admin/status-mapping")]
    [InlineData("/admin/account-exclusions")]
    [InlineData("/api/admin/account-exclusions")]
    [InlineData("/admin/settings")]
    public async Task KeyRoutes_ReturnOk(string url)
    {
        var response = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

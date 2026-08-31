using Icewireless.AccountServiceDashboard.Application.Configuration;
using Icewireless.AccountServiceDashboard.Application.DTOs;

namespace Icewireless.AccountServiceDashboard.Tests.Services;

public class ProviderSelectionTests
{
    [Fact]
    public void Normalize_DefaultsProviderToIcenp()
    {
        var filter = new DashboardFilter().Normalize(50);
        Assert.Equal("ICENP", filter.ProviderCode);
    }

    [Theory]
    [InlineData("IRISWV", "IRISWV")]
    [InlineData("iriswv", "IRISWV")]
    [InlineData("ICENP", "ICENP")]
    [InlineData("unknown", "ICENP")]
    [InlineData("", "ICENP")]
    public void Normalize_AcceptsKnownProviders(string? requested, string expected)
    {
        var filter = new DashboardFilter { ProviderCode = requested }.Normalize(50);
        Assert.Equal(expected, filter.ProviderCode);
    }

    [Fact]
    public void Settings_ResolveProvider_SelectsIriswv()
    {
        var settings = new DashboardSettings();
        Assert.Equal("IRISWV", settings.ResolveProvider("IRISWV"));
        Assert.Equal("ICENP", settings.ResolveProvider(null));
        Assert.Equal("ICENP", settings.ResolveProvider("NOPE"));
    }

    [Theory]
    [InlineData("ICENP", "Ice Wireless")]
    [InlineData("icenp", "Ice Wireless")]
    [InlineData("IRISWV", "Iristel Wireless")]
    [InlineData("iriswv", "Iristel Wireless")]
    [InlineData(null, "Ice Wireless")]
    public void ProviderDisplayName_UsesReadableLabels(string? code, string expected)
    {
        Assert.Equal(expected, DashboardSettings.ProviderDisplayName(code));
    }
}

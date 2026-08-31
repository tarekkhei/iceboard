using Icewireless.AccountServiceDashboard.Application.Services;
using Icewireless.AccountServiceDashboard.Domain.Enums;

namespace Icewireless.AccountServiceDashboard.Tests.Services;

public class StatusMappingServiceTests
{
    private readonly StatusMappingService _sut = new();

    [Theory]
    [InlineData(StatusCodes.New, ServiceStatus.New, "New")]
    [InlineData(StatusCodes.Active, ServiceStatus.Active, "Active")]
    [InlineData(StatusCodes.Suspended, ServiceStatus.Suspended, "Suspended")]
    [InlineData(StatusCodes.PendingClose, ServiceStatus.PendingClose, "Pending to Close")]
    [InlineData(StatusCodes.PermanentClosed, ServiceStatus.PermanentClosed, "Permanently Closed")]
    [InlineData(StatusCodes.Old, ServiceStatus.Old, "Old")]
    [InlineData(StatusCodes.Archived, ServiceStatus.Archived, "Archived")]
    [InlineData("x", ServiceStatus.Unknown, "Unknown")]
    [InlineData("", ServiceStatus.Unknown, "Unknown")]
    [InlineData(null, ServiceStatus.Unknown, "Unknown")]
    public void Map_And_Label_AreCentralized(string? code, ServiceStatus expected, string label)
    {
        Assert.Equal(expected, _sut.Map(code));
        Assert.Equal(label, _sut.GetDisplayLabel(code));
    }

    [Fact]
    public void GetAllMappings_ContainsCanonicalCodes()
    {
        var codes = _sut.GetAllMappings().Select(m => m.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains(StatusCodes.New, codes);
        Assert.Contains(StatusCodes.Active, codes);
        Assert.Contains(StatusCodes.PendingClose, codes);
    }
}

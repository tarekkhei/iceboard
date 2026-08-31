namespace Icewireless.AccountServiceDashboard.Domain.Enums;

/// <summary>
/// Canonical service/account status codes used across Ice Wireless.
/// Values must only be referenced via <see cref="StatusCodes"/> / StatusMappingService.
/// </summary>
public enum ServiceStatus
{
    New,
    Active,
    Suspended,
    PendingClose,
    PermanentClosed,
    Old,
    Archived,
    Unknown
}

namespace Icewireless.AccountServiceDashboard.Domain.Enums;

/// <summary>
/// Centralized Oracle status code constants. Never hardcode these literals elsewhere.
/// </summary>
public static class StatusCodes
{
    public const string New = "N";
    public const string Active = "A";
    public const string Suspended = "S";
    public const string PendingClose = "C";
    public const string PermanentClosed = "T";
    public const string Old = "O";
    public const string Archived = "R";
}

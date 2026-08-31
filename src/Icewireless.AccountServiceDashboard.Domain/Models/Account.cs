namespace Icewireless.AccountServiceDashboard.Domain.Models;

/// <summary>
/// Placeholder domain entity for ACCOUNTS. Queries not implemented in this phase.
/// </summary>
public sealed class Account
{
    public long Id { get; set; }
    public string? Code { get; set; }
    public string? AccountName { get; set; }
    public string? ProviderCode { get; set; }
    public string? OwnStatus { get; set; }
    public DateTime? RegistrationDate { get; set; }
    public DateTime? ActivationDate { get; set; }
    public string? Category { get; set; }
    public long? Parent { get; set; }
}

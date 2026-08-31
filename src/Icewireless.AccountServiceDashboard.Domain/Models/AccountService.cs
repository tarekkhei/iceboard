namespace Icewireless.AccountServiceDashboard.Domain.Models;

/// <summary>
/// Placeholder domain entity for ACCOUNTSERVICES. Queries not implemented in this phase.
/// </summary>
public sealed class AccountService
{
    public long Id { get; set; }
    public long AccountId { get; set; }
    public string? ProductCode { get; set; }
    public string? ServiceCode { get; set; }
    public string? OwnStatus { get; set; }
    public DateTime? FirstActivationDate { get; set; }
    public string? ServiceType { get; set; }
}

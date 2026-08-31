namespace Icewireless.AccountServiceDashboard.Domain.Models;

/// <summary>
/// Placeholder domain entity for ACCTSTATUS. Queries not implemented in this phase.
/// </summary>
public sealed class AccountStatusEvent
{
    public long AccountId { get; set; }
    public string? Status { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public long? Reason { get; set; }
    public long? Subreason { get; set; }
    public long? OperationId { get; set; }
    public string? TransactionId { get; set; }
}

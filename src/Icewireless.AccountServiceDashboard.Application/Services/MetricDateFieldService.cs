using Icewireless.AccountServiceDashboard.Application.Interfaces;

namespace Icewireless.AccountServiceDashboard.Application.Services;

/// <summary>
/// Mandatory date-field resolver for Ice Wireless metrics.
/// </summary>
public sealed class MetricDateFieldService : IMetricDateFieldService
{
    public const string NewActiveLines = "new-active-lines";
    public const string CurrentActiveLines = "current-active-lines";
    public const string ActivatedDuringPeriod = "activated-during-period";
    public const string StatusEvents = "status-events";
    public const string StatusEnd = "status-end";

    public string ResolveDateField(string metricKey) => metricKey.Trim().ToLowerInvariant() switch
    {
        NewActiveLines or "new active lines" or "activations" => "ACCOUNTS.REGISTRATIONDATE",
        CurrentActiveLines or "current active lines" or "snapshot" => "Snapshot",
        ActivatedDuringPeriod or "activated during selected period" => "ACCOUNTS.ACTIVATIONDATE",
        "suspended" or "suspended-events" or "suspended events" => "ACCTSTATUS.FROMDATE",
        "pending-cancellation" or "pending cancellation" or "accounts pending cancellation during period"
            => "ACCTSTATUS.FROMDATE",
        "cancellation" or "cancellation-events" or "cancellation events"
            => "ACCTSTATUS.FROMDATE",
        "permanent-closed" or "permanently-closed" or "permanent closed"
            => "ACCTSTATUS.FROMDATE",
        StatusEvents or "status-event" or "active-status-events"
            or "old" or "archived"
            => "ACCTSTATUS.FROMDATE",
        StatusEnd or "status-end-date" => "ACCTSTATUS.TODATE",
        _ => throw new ArgumentOutOfRangeException(nameof(metricKey), metricKey, "Unknown metric key for date field resolution.")
    };
}

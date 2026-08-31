using Icewireless.AccountServiceDashboard.Application.DTOs;
using Icewireless.AccountServiceDashboard.Application.Interfaces;
using Icewireless.AccountServiceDashboard.Domain.Enums;

namespace Icewireless.AccountServiceDashboard.Application.Services;

public sealed class StatusMappingService : IStatusMappingService
{
    private static readonly IReadOnlyDictionary<string, ServiceStatus> ByCode =
        new Dictionary<string, ServiceStatus>(StringComparer.OrdinalIgnoreCase)
        {
            [StatusCodes.New] = ServiceStatus.New,
            [StatusCodes.Active] = ServiceStatus.Active,
            [StatusCodes.Suspended] = ServiceStatus.Suspended,
            [StatusCodes.PendingClose] = ServiceStatus.PendingClose,
            [StatusCodes.PermanentClosed] = ServiceStatus.PermanentClosed,
            [StatusCodes.Old] = ServiceStatus.Old,
            [StatusCodes.Archived] = ServiceStatus.Archived
        };

    public ServiceStatus Map(string? statusCode)
    {
        if (string.IsNullOrWhiteSpace(statusCode))
        {
            return ServiceStatus.Unknown;
        }

        return ByCode.TryGetValue(statusCode.Trim(), out var status)
            ? status
            : ServiceStatus.Unknown;
    }

    public string GetDisplayLabel(ServiceStatus status) => status switch
    {
        ServiceStatus.New => "New",
        ServiceStatus.Active => "Active",
        ServiceStatus.Suspended => "Suspended",
        ServiceStatus.PendingClose => "Pending to Close",
        ServiceStatus.PermanentClosed => "Permanently Closed",
        ServiceStatus.Old => "Old",
        ServiceStatus.Archived => "Archived",
        _ => "Unknown"
    };

    public string GetDisplayLabel(string? statusCode) => GetDisplayLabel(Map(statusCode));

    public string? GetCode(ServiceStatus status) => status switch
    {
        ServiceStatus.New => StatusCodes.New,
        ServiceStatus.Active => StatusCodes.Active,
        ServiceStatus.Suspended => StatusCodes.Suspended,
        ServiceStatus.PendingClose => StatusCodes.PendingClose,
        ServiceStatus.PermanentClosed => StatusCodes.PermanentClosed,
        ServiceStatus.Old => StatusCodes.Old,
        ServiceStatus.Archived => StatusCodes.Archived,
        _ => null
    };

    public IReadOnlyList<StatusMappingDto> GetAllMappings() =>
        ByCode.Select(kv => new StatusMappingDto(kv.Key, GetDisplayLabel(kv.Value), kv.Value.ToString()))
            .OrderBy(m => m.Label)
            .ToList();
}

using System.Globalization;
using Icewireless.AccountServiceDashboard.Application.DTOs;
using Icewireless.AccountServiceDashboard.Application.Interfaces;

namespace Icewireless.AccountServiceDashboard.Application.Services;

public sealed class KpiLinesService : IKpiLinesService
{
    private static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        "new-active",
        "current-active",
        "suspended",
        "pending-cancellation",
        "cancellation",
        "permanent-closed"
    };

    private readonly IStatusService _statusService;

    public KpiLinesService(IStatusService statusService)
    {
        _statusService = statusService;
    }

    public bool SupportsKey(string kpiKey) => Supported.Contains(NormalizeKey(kpiKey));

    public async Task<KpiLinesPageDto> GetLinesAsync(string kpiKey, DashboardFilter filter, CancellationToken cancellationToken = default)
    {
        var key = NormalizeKey(kpiKey);
        if (!Supported.Contains(key))
            throw new ArgumentOutOfRangeException(nameof(kpiKey), kpiKey, "KPI does not support line preview.");

        return key switch
        {
            "new-active" => await LoadNewActiveAsync(filter, cancellationToken),
            "current-active" => await LoadCurrentActiveAsync(filter, cancellationToken),
            "suspended" => await LoadStatusAsync("suspended", "Suspended Accounts", "/status-events/suspended/export/csv", filter, cancellationToken),
            "pending-cancellation" => await LoadStatusAsync("pending-cancellation", "Pending Cancellation Accounts", "/status-events/pending-cancellation/export/csv", filter, cancellationToken),
            "cancellation" => await LoadCancellationAsync(filter, cancellationToken),
            "permanent-closed" => await LoadPermanentClosedAsync(filter, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(kpiKey), kpiKey, "KPI does not support line preview.")
        };
    }

    private async Task<KpiLinesPageDto> LoadPermanentClosedAsync(DashboardFilter filter, CancellationToken ct)
    {
        var page = await _statusService.GetStatusEventsAsync("permanently-closed", filter, ct);
        var columns = new[]
        {
            "Account Code", "Account Name", "Current Status", "Closed From", "Closed To",
            "Reason", "Product", "Service Count", "Region", "Customer"
        };
        var rows = page.Items.Select(r => (IReadOnlyList<string?>)new string?[]
        {
            r.AccountCode,
            r.AccountName,
            r.ServiceType,
            r.StatusFromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            r.StatusEndDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            r.Reason,
            r.Product,
            r.Service,
            r.Region,
            r.Customer
        }).ToList();

        return new KpiLinesPageDto(
            "permanent-closed",
            "Permanent Closed",
            columns,
            rows,
            page.TotalCount,
            page.Page,
            page.PageSize,
            "/status-events/permanently-closed/export/csv");
    }

    private async Task<KpiLinesPageDto> LoadCancellationAsync(DashboardFilter filter, CancellationToken ct)
    {
        var page = await _statusService.GetStatusEventsAsync("cancellation", filter, ct);
        var columns = new[]
        {
            "Account Code", "Account Name", "Current Status", "Cancellation From", "Cancellation To",
            "Reason", "Product", "Service Count", "Region", "Customer"
        };
        var rows = page.Items.Select(r => (IReadOnlyList<string?>)new string?[]
        {
            r.AccountCode,
            r.AccountName,
            r.ServiceType, // mapped from a.ownstatus in SelectCancellationAccounts
            r.StatusFromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            r.StatusEndDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            r.Reason,
            r.Product,
            r.Service,
            r.Region,
            r.Customer
        }).ToList();

        return new KpiLinesPageDto(
            "cancellation",
            "Cancellation Events",
            columns,
            rows,
            page.TotalCount,
            page.Page,
            page.PageSize,
            "/status-events/cancellation/export/csv");
    }

    private async Task<KpiLinesPageDto> LoadNewActiveAsync(DashboardFilter filter, CancellationToken ct)
    {
        var page = await _statusService.GetNewActiveLinesAsync(filter, ct);
        var columns = new[] { "Registration Date", "Account Code", "Account Name", "Product", "Service", "Service Type", "Customer", "Region", "Email" };
        var rows = page.Items.Select(r => (IReadOnlyList<string?>)new string?[]
        {
            r.RegistrationDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            r.AccountCode,
            r.AccountName,
            r.Product,
            r.Service,
            r.ServiceType,
            r.Customer,
            r.Region,
            r.Email
        }).ToList();

        return new KpiLinesPageDto(
            "new-active",
            "New Active Lines",
            columns,
            rows,
            page.TotalCount,
            page.Page,
            page.PageSize,
            "/line-details/new-active/export/csv");
    }

    private async Task<KpiLinesPageDto> LoadCurrentActiveAsync(DashboardFilter filter, CancellationToken ct)
    {
        var page = await _statusService.GetCurrentActiveLinesAsync(filter, ct);
        var columns = new[] { "Account Code", "Account Name", "Status", "Activation Date", "Product", "Service", "Service Type", "Customer", "Region", "Email" };
        var rows = page.Items.Select(r => (IReadOnlyList<string?>)new string?[]
        {
            r.AccountCode,
            r.AccountName,
            r.StatusLabel ?? r.StatusCode,
            r.ActivationDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            r.Product,
            r.Service,
            r.ServiceType,
            r.Customer,
            r.Region,
            r.Email
        }).ToList();

        return new KpiLinesPageDto(
            "current-active",
            "Current Active Lines",
            columns,
            rows,
            page.TotalCount,
            page.Page,
            page.PageSize,
            "/line-details/current-active/export/csv");
    }

    private async Task<KpiLinesPageDto> LoadStatusAsync(
        string eventKey,
        string title,
        string csvPath,
        DashboardFilter filter,
        CancellationToken ct)
    {
        var page = await _statusService.GetStatusEventsAsync(eventKey, filter, ct);
        var columns = new[] { "Account Code", "Account Name", "Status", "From Date", "End Date", "Reason", "Product", "Service", "Service Type", "Region", "Customer" };
        var rows = page.Items.Select(r => (IReadOnlyList<string?>)new string?[]
        {
            r.AccountCode,
            r.AccountName,
            r.StatusLabel,
            r.StatusFromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            r.StatusEndDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            r.Reason,
            r.Product,
            r.Service,
            r.ServiceType,
            r.Region,
            r.Customer
        }).ToList();

        return new KpiLinesPageDto(
            eventKey == "permanently-closed" ? "permanent-closed" : eventKey,
            title,
            columns,
            rows,
            page.TotalCount,
            page.Page,
            page.PageSize,
            csvPath);
    }

    private static string NormalizeKey(string kpiKey) => (kpiKey ?? string.Empty).Trim().ToLowerInvariant();
}

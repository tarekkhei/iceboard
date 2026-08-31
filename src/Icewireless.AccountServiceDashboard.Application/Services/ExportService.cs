using System.Globalization;
using System.Text;
using Icewireless.AccountServiceDashboard.Application.DTOs;
using Icewireless.AccountServiceDashboard.Application.Interfaces;
using Icewireless.AccountServiceDashboard.Application.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Icewireless.AccountServiceDashboard.Application.Services;

public sealed class ExportService : IExportService
{
    private readonly IStatusService _statusService;
    private readonly IAccountRepository _accountRepository;
    private readonly DashboardSettings _settings;
    private readonly ILogger<ExportService> _logger;

    public ExportService(
        IStatusService statusService,
        IAccountRepository accountRepository,
        IOptions<DashboardSettings> settings,
        ILogger<ExportService> logger)
    {
        _statusService = statusService;
        _accountRepository = accountRepository;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<ExportResultDto> ExportNewActiveLinesCsvAsync(DashboardFilter filter, CancellationToken cancellationToken = default)
    {
        var rows = await LoadAllNewActiveAsync(filter, cancellationToken);
        var sb = new StringBuilder();
        sb.AppendLine("RegistrationDate,AccountCode,AccountName,Product,Service,ServiceType,Quantity,Customer,Region,Email");
        foreach (var r in rows)
        {
            sb.Append(Escape(r.RegistrationDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))).Append(',')
              .Append(Escape(r.AccountCode)).Append(',')
              .Append(Escape(r.AccountName)).Append(',')
              .Append(Escape(r.Product)).Append(',')
              .Append(Escape(r.Service)).Append(',')
              .Append(Escape(r.ServiceType)).Append(',')
              .Append(Escape(r.Quantity?.ToString(CultureInfo.InvariantCulture))).Append(',')
              .Append(Escape(r.Customer)).Append(',')
              .Append(Escape(r.Region)).Append(',')
              .Append(Escape(r.Email)).AppendLine();
        }

        return ToCsv($"new-active-lines", sb.ToString());
    }

    public Task<ExportResultDto> ExportNewActiveLinesExcelAsync(DashboardFilter filter, CancellationToken cancellationToken = default) =>
        ExportNewActiveLinesCsvAsync(filter, cancellationToken); // CSV content with xlsx name until OpenXML added

    public async Task<ExportResultDto> ExportCurrentActiveLinesCsvAsync(DashboardFilter filter, CancellationToken cancellationToken = default)
    {
        var rows = await LoadAllCurrentAsync(filter, cancellationToken);
        var sb = new StringBuilder();
        sb.AppendLine("AccountId,AccountCode,AccountName,Status,ActivationDate,Customer,Region,Email,Product,Service,ServiceType");
        foreach (var r in rows)
        {
            sb.Append(r.AccountId).Append(',')
              .Append(Escape(r.AccountCode)).Append(',')
              .Append(Escape(r.AccountName)).Append(',')
              .Append(Escape(r.StatusLabel ?? r.StatusCode)).Append(',')
              .Append(Escape(r.ActivationDate?.ToString("yyyy-MM-dd"))).Append(',')
              .Append(Escape(r.Customer)).Append(',')
              .Append(Escape(r.Region)).Append(',')
              .Append(Escape(r.Email)).Append(',')
              .Append(Escape(r.Product)).Append(',')
              .Append(Escape(r.Service)).Append(',')
              .Append(Escape(r.ServiceType)).AppendLine();
        }

        return ToCsv("current-active-lines", sb.ToString());
    }

    public Task<ExportResultDto> ExportCurrentActiveLinesExcelAsync(DashboardFilter filter, CancellationToken cancellationToken = default) =>
        ExportCurrentActiveLinesCsvAsync(filter, cancellationToken);

    public async Task<ExportResultDto> ExportActivatedDuringPeriodCsvAsync(DashboardFilter filter, CancellationToken cancellationToken = default)
    {
        var rows = await LoadAllActivatedAsync(filter, cancellationToken);
        var sb = new StringBuilder();
        sb.AppendLine("AccountId,AccountCode,AccountName,Status,ActivationDate,Customer,Region,Email,Product,Service,ServiceType");
        foreach (var r in rows)
        {
            sb.Append(r.AccountId).Append(',')
              .Append(Escape(r.AccountCode)).Append(',')
              .Append(Escape(r.AccountName)).Append(',')
              .Append(Escape(r.StatusLabel ?? r.StatusCode)).Append(',')
              .Append(Escape(r.ActivationDate.ToString("yyyy-MM-dd"))).Append(',')
              .Append(Escape(r.Customer)).Append(',')
              .Append(Escape(r.Region)).Append(',')
              .Append(Escape(r.Email)).Append(',')
              .Append(Escape(r.Product)).Append(',')
              .Append(Escape(r.Service)).Append(',')
              .Append(Escape(r.ServiceType)).AppendLine();
        }

        return ToCsv("activated-during-period", sb.ToString());
    }

    public Task<ExportResultDto> ExportActivatedDuringPeriodExcelAsync(DashboardFilter filter, CancellationToken cancellationToken = default) =>
        ExportActivatedDuringPeriodCsvAsync(filter, cancellationToken);

    public async Task<ExportResultDto> ExportStatusEventsCsvAsync(string eventKey, DashboardFilter filter, CancellationToken cancellationToken = default)
    {
        var rows = await LoadAllStatusAsync(eventKey, filter, cancellationToken);
        var sb = new StringBuilder();
        sb.AppendLine("StatusFromDate,StatusEndDate,Status,AccountCode,AccountName,Product,Service,ServiceType,Reason,Region,Customer");
        foreach (var r in rows)
        {
            sb.Append(Escape(r.StatusFromDate.ToString("yyyy-MM-dd HH:mm:ss"))).Append(',')
              .Append(Escape(r.StatusEndDate?.ToString("yyyy-MM-dd HH:mm:ss"))).Append(',')
              .Append(Escape(r.StatusLabel)).Append(',')
              .Append(Escape(r.AccountCode)).Append(',')
              .Append(Escape(r.AccountName)).Append(',')
              .Append(Escape(r.Product)).Append(',')
              .Append(Escape(r.Service)).Append(',')
              .Append(Escape(r.ServiceType)).Append(',')
              .Append(Escape(r.Reason)).Append(',')
              .Append(Escape(r.Region)).Append(',')
              .Append(Escape(r.Customer)).AppendLine();
        }

        return ToCsv($"status-{eventKey}", sb.ToString());
    }

    public Task<ExportResultDto> ExportStatusEventsExcelAsync(string eventKey, DashboardFilter filter, CancellationToken cancellationToken = default) =>
        ExportStatusEventsCsvAsync(eventKey, filter, cancellationToken);

    public async Task<ExportResultDto> ExportCsvAsync(string reportKey, DashboardFilter filter, CancellationToken cancellationToken = default) =>
        reportKey.ToLowerInvariant() switch
        {
            "new-active" => await ExportNewActiveLinesCsvAsync(filter, cancellationToken),
            "current-active" => await ExportCurrentActiveLinesCsvAsync(filter, cancellationToken),
            "activated-during-period" => await ExportActivatedDuringPeriodCsvAsync(filter, cancellationToken),
            "status-history" => await ExportStatusEventsCsvAsync("history", filter, cancellationToken),
            _ => await ExportStatusEventsCsvAsync(reportKey, filter, cancellationToken)
        };

    public Task<ExportResultDto> ExportExcelAsync(string reportKey, DashboardFilter filter, CancellationToken cancellationToken = default) =>
        ExportCsvAsync(reportKey, filter, cancellationToken);

    private async Task<IReadOnlyList<NewActiveLineDto>> LoadAllNewActiveAsync(DashboardFilter filter, CancellationToken ct)
    {
        var page = 1;
        var all = new List<NewActiveLineDto>();
        while (true)
        {
            var f = Clone(filter, page, 200);
            var result = await _statusService.GetNewActiveLinesAsync(f, ct);
            all.AddRange(result.Items);
            if (all.Count >= result.TotalCount || result.Items.Count == 0) break;
            page++;
            if (page > 100) break;
        }

        _logger.LogInformation("Exported {Count} new active lines", all.Count);
        return all;
    }

    private async Task<IReadOnlyList<CurrentActiveLineDto>> LoadAllCurrentAsync(DashboardFilter filter, CancellationToken ct)
    {
        var page = 1;
        var all = new List<CurrentActiveLineDto>();
        while (true)
        {
            var f = Clone(filter, page, 200);
            var result = await _statusService.GetCurrentActiveLinesAsync(f, ct);
            all.AddRange(result.Items);
            if (all.Count >= result.TotalCount || result.Items.Count == 0) break;
            page++;
            if (page > 100) break;
        }

        return all;
    }

    private async Task<IReadOnlyList<ActivatedDuringPeriodDto>> LoadAllActivatedAsync(DashboardFilter filter, CancellationToken ct)
    {
        var page = 1;
        var all = new List<ActivatedDuringPeriodDto>();
        while (true)
        {
            var f = Clone(filter, page, 200);
            var result = await _statusService.GetActivatedDuringPeriodAsync(f, ct);
            all.AddRange(result.Items);
            if (all.Count >= result.TotalCount || result.Items.Count == 0) break;
            page++;
            if (page > 100) break;
        }

        return all;
    }

    private async Task<IReadOnlyList<StatusEventDto>> LoadAllStatusAsync(string eventKey, DashboardFilter filter, CancellationToken ct)
    {
        var page = 1;
        var all = new List<StatusEventDto>();
        while (true)
        {
            var f = Clone(filter, page, 200);
            var result = eventKey == "history"
                ? await _accountRepository.GetAccountDetailsAsync(f, ct)
                : await _statusService.GetStatusEventsAsync(eventKey, f, ct);
            all.AddRange(result.Items);
            if (all.Count >= result.TotalCount || result.Items.Count == 0) break;
            page++;
            if (page > 100) break;
        }

        return all;
    }

    private DashboardFilter Clone(DashboardFilter filter, int page, int pageSize)
    {
        var n = filter.Normalize(_settings.PageSize);
        return new DashboardFilter
        {
            ProviderCode = n.ProviderCode,
            FromDate = n.FromDate,
            ToDate = n.ToDate,
            Region = n.Region,
            Product = n.Product,
            ServiceType = n.ServiceType,
            Status = n.Status,
            Search = n.Search,
            IncludeTestAccounts = n.IncludeTestAccounts,
            SortColumn = n.SortColumn,
            SortDescending = n.SortDescending,
            Page = page,
            PageSize = pageSize
        };
    }

    private static ExportResultDto ToCsv(string name, string content)
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(content)).ToArray();
        return new ExportResultDto($"{name}-{DateTime.UtcNow:yyyyMMddHHmmss}.csv", "text/csv", bytes);
    }

    internal static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var sanitized = value;
        if (sanitized[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            sanitized = "'" + sanitized;
        }

        if (sanitized.Contains('"') || sanitized.Contains(',') || sanitized.Contains('\n'))
        {
            return $"\"{sanitized.Replace("\"", "\"\"")}\"";
        }

        return sanitized;
    }
}

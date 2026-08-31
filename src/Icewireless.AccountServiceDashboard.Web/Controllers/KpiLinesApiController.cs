using Icewireless.AccountServiceDashboard.Application.Configuration;
using Icewireless.AccountServiceDashboard.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Icewireless.AccountServiceDashboard.Web.Controllers;

[ApiController]
[Route("api/dashboard/kpi-lines")]
public sealed class KpiLinesApiController : ControllerBase
{
    private readonly IKpiLinesService _kpiLines;
    private readonly DashboardSettings _settings;

    public KpiLinesApiController(IKpiLinesService kpiLines, IOptions<DashboardSettings> settings)
    {
        _kpiLines = kpiLines;
        _settings = settings.Value;
    }

    [HttpGet("{key}")]
    public async Task<IActionResult> Get(string key, [FromQuery] FilterQuery query, CancellationToken cancellationToken)
    {
        if (!_kpiLines.SupportsKey(key))
            return NotFound(new { error = $"KPI '{key}' does not support line preview." });

        var filter = query.ToFilter(_settings.PageSize);
        if (filter.PageSize < 1)
            filter.PageSize = 50;
        if (filter.PageSize > 100)
            filter.PageSize = 100;

        var page = await _kpiLines.GetLinesAsync(key, filter, cancellationToken);
        return Ok(page);
    }
}

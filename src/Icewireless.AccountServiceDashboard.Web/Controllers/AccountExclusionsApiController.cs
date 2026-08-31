using Icewireless.AccountServiceDashboard.Application.DTOs;
using Icewireless.AccountServiceDashboard.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Icewireless.AccountServiceDashboard.Web.Controllers;

[ApiController]
[Route("api/admin/account-exclusions")]
public sealed class AccountExclusionsApiController : ControllerBase
{
    private readonly IAccountExclusionService _service;

    public AccountExclusionsApiController(IAccountExclusionService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<AccountExclusionRuleDto>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.SearchAsync(new AccountExclusionSearchDto(search, isActive, page, pageSize), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<AccountExclusionRuleDto>> GetById(long id, CancellationToken cancellationToken)
    {
        var rule = await _service.GetByIdAsync(id, cancellationToken);
        return rule is null ? NotFound() : Ok(rule);
    }

    [HttpPost]
    public async Task<ActionResult<AccountExclusionRuleDto>> Create(
        [FromBody] AccountExclusionRuleWriteDto body,
        CancellationToken cancellationToken)
    {
        try
        {
            var created = await _service.CreateAsync(body, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<AccountExclusionRuleDto>> Update(
        long id,
        [FromBody] AccountExclusionRuleWriteDto body,
        CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _service.UpdateAsync(id, body, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        var ok = await _service.DeleteAsync(id, cancellationToken);
        return ok ? NoContent() : NotFound();
    }

    [HttpPatch("{id:long}/enable")]
    public async Task<ActionResult<AccountExclusionRuleDto>> Enable(long id, CancellationToken cancellationToken)
    {
        var rule = await _service.EnableAsync(id, cancellationToken);
        return rule is null ? NotFound() : Ok(rule);
    }

    [HttpPatch("{id:long}/disable")]
    public async Task<ActionResult<AccountExclusionRuleDto>> Disable(long id, CancellationToken cancellationToken)
    {
        var rule = await _service.DisableAsync(id, cancellationToken);
        return rule is null ? NotFound() : Ok(rule);
    }
}

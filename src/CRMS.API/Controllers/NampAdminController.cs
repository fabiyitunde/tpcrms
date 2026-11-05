using System.Security.Claims;
using CRMS.Application.Namp.Commands;
using CRMS.Application.Namp.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CRMS.Application.Namp.DTOs;

namespace CRMS.API.Controllers;

[ApiController]
[Route("api/v1/namp/admin")]
[Authorize]
public class NampAdminController : ControllerBase
{
    private readonly GetNampRoutingConfigsHandler _getConfigs;
    private readonly CreateNampRoutingConfigHandler _create;
    private readonly UpdateNampRoutingConfigHandler _update;
    private readonly ToggleNampRoutingConfigHandler _toggle;

    public NampAdminController(
        GetNampRoutingConfigsHandler getConfigs,
        CreateNampRoutingConfigHandler create,
        UpdateNampRoutingConfigHandler update,
        ToggleNampRoutingConfigHandler toggle)
    {
        _getConfigs = getConfigs;
        _create = create;
        _update = update;
        _toggle = toggle;
    }

    /// <summary>List the NAMP committee routing rules.</summary>
    [HttpGet("routing-configs")]
    [ProducesResponseType(typeof(List<NampRoutingConfigDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRoutingConfigs(CancellationToken ct)
    {
        var result = await _getConfigs.Handle(new GetNampRoutingConfigsQuery(), ct);
        return Ok(result.Data);
    }

    /// <summary>Add a NAMP committee routing rule.</summary>
    [HttpPost("routing-configs")]
    [ProducesResponseType(typeof(NampRoutingConfigDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateRoutingConfigRequest body, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _create.Handle(
            new CreateNampRoutingConfigCommand(
                body.ApplicantCategory,
                body.CommitteeTier,
                body.MinEquipmentValue,
                body.MaxEquipmentValue,
                body.Priority,
                userId), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>Update a NAMP committee routing rule.</summary>
    [HttpPut("routing-configs/{id:guid}")]
    [ProducesResponseType(typeof(NampRoutingConfigDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRoutingConfigRequest body, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _update.Handle(
            new UpdateNampRoutingConfigCommand(id, body.MinEquipmentValue, body.MaxEquipmentValue, body.Priority, userId), ct);
        return result.IsSuccess ? Ok(result.Data) : NotFound(result.Error);
    }

    /// <summary>Switch a NAMP committee routing rule on or off.</summary>
    [HttpPost("routing-configs/{id:guid}/toggle")]
    [ProducesResponseType(typeof(NampRoutingConfigDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Toggle(Guid id, [FromBody] ToggleRequest body, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _toggle.Handle(new ToggleNampRoutingConfigCommand(id, body.Activate, userId), ct);
        return result.IsSuccess ? Ok(result.Data) : NotFound(result.Error);
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }
}

public record CreateRoutingConfigRequest(
    string ApplicantCategory,
    string CommitteeTier,
    decimal MinEquipmentValue,
    decimal MaxEquipmentValue,
    int Priority = 0);

public record UpdateRoutingConfigRequest(
    decimal MinEquipmentValue,
    decimal MaxEquipmentValue,
    int Priority);

public record ToggleRequest(bool Activate);

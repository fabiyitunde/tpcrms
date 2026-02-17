using CRMS.Application.Configuration.Commands;
using CRMS.Application.Configuration.DTOs;
using CRMS.Application.Configuration.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRMS.API.Controllers;

/// <summary>
/// Manages scoring configuration parameters with maker-checker workflow.
/// Restricted to System Administrator role only.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SystemAdministrator")]
public class ScoringConfigurationController : ControllerBase
{
    private readonly IServiceProvider _serviceProvider;

    public ScoringConfigurationController(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Get all active scoring parameters grouped by category.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<ScoringParameterDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var handler = _serviceProvider.GetRequiredService<GetAllScoringParametersHandler>();
        var result = await handler.Handle(new GetAllScoringParametersQuery(), ct);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>
    /// Get category summaries with pending change counts.
    /// </summary>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(List<CategorySummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategories(CancellationToken ct)
    {
        var handler = _serviceProvider.GetRequiredService<GetCategorySummariesHandler>();
        var result = await handler.Handle(new GetCategorySummariesQuery(), ct);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>
    /// Get scoring parameters by category.
    /// </summary>
    [HttpGet("category/{category}")]
    [ProducesResponseType(typeof(List<ScoringParameterDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByCategory(string category, CancellationToken ct)
    {
        var handler = _serviceProvider.GetRequiredService<GetScoringParametersByCategoryHandler>();
        var result = await handler.Handle(new GetScoringParametersByCategoryQuery(category), ct);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>
    /// Get a single scoring parameter by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ScoringParameterDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var handler = _serviceProvider.GetRequiredService<GetScoringParameterByIdHandler>();
        var result = await handler.Handle(new GetScoringParameterByIdQuery(id), ct);

        return result.IsSuccess ? Ok(result.Data) : NotFound(result.Error);
    }

    /// <summary>
    /// Get all pending parameter changes awaiting approval.
    /// </summary>
    [HttpGet("pending")]
    [ProducesResponseType(typeof(List<PendingChangeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingChanges(CancellationToken ct)
    {
        var handler = _serviceProvider.GetRequiredService<GetPendingChangesHandler>();
        var result = await handler.Handle(new GetPendingChangesQuery(), ct);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>
    /// Get change history for a specific parameter.
    /// </summary>
    [HttpGet("{id:guid}/history")]
    [ProducesResponseType(typeof(List<ScoringParameterHistoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory(Guid id, CancellationToken ct)
    {
        var handler = _serviceProvider.GetRequiredService<GetParameterHistoryHandler>();
        var result = await handler.Handle(new GetParameterHistoryQuery(id), ct);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>
    /// Get recent change history across all parameters.
    /// </summary>
    [HttpGet("history")]
    [ProducesResponseType(typeof(List<ScoringParameterHistoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecentHistory([FromQuery] int count = 50, CancellationToken ct = default)
    {
        var handler = _serviceProvider.GetRequiredService<GetRecentHistoryHandler>();
        var result = await handler.Handle(new GetRecentHistoryQuery(count), ct);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>
    /// Request a change to a scoring parameter (Maker step).
    /// Change will be pending until approved by another System Administrator.
    /// </summary>
    [HttpPost("{id:guid}/request-change")]
    [ProducesResponseType(typeof(ScoringParameterDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RequestChange(
        Guid id, 
        [FromBody] RequestParameterChangeRequest request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var handler = _serviceProvider.GetRequiredService<RequestParameterChangeHandler>();
        var result = await handler.Handle(
            new RequestParameterChangeCommand(id, request.NewValue, request.Reason, userId), ct);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>
    /// Approve a pending parameter change (Checker step).
    /// Must be a different user from the one who requested the change.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(typeof(ScoringParameterDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ApproveChange(
        Guid id,
        [FromBody] ApproveChangeRequest request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var handler = _serviceProvider.GetRequiredService<ApproveParameterChangeHandler>();
        var result = await handler.Handle(
            new ApproveParameterChangeCommand(id, userId, request.Notes), ct);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>
    /// Reject a pending parameter change.
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType(typeof(ScoringParameterDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RejectChange(
        Guid id,
        [FromBody] RejectChangeRequest request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var handler = _serviceProvider.GetRequiredService<RejectParameterChangeHandler>();
        var result = await handler.Handle(
            new RejectParameterChangeCommand(id, userId, request.Reason), ct);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>
    /// Cancel a pending change (only the original requester can cancel).
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(ScoringParameterDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CancelChange(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var handler = _serviceProvider.GetRequiredService<CancelParameterChangeHandler>();
        var result = await handler.Handle(new CancelParameterChangeCommand(id, userId), ct);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>
    /// Seed default scoring parameters (initial setup only).
    /// </summary>
    [HttpPost("seed")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    public async Task<IActionResult> SeedDefaults(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var handler = _serviceProvider.GetRequiredService<SeedDefaultParametersHandler>();
        var result = await handler.Handle(new SeedDefaultParametersCommand(userId), ct);

        return result.IsSuccess 
            ? Ok(new { ParametersCreated = result.Data }) 
            : BadRequest(result.Error);
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("sub") ?? User.FindFirst("userId");
        return userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId) 
            ? userId 
            : Guid.Empty;
    }
}

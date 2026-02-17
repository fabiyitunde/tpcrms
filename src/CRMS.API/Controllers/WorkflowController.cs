using CRMS.Application.Workflow.Commands;
using CRMS.Application.Workflow.DTOs;
using CRMS.Application.Workflow.Queries;
using CRMS.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRMS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WorkflowController : ControllerBase
{
    private readonly IServiceProvider _serviceProvider;

    public WorkflowController(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Get workflow instance by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(WorkflowInstanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var handler = _serviceProvider.GetRequiredService<GetWorkflowInstanceByIdHandler>();
        var result = await handler.Handle(new GetWorkflowInstanceByIdQuery(id), ct);

        return result.IsSuccess ? Ok(result.Data) : NotFound(result.Error);
    }

    /// <summary>
    /// Get workflow by loan application ID.
    /// </summary>
    [HttpGet("by-loan-application/{loanApplicationId:guid}")]
    [ProducesResponseType(typeof(WorkflowInstanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByLoanApplication(Guid loanApplicationId, CancellationToken ct)
    {
        var handler = _serviceProvider.GetRequiredService<GetWorkflowByLoanApplicationHandler>();
        var result = await handler.Handle(new GetWorkflowByLoanApplicationQuery(loanApplicationId), ct);

        return result.IsSuccess ? Ok(result.Data) : NotFound(result.Error);
    }

    /// <summary>
    /// Get available actions for current workflow state and user role.
    /// </summary>
    [HttpGet("{id:guid}/available-actions")]
    [ProducesResponseType(typeof(List<AvailableActionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAvailableActions(Guid id, CancellationToken ct)
    {
        var userRole = GetCurrentUserRole();
        var handler = _serviceProvider.GetRequiredService<GetAvailableActionsHandler>();
        var result = await handler.Handle(new GetAvailableActionsQuery(id, userRole), ct);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>
    /// Transition workflow to next state.
    /// </summary>
    [HttpPost("{id:guid}/transition")]
    [ProducesResponseType(typeof(WorkflowInstanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Transition(
        Guid id,
        [FromBody] TransitionWorkflowRequest request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var userRole = GetCurrentUserRole();

        if (!Enum.TryParse<LoanApplicationStatus>(request.ToStatus, out var toStatus))
            return BadRequest($"Invalid status: {request.ToStatus}");

        if (!Enum.TryParse<WorkflowAction>(request.Action, out var action))
            return BadRequest($"Invalid action: {request.Action}");

        var handler = _serviceProvider.GetRequiredService<TransitionWorkflowHandler>();
        var result = await handler.Handle(
            new TransitionWorkflowCommand(id, toStatus, action, userId, userRole, request.Comment), ct);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>
    /// Assign workflow to a specific user.
    /// </summary>
    [HttpPost("{id:guid}/assign")]
    [ProducesResponseType(typeof(WorkflowInstanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Assign(
        Guid id,
        [FromBody] AssignWorkflowRequest request,
        CancellationToken ct)
    {
        var assignedBy = GetCurrentUserId();
        var handler = _serviceProvider.GetRequiredService<AssignWorkflowHandler>();
        var result = await handler.Handle(
            new AssignWorkflowCommand(id, request.AssignToUserId, assignedBy), ct);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>
    /// Unassign workflow from current user.
    /// </summary>
    [HttpPost("{id:guid}/unassign")]
    [ProducesResponseType(typeof(WorkflowInstanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Unassign(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var handler = _serviceProvider.GetRequiredService<UnassignWorkflowHandler>();
        var result = await handler.Handle(new UnassignWorkflowCommand(id, userId), ct);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>
    /// Escalate workflow to next level.
    /// </summary>
    [HttpPost("{id:guid}/escalate")]
    [ProducesResponseType(typeof(WorkflowInstanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Escalate(
        Guid id,
        [FromBody] string reason,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var handler = _serviceProvider.GetRequiredService<EscalateWorkflowHandler>();
        var result = await handler.Handle(new EscalateWorkflowCommand(id, userId, reason), ct);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>
    /// Get workflow queue for a specific role.
    /// </summary>
    [HttpGet("queue/{role}")]
    [ProducesResponseType(typeof(List<WorkflowInstanceSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetQueueByRole(
        string role,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var handler = _serviceProvider.GetRequiredService<GetWorkflowQueueByRoleHandler>();
        var result = await handler.Handle(new GetWorkflowQueueByRoleQuery(role, pageNumber, pageSize), ct);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>
    /// Get current user's assigned workflows.
    /// </summary>
    [HttpGet("my-queue")]
    [ProducesResponseType(typeof(List<WorkflowInstanceSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyQueue(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var handler = _serviceProvider.GetRequiredService<GetMyWorkflowQueueHandler>();
        var result = await handler.Handle(new GetMyWorkflowQueueQuery(userId), ct);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>
    /// Get all overdue workflows.
    /// </summary>
    [HttpGet("overdue")]
    [Authorize(Roles = "RiskManager,SystemAdministrator")]
    [ProducesResponseType(typeof(List<WorkflowInstanceSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOverdue(CancellationToken ct)
    {
        var handler = _serviceProvider.GetRequiredService<GetOverdueWorkflowsHandler>();
        var result = await handler.Handle(new GetOverdueWorkflowsQuery(), ct);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>
    /// Get queue summary across all roles.
    /// </summary>
    [HttpGet("queue-summary")]
    [Authorize(Roles = "RiskManager,SystemAdministrator")]
    [ProducesResponseType(typeof(List<WorkflowQueueSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetQueueSummary(CancellationToken ct)
    {
        var handler = _serviceProvider.GetRequiredService<GetQueueSummaryHandler>();
        var result = await handler.Handle(new GetQueueSummaryQuery(), ct);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>
    /// Get workflow definition for a loan type.
    /// </summary>
    [HttpGet("definition/{applicationType}")]
    [ProducesResponseType(typeof(WorkflowDefinitionDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDefinition(string applicationType, CancellationToken ct)
    {
        if (!Enum.TryParse<LoanApplicationType>(applicationType, out var type))
            return BadRequest($"Invalid application type: {applicationType}");

        var handler = _serviceProvider.GetRequiredService<GetWorkflowDefinitionHandler>();
        var result = await handler.Handle(new GetWorkflowDefinitionQuery(type), ct);

        return result.IsSuccess ? Ok(result.Data) : NotFound(result.Error);
    }

    /// <summary>
    /// Seed the corporate loan workflow definition (admin only).
    /// </summary>
    [HttpPost("seed-corporate-workflow")]
    [Authorize(Roles = "SystemAdministrator")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> SeedCorporateWorkflow(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var handler = _serviceProvider.GetRequiredService<SeedCorporateLoanWorkflowHandler>();
        var result = await handler.Handle(new SeedCorporateLoanWorkflowCommand(userId), ct);

        return result.IsSuccess 
            ? Ok(new { WorkflowDefinitionId = result.Data }) 
            : BadRequest(result.Error);
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("sub") ?? User.FindFirst("userId");
        return userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId)
            ? userId
            : Guid.Empty;
    }

    private string GetCurrentUserRole()
    {
        var roleClaim = User.FindFirst("role") ?? User.FindFirst(System.Security.Claims.ClaimTypes.Role);
        return roleClaim?.Value ?? "User";
    }
}

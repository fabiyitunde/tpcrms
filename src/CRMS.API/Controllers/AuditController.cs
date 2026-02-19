using CRMS.Application.Audit.DTOs;
using CRMS.Application.Audit.Queries;
using CRMS.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRMS.API.Controllers;

/// <summary>
/// Audit trail endpoints for compliance and monitoring.
/// Restricted to authorized compliance and admin roles.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Auditor,RiskManager,SystemAdmin")]
public class AuditController : ControllerBase
{
    private readonly IServiceProvider _serviceProvider;

    public AuditController(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Get audit log by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AuditLogDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var handler = _serviceProvider.GetRequiredService<GetAuditLogByIdHandler>();
        var result = await handler.Handle(new GetAuditLogByIdQuery(id), ct);

        return result.IsSuccess ? Ok(result.Data) : NotFound(result.Error);
    }

    /// <summary>
    /// Get audit trail for a loan application.
    /// </summary>
    [HttpGet("by-loan-application/{loanApplicationId:guid}")]
    [ProducesResponseType(typeof(List<AuditLogSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByLoanApplication(Guid loanApplicationId, CancellationToken ct)
    {
        var handler = _serviceProvider.GetRequiredService<GetAuditLogsByLoanApplicationHandler>();
        var result = await handler.Handle(new GetAuditLogsByLoanApplicationQuery(loanApplicationId), ct);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>
    /// Get audit trail for a specific entity.
    /// </summary>
    [HttpGet("by-entity/{entityType}/{entityId:guid}")]
    [ProducesResponseType(typeof(List<AuditLogSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByEntity(string entityType, Guid entityId, CancellationToken ct)
    {
        var handler = _serviceProvider.GetRequiredService<GetAuditLogsByEntityHandler>();
        var result = await handler.Handle(new GetAuditLogsByEntityQuery(entityType, entityId), ct);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>
    /// Get audit trail for a user.
    /// </summary>
    [HttpGet("by-user/{userId:guid}")]
    [ProducesResponseType(typeof(List<AuditLogSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByUser(
        Guid userId,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        var handler = _serviceProvider.GetRequiredService<GetAuditLogsByUserHandler>();
        var result = await handler.Handle(new GetAuditLogsByUserQuery(userId, from, to), ct);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>
    /// Get recent audit logs.
    /// </summary>
    [HttpGet("recent")]
    [ProducesResponseType(typeof(List<AuditLogSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecent([FromQuery] int count = 100, CancellationToken ct = default)
    {
        var handler = _serviceProvider.GetRequiredService<GetRecentAuditLogsHandler>();
        var result = await handler.Handle(new GetRecentAuditLogsQuery(count), ct);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>
    /// Get failed actions for investigation.
    /// </summary>
    [HttpGet("failed")]
    [ProducesResponseType(typeof(List<AuditLogSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFailed(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        var handler = _serviceProvider.GetRequiredService<GetFailedActionsHandler>();
        var result = await handler.Handle(new GetFailedActionsQuery(from, to), ct);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>
    /// Search audit logs with filters.
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(AuditSearchResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] string? category = null,
        [FromQuery] string? action = null,
        [FromQuery] Guid? userId = null,
        [FromQuery] Guid? loanApplicationId = null,
        [FromQuery] string? entityType = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] bool? isSuccess = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        AuditCategory? categoryEnum = null;
        if (!string.IsNullOrEmpty(category) && Enum.TryParse<AuditCategory>(category, out var c))
            categoryEnum = c;

        AuditAction? actionEnum = null;
        if (!string.IsNullOrEmpty(action) && Enum.TryParse<AuditAction>(action, out var a))
            actionEnum = a;

        var handler = _serviceProvider.GetRequiredService<SearchAuditLogsHandler>();
        var result = await handler.Handle(new SearchAuditLogsQuery(
            categoryEnum, actionEnum, userId, loanApplicationId, entityType,
            from, to, isSuccess, pageNumber, pageSize), ct);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>
    /// Get data access logs for a user.
    /// </summary>
    [HttpGet("data-access/by-user/{userId:guid}")]
    [ProducesResponseType(typeof(List<DataAccessLogDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDataAccessByUser(
        Guid userId,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        var handler = _serviceProvider.GetRequiredService<GetDataAccessLogsByUserHandler>();
        var result = await handler.Handle(new GetDataAccessLogsByUserQuery(userId, from, to), ct);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>
    /// Get data access logs for a loan application.
    /// </summary>
    [HttpGet("data-access/by-loan-application/{loanApplicationId:guid}")]
    [ProducesResponseType(typeof(List<DataAccessLogDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDataAccessByLoanApplication(Guid loanApplicationId, CancellationToken ct)
    {
        var handler = _serviceProvider.GetRequiredService<GetDataAccessLogsByLoanApplicationHandler>();
        var result = await handler.Handle(new GetDataAccessLogsByLoanApplicationQuery(loanApplicationId), ct);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }
}

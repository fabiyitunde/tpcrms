using CRMS.Application.Notification.DTOs;
using CRMS.Application.Notification.Interfaces;
using CRMS.Application.Notification.Queries;
using CRMS.Domain.Aggregates.Notification;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRMS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly INotificationRepository _notificationRepository;
    private readonly INotificationTemplateRepository _templateRepository;
    private readonly IUnitOfWork _unitOfWork;

    public NotificationController(
        INotificationService notificationService,
        INotificationRepository notificationRepository,
        INotificationTemplateRepository templateRepository,
        IUnitOfWork unitOfWork)
    {
        _notificationService = notificationService;
        _notificationRepository = notificationRepository;
        _templateRepository = templateRepository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Get notification by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<NotificationDto>> GetById(Guid id, CancellationToken ct)
    {
        var handler = new GetNotificationByIdHandler(_notificationRepository);
        var result = await handler.Handle(new GetNotificationByIdQuery(id), ct);
        
        if (!result.IsSuccess)
            return NotFound(result.Error);
        
        return Ok(result.Data);
    }

    /// <summary>
    /// Get notifications for the current user.
    /// </summary>
    [HttpGet("my")]
    public async Task<ActionResult<List<NotificationSummaryDto>>> GetMyNotifications(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var handler = new GetNotificationsByUserHandler(_notificationRepository);
        var result = await handler.Handle(new GetNotificationsByUserQuery(userId), ct);
        
        return Ok(result.Data);
    }

    /// <summary>
    /// Get unread notification count for the current user.
    /// </summary>
    [HttpGet("my/unread-count")]
    public async Task<ActionResult<UnreadCountDto>> GetMyUnreadCount(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var handler = new GetUnreadCountHandler(_notificationRepository);
        var result = await handler.Handle(new GetUnreadCountQuery(userId), ct);
        
        return Ok(result.Data);
    }

    /// <summary>
    /// Get notifications for a loan application.
    /// </summary>
    [HttpGet("loan-application/{loanApplicationId:guid}")]
    public async Task<ActionResult<List<NotificationSummaryDto>>> GetByLoanApplication(Guid loanApplicationId, CancellationToken ct)
    {
        var handler = new GetNotificationsByLoanApplicationHandler(_notificationRepository);
        var result = await handler.Handle(new GetNotificationsByLoanApplicationQuery(loanApplicationId), ct);
        
        return Ok(result.Data);
    }

    /// <summary>
    /// Mark a notification as read.
    /// </summary>
    [HttpPost("{id:guid}/mark-read")]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken ct)
    {
        var notification = await _notificationRepository.GetByIdAsync(id, ct);
        if (notification == null)
            return NotFound();

        var result = notification.MarkAsRead();
        if (!result.IsSuccess)
            return BadRequest(result.Error);

        _notificationRepository.Update(notification);
        await _unitOfWork.SaveChangesAsync(ct);

        return Ok();
    }

    /// <summary>
    /// Send a notification (admin only).
    /// </summary>
    [HttpPost("send")]
    [Authorize(Roles = "SystemAdministrator")]
    public async Task<ActionResult<Guid>> SendNotification([FromBody] SendNotificationRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<NotificationType>(request.Type, out var type))
            return BadRequest("Invalid notification type");

        if (!Enum.TryParse<NotificationChannel>(request.Channel, out var channel))
            return BadRequest("Invalid notification channel");

        if (!Enum.TryParse<NotificationPriority>(request.Priority, out var priority))
            priority = NotificationPriority.Normal;

        var notificationId = await _notificationService.SendAsync(
            type,
            channel,
            request.RecipientAddress,
            request.RecipientName,
            request.TemplateCode,
            request.Variables,
            request.RecipientUserId,
            request.LoanApplicationId,
            request.LoanApplicationNumber,
            priority,
            request.ScheduledAt,
            ct);

        return Ok(notificationId);
    }

    // Template endpoints

    /// <summary>
    /// Get all notification templates.
    /// </summary>
    [HttpGet("templates")]
    [Authorize(Roles = "SystemAdministrator")]
    public async Task<ActionResult<List<NotificationTemplateDto>>> GetAllTemplates(CancellationToken ct)
    {
        var handler = new GetAllNotificationTemplatesHandler(_templateRepository);
        var result = await handler.Handle(new GetAllNotificationTemplatesQuery(), ct);
        
        return Ok(result.Data);
    }

    /// <summary>
    /// Get notification template by ID.
    /// </summary>
    [HttpGet("templates/{id:guid}")]
    [Authorize(Roles = "SystemAdministrator")]
    public async Task<ActionResult<NotificationTemplateDto>> GetTemplateById(Guid id, CancellationToken ct)
    {
        var handler = new GetNotificationTemplateByIdHandler(_templateRepository);
        var result = await handler.Handle(new GetNotificationTemplateByIdQuery(id), ct);
        
        if (!result.IsSuccess)
            return NotFound(result.Error);
        
        return Ok(result.Data);
    }

    /// <summary>
    /// Create a notification template.
    /// </summary>
    [HttpPost("templates")]
    [Authorize(Roles = "SystemAdministrator")]
    public async Task<ActionResult<Guid>> CreateTemplate([FromBody] CreateNotificationTemplateRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<NotificationType>(request.Type, out var type))
            return BadRequest("Invalid notification type");

        if (!Enum.TryParse<NotificationChannel>(request.Channel, out var channel))
            return BadRequest("Invalid notification channel");

        var userId = GetCurrentUserId();

        var result = NotificationTemplate.Create(
            request.Code,
            request.Name,
            request.Description,
            type,
            channel,
            request.BodyTemplate,
            userId,
            request.Subject,
            request.BodyHtmlTemplate,
            request.AvailableVariables,
            request.Language);

        if (!result.IsSuccess)
            return BadRequest(result.Error);

        await _templateRepository.AddAsync(result.Value, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetTemplateById), new { id = result.Value.Id }, result.Value.Id);
    }

    /// <summary>
    /// Update a notification template.
    /// </summary>
    [HttpPut("templates/{id:guid}")]
    [Authorize(Roles = "SystemAdministrator")]
    public async Task<IActionResult> UpdateTemplate(Guid id, [FromBody] UpdateNotificationTemplateRequest request, CancellationToken ct)
    {
        var template = await _templateRepository.GetByIdAsync(id, ct);
        if (template == null)
            return NotFound();

        var userId = GetCurrentUserId();

        var result = template.Update(
            request.Name,
            request.Description,
            request.BodyTemplate,
            userId,
            request.Subject,
            request.BodyHtmlTemplate,
            request.AvailableVariables);

        if (!result.IsSuccess)
            return BadRequest(result.Error);

        _templateRepository.Update(template);
        await _unitOfWork.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>
    /// Deactivate a notification template.
    /// </summary>
    [HttpPost("templates/{id:guid}/deactivate")]
    [Authorize(Roles = "SystemAdministrator")]
    public async Task<IActionResult> DeactivateTemplate(Guid id, CancellationToken ct)
    {
        var template = await _templateRepository.GetByIdAsync(id, ct);
        if (template == null)
            return NotFound();

        template.Deactivate();
        _templateRepository.Update(template);
        await _unitOfWork.SaveChangesAsync(ct);

        return NoContent();
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }
}

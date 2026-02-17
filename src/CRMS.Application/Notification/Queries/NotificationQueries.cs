using CRMS.Application.Common;
using CRMS.Application.Notification.DTOs;
using CRMS.Domain.Interfaces;
using N = CRMS.Domain.Aggregates.Notification;

namespace CRMS.Application.Notification.Queries;

public record GetNotificationByIdQuery(Guid Id) : IRequest<ApplicationResult<NotificationDto>>;

public class GetNotificationByIdHandler : IRequestHandler<GetNotificationByIdQuery, ApplicationResult<NotificationDto>>
{
    private readonly INotificationRepository _repository;

    public GetNotificationByIdHandler(INotificationRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<NotificationDto>> Handle(GetNotificationByIdQuery request, CancellationToken ct = default)
    {
        var notification = await _repository.GetByIdAsync(request.Id, ct);
        if (notification == null)
            return ApplicationResult<NotificationDto>.Failure("Notification not found");

        return ApplicationResult<NotificationDto>.Success(MapToDto(notification));
    }

    private static NotificationDto MapToDto(N.Notification n) => new(
        n.Id,
        n.Type.ToString(),
        n.Channel.ToString(),
        n.Priority.ToString(),
        n.Status.ToString(),
        n.RecipientUserId,
        n.RecipientName,
        n.RecipientAddress,
        n.Subject,
        n.Body,
        n.LoanApplicationId,
        n.LoanApplicationNumber,
        n.ScheduledAt,
        n.SentAt,
        n.DeliveredAt,
        n.FailedAt,
        n.FailureReason,
        n.RetryCount,
        n.CreatedAt);
}

public record GetNotificationsByUserQuery(Guid UserId) : IRequest<ApplicationResult<List<NotificationSummaryDto>>>;

public class GetNotificationsByUserHandler : IRequestHandler<GetNotificationsByUserQuery, ApplicationResult<List<NotificationSummaryDto>>>
{
    private readonly INotificationRepository _repository;

    public GetNotificationsByUserHandler(INotificationRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<List<NotificationSummaryDto>>> Handle(GetNotificationsByUserQuery request, CancellationToken ct = default)
    {
        var notifications = await _repository.GetByRecipientUserIdAsync(request.UserId, ct);
        var dtos = notifications.Select(n => new NotificationSummaryDto(
            n.Id,
            n.Type.ToString(),
            n.Channel.ToString(),
            n.Status.ToString(),
            n.RecipientName,
            n.Subject,
            n.SentAt,
            n.CreatedAt)).ToList();

        return ApplicationResult<List<NotificationSummaryDto>>.Success(dtos);
    }
}

public record GetNotificationsByLoanApplicationQuery(Guid LoanApplicationId) : IRequest<ApplicationResult<List<NotificationSummaryDto>>>;

public class GetNotificationsByLoanApplicationHandler : IRequestHandler<GetNotificationsByLoanApplicationQuery, ApplicationResult<List<NotificationSummaryDto>>>
{
    private readonly INotificationRepository _repository;

    public GetNotificationsByLoanApplicationHandler(INotificationRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<List<NotificationSummaryDto>>> Handle(GetNotificationsByLoanApplicationQuery request, CancellationToken ct = default)
    {
        var notifications = await _repository.GetByLoanApplicationIdAsync(request.LoanApplicationId, ct);
        var dtos = notifications.Select(n => new NotificationSummaryDto(
            n.Id,
            n.Type.ToString(),
            n.Channel.ToString(),
            n.Status.ToString(),
            n.RecipientName,
            n.Subject,
            n.SentAt,
            n.CreatedAt)).ToList();

        return ApplicationResult<List<NotificationSummaryDto>>.Success(dtos);
    }
}

public record GetUnreadCountQuery(Guid UserId) : IRequest<ApplicationResult<UnreadCountDto>>;

public class GetUnreadCountHandler : IRequestHandler<GetUnreadCountQuery, ApplicationResult<UnreadCountDto>>
{
    private readonly INotificationRepository _repository;

    public GetUnreadCountHandler(INotificationRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<UnreadCountDto>> Handle(GetUnreadCountQuery request, CancellationToken ct = default)
    {
        var count = await _repository.GetUnreadCountAsync(request.UserId, ct);
        return ApplicationResult<UnreadCountDto>.Success(new UnreadCountDto(count));
    }
}

// Template queries
public record GetNotificationTemplateByIdQuery(Guid Id) : IRequest<ApplicationResult<NotificationTemplateDto>>;

public class GetNotificationTemplateByIdHandler : IRequestHandler<GetNotificationTemplateByIdQuery, ApplicationResult<NotificationTemplateDto>>
{
    private readonly INotificationTemplateRepository _repository;

    public GetNotificationTemplateByIdHandler(INotificationTemplateRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<NotificationTemplateDto>> Handle(GetNotificationTemplateByIdQuery request, CancellationToken ct = default)
    {
        var template = await _repository.GetByIdAsync(request.Id, ct);
        if (template == null)
            return ApplicationResult<NotificationTemplateDto>.Failure("Template not found");

        return ApplicationResult<NotificationTemplateDto>.Success(new NotificationTemplateDto(
            template.Id,
            template.Code,
            template.Name,
            template.Description,
            template.Type.ToString(),
            template.Channel.ToString(),
            template.Language,
            template.Subject,
            template.BodyTemplate,
            template.BodyHtmlTemplate,
            template.IsActive,
            template.Version));
    }
}

public record GetAllNotificationTemplatesQuery : IRequest<ApplicationResult<List<NotificationTemplateDto>>>;

public class GetAllNotificationTemplatesHandler : IRequestHandler<GetAllNotificationTemplatesQuery, ApplicationResult<List<NotificationTemplateDto>>>
{
    private readonly INotificationTemplateRepository _repository;

    public GetAllNotificationTemplatesHandler(INotificationTemplateRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<List<NotificationTemplateDto>>> Handle(GetAllNotificationTemplatesQuery request, CancellationToken ct = default)
    {
        var templates = await _repository.GetAllActiveAsync(ct);
        var dtos = templates.Select(t => new NotificationTemplateDto(
            t.Id,
            t.Code,
            t.Name,
            t.Description,
            t.Type.ToString(),
            t.Channel.ToString(),
            t.Language,
            t.Subject,
            t.BodyTemplate,
            t.BodyHtmlTemplate,
            t.IsActive,
            t.Version)).ToList();

        return ApplicationResult<List<NotificationTemplateDto>>.Success(dtos);
    }
}

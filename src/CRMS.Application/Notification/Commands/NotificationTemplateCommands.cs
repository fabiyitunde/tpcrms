using CRMS.Application.Common;
using CRMS.Application.Notification.DTOs;
using CRMS.Domain.Aggregates.Notification;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Notification.Commands;

public record CreateNotificationTemplateCommand(
    string Code,
    string Name,
    string Description,
    string Type,
    string Channel,
    string BodyTemplate,
    Guid CreatedByUserId,
    string? Subject = null,
    string? BodyHtmlTemplate = null,
    string? AvailableVariables = null,
    string Language = "en"
) : IRequest<ApplicationResult<NotificationTemplateDto>>;

public class CreateNotificationTemplateHandler : IRequestHandler<CreateNotificationTemplateCommand, ApplicationResult<NotificationTemplateDto>>
{
    private readonly INotificationTemplateRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateNotificationTemplateHandler(INotificationTemplateRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<NotificationTemplateDto>> Handle(CreateNotificationTemplateCommand request, CancellationToken ct = default)
    {
        if (!Enum.TryParse<NotificationType>(request.Type, out var type))
            return ApplicationResult<NotificationTemplateDto>.Failure($"Invalid notification type: {request.Type}");

        if (!Enum.TryParse<NotificationChannel>(request.Channel, out var channel))
            return ApplicationResult<NotificationTemplateDto>.Failure($"Invalid notification channel: {request.Channel}");

        // Check for duplicate code
        var existing = await _repository.GetByCodeAsync(request.Code, channel, request.Language, ct);
        if (existing != null)
            return ApplicationResult<NotificationTemplateDto>.Failure($"Template with code '{request.Code}' already exists for channel '{request.Channel}'");

        var templateResult = NotificationTemplate.Create(
            request.Code,
            request.Name,
            request.Description,
            type,
            channel,
            request.BodyTemplate,
            request.CreatedByUserId,
            request.Subject,
            request.BodyHtmlTemplate,
            request.AvailableVariables,
            request.Language
        );

        if (templateResult.IsFailure)
            return ApplicationResult<NotificationTemplateDto>.Failure(templateResult.Error);

        var template = templateResult.Value;
        await _repository.AddAsync(template, ct);
        await _unitOfWork.SaveChangesAsync(ct);

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
            template.Version
        ));
    }
}

public record UpdateNotificationTemplateCommand(
    Guid Id,
    string Name,
    string Description,
    string BodyTemplate,
    Guid ModifiedByUserId,
    string? Subject = null,
    string? BodyHtmlTemplate = null,
    string? AvailableVariables = null
) : IRequest<ApplicationResult<NotificationTemplateDto>>;

public class UpdateNotificationTemplateHandler : IRequestHandler<UpdateNotificationTemplateCommand, ApplicationResult<NotificationTemplateDto>>
{
    private readonly INotificationTemplateRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateNotificationTemplateHandler(INotificationTemplateRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<NotificationTemplateDto>> Handle(UpdateNotificationTemplateCommand request, CancellationToken ct = default)
    {
        var template = await _repository.GetByIdAsync(request.Id, ct);
        if (template == null)
            return ApplicationResult<NotificationTemplateDto>.Failure("Template not found");

        var updateResult = template.Update(
            request.Name,
            request.Description,
            request.BodyTemplate,
            request.ModifiedByUserId,
            request.Subject,
            request.BodyHtmlTemplate,
            request.AvailableVariables
        );

        if (updateResult.IsFailure)
            return ApplicationResult<NotificationTemplateDto>.Failure(updateResult.Error);

        _repository.Update(template);
        await _unitOfWork.SaveChangesAsync(ct);

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
            template.Version
        ));
    }
}

public record ToggleNotificationTemplateCommand(Guid Id, bool Activate) : IRequest<ApplicationResult>;

public class ToggleNotificationTemplateHandler : IRequestHandler<ToggleNotificationTemplateCommand, ApplicationResult>
{
    private readonly INotificationTemplateRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ToggleNotificationTemplateHandler(INotificationTemplateRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(ToggleNotificationTemplateCommand request, CancellationToken ct = default)
    {
        var template = await _repository.GetByIdAsync(request.Id, ct);
        if (template == null)
            return ApplicationResult.Failure("Template not found");

        if (request.Activate)
            template.Activate();
        else
            template.Deactivate();

        _repository.Update(template);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}

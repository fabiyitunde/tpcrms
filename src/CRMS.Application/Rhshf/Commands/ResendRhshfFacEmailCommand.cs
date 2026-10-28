using CRMS.Application.Common;
using CRMS.Application.Rhshf.Queries;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>
/// Sends an email to the FAC again, exactly as it was first written — for one that was not delivered,
/// or one the FAC says never arrived. A new record is queued; the original stays, so the case keeps
/// the history of every attempt. QA asked for a controlled test that reaches a real inbox, and before
/// 28 Oct TEST "sent" every email to a stand-in that only logged it (CRMSQA#2).
/// </summary>
public record ResendRhshfFacEmailCommand(string Reference, Guid NotificationId) : IRequest<ApplicationResult>;

public class ResendRhshfFacEmailHandler : IRequestHandler<ResendRhshfFacEmailCommand, ApplicationResult>
{
    private readonly INotificationRepository _notifications;
    private readonly IUnitOfWork _uow;

    public ResendRhshfFacEmailHandler(INotificationRepository notifications, IUnitOfWork uow)
    {
        _notifications = notifications;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(ResendRhshfFacEmailCommand request, CancellationToken ct = default)
    {
        var onCase = await _notifications.GetByLoanApplicationNumberAsync(request.Reference, ct);
        var original = onCase.FirstOrDefault(n => n.Id == request.NotificationId);
        if (original is null || !original.TemplateCode.StartsWith(GetRhshfFacEmailsHandler.TemplatePrefix, StringComparison.Ordinal))
            return ApplicationResult.Failure("That email was not found on this case.");

        // A second click while the first copy is still waiting would only send the FAC the same email twice.
        if (onCase.Any(n => n.Subject == original.Subject
                            && n.Status is NotificationStatus.Pending or NotificationStatus.Sending or NotificationStatus.Retry))
            return ApplicationResult.Failure("This email is already queued to send.");

        var copy = Domain.Aggregates.Notification.Notification.Create(
            original.Type, original.Channel, original.Priority, original.RecipientName, original.RecipientAddress,
            original.TemplateCode, original.Subject, original.Body, original.BodyHtml,
            original.RecipientUserId, original.LoanApplicationId, original.LoanApplicationNumber, original.ContextData);
        if (copy.IsFailure)
            return ApplicationResult.Failure(copy.Error);

        await _notifications.AddAsync(copy.Value, ct);
        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}

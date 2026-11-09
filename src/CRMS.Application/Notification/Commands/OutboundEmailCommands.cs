using CRMS.Application.Common;
using CRMS.Application.Notification.Queries;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Notification.Commands;

/// <summary>Writes an audit entry for an administrator's action on the outbound email queue.</summary>
public interface IOutboundEmailAudit
{
    Task RecordAsync(OutboundEmailAuditEntry entry, CancellationToken ct = default);
}

public record OutboundEmailAuditEntry(string Event, Guid NotificationId, string Recipient, string Description, Guid? UserId, string? UserName)
{
    public const string Resent = "EMAIL_RESENT";
    public const string Released = "EMAIL_RELEASED";
    public const string TestSent = "EMAIL_TEST_SENT";
}

/// <summary>
/// Sends an email again, exactly as first written, as a new queue entry; the original keeps its
/// history. Allowed once the first attempt has ended (Sent, Failed or Cancelled): a recipient who
/// says a sent email never arrived gets the same copy, and a failed one goes out once the cause is
/// fixed. Refused while a copy is still waiting, and for a password reset whose link has expired,
/// because delivering a dead link only sends the user back to the Forgot Password page.
/// </summary>
public record ResendOutboundEmailCommand(Guid NotificationId, Guid UserId, string UserName) : IRequest<ApplicationResult<Guid>>;

public class ResendOutboundEmailHandler : IRequestHandler<ResendOutboundEmailCommand, ApplicationResult<Guid>>
{
    public const string PasswordResetTemplate = "PASSWORD_RESET";
    public static readonly TimeSpan PasswordResetLinkLife = TimeSpan.FromHours(1);

    private readonly INotificationRepository _notifications;
    private readonly IOutboundEmailAudit _audit;
    private readonly IUnitOfWork _uow;

    public ResendOutboundEmailHandler(INotificationRepository notifications, IOutboundEmailAudit audit, IUnitOfWork uow)
    {
        _notifications = notifications;
        _audit = audit;
        _uow = uow;
    }

    public async Task<ApplicationResult<Guid>> Handle(ResendOutboundEmailCommand request, CancellationToken ct = default)
    {
        var original = await _notifications.GetByIdAsync(request.NotificationId, ct);
        if (original is null || original.Channel != NotificationChannel.Email)
            return ApplicationResult<Guid>.Failure("That email was not found.");

        if (original.Status is not (NotificationStatus.Sent or NotificationStatus.Delivered or NotificationStatus.Read
                                    or NotificationStatus.Failed or NotificationStatus.Cancelled))
            return ApplicationResult<Guid>.Failure("This email has not finished its first attempt yet. Wait for it, or release it if it is stuck.");

        if (original.TemplateCode == PasswordResetTemplate && original.CreatedAt < DateTime.UtcNow - PasswordResetLinkLife)
            return ApplicationResult<Guid>.Failure("This password reset link has expired, so sending it again would not help. Ask the user to request a new reset.");

        if (await _notifications.IsQueuedAsync(NotificationChannel.Email, original.RecipientAddress, original.Subject, ct))
            return ApplicationResult<Guid>.Failure("A copy of this email is already waiting to go out.");

        var copy = Domain.Aggregates.Notification.Notification.Create(
            original.Type, original.Channel, original.Priority, original.RecipientName, original.RecipientAddress,
            original.TemplateCode, original.Subject, original.Body, original.BodyHtml,
            original.RecipientUserId, original.LoanApplicationId, original.LoanApplicationNumber, original.ContextData);
        if (copy.IsFailure)
            return ApplicationResult<Guid>.Failure(copy.Error);

        await _notifications.AddAsync(copy.Value, ct);
        await _uow.SaveChangesAsync(ct);

        await _audit.RecordAsync(new OutboundEmailAuditEntry(
            OutboundEmailAuditEntry.Resent, copy.Value.Id, original.RecipientAddress,
            $"Email \"{original.Subject}\" ({original.TemplateCode}) to {original.RecipientAddress} was queued again by {request.UserName}; the first copy ({original.Id}) was {original.Status}.",
            request.UserId, request.UserName), ct);

        return ApplicationResult<Guid>.Success(copy.Value.Id);
    }
}

/// <summary>
/// Returns an email stuck in Sending to the queue so the next pass sends it. Only for rows that have
/// sat in Sending longer than <see cref="OutboundEmailDto.StuckAfter"/>; a row younger than that
/// may still be mid-send.
/// </summary>
public record ReleaseStuckOutboundEmailCommand(Guid NotificationId, Guid UserId, string UserName) : IRequest<ApplicationResult>;

public class ReleaseStuckOutboundEmailHandler : IRequestHandler<ReleaseStuckOutboundEmailCommand, ApplicationResult>
{
    private readonly INotificationRepository _notifications;
    private readonly IOutboundEmailAudit _audit;
    private readonly IUnitOfWork _uow;

    public ReleaseStuckOutboundEmailHandler(INotificationRepository notifications, IOutboundEmailAudit audit, IUnitOfWork uow)
    {
        _notifications = notifications;
        _audit = audit;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(ReleaseStuckOutboundEmailCommand request, CancellationToken ct = default)
    {
        var n = await _notifications.GetByIdAsync(request.NotificationId, ct);
        if (n is null || n.Channel != NotificationChannel.Email)
            return ApplicationResult.Failure("That email was not found.");

        if (!OutboundEmailDto.From(n, DateTime.UtcNow).IsStuck)
            return ApplicationResult.Failure(n.Status == NotificationStatus.Sending
                ? $"This email went into Sending less than {OutboundEmailDto.StuckAfter.TotalMinutes:0} minutes ago and may still be on its way. Try again later."
                : "Only an email stuck in Sending can be released.");

        var result = n.ReturnToQueue();
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        _notifications.Update(n);
        await _uow.SaveChangesAsync(ct);

        await _audit.RecordAsync(new OutboundEmailAuditEntry(
            OutboundEmailAuditEntry.Released, n.Id, n.RecipientAddress,
            $"Email \"{n.Subject}\" ({n.TemplateCode}) to {n.RecipientAddress}, stuck in Sending, was returned to the queue by {request.UserName}.",
            request.UserId, request.UserName), ct);

        return ApplicationResult.Success();
    }
}

using CRMS.Application.Notification.Commands;
using CRMS.Domain.Enums;
using CRMS.Domain.Services;

namespace CRMS.Infrastructure.Services;

/// <summary>Audit trail for Admin > Outbound Email actions, through the shared <see cref="AuditService"/>.</summary>
public class OutboundEmailAudit : IOutboundEmailAudit
{
    private readonly AuditService _audit;

    public OutboundEmailAudit(AuditService audit) => _audit = audit;

    public Task RecordAsync(OutboundEmailAuditEntry entry, CancellationToken ct = default) =>
        _audit.LogAsync(
            entry.Event == OutboundEmailAuditEntry.Resent ? AuditAction.Create : AuditAction.Update,
            AuditCategory.System,
            entry.Description,
            "Notification",
            entityId: entry.NotificationId,
            entityReference: entry.Recipient,
            userId: entry.UserId,
            userName: entry.UserName,
            additionalData: new { Event = entry.Event },
            ct: ct);
}

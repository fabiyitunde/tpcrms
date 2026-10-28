using CRMS.Application.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Queries;

/// <summary>Every email CRMS has queued for the FAC on a case, newest first, with where it got to.
/// QA asked whether an offer email was generated, sent or failed; a status change on the case is not
/// evidence of that, this record is (CRMSQA#2).</summary>
public record GetRhshfFacEmailsQuery(string Reference) : IRequest<ApplicationResult<List<RhshfFacEmailDto>>>;

public record RhshfFacEmailDto(
    string Subject,
    string RecipientAddress,
    NotificationStatus Status,
    DateTime QueuedAt,
    DateTime? SentAt,
    DateTime? FailedAt,
    string? FailureReason,
    int RetryCount);

public class GetRhshfFacEmailsHandler : IRequestHandler<GetRhshfFacEmailsQuery, ApplicationResult<List<RhshfFacEmailDto>>>
{
    /// <summary>Every RH-SHF FAC template starts with this; staff alerts on the same case would not.</summary>
    public const string TemplatePrefix = "RHSHF_";

    private readonly INotificationRepository _notifications;

    public GetRhshfFacEmailsHandler(INotificationRepository notifications) => _notifications = notifications;

    public async Task<ApplicationResult<List<RhshfFacEmailDto>>> Handle(GetRhshfFacEmailsQuery request, CancellationToken ct = default)
    {
        var all = await _notifications.GetByLoanApplicationNumberAsync(request.Reference, ct);
        var emails = all
            .Where(n => n.Channel == NotificationChannel.Email && n.TemplateCode.StartsWith(TemplatePrefix, StringComparison.Ordinal))
            .Select(n => new RhshfFacEmailDto(
                n.Subject, n.RecipientAddress, n.Status, n.CreatedAt, n.SentAt, n.FailedAt, n.FailureReason, n.RetryCount))
            .ToList();

        return ApplicationResult<List<RhshfFacEmailDto>>.Success(emails);
    }
}

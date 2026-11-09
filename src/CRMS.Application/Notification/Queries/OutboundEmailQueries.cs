using CRMS.Application.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using N = CRMS.Domain.Aggregates.Notification;

namespace CRMS.Application.Notification.Queries;

// Admin > Outbound Email. The Notifications table is the only record of what the system tried to
// send and what the provider answered; until this page existed, an email outage was invisible
// (production SES refused every send for weeks in late 2026 and nobody could see it).

/// <summary>One row of the outbound queue.</summary>
public record OutboundEmailDto(
    Guid Id,
    DateTime CreatedAt,
    string Status,
    string Priority,
    string TemplateCode,
    string RecipientName,
    string RecipientAddress,
    string Subject,
    int RetryCount,
    int MaxRetries,
    DateTime? NextRetryAt,
    DateTime? SentAt,
    DateTime? FailedAt,
    string? FailureReason,
    string? ProviderName,
    string? ExternalMessageId,
    string? CaseReference,
    bool IsStuck)
{
    /// <summary>A row left in Sending this long was abandoned by a process that died mid-send.</summary>
    public static readonly TimeSpan StuckAfter = TimeSpan.FromMinutes(10);

    public static OutboundEmailDto From(N.Notification n, DateTime nowUtc) => new(
        n.Id, n.CreatedAt, n.Status.ToString(), n.Priority.ToString(), n.TemplateCode,
        n.RecipientName, n.RecipientAddress, n.Subject,
        n.RetryCount, n.MaxRetries, n.NextRetryAt, n.SentAt, n.FailedAt, n.FailureReason,
        n.ProviderName, n.ExternalMessageId, n.LoanApplicationNumber,
        IsStuck: n.Status == NotificationStatus.Sending && (n.FailedAt ?? n.CreatedAt) < nowUtc - StuckAfter);
}

/// <summary>
/// The row plus what was actually sent and what the provider said. For an email that carries a
/// credential (a reset link, a temporary password, a one-time setup link) the body is withheld:
/// an administrator who could read it here could take the recipient's account or credentials.
/// </summary>
public record OutboundEmailDetailDto(
    OutboundEmailDto Summary,
    string Body,
    string? BodyHtml,
    string? ProviderResponse,
    Guid? RecipientUserId,
    bool BodyWithheld)
{
    /// <summary>Templates whose message carries a secret. Add any new one that does.</summary>
    public static readonly IReadOnlySet<string> CredentialTemplates = new HashSet<string>(StringComparer.Ordinal)
    {
        "PASSWORD_RESET",
        "ACCOUNT_CREATED",
        "API_CONSUMER_SETUP_LINK",
    };
}

public record OutboundEmailPageDto(int Page, int PageSize, int Total, IReadOnlyList<OutboundEmailDto> Items);

/// <summary>Counts for the tiles at the top of the page.</summary>
public record OutboundEmailSummaryDto(
    int SentLast24Hours,
    int FailedLast24Hours,
    int FailedLast7Days,
    int Waiting,
    int Stuck);

public record SearchOutboundEmailsQuery(
    string? Status, string? TemplateCode, string? Recipient, DateTime? FromUtc, DateTime? ToUtc, int Page, int PageSize)
    : IRequest<ApplicationResult<OutboundEmailPageDto>>;

public class SearchOutboundEmailsHandler : IRequestHandler<SearchOutboundEmailsQuery, ApplicationResult<OutboundEmailPageDto>>
{
    private readonly INotificationRepository _notifications;

    public SearchOutboundEmailsHandler(INotificationRepository notifications) => _notifications = notifications;

    public async Task<ApplicationResult<OutboundEmailPageDto>> Handle(SearchOutboundEmailsQuery request, CancellationToken ct = default)
    {
        NotificationStatus? status = null;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<NotificationStatus>(request.Status, true, out var parsed))
                return ApplicationResult<OutboundEmailPageDto>.Failure($"Unknown status '{request.Status}'.");
            status = parsed;
        }

        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 200);
        var (items, total) = await _notifications.SearchAsync(new NotificationSearch(
            NotificationChannel.Email, status, request.TemplateCode, request.Recipient,
            request.FromUtc, request.ToUtc, page, size), ct);

        var now = DateTime.UtcNow;
        return ApplicationResult<OutboundEmailPageDto>.Success(
            new OutboundEmailPageDto(page, size, total, items.Select(n => OutboundEmailDto.From(n, now)).ToList()));
    }
}

public record GetOutboundEmailQuery(Guid Id) : IRequest<ApplicationResult<OutboundEmailDetailDto>>;

public class GetOutboundEmailHandler : IRequestHandler<GetOutboundEmailQuery, ApplicationResult<OutboundEmailDetailDto>>
{
    private readonly INotificationRepository _notifications;

    public GetOutboundEmailHandler(INotificationRepository notifications) => _notifications = notifications;

    public async Task<ApplicationResult<OutboundEmailDetailDto>> Handle(GetOutboundEmailQuery request, CancellationToken ct = default)
    {
        var n = await _notifications.GetByIdAsync(request.Id, ct);
        if (n is null || n.Channel != NotificationChannel.Email)
            return ApplicationResult<OutboundEmailDetailDto>.Failure("That email was not found.");

        var withheld = OutboundEmailDetailDto.CredentialTemplates.Contains(n.TemplateCode);
        return ApplicationResult<OutboundEmailDetailDto>.Success(new OutboundEmailDetailDto(
            OutboundEmailDto.From(n, DateTime.UtcNow),
            withheld ? string.Empty : n.Body,
            withheld ? null : n.BodyHtml,
            n.ProviderResponse, n.RecipientUserId, withheld));
    }
}

public record GetOutboundEmailSummaryQuery : IRequest<ApplicationResult<OutboundEmailSummaryDto>>;

public class GetOutboundEmailSummaryHandler : IRequestHandler<GetOutboundEmailSummaryQuery, ApplicationResult<OutboundEmailSummaryDto>>
{
    private readonly INotificationRepository _notifications;

    public GetOutboundEmailSummaryHandler(INotificationRepository notifications) => _notifications = notifications;

    public async Task<ApplicationResult<OutboundEmailSummaryDto>> Handle(GetOutboundEmailSummaryQuery request, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var day = await _notifications.CountByStatusAsync(NotificationChannel.Email, now.AddHours(-24), ct);
        var week = await _notifications.CountByStatusAsync(NotificationChannel.Email, now.AddDays(-7), ct);
        var all = await _notifications.CountByStatusAsync(NotificationChannel.Email, null, ct);

        static int Of(IReadOnlyDictionary<NotificationStatus, int> d, params NotificationStatus[] statuses)
            => statuses.Sum(s => d.TryGetValue(s, out var c) ? c : 0);

        // "Stuck" needs each row's age, not a count by status; the Sending rows are few, so read them.
        var (sending, _) = await _notifications.SearchAsync(
            new NotificationSearch(NotificationChannel.Email, NotificationStatus.Sending, PageSize: 200), ct);
        var stuck = sending.Count(n => OutboundEmailDto.From(n, now).IsStuck);

        return ApplicationResult<OutboundEmailSummaryDto>.Success(new OutboundEmailSummaryDto(
            SentLast24Hours: Of(day, NotificationStatus.Sent, NotificationStatus.Delivered, NotificationStatus.Read),
            FailedLast24Hours: Of(day, NotificationStatus.Failed),
            FailedLast7Days: Of(week, NotificationStatus.Failed),
            Waiting: Of(all, NotificationStatus.Pending, NotificationStatus.Retry, NotificationStatus.Scheduled) + Of(all, NotificationStatus.Sending) - stuck,
            Stuck: stuck));
    }
}

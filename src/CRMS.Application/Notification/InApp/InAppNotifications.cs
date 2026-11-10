using CRMS.Application.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Notification.InApp;

/// <summary>One line on the intranet's bell.</summary>
public record InAppNotificationDto(
    Guid Id,
    string Title,
    string Message,
    string? ActionUrl,
    string Type,
    string Priority,
    DateTime CreatedAt,
    DateTime? ReadAt,
    string? ApplicationNumber)
{
    public bool IsUnread => ReadAt is null;

    public static InAppNotificationDto From(Domain.Aggregates.Notification.Notification n) => new(
        n.Id, n.Subject, n.Body, n.ActionUrl, n.Type.ToString(), n.Priority.ToString(),
        n.CreatedAt, n.ReadAt, n.LoanApplicationNumber);
}

public record InAppNotificationPageDto(IReadOnlyList<InAppNotificationDto> Items, int Total, int Unread);

// ── Queries ─────────────────────────────────────────────────────────────────

/// <summary>A page of one user's notifications, newest first, with the unread total for the badge.</summary>
public record GetMyInAppNotificationsQuery(Guid UserId, bool UnreadOnly = false, int Page = 1, int PageSize = 10)
    : IRequest<ApplicationResult<InAppNotificationPageDto>>;

public class GetMyInAppNotificationsHandler : IRequestHandler<GetMyInAppNotificationsQuery, ApplicationResult<InAppNotificationPageDto>>
{
    private readonly INotificationRepository _repo;
    public GetMyInAppNotificationsHandler(INotificationRepository repo) => _repo = repo;

    public async Task<ApplicationResult<InAppNotificationPageDto>> Handle(GetMyInAppNotificationsQuery request, CancellationToken ct = default)
    {
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var (items, total) = await _repo.GetInAppForUserAsync(request.UserId, request.UnreadOnly, Math.Max(1, request.Page), pageSize, ct);
        var unread = await _repo.GetUnreadCountAsync(request.UserId, ct);
        return ApplicationResult<InAppNotificationPageDto>.Success(
            new InAppNotificationPageDto(items.Select(InAppNotificationDto.From).ToList(), total, unread));
    }
}

// ── Commands ────────────────────────────────────────────────────────────────

/// <summary>Marks one notification read. Only its recipient may; anyone else gets "not found".</summary>
public record MarkInAppNotificationReadCommand(Guid NotificationId, Guid UserId) : IRequest<ApplicationResult>;

public class MarkInAppNotificationReadHandler : IRequestHandler<MarkInAppNotificationReadCommand, ApplicationResult>
{
    private readonly INotificationRepository _repo;
    private readonly IUnitOfWork _uow;

    public MarkInAppNotificationReadHandler(INotificationRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(MarkInAppNotificationReadCommand request, CancellationToken ct = default)
    {
        var n = await _repo.GetByIdAsync(request.NotificationId, ct);
        if (n is null || n.RecipientUserId != request.UserId || n.Channel != NotificationChannel.InApp)
            return ApplicationResult.Failure("Notification not found.");

        if (n.ReadAt is not null)
            return ApplicationResult.Success();

        var result = n.MarkAsRead();
        if (result.IsFailure) return ApplicationResult.Failure(result.Error);

        _repo.Update(n);
        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}

public record MarkAllInAppNotificationsReadCommand(Guid UserId) : IRequest<ApplicationResult<int>>;

public class MarkAllInAppNotificationsReadHandler : IRequestHandler<MarkAllInAppNotificationsReadCommand, ApplicationResult<int>>
{
    private readonly INotificationRepository _repo;
    public MarkAllInAppNotificationsReadHandler(INotificationRepository repo) => _repo = repo;

    public async Task<ApplicationResult<int>> Handle(MarkAllInAppNotificationsReadCommand request, CancellationToken ct = default)
    {
        var count = await _repo.MarkAllInAppReadAsync(request.UserId, ct);
        return ApplicationResult<int>.Success(count);
    }
}

/// <summary>Housekeeping: read notifications older than the retention are deleted, unread ones never.</summary>
public record PurgeReadInAppNotificationsCommand(int RetentionDays = 90) : IRequest<ApplicationResult<int>>;

public class PurgeReadInAppNotificationsHandler : IRequestHandler<PurgeReadInAppNotificationsCommand, ApplicationResult<int>>
{
    private readonly INotificationRepository _repo;
    public PurgeReadInAppNotificationsHandler(INotificationRepository repo) => _repo = repo;

    public async Task<ApplicationResult<int>> Handle(PurgeReadInAppNotificationsCommand request, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-Math.Max(1, request.RetentionDays));
        var count = await _repo.DeleteReadInAppOlderThanAsync(cutoff, ct);
        return ApplicationResult<int>.Success(count);
    }
}

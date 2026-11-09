using CRMS.Domain.Aggregates.Notification;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly CRMSDbContext _context;

    public NotificationRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Notifications.FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<IReadOnlyList<Notification>> GetByLoanApplicationNumberAsync(string loanApplicationNumber, CancellationToken ct = default)
        => await _context.Notifications
            .Where(x => x.LoanApplicationNumber == loanApplicationNumber)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Notification>> GetByLoanApplicationIdAsync(Guid loanApplicationId, CancellationToken ct = default)
    {
        return await _context.Notifications
            .Where(x => x.LoanApplicationId == loanApplicationId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Notification>> GetByRecipientUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.Notifications
            .Where(x => x.RecipientUserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Notification>> GetPendingAsync(int limit = 100, CancellationToken ct = default)
    {
        return await _context.Notifications
            .Where(x => x.Status == NotificationStatus.Pending)
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Notification>> GetForRetryAsync(DateTime asOf, int limit = 50, CancellationToken ct = default)
    {
        return await _context.Notifications
            .Where(x => x.Status == NotificationStatus.Retry)
            .Where(x => x.NextRetryAt == null || x.NextRetryAt <= asOf)
            .OrderBy(x => x.NextRetryAt ?? x.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Notification>> GetScheduledDueAsync(DateTime asOf, int limit = 100, CancellationToken ct = default)
    {
        return await _context.Notifications
            .Where(x => x.Status == NotificationStatus.Scheduled && x.ScheduledAt <= asOf)
            .OrderBy(x => x.ScheduledAt)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.Notifications
            .Where(x => x.RecipientUserId == userId)
            .Where(x => x.Status == NotificationStatus.Delivered || x.Status == NotificationStatus.Sent)
            .CountAsync(ct);
    }

    public async Task<(IReadOnlyList<Notification> Items, int Total)> SearchAsync(NotificationSearch search, CancellationToken ct = default)
    {
        var q = _context.Notifications.AsNoTracking().AsQueryable();
        if (search.Channel is { } channel) q = q.Where(x => x.Channel == channel);
        if (search.Status is { } status) q = q.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(search.TemplateCode))
        {
            var code = search.TemplateCode.Trim();
            q = q.Where(x => x.TemplateCode.Contains(code));
        }
        if (!string.IsNullOrWhiteSpace(search.Recipient))
        {
            var who = search.Recipient.Trim();
            q = q.Where(x => x.RecipientAddress.Contains(who) || x.RecipientName.Contains(who));
        }
        if (search.CreatedFromUtc is { } from) q = q.Where(x => x.CreatedAt >= from);
        if (search.CreatedToUtc is { } to) q = q.Where(x => x.CreatedAt < to);

        var page = Math.Max(1, search.Page);
        var size = Math.Clamp(search.PageSize, 1, 200);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(ct);
        return (items, total);
    }

    public async Task<IReadOnlyDictionary<NotificationStatus, int>> CountByStatusAsync(NotificationChannel channel, DateTime? createdSinceUtc, CancellationToken ct = default)
    {
        var q = _context.Notifications.AsNoTracking().Where(x => x.Channel == channel);
        if (createdSinceUtc is { } since) q = q.Where(x => x.CreatedAt >= since);
        var rows = await q.GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.Status, r => r.Count);
    }

    public Task<bool> IsQueuedAsync(NotificationChannel channel, string recipientAddress, string subject, CancellationToken ct = default)
        => _context.Notifications.AsNoTracking().AnyAsync(x =>
            x.Channel == channel && x.RecipientAddress == recipientAddress && x.Subject == subject
            && (x.Status == NotificationStatus.Pending || x.Status == NotificationStatus.Sending
                || x.Status == NotificationStatus.Retry || x.Status == NotificationStatus.Scheduled), ct);

    public async Task AddAsync(Notification notification, CancellationToken ct = default)
    {
        await _context.Notifications.AddAsync(notification, ct);
    }

    public void Update(Notification notification)
    {
        _context.Notifications.Update(notification);
    }
}

public class NotificationTemplateRepository : INotificationTemplateRepository
{
    private readonly CRMSDbContext _context;

    public NotificationTemplateRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<NotificationTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.NotificationTemplates.FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<NotificationTemplate?> GetByCodeAsync(string code, NotificationChannel channel, string language = "en", CancellationToken ct = default)
    {
        return await _context.NotificationTemplates
            .Where(x => x.Code == code && x.Channel == channel && x.Language == language && x.IsActive)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<NotificationTemplate>> GetByTypeAsync(NotificationType type, CancellationToken ct = default)
    {
        return await _context.NotificationTemplates
            .Where(x => x.Type == type && x.IsActive)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<NotificationTemplate>> GetAllActiveAsync(CancellationToken ct = default)
    {
        return await _context.NotificationTemplates
            .Where(x => x.IsActive)
            .OrderBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<NotificationTemplate>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.NotificationTemplates
            .OrderBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task AddAsync(NotificationTemplate template, CancellationToken ct = default)
    {
        await _context.NotificationTemplates.AddAsync(template, ct);
    }

    public void Update(NotificationTemplate template)
    {
        _context.NotificationTemplates.Update(template);
    }
}

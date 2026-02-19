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

    public async Task AddAsync(NotificationTemplate template, CancellationToken ct = default)
    {
        await _context.NotificationTemplates.AddAsync(template, ct);
    }

    public void Update(NotificationTemplate template)
    {
        _context.NotificationTemplates.Update(template);
    }
}

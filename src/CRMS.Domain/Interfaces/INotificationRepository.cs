using CRMS.Domain.Aggregates.Notification;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Interfaces;

public interface INotificationRepository
{
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Notification>> GetByLoanApplicationIdAsync(Guid loanApplicationId, CancellationToken ct = default);
    Task<IReadOnlyList<Notification>> GetByRecipientUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<Notification>> GetPendingAsync(int limit = 100, CancellationToken ct = default);
    Task<IReadOnlyList<Notification>> GetForRetryAsync(DateTime asOf, int limit = 50, CancellationToken ct = default);
    Task<IReadOnlyList<Notification>> GetScheduledDueAsync(DateTime asOf, int limit = 100, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(Notification notification, CancellationToken ct = default);
    void Update(Notification notification);
}

public interface INotificationTemplateRepository
{
    Task<NotificationTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<NotificationTemplate?> GetByCodeAsync(string code, NotificationChannel channel, string language = "en", CancellationToken ct = default);
    Task<IReadOnlyList<NotificationTemplate>> GetByTypeAsync(NotificationType type, CancellationToken ct = default);
    Task<IReadOnlyList<NotificationTemplate>> GetAllActiveAsync(CancellationToken ct = default);
    Task AddAsync(NotificationTemplate template, CancellationToken ct = default);
    void Update(NotificationTemplate template);
}

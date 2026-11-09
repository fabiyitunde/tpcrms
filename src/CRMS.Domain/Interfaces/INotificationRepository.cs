using CRMS.Domain.Aggregates.Notification;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Interfaces;

public interface INotificationRepository
{
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>Notifications filed under a case number. RH-SHF and NAMP cases are not
    /// LoanApplications, so their reference goes in LoanApplicationNumber.</summary>
    Task<IReadOnlyList<Notification>> GetByLoanApplicationNumberAsync(string loanApplicationNumber, CancellationToken ct = default);
    Task<IReadOnlyList<Notification>> GetByLoanApplicationIdAsync(Guid loanApplicationId, CancellationToken ct = default);
    Task<IReadOnlyList<Notification>> GetByRecipientUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<Notification>> GetPendingAsync(int limit = 100, CancellationToken ct = default);
    Task<IReadOnlyList<Notification>> GetForRetryAsync(DateTime asOf, int limit = 50, CancellationToken ct = default);
    Task<IReadOnlyList<Notification>> GetScheduledDueAsync(DateTime asOf, int limit = 100, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default);
    /// <summary>The outbound queue as an administrator sees it: newest first, filtered and paged.</summary>
    Task<(IReadOnlyList<Notification> Items, int Total)> SearchAsync(NotificationSearch search, CancellationToken ct = default);
    /// <summary>How many notifications of a channel are in each status, among those created since a moment (or all).</summary>
    Task<IReadOnlyDictionary<NotificationStatus, int>> CountByStatusAsync(NotificationChannel channel, DateTime? createdSinceUtc, CancellationToken ct = default);
    /// <summary>Whether a message with this subject to this address is already waiting to go out.</summary>
    Task<bool> IsQueuedAsync(NotificationChannel channel, string recipientAddress, string subject, CancellationToken ct = default);
    Task AddAsync(Notification notification, CancellationToken ct = default);
    void Update(Notification notification);
}

/// <summary>Filter for <see cref="INotificationRepository.SearchAsync"/>. Text filters match anywhere in the field.</summary>
public record NotificationSearch(
    NotificationChannel? Channel = null,
    NotificationStatus? Status = null,
    string? TemplateCode = null,
    string? Recipient = null,
    DateTime? CreatedFromUtc = null,
    DateTime? CreatedToUtc = null,
    int Page = 1,
    int PageSize = 25);

public interface INotificationTemplateRepository
{
    Task<NotificationTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<NotificationTemplate?> GetByCodeAsync(string code, NotificationChannel channel, string language = "en", CancellationToken ct = default);
    Task<IReadOnlyList<NotificationTemplate>> GetByTypeAsync(NotificationType type, CancellationToken ct = default);
    Task<IReadOnlyList<NotificationTemplate>> GetAllActiveAsync(CancellationToken ct = default);
    Task<IReadOnlyList<NotificationTemplate>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(NotificationTemplate template, CancellationToken ct = default);
    void Update(NotificationTemplate template);
}

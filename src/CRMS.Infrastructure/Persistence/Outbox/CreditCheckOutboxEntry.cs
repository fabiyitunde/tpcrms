namespace CRMS.Infrastructure.Persistence.Outbox;

public enum CreditCheckOutboxStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}

/// <summary>
/// Persistent outbox entry for credit check requests.
/// Written atomically when a loan is branch-approved; processed by the background polling service.
/// Survives app restarts, deployments, and crashes — unlike the previous in-memory channel.
/// </summary>
public class CreditCheckOutboxEntry
{
    public Guid Id { get; set; }
    public Guid LoanApplicationId { get; set; }
    public Guid SystemUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public CreditCheckOutboxStatus Status { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public int AttemptCount { get; set; }
}

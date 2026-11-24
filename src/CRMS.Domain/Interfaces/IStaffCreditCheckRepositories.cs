using CRMS.Domain.Aggregates.Access;
using CRMS.Domain.Aggregates.CreditCheck;

namespace CRMS.Domain.Interfaces;

public interface IStaffCreditCheckRepository
{
    Task AddAsync(StaffCreditCheck check, CancellationToken ct = default);
    Task<StaffCreditCheck?> GetByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>Checks the user has queued or running.</summary>
    Task<int> CountOpenAsync(Guid userId, CancellationToken ct = default);
    /// <summary>Atomically takes the next runnable check: the oldest queued one, or one left
    /// PROCESSING since before <paramref name="staleBefore"/>. Null when there is none.</summary>
    Task<Guid?> ClaimNextAsync(DateTime now, DateTime staleBefore, CancellationToken ct = default);
    /// <summary>Newest first, without the stored request or result.</summary>
    Task<(List<StaffCreditCheck> Items, int Total)> SearchAsync(StaffCreditCheckFilter filter, CancellationToken ct = default);
    void Update(StaffCreditCheck check);
}

/// <summary>History search. Null means "any"; <see cref="RequestedBy"/> null lists everyone's.</summary>
public record StaffCreditCheckFilter(
    Guid? RequestedBy,
    string? SubjectType,
    string? Status,
    string? SubjectHash,
    DateTime? From,
    DateTime? To,
    int Page,
    int PageSize);

public interface IFeatureAccessRuleRepository
{
    Task<FeatureAccessRule?> GetByKeyAsync(string featureKey, CancellationToken ct = default);
    Task<List<FeatureAccessRule>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(FeatureAccessRule rule, CancellationToken ct = default);
    void Update(FeatureAccessRule rule);
}

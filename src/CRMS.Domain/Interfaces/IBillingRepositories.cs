using CRMS.Domain.Aggregates.Billing;

namespace CRMS.Domain.Interfaces;

/// <summary>Count of usage rows for one item, flow, month and outcome.</summary>
public sealed record BillingUsageCount(string Item, BillingFlow Flow, DateTime Month, bool Succeeded, int Count);

/// <summary>Filter for the charges drill-down. Dates are UTC; Month is a first-of-month.</summary>
public sealed record BillingUsageSearch(
    DateTime? Month = null,
    BillingFlow? Flow = null,
    BillingCategory? Category = null,
    string? Item = null,
    string? CaseReference = null,
    Guid? CaseId = null,
    bool? Succeeded = null,
    int Page = 1,
    int PageSize = 50);

public interface IBillingUsageRepository
{
    Task AddAsync(BillingUsage usage, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<BillingUsage> usages, CancellationToken ct = default);
    Task<bool> SourceKeyExistsAsync(string sourceKey, CancellationToken ct = default);
    /// <summary>Which of the given keys already exist, so a sync or backfill only adds the missing rows.</summary>
    Task<HashSet<string>> ExistingSourceKeysAsync(IEnumerable<string> sourceKeys, CancellationToken ct = default);
    /// <summary>Counts grouped by item, flow, month and outcome, for months from <paramref name="fromMonth"/> (inclusive) to <paramref name="toMonth"/> (inclusive).</summary>
    Task<IReadOnlyList<BillingUsageCount>> CountsAsync(DateTime fromMonth, DateTime toMonth, BillingFlow? flow, CancellationToken ct = default);
    Task<(IReadOnlyList<BillingUsage> Items, int Total)> SearchAsync(BillingUsageSearch search, CancellationToken ct = default);
    Task<IReadOnlyList<BillingUsage>> ForCaseAsync(Guid caseId, CancellationToken ct = default);
    /// <summary>When the first live row was written, or null when none has been. Reconstruction stops here.</summary>
    Task<DateTime?> FirstLiveAtAsync(CancellationToken ct = default);
    Task<int> CountBySourceAsync(BillingUsageSource source, CancellationToken ct = default);
    /// <summary>Usage rows for the given cases, grouped in memory by the caller.</summary>
    Task<IReadOnlyList<BillingUsage>> ForCasesAsync(IEnumerable<Guid> caseIds, CancellationToken ct = default);
}

public interface IBillingRateRepository
{
    Task<IReadOnlyList<BillingRate>> GetAllAsync(CancellationToken ct = default);
    Task<BillingRate?> FindAsync(string item, BillingFlow? flow, DateTime effectiveFromMonth, CancellationToken ct = default);
    Task AddAsync(BillingRate rate, CancellationToken ct = default);
    Task UpdateAsync(BillingRate rate, CancellationToken ct = default);
}

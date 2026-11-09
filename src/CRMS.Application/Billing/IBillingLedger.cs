using CRMS.Domain.Aggregates.Billing;

namespace CRMS.Application.Billing;

/// <summary>A billable event as the caller knows it. The ledger adds the ambient scope, the host and the time.</summary>
/// <param name="Item">A key from <see cref="BillingItems"/>.</param>
/// <param name="Succeeded">Whether the provider answered with a result. Billed either way.</param>
/// <param name="Detail">The provider's words when it failed.</param>
/// <param name="SubjectHint">Last four digits of a BVN, an RC number, or nothing.</param>
/// <param name="Scope">Overrides the ambient scope when the caller knows better (a partner check, a loan event).</param>
public sealed record BillingUsageDraft(
    string Item,
    bool Succeeded,
    string? Detail = null,
    string? SubjectHint = null,
    int? DurationMs = null,
    BillingScope? Scope = null,
    DateTime? OccurredAtUtc = null,
    BillingUsageSource Source = BillingUsageSource.Live,
    string? SourceKey = null);

/// <summary>
/// Writes usage rows. Implementations commit each row on its own, outside the caller's unit of work:
/// a call that happened is billable even when the work around it later fails or rolls back, and a
/// failure to record must never fail the business operation.
/// </summary>
public interface IBillingLedger
{
    Task RecordAsync(BillingUsageDraft draft, CancellationToken ct = default);
}

/// <summary>Helpers for the hints a usage row may carry without identifying anyone.</summary>
public static class BillingHints
{
    public static string? Last4(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits.Length >= 4 ? "…" + digits[^4..] : null;
    }

    /// <summary>RC numbers are public registry identifiers; they are kept whole.</summary>
    public static string? Rc(string? rcNumber) => string.IsNullOrWhiteSpace(rcNumber) ? null : rcNumber.Trim();
}

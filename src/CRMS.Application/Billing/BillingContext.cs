using CRMS.Domain.Aggregates.Billing;

namespace CRMS.Application.Billing;

/// <summary>Which case a third-party call is being made for.</summary>
public sealed record BillingScope(BillingFlow Flow, Guid? CaseId, string? CaseReference, string? SubjectType = null, Guid? TriggeredBy = null)
{
    public BillingScope WithSubject(string subjectType) => this with { SubjectType = subjectType };
}

/// <summary>
/// The ambient answer to "who is this call for?". A handler opens a scope around the work it does
/// for a case; the billing wrapper around the SmartComply client reads it when it records a call.
/// Flows through awaits and into the provider, so no call site has to pass billing details down.
/// A call made outside any scope is recorded as Unattributed, never dropped.
/// </summary>
public static class BillingContext
{
    private static readonly AsyncLocal<BillingScope?> Ambient = new();

    public static BillingScope? Current => Ambient.Value;

    public static IDisposable Begin(BillingFlow flow, Guid? caseId, string? caseReference, string? subjectType = null, Guid? triggeredBy = null)
        => Begin(new BillingScope(flow, caseId, caseReference, subjectType, triggeredBy));

    public static IDisposable Begin(BillingScope scope)
    {
        var previous = Ambient.Value;
        Ambient.Value = scope;
        return new Restore(previous);
    }

    /// <summary>Narrows the current scope to one subject (a director, a guarantor) for the calls inside it.</summary>
    public static IDisposable Subject(string subjectType)
    {
        var current = Ambient.Value;
        return current is null ? new Restore(null) : Begin(current.WithSubject(subjectType));
    }

    private sealed class Restore(BillingScope? previous) : IDisposable
    {
        public void Dispose() => Ambient.Value = previous;
    }
}

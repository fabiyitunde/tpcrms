namespace CRMS.Domain.Aggregates.Billing;

/// <summary>Which product, or which other activity, a billable event belongs to.</summary>
public enum BillingFlow
{
    Corporate,
    Namp,
    Rhshf,
    /// <summary>A partner's customer credit check through the public API.</summary>
    PartnerApi,
    /// <summary>An administrator trying a service from Admin > Diagnostics.</summary>
    Diagnostics,
    /// <summary>A staff lookup made outside any case, such as a bureau search by BVN.</summary>
    Other,
    /// <summary>A call no handler tagged. Still billed; shown for review.</summary>
    Unattributed,
}

public enum BillingCategory
{
    Identity,
    CreditBureau,
    BusinessBureau,
    FraudCheck,
    Advisory,
    PartnerApi,
    Platform,
}

/// <summary>How a usage row came to exist.</summary>
public enum BillingUsageSource
{
    /// <summary>Written at the moment the call happened.</summary>
    Live,
    /// <summary>Read off a loan's current state by the loan-event sync; exact, and re-derivable.</summary>
    Derived,
    /// <summary>Rebuilt from records kept before billing existed; a lower bound, not a count of calls.</summary>
    Reconstructed,
}

public enum BillingLoanOutcome
{
    Processing,
    Booked,
    Declined,
    Cancelled,
}

using CRMS.Domain.Aggregates.Billing;
using CRMS.Domain.Enums;

namespace CRMS.Application.Billing;

/// <summary>
/// The one place that says what a loan's status means for billing: booked, declined, cancelled or
/// still processing. Each track names its terminal states differently; the dashboard, the loan list
/// and the platform fees all read this and nothing else.
/// </summary>
public static class BillingLoanClassifier
{
    public static BillingLoanOutcome Corporate(LoanApplicationStatus status, bool hasCoreBankingLoan) => status switch
    {
        LoanApplicationStatus.Disbursed or LoanApplicationStatus.Closed => BillingLoanOutcome.Booked,
        _ when hasCoreBankingLoan => BillingLoanOutcome.Booked,
        LoanApplicationStatus.BranchRejected or LoanApplicationStatus.CommitteeRejected or
        LoanApplicationStatus.Rejected or LoanApplicationStatus.RatificationDeclined => BillingLoanOutcome.Declined,
        LoanApplicationStatus.Cancelled => BillingLoanOutcome.Cancelled,
        _ => BillingLoanOutcome.Processing,
    };

    public static BillingLoanOutcome Namp(NampApplicationStatus status, bool hasCoreBankingLoan) => status switch
    {
        NampApplicationStatus.Deployment or NampApplicationStatus.Active or NampApplicationStatus.Closed => BillingLoanOutcome.Booked,
        _ when hasCoreBankingLoan => BillingLoanOutcome.Booked,
        NampApplicationStatus.FinancialDeclined or NampApplicationStatus.RiskDeclined or
        NampApplicationStatus.BranchCommitteeDeclined or NampApplicationStatus.ZonalCommitteeDeclined or
        NampApplicationStatus.RegionalCommitteeDeclined or NampApplicationStatus.HOCommitteeDeclined or
        NampApplicationStatus.RatificationDeclined or NampApplicationStatus.LegalDeclined or
        NampApplicationStatus.Declined => BillingLoanOutcome.Declined,
        NampApplicationStatus.OfferLapsed => BillingLoanOutcome.Cancelled,
        _ => BillingLoanOutcome.Processing,
    };

    public static BillingLoanOutcome Rhshf(RhshfCaseStatus status, bool isBooked) => status switch
    {
        RhshfCaseStatus.Approved when isBooked => BillingLoanOutcome.Booked,
        RhshfCaseStatus.Approved => BillingLoanOutcome.Processing,   // approved in principle, not yet in core banking
        RhshfCaseStatus.Declined => BillingLoanOutcome.Declined,
        RhshfCaseStatus.Cancelled or RhshfCaseStatus.Expired => BillingLoanOutcome.Cancelled,
        _ => BillingLoanOutcome.Processing,
    };

    /// <summary>The platform-fee item a terminal outcome earns, or null while processing.</summary>
    public static string? OutcomeItem(BillingLoanOutcome outcome) => outcome switch
    {
        BillingLoanOutcome.Booked => BillingItems.LoanBooked,
        BillingLoanOutcome.Declined => BillingItems.LoanDeclined,
        BillingLoanOutcome.Cancelled => BillingItems.LoanCancelled,
        _ => null,
    };

    public static string DisplayName(BillingLoanOutcome outcome) => outcome switch
    {
        BillingLoanOutcome.Booked => "Booked",
        BillingLoanOutcome.Declined => "Declined",
        BillingLoanOutcome.Cancelled => "Cancelled",
        _ => "Processing",
    };
}

/// <summary>A loan as the billing sync sees it: enough to write its platform-fee rows and list it.</summary>
public sealed record BillingLoanSnapshot(
    BillingFlow Flow,
    Guid CaseId,
    string Reference,
    string? ApplicantName,
    DateTime ReceivedAt,
    BillingLoanOutcome Outcome,
    DateTime? OutcomeAt,
    decimal? Amount);

/// <summary>Reads every loan of every flow in billing terms. Implemented in Infrastructure over the three aggregates.</summary>
public interface IBillingLoanSource
{
    Task<IReadOnlyList<BillingLoanSnapshot>> AllLoansAsync(CancellationToken ct = default);
}

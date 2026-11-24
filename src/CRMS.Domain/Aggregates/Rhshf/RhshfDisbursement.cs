using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Rhshf;

/// <summary>
/// Disbursement Officer's booking attempt — sixth and final stage of the post-profiling pipeline
/// (design doc §3.6, Phase 9). Child entity of RhshfCreditProfile (unlike RhshfLegalClearance) —
/// one row per attempt, append-only; a case can accumulate more than one per cycle if earlier
/// attempts failed and were retried.
/// The loan is disbursed with no destination: core banking credits the product's settlement account
/// and the bank pays each supplier from there itself, outside the CRMS.
/// </summary>
public class RhshfDisbursement : Entity
{
    public Guid RhshfCreditProfileId { get; private set; }
    public int CycleNumber { get; private set; }
    public Guid DisbursementOfficerId { get; private set; }
    public DateTime BookedAt { get; private set; }
    public long? FineractLoanId { get; private set; }
    public string? FineractLoanAccountNumber { get; private set; }
    public decimal DisbursedAmount { get; private set; }

    /// <summary>Recorded on attempts made before 23 Nov 2026, when the officer typed in one supplier
    /// account. It was never sent to core banking, and an RH-SHF loan pays several suppliers, so it is
    /// no longer asked for; kept on old rows as the record of what was entered.</summary>
    public string? SupplierAccountNumber { get; private set; }
    public string? SupplierName { get; private set; }

    public RhshfDisbursementStatus Status { get; private set; }
    public string? FailureReason { get; private set; }

    protected RhshfDisbursement() { }

    public RhshfDisbursement(
        Guid rhshfCreditProfileId, int cycleNumber, Guid disbursementOfficerId, decimal disbursedAmount,
        RhshfDisbursementStatus status, long? fineractLoanId, string? fineractLoanAccountNumber, string? failureReason)
    {
        RhshfCreditProfileId = rhshfCreditProfileId;
        CycleNumber = cycleNumber;
        DisbursementOfficerId = disbursementOfficerId;
        BookedAt = DateTime.UtcNow;
        DisbursedAmount = disbursedAmount;
        Status = status;
        FineractLoanId = fineractLoanId;
        FineractLoanAccountNumber = fineractLoanAccountNumber;
        FailureReason = failureReason;
    }
}

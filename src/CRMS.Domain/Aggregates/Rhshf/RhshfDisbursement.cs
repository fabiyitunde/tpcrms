using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Rhshf;

/// <summary>
/// Disbursement Officer's booking attempt — sixth and final stage of the post-profiling pipeline
/// (design doc §3.6, Phase 9). Child entity of RhshfCreditProfile (unlike RhshfLegalClearance) —
/// one row per attempt, append-only; a case can accumulate more than one per cycle if earlier
/// attempts failed and were retried.
/// Loan proceeds go to an input-supplier/vendor account, not the FAC's own savings (booked with
/// DisburseToSavings=false, same shape as NAMP's equipment-vendor disbursement) — Fineract's plain
/// "disburse" call takes no destination-account parameter for that path, so SupplierAccountNumber/
/// SupplierName are CRMS-side audit fields the Disbursement Officer records manually, not sent to
/// Fineract itself.
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
    public string SupplierAccountNumber { get; private set; } = string.Empty;
    public string? SupplierName { get; private set; }
    public RhshfDisbursementStatus Status { get; private set; }
    public string? FailureReason { get; private set; }

    protected RhshfDisbursement() { }

    public RhshfDisbursement(
        Guid rhshfCreditProfileId, int cycleNumber, Guid disbursementOfficerId, decimal disbursedAmount,
        string supplierAccountNumber, string? supplierName, RhshfDisbursementStatus status,
        long? fineractLoanId, string? fineractLoanAccountNumber, string? failureReason)
    {
        RhshfCreditProfileId = rhshfCreditProfileId;
        CycleNumber = cycleNumber;
        DisbursementOfficerId = disbursementOfficerId;
        BookedAt = DateTime.UtcNow;
        DisbursedAmount = disbursedAmount;
        SupplierAccountNumber = supplierAccountNumber;
        SupplierName = supplierName;
        Status = status;
        FineractLoanId = fineractLoanId;
        FineractLoanAccountNumber = fineractLoanAccountNumber;
        FailureReason = failureReason;
    }
}

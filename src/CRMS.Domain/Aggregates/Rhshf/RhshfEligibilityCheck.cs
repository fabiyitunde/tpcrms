using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Rhshf;

/// <summary>
/// One row per eligibility criterion per cycle (RSHSF_Programme_Details.docx S/N 15) — recorded by
/// the Credit Officer as part of Appraisal. Own aggregate root, own table, independent of any
/// NAMP/Corporate eligibility concept — a fixed programme-wide checklist, not evaluated against
/// LoanProduct's configurable EligibilityRule engine. Append-only per cycle: recording again for
/// the same criterion/cycle is a data-entry correction, not expected in normal flow, so no update
/// method — only a fresh Create() per criterion.
/// </summary>
public class RhshfEligibilityCheck : AggregateRoot
{
    public Guid RhshfCreditProfileId { get; private set; }
    public int CycleNumber { get; private set; }
    public RhshfEligibilityCriterion Criterion { get; private set; }
    public bool IsSatisfied { get; private set; }
    public string? Notes { get; private set; }
    public Guid VerifiedBy { get; private set; }
    public DateTime VerifiedAt { get; private set; }

    protected RhshfEligibilityCheck() { }

    public static Result<RhshfEligibilityCheck> Create(
        Guid rhshfCreditProfileId, int cycleNumber, RhshfEligibilityCriterion criterion,
        bool isSatisfied, string? notes, Guid verifiedBy)
    {
        if (verifiedBy == Guid.Empty)
            return Result.Failure<RhshfEligibilityCheck>("verifiedBy is required.");

        return Result.Success(new RhshfEligibilityCheck
        {
            RhshfCreditProfileId = rhshfCreditProfileId,
            CycleNumber = cycleNumber,
            Criterion = criterion,
            IsSatisfied = isSatisfied,
            Notes = notes,
            VerifiedBy = verifiedBy,
            VerifiedAt = DateTime.UtcNow,
        });
    }
}

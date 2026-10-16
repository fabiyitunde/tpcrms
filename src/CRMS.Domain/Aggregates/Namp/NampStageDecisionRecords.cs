using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Namp;

/// <summary>
/// One pass of financial appraisal, kept append-only.
/// <para>
/// NampApplication's FinancialAppraisalNote / ByUserId / At are single fields assigned straight over
/// on each pass, so a case returned to the Credit Officer and re-appraised lost the original
/// appraisal entirely — who signed it off, when, and on what note. Those fields remain as "the
/// latest pass" because the DTOs, loan pack and detail page all read them; this is the history
/// beside them.
/// </para>
/// <para>
/// Mirrors the RH-SHF pattern, where appraisals and risk reviews are cycle-numbered collections for
/// exactly this reason. NAMP predates it.
/// </para>
/// </summary>
public class NampFinancialAppraisalRecord : Entity
{
    public Guid NampApplicationId { get; private set; }

    /// <summary>Which trip through the pipeline this was. 1 on the first appraisal, incremented each
    /// time the case is returned from committee, so a reader can tell a revision from a first look.</summary>
    public int PassNumber { get; private set; }

    public Guid AppraisedByUserId { get; private set; }
    public DateTime AppraisedAt { get; private set; }

    /// <summary>True when the Credit Officer forwarded it to Risk Review; false when declined.</summary>
    public bool IsApproved { get; private set; }

    public string? Note { get; private set; }

    private NampFinancialAppraisalRecord() { }

    internal static NampFinancialAppraisalRecord Create(
        Guid nampApplicationId, int passNumber, Guid appraisedByUserId, bool isApproved, string? note)
        => new()
        {
            NampApplicationId = nampApplicationId,
            PassNumber = passNumber,
            AppraisedByUserId = appraisedByUserId,
            IsApproved = isApproved,
            Note = note,
            AppraisedAt = DateTime.UtcNow,
        };
}

/// <summary>One pass of risk review, kept append-only for the same reason as
/// <see cref="NampFinancialAppraisalRecord"/>.</summary>
public class NampRiskReviewRecord : Entity
{
    public Guid NampApplicationId { get; private set; }

    public int PassNumber { get; private set; }

    public Guid ReviewedByUserId { get; private set; }
    public DateTime ReviewedAt { get; private set; }

    /// <summary>What the Risk Officer did: approved to committee, returned to the Credit Officer, or
    /// declined outright. A single bool could not tell a return from a decline, and the difference
    /// is what a reader of the history needs.</summary>
    public NampRiskReviewOutcome Outcome { get; private set; }

    public string? Note { get; private set; }

    private NampRiskReviewRecord() { }

    internal static NampRiskReviewRecord Create(
        Guid nampApplicationId, int passNumber, Guid reviewedByUserId,
        NampRiskReviewOutcome outcome, string? note)
        => new()
        {
            NampApplicationId = nampApplicationId,
            PassNumber = passNumber,
            ReviewedByUserId = reviewedByUserId,
            Outcome = outcome,
            Note = note,
            ReviewedAt = DateTime.UtcNow,
        };
}

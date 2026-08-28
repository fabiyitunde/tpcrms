using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Rhshf;

/// <summary>
/// The RH-SHF credit-profiling case — "the case" in the integration brief. Created when the portal
/// submits a BOA-certified consolidated EOP (§4.1). A separate loan track from NAMP: independent
/// aggregate, independent enums, independent tables. See docs/rhshf resources/ for the full design.
/// </summary>
public class RhshfCreditProfile : AggregateRoot
{
    // ── Identity ───────────────────────────────────────────────────────────
    public string Reference { get; private set; } = string.Empty;
    public Guid SubmissionId { get; private set; }

    // ── Programme / Session ───────────────────────────────────────────────
    public string ProgrammeCode { get; private set; } = string.Empty;
    public string ProgrammeName { get; private set; } = string.Empty;
    public string SessionCode { get; private set; } = string.Empty;
    public string SessionName { get; private set; } = string.Empty;

    // ── FAC ────────────────────────────────────────────────────────────────
    public Guid FacId { get; private set; }
    public string CompanyName { get; private set; } = string.Empty;
    public string RcNumber { get; private set; } = string.Empty;
    public string Tin { get; private set; } = string.Empty;
    public string BoaAccountNumber { get; private set; } = string.Empty;
    public string ContactEmail { get; private set; } = string.Empty;
    public string ContactPhone { get; private set; } = string.Empty;
    public string State { get; private set; } = string.Empty;
    public string Lga { get; private set; } = string.Empty;

    // ── EOP ────────────────────────────────────────────────────────────────
    public decimal TotalEopValue { get; private set; }
    public string Currency { get; private set; } = "NGN";
    public int? FarmerCount { get; private set; }

    // ── Portal integration ────────────────────────────────────────────────
    public string CallbackUrl { get; private set; } = string.Empty;
    public string? CertifiedByAdmin { get; private set; }
    public DateTime? CertifiedAt { get; private set; }

    // ── Branch resolution — resolved from BoaAccountNumber, own logic, not NampStagingRecord's ──
    public Guid? ResolvedBranchId { get; private set; }
    public Guid? ResolvedOfficeId { get; private set; }
    public string? BranchResolutionNote { get; private set; }

    // ── Workflow state ─────────────────────────────────────────────────────
    public RhshfCaseStatus Status { get; private set; }
    public RhshfProfilingStage? CurrentStage { get; private set; }

    // ── Post-profiling pipeline (design doc §3.6) ─────────────────────────
    // CurrentCycleNumber increments each time the case (re-)enters UnderReview — 1 on the first
    // pass, 2+ after any ReturnToFac round-trip (design doc §6 #8). InternalStage is null outside
    // UnderReview (during profiling, or once terminal).
    public int CurrentCycleNumber { get; private set; }
    public RhshfInternalStage? InternalStage { get; private set; }

    // ── Decision (set from Phase 4-9's pipeline) ──────────────────────────
    public RhshfDecisionOutcome? DecisionOutcome { get; private set; }
    public decimal? ApprovedAmount { get; private set; }
    public DateTime? DecidedAt { get; private set; }
    public string? DecidedBy { get; private set; }
    public string? DecisionNotes { get; private set; }

    // ── CreditBureauCheck stage result (§4 stage 2) — informational for Phase 4+'s review, not an
    // automated gate. Singleton per case (not a collection) — v1 runs the check exactly once. ────
    public RhshfBureauOutcome BureauCheckOutcome { get; private set; } = RhshfBureauOutcome.NotRun;
    public DateTime? BureauCheckedAt { get; private set; }
    public int? BureauTotalLoans { get; private set; }
    public int? BureauActiveLoans { get; private set; }
    public int? BureauDelinquentFacilities { get; private set; }
    public decimal? BureauTotalOutstanding { get; private set; }
    public decimal? BureauTotalOverdue { get; private set; }
    public string? BureauRawJson { get; private set; }

    // ── Traceability ───────────────────────────────────────────────────────
    public string RawSubmissionPayload { get; private set; } = string.Empty;
    public DateTime ReceivedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private readonly List<RhshfEopLine> _eopLines = [];
    public IReadOnlyCollection<RhshfEopLine> EopLines => _eopLines.AsReadOnly();

    private readonly List<RhshfIssuedToken> _issuedTokens = [];
    public IReadOnlyCollection<RhshfIssuedToken> IssuedTokens => _issuedTokens.AsReadOnly();

    private readonly List<RhshfSupportingDocument> _supportingDocuments = [];
    public IReadOnlyCollection<RhshfSupportingDocument> SupportingDocuments => _supportingDocuments.AsReadOnly();

    private readonly List<RhshfAppraisal> _appraisals = [];
    public IReadOnlyCollection<RhshfAppraisal> Appraisals => _appraisals.AsReadOnly();

    private readonly List<RhshfRiskReview> _riskReviews = [];
    public IReadOnlyCollection<RhshfRiskReview> RiskReviews => _riskReviews.AsReadOnly();

    private readonly List<RhshfRatification> _ratifications = [];
    public IReadOnlyCollection<RhshfRatification> Ratifications => _ratifications.AsReadOnly();

    protected RhshfCreditProfile() { }

    public static Result<RhshfCreditProfile> Create(
        Guid submissionId,
        string programmeCode,
        string programmeName,
        string sessionCode,
        string sessionName,
        Guid facId,
        string companyName,
        string rcNumber,
        string tin,
        string boaAccountNumber,
        string contactEmail,
        string contactPhone,
        string state,
        string lga,
        decimal totalEopValue,
        string currency,
        int? farmerCount,
        string callbackUrl,
        string? certifiedByAdmin,
        DateTime? certifiedAt,
        string rawSubmissionPayload,
        IEnumerable<(string Commodity, decimal QuantityKg, decimal UnitPricePerKg, decimal LineValue)>? eopLines,
        Guid? resolvedBranchId,
        Guid? resolvedOfficeId)
    {
        if (string.IsNullOrWhiteSpace(companyName))
            return Result.Failure<RhshfCreditProfile>("fac.companyName is required.");
        if (string.IsNullOrWhiteSpace(rcNumber))
            return Result.Failure<RhshfCreditProfile>("fac.rcNumber is required.");
        if (string.IsNullOrWhiteSpace(tin))
            return Result.Failure<RhshfCreditProfile>("fac.tin is required.");
        if (string.IsNullOrWhiteSpace(boaAccountNumber))
            return Result.Failure<RhshfCreditProfile>("fac.boaAccountNumber is required.");
        if (string.IsNullOrWhiteSpace(programmeCode))
            return Result.Failure<RhshfCreditProfile>("programme.code is required.");
        if (string.IsNullOrWhiteSpace(sessionCode))
            return Result.Failure<RhshfCreditProfile>("session.code is required.");
        if (string.IsNullOrWhiteSpace(callbackUrl) || !Uri.TryCreate(callbackUrl, UriKind.Absolute, out _))
            return Result.Failure<RhshfCreditProfile>("callbackUrl is required and must be an absolute URL.");
        if (totalEopValue <= 0)
            return Result.Failure<RhshfCreditProfile>("totalEopValue must be greater than zero.");
        if (string.IsNullOrWhiteSpace(currency))
            return Result.Failure<RhshfCreditProfile>("currency is required.");

        var now = DateTime.UtcNow;
        var profile = new RhshfCreditProfile
        {
            Reference = GenerateReference(),
            SubmissionId = submissionId,
            ProgrammeCode = programmeCode,
            ProgrammeName = programmeName,
            SessionCode = sessionCode,
            SessionName = sessionName,
            FacId = facId,
            CompanyName = companyName,
            RcNumber = rcNumber,
            Tin = tin,
            BoaAccountNumber = boaAccountNumber,
            ContactEmail = contactEmail,
            ContactPhone = contactPhone,
            State = state,
            Lga = lga,
            TotalEopValue = totalEopValue,
            Currency = currency,
            FarmerCount = farmerCount,
            CallbackUrl = callbackUrl,
            CertifiedByAdmin = certifiedByAdmin,
            CertifiedAt = certifiedAt,
            ResolvedBranchId = resolvedBranchId,
            ResolvedOfficeId = resolvedOfficeId,
            // Token minting happens in the same handler call that invokes Create() — by the time
            // this aggregate is persisted, a token has already been issued, so the case is already
            // past "Received" (see design doc §5).
            Status = RhshfCaseStatus.ProfilingPending,
            CurrentStage = RhshfProfilingStage.CompanyVerification,
            RawSubmissionPayload = rawSubmissionPayload,
            ReceivedAt = now,
            UpdatedAt = now,
        };

        foreach (var line in eopLines ?? [])
            profile._eopLines.Add(new RhshfEopLine(profile.Id, line.Commodity, line.QuantityKg, line.UnitPricePerKg, line.LineValue));

        return Result.Success(profile);
    }

    /// <summary>Records that a token was issued for this case (§4.2/§4.6) — does not change Status.</summary>
    public void IssueToken(string jti, DateTime issuedAt, DateTime expiresAt)
    {
        _issuedTokens.Add(new RhshfIssuedToken(Id, jti, issuedAt, expiresAt));
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Enforces single-use: the token authenticating the profiling form's first page load
    /// can be consumed exactly once (design doc §6 #5).</summary>
    public Result ConsumeToken(string jti)
    {
        var token = _issuedTokens.FirstOrDefault(t => t.Jti == jti);
        if (token is null)
            return Result.Failure("Token not recognised for this case.");

        return token.Consume();
    }

    public bool IsTerminal => Status is RhshfCaseStatus.Approved or RhshfCaseStatus.Declined
        or RhshfCaseStatus.Expired or RhshfCaseStatus.Cancelled;

    /// <summary>Records the automated business bureau pull at the CreditBureauCheck stage (§4.3).
    /// Idempotent by design at the Application layer — call only when BureauCheckOutcome is
    /// still NotRun. Does not advance the stage; that's a separate, explicit FAC action.</summary>
    public Result RecordBureauCheck(
        RhshfBureauOutcome outcome, int totalLoans, int activeLoans, int delinquentFacilities,
        decimal totalOutstanding, decimal totalOverdue, string? rawJson)
    {
        if (CurrentStage != RhshfProfilingStage.CreditBureauCheck)
            return Result.Failure("Case is not on the credit bureau check stage.");

        BureauCheckOutcome = outcome;
        BureauCheckedAt = DateTime.UtcNow;
        BureauTotalLoans = totalLoans;
        BureauActiveLoans = activeLoans;
        BureauDelinquentFacilities = delinquentFacilities;
        BureauTotalOutstanding = totalOutstanding;
        BureauTotalOverdue = totalOverdue;
        BureauRawJson = rawJson;
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    /// <summary>Records a failed bureau pull — still lets the FAC proceed (informational, not a
    /// gate); Phase 4's credit officer sees "Failed" and can decide how to handle it.</summary>
    public Result RecordBureauCheckFailure()
    {
        if (CurrentStage != RhshfProfilingStage.CreditBureauCheck)
            return Result.Failure("Case is not on the credit bureau check stage.");

        BureauCheckOutcome = RhshfBureauOutcome.Failed;
        BureauCheckedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    /// <summary>No fixed required-document checklist in v1 (see design doc) — any number of files,
    /// added only while on the SupportingDocuments stage.</summary>
    public Result<RhshfSupportingDocument> AddSupportingDocument(string fileName, string contentType, string storagePath, long sizeBytes)
    {
        if (CurrentStage != RhshfProfilingStage.SupportingDocuments)
            return Result.Failure<RhshfSupportingDocument>("Case is not on the supporting documents stage.");

        var document = new RhshfSupportingDocument(Id, fileName, contentType, storagePath, sizeBytes);
        _supportingDocuments.Add(document);
        UpdatedAt = DateTime.UtcNow;
        return Result.Success(document);
    }

    /// <summary>Advances from one profiling stage to the next, or — from the final stage — completes
    /// profiling entirely (external status flips to UnderReview, §5). The single mechanism behind
    /// every "confirm and continue" action across all 5 stages; guards against skipping or replaying
    /// a stage via direct requests (Phase 3 test: "stage order is fixed and enforced server-side").</summary>
    public Result AdvanceStage(RhshfProfilingStage expectedCurrentStage)
    {
        if (Status != RhshfCaseStatus.ProfilingPending && Status != RhshfCaseStatus.ProfilingInProgress)
            return Result.Failure("Profiling is not currently in progress for this case.");
        if (CurrentStage != expectedCurrentStage)
            return Result.Failure("Stage mismatch — cannot skip or replay a profiling stage.");

        if (Status == RhshfCaseStatus.ProfilingPending)
            Status = RhshfCaseStatus.ProfilingInProgress;

        if (expectedCurrentStage == RhshfProfilingStage.ReviewAndSubmit)
        {
            Status = RhshfCaseStatus.UnderReview;
            CurrentStage = null;
            // A fresh pass through the staff pipeline starts here — whether this is the case's
            // first submission or a resubmission after ReturnToFac, the maker-checker floor
            // (design doc §6 #2/#8) applies again from Appraisal.
            CurrentCycleNumber++;
            InternalStage = RhshfInternalStage.Appraisal;
        }
        else
        {
            CurrentStage = (RhshfProfilingStage)((int)expectedCurrentStage + 1);
        }

        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    /// <summary>Best-effort — a failed resolution does not block the case; it's just left unrouted
    /// (BranchResolutionNote explains why) until someone resolves it manually. Blocking submission
    /// on a live Fineract round-trip would put an external dependency in the portal's critical path.</summary>
    public void ResolveBranch(Guid? branchId, Guid? officeId, string? note)
    {
        ResolvedBranchId = branchId;
        ResolvedOfficeId = officeId;
        BranchResolutionNote = note;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Credit Officer's appraisal — first stage of the post-profiling pipeline (design doc
    /// §3.6). One per CycleNumber; a second call for the same cycle fails (no re-appraising).</summary>
    public Result Appraise(Guid creditOfficerId, RhshfAppraisalOutcome outcome, string? notes, RhshfProfilingStage? returnToStage = null)
    {
        if (Status != RhshfCaseStatus.UnderReview)
            return Result.Failure("Case is not under review.");
        if (_appraisals.Any(a => a.CycleNumber == CurrentCycleNumber))
            return Result.Failure("This cycle has already been appraised.");

        _appraisals.Add(new RhshfAppraisal(Id, CurrentCycleNumber, creditOfficerId, outcome, notes));

        switch (outcome)
        {
            case RhshfAppraisalOutcome.Proceed:
                InternalStage = RhshfInternalStage.RiskReview;
                break;
            case RhshfAppraisalOutcome.ReturnToFac:
                ReturnToFac(returnToStage);
                break;
            case RhshfAppraisalOutcome.Decline:
                Decline("CRMS Appraisal", notes);
                break;
        }

        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    /// <summary>Risk Officer's review — second stage (design doc §3.6). Must differ from that
    /// cycle's appraising Credit Officer; cannot run before that cycle's Appraisal.</summary>
    public Result ReviewRisk(Guid riskOfficerId, RhshfRiskReviewOutcome outcome, string? notes, RhshfProfilingStage? returnToStage = null)
    {
        if (Status != RhshfCaseStatus.UnderReview)
            return Result.Failure("Case is not under review.");

        var appraisal = _appraisals.FirstOrDefault(a => a.CycleNumber == CurrentCycleNumber);
        if (appraisal is null || appraisal.Outcome != RhshfAppraisalOutcome.Proceed)
            return Result.Failure("This cycle has not been appraised with a Proceed outcome yet.");
        if (appraisal.CreditOfficerId == riskOfficerId)
            return Result.Failure("The Risk Officer must be a different person from the Credit Officer who appraised this case.");
        if (_riskReviews.Any(r => r.CycleNumber == CurrentCycleNumber))
            return Result.Failure("This cycle has already had a risk review.");

        _riskReviews.Add(new RhshfRiskReview(Id, CurrentCycleNumber, riskOfficerId, outcome, notes));

        switch (outcome)
        {
            case RhshfRiskReviewOutcome.Cleared:
                InternalStage = RhshfInternalStage.CommitteeVoting;
                break;
            case RhshfRiskReviewOutcome.ReturnToFac:
                ReturnToFac(returnToStage);
                break;
            case RhshfRiskReviewOutcome.Decline:
                Decline("CRMS Risk Review", notes);
                break;
        }

        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    /// <summary>Shared by every stage's ReturnToFac outcome (design doc §6 #7) — resets external
    /// status so the FAC re-enters the form; the next AdvanceStage(ReviewAndSubmit) opens a fresh
    /// cycle (§6 #8).</summary>
    private void ReturnToFac(RhshfProfilingStage? returnToStage)
    {
        Status = RhshfCaseStatus.ProfilingInProgress;
        CurrentStage = returnToStage ?? RhshfProfilingStage.ReviewAndSubmit;
        InternalStage = null;
    }

    /// <summary>Shared by every stage's Decline outcome — terminal, fires the domain event Phase 10's
    /// webhook dispatcher listens for.</summary>
    private void Decline(string decidedBy, string? notes)
    {
        Status = RhshfCaseStatus.Declined;
        DecisionOutcome = RhshfDecisionOutcome.Declined;
        DecidedAt = DateTime.UtcNow;
        DecidedBy = decidedBy;
        DecisionNotes = notes;
        InternalStage = null;
        AddDomainEvent(new RhshfCaseDecidedEvent(Id));
    }

    /// <summary>That cycle's Credit Officer + Risk Officer — committee voters must be distinct from
    /// both (design doc Phase 5 §4). Used by the Application layer when casting a committee vote.</summary>
    public IReadOnlyCollection<Guid> GetCurrentCycleAppraisalAndRiskActorIds()
    {
        var ids = new List<Guid>();
        var appraisal = _appraisals.FirstOrDefault(a => a.CycleNumber == CurrentCycleNumber);
        if (appraisal is not null)
            ids.Add(appraisal.CreditOfficerId);
        var riskReview = _riskReviews.FirstOrDefault(r => r.CycleNumber == CurrentCycleNumber);
        if (riskReview is not null)
            ids.Add(riskReview.RiskOfficerId);
        return ids;
    }

    /// <summary>Committee voting reached Approved (design doc §3.6, Phase 5) — advances to
    /// Ratification (Phase 6). Called by the Application layer once RhshfCommitteeReview.CastVote
    /// returns a decision, since committee voting lives in its own aggregate.</summary>
    public Result AdvanceToRatification()
    {
        if (Status != RhshfCaseStatus.UnderReview || InternalStage != RhshfInternalStage.CommitteeVoting)
            return Result.Failure("Case is not at the committee voting stage.");

        InternalStage = RhshfInternalStage.Ratification;
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    /// <summary>Committee voting reached Rejected — terminal.</summary>
    public Result DeclineAtCommittee(string? notes)
    {
        if (Status != RhshfCaseStatus.UnderReview || InternalStage != RhshfInternalStage.CommitteeVoting)
            return Result.Failure("Case is not at the committee voting stage.");

        Decline("CRMS Committee", notes);
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    /// <summary>Committee sent the case back to the FAC before reaching a vote tally.</summary>
    public Result ReturnToFacFromCommittee(RhshfProfilingStage? returnToStage)
    {
        if (Status != RhshfCaseStatus.UnderReview || InternalStage != RhshfInternalStage.CommitteeVoting)
            return Result.Failure("Case is not at the committee voting stage.");

        ReturnToFac(returnToStage);
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    /// <summary>Final Approver's ratification — fourth stage (design doc §3.6, Phase 6). Ratified
    /// requires approvedAmount == TotalEopValue exactly (design doc §6 #1) — no partial approval in
    /// v1. excludedActorIds is the union of that cycle's Appraisal/RiskReview actors and the
    /// committee members who voted Approve (computed by the Application layer, since committee
    /// voting lives in a separate aggregate this method has no access to). On Ratified, advances to
    /// AwaitingOfferAcceptance directly — offer generation itself is an Application-layer side
    /// effect (PDF rendering, file storage), not something a domain method can do.</summary>
    public Result Ratify(
        Guid finalApproverId, RhshfRatificationOutcome outcome, decimal? approvedAmount, string? notes,
        RhshfProfilingStage? returnToStage, IReadOnlyCollection<Guid> excludedActorIds)
    {
        if (Status != RhshfCaseStatus.UnderReview || InternalStage != RhshfInternalStage.Ratification)
            return Result.Failure("Case is not at the ratification stage.");
        if (excludedActorIds.Contains(finalApproverId))
            return Result.Failure("The Final Approver must be a different person from this cycle's appraiser, risk officer, and approving committee members.");
        if (_ratifications.Any(r => r.CycleNumber == CurrentCycleNumber))
            return Result.Failure("This cycle has already been ratified.");
        if (outcome == RhshfRatificationOutcome.Ratified && approvedAmount != TotalEopValue)
            return Result.Failure("Approved amount must equal the total EOP value exactly — no partial approval in v1.");

        _ratifications.Add(new RhshfRatification(Id, CurrentCycleNumber, finalApproverId, outcome, approvedAmount, notes));

        switch (outcome)
        {
            case RhshfRatificationOutcome.Ratified:
                InternalStage = RhshfInternalStage.AwaitingOfferAcceptance;
                break;
            case RhshfRatificationOutcome.ReturnToFac:
                ReturnToFac(returnToStage);
                break;
            case RhshfRatificationOutcome.Declined:
                Decline("CRMS Ratification", notes);
                break;
        }

        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    private static string GenerateReference()
    {
        // Random 6-digit suffix rather than a true incrementing sequence — avoids a concurrency
        // hazard (concurrent submits racing for the "next" number) for cosmetic sequentiality the
        // brief doesn't actually require. Mirrors NAMP's own NampStagingRecord approach.
        var suffix = Random.Shared.Next(0, 1_000_000).ToString("D6");
        return $"RHSHF-{DateTime.UtcNow:yyyy}-{suffix}";
    }
}

/// <summary>Raised whenever a case reaches a terminal decision (Approved or Declined), from any of
/// the pipeline's several possible trigger points (design doc §6 #9). Phase 10's webhook dispatcher
/// listens for this — kept minimal (just the id) since the handler re-loads the aggregate to build
/// the actual webhook payload, rather than duplicating that shape here.</summary>
public record RhshfCaseDecidedEvent(Guid RhshfCreditProfileId) : DomainEvent;

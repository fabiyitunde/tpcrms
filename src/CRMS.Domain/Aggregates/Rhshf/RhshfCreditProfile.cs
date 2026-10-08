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
    /// <summary>
    /// Optional. The RH-SHF portal has never captured a TIN, and BOA accepted that a case can be
    /// assessed without one rather than block every existing FAC behind a collection exercise. It is
    /// still worth having, so it is surfaced as explicitly missing rather than quietly blank.
    /// </summary>
    public string? Tin { get; private set; }
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

    /// <summary>
    /// The cycle that work being captured right now belongs to.
    ///
    /// During profiling the FAC is preparing the *next* review pass, and CurrentCycleNumber has not
    /// been incremented yet (it increments on submit). So anything the FAC records — farm plans,
    /// stage confirmations — must be filed against CurrentCycleNumber + 1, or the staff side will
    /// look for it under the wrong cycle after submission and find nothing. Once the case is under
    /// review the two are the same.
    /// </summary>
    public int ProfilingTargetCycleNumber =>
        Status is RhshfCaseStatus.ProfilingPending or RhshfCaseStatus.ProfilingInProgress
            ? CurrentCycleNumber + 1
            : CurrentCycleNumber;
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

    // ── CAC company profile (SmartComply lookup, Phase B) ─────────────────
    // Fetched on demand in CRMS, not supplied by the portal — the submit payload carries only
    // RC/TIN/BOA account. Populated alongside the directors list by FetchRhshfCacDetailsCommand.
    public string? CacStatus { get; private set; }
    public string? CacEntityType { get; private set; }
    public string? CacRegistrationDate { get; private set; }
    public string? CacNatureOfBusiness { get; private set; }
    public decimal? CacShareCapital { get; private set; }
    public long? CacCompanyId { get; private set; }
    public string? CacAddress { get; private set; }
    public string? CacCity { get; private set; }
    public string? CacState { get; private set; }
    public string? CacRawJson { get; private set; }
    public DateTime? CacFetchedAt { get; private set; }

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

    private readonly List<RhshfDisbursement> _disbursements = [];
    public IReadOnlyCollection<RhshfDisbursement> Disbursements => _disbursements.AsReadOnly();

    private readonly List<RhshfPreDeploymentChecklistItem> _preDeploymentChecklist = [];
    public IReadOnlyCollection<RhshfPreDeploymentChecklistItem> PreDeploymentChecklist => _preDeploymentChecklist.AsReadOnly();

    private readonly List<RhshfDirector> _directors = [];
    public IReadOnlyList<RhshfDirector> Directors => _directors.AsReadOnly();

    private readonly List<RhshfFarmPlan> _farmPlans = [];
    public IReadOnlyList<RhshfFarmPlan> FarmPlans => _farmPlans.AsReadOnly();

    private readonly List<RhshfStageConfirmation> _stageConfirmations = [];
    public IReadOnlyList<RhshfStageConfirmation> StageConfirmations => _stageConfirmations.AsReadOnly();

    private readonly List<RhshfStatusHistory> _statusHistory = [];
    public IReadOnlyList<RhshfStatusHistory> StatusHistory => _statusHistory.AsReadOnly();

    private readonly List<RhshfFinancialAppraisalReport> _financialAppraisals = [];
    public IReadOnlyList<RhshfFinancialAppraisalReport> FinancialAppraisals => _financialAppraisals.AsReadOnly();

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
        string? tin,
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

        // The trail starts where the case does — otherwise the first entry a reader sees is the
        // FAC already midway through the form, with no record of the case arriving.
        profile.RecordTransition("Submission received from the portal", actorUserId: null, actorLabel: "Portal");

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
    public Result<RhshfSupportingDocument> AddSupportingDocument(
        RhshfDocumentCategory category, string fileName, string contentType, string storagePath, long sizeBytes)
    {
        if (CurrentStage != RhshfProfilingStage.SupportingDocuments)
            return Result.Failure<RhshfSupportingDocument>("Case is not on the supporting documents stage.");

        var document = new RhshfSupportingDocument(Id, category, fileName, contentType, storagePath, sizeBytes);
        _supportingDocuments.Add(document);
        UpdatedAt = DateTime.UtcNow;
        return Result.Success(document);
    }

    public Result RemoveSupportingDocument(Guid documentId)
    {
        if (CurrentStage != RhshfProfilingStage.SupportingDocuments)
            return Result.Failure("Documents can only be removed while the case is on the supporting documents stage.");

        var document = _supportingDocuments.FirstOrDefault(d => d.Id == documentId);
        if (document is null)
            return Result.Failure("Document not found.");

        _supportingDocuments.Remove(document);
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    /// <summary>Mandatory categories with nothing attached against them. Empty means the case can be
    /// submitted; anything here blocks it.</summary>
    public IReadOnlyList<RhshfDocumentCategory> MissingMandatoryDocuments(IEnumerable<RhshfDocumentRequirement> requirements)
    {
        var attached = _supportingDocuments.Select(d => d.Category).ToHashSet();
        return requirements
            .Where(r => r.IsActive && r.IsMandatory && !attached.Contains(r.Category))
            .Select(r => r.Category)
            .Distinct()
            .ToList();
    }

    /// <summary>Advances from one profiling stage to the next, or — from the final stage — completes
    /// profiling entirely (external status flips to UnderReview, §5). The single mechanism behind
    /// every "confirm and continue" action across all 5 stages; guards against skipping or replaying
    /// a stage via direct requests (Phase 3 test: "stage order is fixed and enforced server-side").</summary>
    public Result AdvanceStage(
        RhshfProfilingStage expectedCurrentStage,
        IEnumerable<RhshfDocumentRequirement>? documentRequirements = null,
        string? ipAddress = null,
        string? userAgent = null)
    {
        if (Status != RhshfCaseStatus.ProfilingPending && Status != RhshfCaseStatus.ProfilingInProgress)
            return Result.Failure("Profiling is not currently in progress for this case.");
        if (CurrentStage != expectedCurrentStage)
            return Result.Failure("Stage mismatch — cannot skip or replay a profiling stage.");

        // Mandatory documents are re-checked both on leaving the documents stage AND at final submit.
        // The submit re-check matters now that the FAC can navigate back (GoToStage): a document
        // removed on a revisited stage must not slip through a forward jump straight to submit.
        // Enforced here rather than in the page so a direct POST can't walk straight past it.
        if ((expectedCurrentStage == RhshfProfilingStage.SupportingDocuments
             || expectedCurrentStage == RhshfProfilingStage.ReviewAndSubmit)
            && documentRequirements is not null)
        {
            var missing = MissingMandatoryDocuments(documentRequirements);
            if (missing.Count > 0)
                return Result.Failure($"Required documents are missing: {string.Join(", ", missing)}.");
        }

        // A case cannot be submitted for credit review without the agronomic basis the appraisal
        // depends on — otherwise the Credit Officer inherits a case they cannot model.
        if (expectedCurrentStage == RhshfProfilingStage.ReviewAndSubmit
            && !_farmPlans.Any(p => p.CycleNumber == ProfilingTargetCycleNumber))
            return Result.Failure("At least one crop must be recorded in the farm plan before submitting.");

        // Every director must carry a BVN — the credit bureau is run per director, and one without a
        // BVN is a subject the bank cannot check. Blank is tolerated during data entry (above), but
        // not at the point the case leaves the FAC for credit review.
        if (expectedCurrentStage == RhshfProfilingStage.ReviewAndSubmit
            && _directors.Any(d => string.IsNullOrWhiteSpace(d.Bvn)))
            return Result.Failure("Every director must have a BVN before submitting — the bank runs a credit check on each one.");

        // Captured before the status flip below, which would otherwise shift ProfilingTargetCycleNumber.
        var targetCycle = ProfilingTargetCycleNumber;

        if (Status == RhshfCaseStatus.ProfilingPending)
            Status = RhshfCaseStatus.ProfilingInProgress;

        // Each stage is now attested, not merely traversed. Idempotent per stage per cycle: when the
        // FAC navigates back and re-continues through an already-attested stage, the original
        // attestation stands rather than stacking duplicates.
        if (!_stageConfirmations.Any(c => c.CycleNumber == targetCycle && c.Stage == expectedCurrentStage))
            _stageConfirmations.Add(new RhshfStageConfirmation(
                Id, targetCycle, expectedCurrentStage, FacId, ipAddress, userAgent));

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

        RecordTransition(
            expectedCurrentStage == RhshfProfilingStage.ReviewAndSubmit
                ? "Submitted for credit review"
                : $"Profiling stage confirmed: {Humanise(expectedCurrentStage)}",
            actorUserId: null, actorLabel: "FAC");

        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    /// <summary>The furthest profiling stage the FAC has legitimately reached this cycle — the
    /// high-water mark that bounds navigation. It is at least the current stage, and at least one
    /// past every stage already attested (a confirmation for stage N means N+1 was reached). Taking
    /// the max of both keeps it correct after a staff ReturnToFac, which can set CurrentStage forward
    /// without a confirmation in the new cycle. Null once profiling is complete.</summary>
    public RhshfProfilingStage? FurthestProfilingStageReached()
    {
        if (CurrentStage is null)
            return null;

        var frontier = (int)CurrentStage.Value;
        var cycle = ProfilingTargetCycleNumber;
        foreach (var c in _stageConfirmations.Where(c => c.CycleNumber == cycle))
        {
            var reached = c.Stage == RhshfProfilingStage.ReviewAndSubmit
                ? (int)RhshfProfilingStage.ReviewAndSubmit
                : (int)c.Stage + 1;
            if (reached > frontier)
                frontier = reached;
        }
        return (RhshfProfilingStage)frontier;
    }

    /// <summary>Backward/forward navigation within the stages the FAC has already reached, so they can
    /// revisit and correct a record set at an earlier stage. Distinct from AdvanceStage: it attests
    /// nothing and runs no gate, and it cannot cross the high-water mark — the frontier is only
    /// extended by confirming a stage (which does run that stage's gate). The staff ReturnToFac path
    /// is the equivalent after submission.</summary>
    public Result GoToStage(RhshfProfilingStage target)
    {
        if (Status != RhshfCaseStatus.ProfilingPending && Status != RhshfCaseStatus.ProfilingInProgress)
            return Result.Failure("Profiling is not currently in progress for this case.");

        var furthest = FurthestProfilingStageReached();
        if (furthest is null)
            return Result.Failure("Profiling is not currently in progress for this case.");

        if ((int)target < (int)RhshfProfilingStage.CompanyVerification || (int)target > (int)furthest.Value)
            return Result.Failure("Cannot navigate to a profiling stage you have not reached yet.");

        CurrentStage = target;
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

        // A Proceed decision requires a saved financial appraisal behind it. NAMP declares exactly
        // this guard (FinDecisionBlocked) and never binds it to anything, so its stage decision can
        // be submitted with no appraisal report at all. Decline and ReturnToFac are unguarded — an
        // officer must always be able to reject or send back a case without first modelling it.
        if (outcome == RhshfAppraisalOutcome.Proceed && GetCurrentCycleFinancialAppraisal() is null)
            return Result.Failure("A financial appraisal must be completed before proceeding to risk review.");

        _appraisals.Add(new RhshfAppraisal(Id, CurrentCycleNumber, creditOfficerId, outcome, notes));

        switch (outcome)
        {
            case RhshfAppraisalOutcome.Proceed:
                InternalStage = RhshfInternalStage.RiskReview;
                RecordTransition("Appraisal completed — proceeding to risk review", creditOfficerId, note: notes);
                break;
            case RhshfAppraisalOutcome.ReturnToFac:
                ReturnToFac(returnToStage, "Returned to the FAC at appraisal", creditOfficerId, notes);
                break;
            case RhshfAppraisalOutcome.Decline:
                Decline("CRMS Appraisal", notes, "Declined at appraisal", creditOfficerId);
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
                RecordTransition("Risk review cleared — circulated to committee", riskOfficerId, note: notes);
                break;
            case RhshfRiskReviewOutcome.ReturnToFac:
                ReturnToFac(returnToStage, "Returned to the FAC at risk review", riskOfficerId, notes);
                break;
            case RhshfRiskReviewOutcome.Decline:
                Decline("CRMS Risk Review", notes, "Declined at risk review", riskOfficerId);
                break;
        }

        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    /// <summary>Shared by every stage's ReturnToFac outcome (design doc §6 #7) — resets external
    /// status so the FAC re-enters the form; the next AdvanceStage(ReviewAndSubmit) opens a fresh
    /// cycle (§6 #8).
    ///
    /// The completed cycle's farm plan is copied into the new one. A return-to-FAC asks for a
    /// specific correction; it is not a reason to make the FAC retype crop economics nobody
    /// questioned — and submission is gated on a plan existing, so without this a case returned
    /// over (say) a missing document could not be resubmitted at all. The copy is independent:
    /// editing it during profiling leaves the appraised cycle's figures intact for the audit trail.</summary>
    private void ReturnToFac(RhshfProfilingStage? returnToStage, string action, Guid? actorUserId, string? note)
    {
        var completedCycle = CurrentCycleNumber;

        Status = RhshfCaseStatus.ProfilingInProgress;
        CurrentStage = returnToStage ?? RhshfProfilingStage.ReviewAndSubmit;
        InternalStage = null;

        CarryFarmPlanForward(completedCycle);
        RecordTransition(action, actorUserId, note: note);
    }

    private void CarryFarmPlanForward(int completedCycle)
    {
        var nextCycle = ProfilingTargetCycleNumber;
        if (_farmPlans.Any(p => p.CycleNumber == nextCycle))
            return;

        foreach (var plan in _farmPlans.Where(p => p.CycleNumber == completedCycle).ToList())
        {
            var copy = RhshfFarmPlan.Create(
                Id, nextCycle, plan.Crop, plan.Hectares,
                plan.ExpectedYieldKgPerHectare, plan.ExpectedPricePerKg, FacId);
            if (copy.IsSuccess)
                _farmPlans.Add(copy.Value);
        }
    }

    /// <summary>Shared by every stage's Decline outcome — terminal, fires the domain event Phase 10's
    /// webhook dispatcher listens for.</summary>
    private void Decline(string decidedBy, string? notes, string action, Guid? actorUserId)
        => Terminate(RhshfCaseStatus.Declined, RhshfDecisionOutcome.Declined, decidedBy, notes, action, actorUserId);

    /// <summary>Shared terminal-transition helper. DecisionOutcome is null for a terminal status
    /// that isn't actually a credit decision (design doc §6 #10) — e.g. a FAC withdrawing from
    /// their own offer (Cancelled) is a lapse, not an adjudication, unlike Declined.</summary>
    private void Terminate(
        RhshfCaseStatus status, RhshfDecisionOutcome? outcome, string decidedBy, string? notes,
        string action, Guid? actorUserId, string? actorLabel = null)
    {
        Status = status;
        DecisionOutcome = outcome;
        DecidedAt = DateTime.UtcNow;
        DecidedBy = decidedBy;
        DecisionNotes = notes;
        InternalStage = null;
        RecordTransition(action, actorUserId, actorLabel, notes);
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
    public Result AdvanceToRatification(Guid? decidingVoterId = null, string? note = null)
    {
        if (Status != RhshfCaseStatus.UnderReview || InternalStage != RhshfInternalStage.CommitteeVoting)
            return Result.Failure("Case is not at the committee voting stage.");

        InternalStage = RhshfInternalStage.Ratification;
        RecordTransition("Committee approved — sent for ratification", decidingVoterId, "Committee", note);
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    /// <summary>Committee voting reached Rejected — terminal.</summary>
    public Result DeclineAtCommittee(string? notes, Guid? decidingVoterId = null)
    {
        if (Status != RhshfCaseStatus.UnderReview || InternalStage != RhshfInternalStage.CommitteeVoting)
            return Result.Failure("Case is not at the committee voting stage.");

        Terminate(RhshfCaseStatus.Declined, RhshfDecisionOutcome.Declined, "CRMS Committee", notes,
            "Committee rejected the application", decidingVoterId, "Committee");
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    /// <summary>Committee sent the case back to the FAC before reaching a vote tally.</summary>
    public Result ReturnToFacFromCommittee(RhshfProfilingStage? returnToStage, Guid? actorUserId = null, string? notes = null)
    {
        if (Status != RhshfCaseStatus.UnderReview || InternalStage != RhshfInternalStage.CommitteeVoting)
            return Result.Failure("Case is not at the committee voting stage.");

        ReturnToFac(returnToStage, "Returned to the FAC by the committee", actorUserId, notes);
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    /// <summary>Final Approver's ratification — fourth stage (design doc §3.6, Phase 6). Ratified
    /// requires approvedAmount == TotalEopValue exactly (design doc §6 #1) — no partial approval in
    /// v1. excludedActorIds is the union of that cycle's Appraisal/RiskReview actors and the
    /// committee members who voted Approve (computed by the Application layer, since committee
    /// voting lives in a separate aggregate this method has no access to). On Ratified, advances to
    /// AwaitingOfferAcceptance directly — offer generation itself is an Application-layer side
    /// effect (PDF rendering, file storage), not something a domain method can do.
    /// Note: unlike Appraise/ReviewRisk, there is no "already ratified this cycle" guard — Legal
    /// Clearance's Returned outcome (Phase 8) sends InternalStage back to Ratification within the
    /// SAME cycle so the Final Approver can re-ratify; the stage guard above is what actually gates
    /// re-entry (every other path out of Ratification sets InternalStage away from Ratification).</summary>
    public Result Ratify(
        Guid finalApproverId, RhshfRatificationOutcome outcome, decimal? approvedAmount, string? notes,
        RhshfProfilingStage? returnToStage, IReadOnlyCollection<Guid> excludedActorIds)
    {
        if (Status != RhshfCaseStatus.UnderReview || InternalStage != RhshfInternalStage.Ratification)
            return Result.Failure("Case is not at the ratification stage.");
        if (excludedActorIds.Contains(finalApproverId))
            return Result.Failure("The Final Approver must be a different person from this cycle's appraiser, risk officer, and approving committee members.");
        if (outcome == RhshfRatificationOutcome.Ratified && approvedAmount != TotalEopValue)
            return Result.Failure("Approved amount must equal the total EOP value exactly — no partial approval in v1.");

        _ratifications.Add(new RhshfRatification(Id, CurrentCycleNumber, finalApproverId, outcome, approvedAmount, notes));

        switch (outcome)
        {
            case RhshfRatificationOutcome.Ratified:
                // Pre-Phase-9, nothing read this off the profile itself (only off the
                // RhshfRatification child record) — Disbursement (Phase 9) is the first stage that
                // needs the ratified amount available directly on the aggregate.
                ApprovedAmount = approvedAmount;
                InternalStage = RhshfInternalStage.AwaitingOfferAcceptance;
                RecordTransition("Ratified — offer issued, awaiting FAC acceptance", finalApproverId, note: notes);
                AddDomainEvent(new RhshfOfferReadyEvent(Id));
                break;
            case RhshfRatificationOutcome.ReturnToFac:
                ReturnToFac(returnToStage, "Returned to the FAC at ratification", finalApproverId, notes);
                break;
            case RhshfRatificationOutcome.Declined:
                Decline("CRMS Ratification", notes, "Declined at ratification", finalApproverId);
                break;
        }

        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    /// <summary>FAC accepted the offer (design doc §3.6, Phase 7) — advances to LegalClearance.
    /// Called by the Application layer once RhshfOffer.Accept() succeeds, since the offer itself
    /// (and its signed-document precondition) lives in a separate aggregate.</summary>
    public Result AdvanceToLegalClearance()
    {
        if (Status != RhshfCaseStatus.UnderReview || InternalStage != RhshfInternalStage.AwaitingOfferAcceptance)
            return Result.Failure("Case is not awaiting offer acceptance.");

        InternalStage = RhshfInternalStage.LegalClearance;
        RecordTransition("Offer accepted — sent for legal clearance", actorUserId: null, actorLabel: "FAC");
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    /// <summary>FAC rejected the offer — a withdrawal, not a bank decision (design doc §6 #10), so
    /// this maps to Cancelled rather than Declined and DecisionOutcome stays null.</summary>
    public Result CancelDueToOfferRejection(string? notes)
    {
        if (Status != RhshfCaseStatus.UnderReview || InternalStage != RhshfInternalStage.AwaitingOfferAcceptance)
            return Result.Failure("Case is not awaiting offer acceptance.");

        Terminate(RhshfCaseStatus.Cancelled, outcome: null, decidedBy: "FAC", notes: notes,
            action: "Offer rejected by the FAC — case cancelled", actorUserId: null, actorLabel: "FAC");
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    /// <summary>That cycle's ratifying Final Approver, if any — the Application layer needs this to
    /// enforce the Legal Officer/Final Approver distinctness check when creating an
    /// RhshfLegalClearance record (design doc §3.6, Phase 8), since legal clearance lives in its own
    /// aggregate with no access to this profile's Ratifications collection.</summary>
    public Guid? GetCurrentCycleFinalApproverId()
        => _ratifications.Where(r => r.CycleNumber == CurrentCycleNumber)
            .OrderByDescending(r => r.RatifiedAt).FirstOrDefault()?.FinalApproverId;

    /// <summary>Legal Clearance Granted — fifth stage (design doc §3.6, Phase 8). Called by the
    /// Application layer once RhshfLegalClearance.Create() succeeds with Granted, since legal
    /// clearance lives in a separate aggregate. Advances to PreDeploymentVerification (not straight
    /// to Disbursement) — an auto-transition, same as NAMP's GrantLegalClearance; the checklist
    /// itself is seeded separately (SeedPreDeploymentChecklist) since it needs the active template
    /// list from the Application layer.</summary>
    public Result AdvanceToDisbursement(Guid? legalOfficerId = null, string? note = null)
    {
        if (Status != RhshfCaseStatus.UnderReview || InternalStage != RhshfInternalStage.LegalClearance)
            return Result.Failure("Case is not at the legal clearance stage.");

        InternalStage = RhshfInternalStage.PreDeploymentVerification;
        RecordTransition("Legal clearance granted — pre-deployment verification opened", legalOfficerId, note: note);
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    /// <summary>Instantiates the active checklist templates onto this cycle — idempotent, skips if
    /// this cycle already has items (mirrors NAMP's seed-on-first-access pattern).</summary>
    public void SeedPreDeploymentChecklist(IEnumerable<RhshfPreDeploymentChecklistTemplate> templates)
    {
        if (_preDeploymentChecklist.Any(i => i.CycleNumber == CurrentCycleNumber))
            return;

        foreach (var template in templates)
            _preDeploymentChecklist.Add(RhshfPreDeploymentChecklistItem.FromTemplate(Id, CurrentCycleNumber, template));
    }

    /// <summary>Disbursement Officer confirming (or unconfirming) one gate item — only while the case
    /// is actually at PreDeploymentVerification.</summary>
    public Result ConfirmPreDeploymentChecklistItem(Guid itemId, Guid userId, bool? isConfirmed, string? notes)
    {
        if (Status != RhshfCaseStatus.UnderReview || InternalStage != RhshfInternalStage.PreDeploymentVerification)
            return Result.Failure("Case is not at the pre-deployment verification stage.");

        var item = _preDeploymentChecklist.FirstOrDefault(i => i.Id == itemId && i.CycleNumber == CurrentCycleNumber);
        if (item is null)
            return Result.Failure("Checklist item not found for this cycle.");

        item.SetConfirmation(userId, isConfirmed, notes);
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    /// <summary>Gate cleared — advances to Disbursement. Blocks if any mandatory item for this cycle
    /// is still unconfirmed (mirrors NAMP's CompletePreDeploymentVerification).</summary>
    public Result CompletePreDeploymentVerification(Guid userId, string? note)
    {
        if (Status != RhshfCaseStatus.UnderReview || InternalStage != RhshfInternalStage.PreDeploymentVerification)
            return Result.Failure("Case is not at the pre-deployment verification stage.");

        var cycleItems = _preDeploymentChecklist.Where(i => i.CycleNumber == CurrentCycleNumber).ToList();
        if (cycleItems.Count == 0)
            return Result.Failure("No checklist items have been recorded for this cycle.");
        if (cycleItems.Any(i => i.BlocksCompletion))
            return Result.Failure("All mandatory checklist items must be confirmed before completing verification.");

        InternalStage = RhshfInternalStage.Disbursement;
        RecordTransition("Pre-deployment verification completed — cleared for disbursement", userId, note: note);
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    /// <summary>Legal Clearance Returned — routes back to Ratification, not Appraisal, for the same
    /// cycle (design doc §6 #12: a legal issue isn't a re-appraisal of the credit). Unlike every
    /// other ReturnToFac path, the case stays UnderReview — this is an internal re-route, not a
    /// round-trip to the FAC.</summary>
    public Result ReturnToRatificationFromLegal(Guid? legalOfficerId = null, string? note = null)
    {
        if (Status != RhshfCaseStatus.UnderReview || InternalStage != RhshfInternalStage.LegalClearance)
            return Result.Failure("Case is not at the legal clearance stage.");

        InternalStage = RhshfInternalStage.Ratification;
        RecordTransition("Returned to ratification by Legal", legalOfficerId, note: note);
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    /// <summary>Legal Clearance Declined — terminal, same as every other stage's Decline (design doc
    /// §6 #9's timing question applies the same way it does to Phase 7's offer rejection: a negative
    /// terminal outcome is reported immediately, unlike the deferred "Approved" signal).</summary>
    public Result DeclineAtLegalClearance(string? notes, Guid? legalOfficerId = null)
    {
        if (Status != RhshfCaseStatus.UnderReview || InternalStage != RhshfInternalStage.LegalClearance)
            return Result.Failure("Case is not at the legal clearance stage.");

        Decline("CRMS Legal Clearance", notes, "Declined at legal clearance", legalOfficerId);
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    /// <summary>Disbursement Officer's booking attempt — sixth and final stage (design doc §3.6,
    /// Phase 9). disbursedAmount must equal the ratified ApprovedAmount — checked here regardless of
    /// outcome, since the Application layer always constructs the booking request off ApprovedAmount
    /// and a mismatch means something upstream is wrong, not a legitimate Fineract failure to record.
    /// On Booked: this is the ONLY point that maps external status to Approved (design doc §6 #9 —
    /// deliberately deferred past Ratification/LegalClearance/AwaitingOfferAcceptance, any of which
    /// could still have killed the case). On Failed: recorded for audit/retry, no state change — the
    /// case stays at Disbursement, not regressed to an earlier stage.</summary>
    public Result RecordDisbursementAttempt(
        Guid disbursementOfficerId, decimal disbursedAmount, string supplierAccountNumber, string? supplierName,
        RhshfDisbursementStatus status, long? fineractLoanId, string? fineractLoanAccountNumber, string? failureReason)
    {
        if (Status != RhshfCaseStatus.UnderReview || InternalStage != RhshfInternalStage.Disbursement)
            return Result.Failure("Case is not at the disbursement stage.");
        if (disbursedAmount != ApprovedAmount)
            return Result.Failure("Disbursed amount must equal the ratified approved amount.");
        if (string.IsNullOrWhiteSpace(supplierAccountNumber))
            return Result.Failure("The input-supplier account number is required to record a disbursement.");

        _disbursements.Add(new RhshfDisbursement(
            Id, CurrentCycleNumber, disbursementOfficerId, disbursedAmount, supplierAccountNumber, supplierName,
            status, fineractLoanId, fineractLoanAccountNumber, failureReason));

        if (status == RhshfDisbursementStatus.Booked)
        {
            // Active, not the legacy Completed marker — the case now enters live post-disbursement
            // monitoring (Phase 3). Status flips to Approved here and stays there through
            // Active/Closed; that's the credit decision, permanent and unaffected by what happens to
            // InternalStage afterward.
            InternalStage = RhshfInternalStage.Active;
            Status = RhshfCaseStatus.Approved;
            DecisionOutcome = RhshfDecisionOutcome.Approved;
            DecidedAt = DateTime.UtcNow;
            DecidedBy = "CRMS Disbursement";
            DecisionNotes = fineractLoanAccountNumber is not null
                ? $"Booked as Fineract loan {fineractLoanAccountNumber}."
                : null;
            RecordTransition("Disbursed and booked — loan now active", disbursementOfficerId, note: DecisionNotes);
            AddDomainEvent(new RhshfCaseDecidedEvent(Id));
        }
        else
        {
            // No state change, but a failed booking attempt is exactly the kind of thing someone
            // later asks "why did this sit at disbursement for a week" about.
            RecordTransition("Disbursement attempt failed — case remains at disbursement",
                disbursementOfficerId, note: failureReason);
        }

        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    // ── Directors / shareholders (Phase B) ────────────────────────────────

    /// <summary>Records the CAC company profile fetched from SmartComply.</summary>
    public void SetCacCompanyProfile(
        string? status, string? entityType, string? registrationDate, string? natureOfBusiness,
        decimal? shareCapital, long? companyId, string? address, string? city, string? state, string? rawJson)
    {
        CacStatus = status;
        CacEntityType = entityType;
        CacRegistrationDate = registrationDate;
        CacNatureOfBusiness = natureOfBusiness;
        CacShareCapital = shareCapital;
        CacCompanyId = companyId;
        CacAddress = address;
        CacCity = city;
        CacState = state;
        CacRawJson = rawJson;
        CacFetchedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Finds an existing CAC-sourced director by CAC id (or, when CAC supplied no id, by name).
    /// Manually-added directors are never matched — a CAC refresh must not absorb or overwrite them.
    /// </summary>
    public RhshfDirector? FindCacDirector(long? cacDirectorId, string fullName) =>
        _directors.FirstOrDefault(d =>
            d.SourcedFromCac &&
            ((cacDirectorId.HasValue && d.CacDirectorId == cacDirectorId) ||
             (!cacDirectorId.HasValue && string.Equals(d.FullName, fullName, StringComparison.OrdinalIgnoreCase))));

    public void AddDirector(RhshfDirector director)
    {
        _directors.Add(director);
        UpdatedAt = DateTime.UtcNow;
    }

    public Result RemoveDirector(Guid directorId)
    {
        var director = _directors.FirstOrDefault(d => d.Id == directorId);
        if (director is null)
            return Result.Failure("Director not found.");

        // Any director may be removed. The old "CAC-sourced directors cannot be removed" guard assumed
        // CAC refresh/prune owned those rows — but CAC is no longer merged into the case list (it is a
        // read-only cross-check now), so the only way to clear a stale CAC- or CBS-origin row left over
        // from the old merging behaviour is to delete it here.
        _directors.Remove(director);
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    /// <summary>Drops CAC-sourced directors that a newer CAC response no longer lists, keeping every
    /// manually-added one. Mirrors FetchNampCacDetailsCommand's prune step.</summary>
    public void PruneCacDirectorsNotIn(IEnumerable<Guid> keepIds)
    {
        var keep = keepIds.ToHashSet();
        _directors.RemoveAll(d => d.SourcedFromCac && !keep.Contains(d.Id));
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Directors with a usable BVN — the subject list for per-director bureau checks.
    /// A method, not a property: an IReadOnlyList&lt;RhshfDirector&gt; property reads to EF Core as a
    /// second navigation to the same entity and makes it invent a shadow FK column.</summary>
    public IReadOnlyList<RhshfDirector> GetDirectorsWithBvn() =>
        _directors.Where(d => !string.IsNullOrWhiteSpace(d.Bvn)).ToList();

    // ── Farm plans & financial appraisal (Phase C) ────────────────────────

    /// <summary>Farm plans for the cycle currently under review.</summary>
    public IReadOnlyList<RhshfFarmPlan> GetCurrentCycleFarmPlans() =>
        _farmPlans.Where(p => p.CycleNumber == CurrentCycleNumber).ToList();

    /// <summary>Farm plans for whichever cycle is currently being worked on — the in-flight
    /// profiling cycle when the FAC is filling the form, the review cycle once submitted.</summary>
    public IReadOnlyList<RhshfFarmPlan> GetTargetCycleFarmPlans() =>
        _farmPlans.Where(p => p.CycleNumber == ProfilingTargetCycleNumber).ToList();

    /// <summary>FAC-side farm plan capture during profiling, gated to the EOP Review stage where the
    /// production assumptions naturally sit alongside the input package they justify.</summary>
    public Result AddFarmPlanDuringProfiling(string crop, decimal hectares, decimal yieldPerHa, decimal pricePerKg)
    {
        if (CurrentStage != RhshfProfilingStage.EopReview)
            return Result.Failure("The farm plan can only be edited on the EOP review stage.");

        var planResult = RhshfFarmPlan.Create(
            Id, ProfilingTargetCycleNumber, crop, hectares, yieldPerHa, pricePerKg, FacId);
        if (planResult.IsFailure)
            return Result.Failure(planResult.Error);

        if (_farmPlans.Any(p => p.CycleNumber == ProfilingTargetCycleNumber
                                && string.Equals(p.Crop, crop.Trim(), StringComparison.OrdinalIgnoreCase)))
            return Result.Failure($"A farm plan for '{crop.Trim()}' already exists — edit it instead.");

        _farmPlans.Add(planResult.Value);
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    public Result RemoveFarmPlanDuringProfiling(Guid planId)
    {
        if (CurrentStage != RhshfProfilingStage.EopReview)
            return Result.Failure("The farm plan can only be edited on the EOP review stage.");

        var plan = _farmPlans.FirstOrDefault(p => p.Id == planId && p.CycleNumber == ProfilingTargetCycleNumber);
        if (plan is null)
            return Result.Failure("Farm plan not found.");

        _farmPlans.Remove(plan);
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    /// <summary>This cycle's financial appraisal, if one has been saved.</summary>
    public RhshfFinancialAppraisalReport? GetCurrentCycleFinancialAppraisal() =>
        _financialAppraisals.FirstOrDefault(r => r.CycleNumber == CurrentCycleNumber);

    public Result AddFarmPlan(RhshfFarmPlan plan)
    {
        if (_farmPlans.Any(p => p.CycleNumber == CurrentCycleNumber
                                && string.Equals(p.Crop, plan.Crop, StringComparison.OrdinalIgnoreCase)))
            return Result.Failure($"A farm plan for '{plan.Crop}' already exists in this cycle — edit it instead.");

        _farmPlans.Add(plan);
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    public Result UpdateFarmPlan(Guid planId, decimal hectares, decimal expectedYieldKgPerHectare, decimal expectedPricePerKg)
    {
        var plan = _farmPlans.FirstOrDefault(p => p.Id == planId && p.CycleNumber == CurrentCycleNumber);
        if (plan is null)
            return Result.Failure("Farm plan not found for this cycle.");

        var result = plan.Update(hectares, expectedYieldKgPerHectare, expectedPricePerKg);
        if (result.IsFailure) return result;

        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    public Result RemoveFarmPlan(Guid planId)
    {
        var plan = _farmPlans.FirstOrDefault(p => p.Id == planId && p.CycleNumber == CurrentCycleNumber);
        if (plan is null)
            return Result.Failure("Farm plan not found for this cycle.");

        _farmPlans.Remove(plan);
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    /// <summary>
    /// Saves (or re-saves) this cycle's financial appraisal. The financed input cost is always the
    /// case's own TotalEopValue — it is not an officer-entered figure, so it cannot drift from the
    /// facility actually being approved.
    /// </summary>
    public Result SaveFinancialAppraisal(
        Guid preparedByUserId, decimal ownProductionCost, decimal harvestAndLogisticsCost,
        int cycleMonths, decimal interestRatePercent, string? assumptionBasisNote,
        RhshfAppraisalThresholds thresholds, RhshfCreditRecommendation recommendation,
        string? summaryNotes, string? overrideJustification)
    {
        if (Status != RhshfCaseStatus.UnderReview || InternalStage != RhshfInternalStage.Appraisal)
            return Result.Failure("Case is not at the appraisal stage.");

        var plans = GetCurrentCycleFarmPlans();
        var existing = GetCurrentCycleFinancialAppraisal();

        if (existing is not null)
        {
            var updateResult = existing.Update(
                preparedByUserId, ownProductionCost, harvestAndLogisticsCost, cycleMonths, interestRatePercent,
                assumptionBasisNote, plans, TotalEopValue, thresholds, recommendation, summaryNotes, overrideJustification);
            if (updateResult.IsFailure) return updateResult;
        }
        else
        {
            var createResult = RhshfFinancialAppraisalReport.Create(
                Id, CurrentCycleNumber, preparedByUserId, ownProductionCost, harvestAndLogisticsCost,
                cycleMonths, interestRatePercent, assumptionBasisNote, plans, TotalEopValue,
                thresholds, recommendation, summaryNotes, overrideJustification);
            if (createResult.IsFailure) return Result.Failure(createResult.Error);

            _financialAppraisals.Add(createResult.Value);
        }

        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    /// <summary>Loan fully repaid — manual attestation by the Disbursement Officer once the core
    /// banking system reports the Fineract loan closed (mirrors NAMP's trivial Close(); no Fineract
    /// call here — this just records the CRMS-side lifecycle marker).</summary>
    public Result MarkClosed(Guid userId)
    {
        if (InternalStage != RhshfInternalStage.Active)
            return Result.Failure("Case must be Active to mark it closed.");

        InternalStage = RhshfInternalStage.Closed;
        RecordTransition("Loan closed — fully repaid", userId);
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    // ── Status history (Phase E) ──────────────────────────────────────────

    /// <summary>
    /// Writes one audit-trail row for the state the case has just moved into.
    ///
    /// Called *after* the mutation, so it snapshots the resulting state rather than restating what
    /// the caller intended — the two cannot drift. Every method that changes Status, InternalStage
    /// or CurrentStage ends in a call to this; RhshfStatusHistoryTests walks the whole lifecycle and
    /// asserts the trail matches the states actually visited, which is what keeps a future
    /// transition from being added without one.
    /// </summary>
    private void RecordTransition(string action, Guid? actorUserId, string? actorLabel = null, string? note = null)
    {
        // Profiling rows belong to the cycle being assembled, not the last completed one — the same
        // reasoning as RhshfStageConfirmation, so the two line up when read side by side.
        var cycle = CurrentStage is not null ? ProfilingTargetCycleNumber : CurrentCycleNumber;

        _statusHistory.Add(new RhshfStatusHistory(
            Id, cycle, Status, InternalStage, CurrentStage, action, actorUserId, actorLabel, note));
    }

    /// <summary>"SupportingDocuments" → "Supporting Documents". The trail is read by people, and a
    /// PascalCase enum name in a sentence reads like a leaked identifier.</summary>
    private static string Humanise(RhshfProfilingStage stage) =>
        System.Text.RegularExpressions.Regex.Replace(stage.ToString(), "(?<!^)([A-Z])", " $1");

    private static string GenerateReference()
    {
        // Random 6-digit suffix rather than a true incrementing sequence — avoids a concurrency
        // hazard (concurrent submits racing for the "next" number) for cosmetic sequentiality the
        // brief doesn't actually require. Mirrors NAMP's own NampStagingRecord approach.
        var suffix = Random.Shared.Next(0, 1_000_000).ToString("D6");
        return $"RHSHF-{DateTime.UtcNow:yyyy}-{suffix}";
    }
}

/// <summary>Raised whenever a case reaches a terminal (or terminal-like) outcome — Approved,
/// Declined, or Cancelled (a FAC withdrawal, §6 #10, which isn't really "decided" but is still the
/// end of the case) — from any of the pipeline's several possible trigger points (design doc §6 #9).
/// Phase 10's webhook dispatcher listens for this — kept minimal (just the id) since the handler
/// re-loads the aggregate to build the actual webhook payload, rather than duplicating that shape
/// here.</summary>
public record RhshfCaseDecidedEvent(Guid RhshfCreditProfileId) : DomainEvent;

/// <summary>Raised when Ratify() reaches the Ratified outcome (design doc §3.6, Phase 6) — added
/// retroactively in Phase 10, since Phase 6 built Ratify() without it. Non-terminal: the case stays
/// UnderReview (§6 #9), but the portal needs a heads-up to send the FAC back to review the offer
/// (§6 #10's non-terminal actionRequired: "REVIEW_OFFER" signal). Fires once per Ratified outcome,
/// including a re-ratification after a Legal Clearance Returned within the same cycle (Phase 8) —
/// each is its own offer the FAC must separately act on.</summary>
public record RhshfOfferReadyEvent(Guid RhshfCreditProfileId) : DomainEvent;

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

    // ── Itemised financing (portal interface change, 14 Oct 2026) ─────────
    // The rows the offer letter prints, as the portal computed them. Null/empty on cases submitted
    // before the portal sent the breakdown — the letter then falls back to the crop-level EopLines
    // and a single balancing row for whatever the lines do not account for.
    //
    // The appraisal and monitoring fees are deliberately absent: they are NOT financed. They sit on
    // the core banking loan product and are deducted at booking, so CRMS reads them from the product
    // rather than storing a copy that could drift from what is actually charged.
    private readonly List<RhshfFinancingLine> _financingLines = [];
    public IReadOnlyList<RhshfFinancingLine> FinancingLines => _financingLines.AsReadOnly();

    private readonly List<RhshfFinancingCharge> _financingCharges = [];
    public IReadOnlyList<RhshfFinancingCharge> FinancingCharges => _financingCharges.AsReadOnly();

    /// <summary>Totals as the portal sent them, kept rather than recomputed so a mismatch between
    /// their arithmetic and ours stays visible instead of being silently papered over.</summary>
    public decimal? FinancingInputsTotal { get; private set; }
    public decimal? FinancingPackagingExcessTotal { get; private set; }
    public decimal? FinancingChargesTotal { get; private set; }
    public decimal? ApprovedEopTotal { get; private set; }
    public decimal? FinancedLoanAmount { get; private set; }

    /// <summary>Approved but not financed under programme policy. Zero today — but it exists, so
    /// nothing may assume the approved total equals the financed amount.</summary>
    public decimal? ExcludedFromLoan { get; private set; }

    /// <summary>True once the portal has sent the itemised breakdown for this case.</summary>
    public bool HasItemisedFinancing => _financingLines.Count > 0 || _financingCharges.Count > 0;

    /// <summary>
    /// What the EOP total does not account for once the crop-level lines are taken off — the single
    /// balancing row the letter prints for a case with no itemised breakdown, so the plan still foots
    /// to the approved amount. Meaningless once <see cref="HasItemisedFinancing"/> is true, where the
    /// real rows are available and reconcile exactly.
    /// </summary>
    public decimal UnitemisedRemainder =>
        TotalEopValue - _eopLines.Sum(l => l.LineValue);

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
        IEnumerable<RhshfEopLineInput>? eopLines,
        Guid? resolvedBranchId,
        Guid? resolvedOfficeId,
        RhshfFinancingInput? financing = null)
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
            profile._eopLines.Add(new RhshfEopLine(
                profile.Id, line.Commodity, line.QuantityKg, line.UnitPricePerKg, line.LineValue,
                line.Supplier, line.Product, line.UnitOfMeasurement));

        if (financing is not null)
        {
            foreach (var line in financing.InputLines)
                profile._financingLines.Add(new RhshfFinancingLine(
                    profile.Id, line.EopId, line.Crop, line.Item, line.Category, line.SpecificInput,
                    line.Supplier, line.QuantityPerFarmer, line.Unit, line.UnitPrice, line.Basis,
                    line.Farmers, line.Amount, line.PackagingExcess, line.InLoan));

            foreach (var charge in financing.Charges)
                profile._financingCharges.Add(new RhshfFinancingCharge(
                    profile.Id, charge.Code, charge.Label, charge.Rate, charge.RateBasis,
                    charge.Amount, charge.InLoan));

            profile.FinancingInputsTotal = financing.InputsTotal;
            profile.FinancingPackagingExcessTotal = financing.PackagingExcessTotal;
            profile.FinancingChargesTotal = financing.ChargesTotal;
            profile.ApprovedEopTotal = financing.ApprovedEopTotal;
            profile.FinancedLoanAmount = financing.LoanAmount;
            profile.ExcludedFromLoan = financing.ExcludedFromLoan;

            var reconciliation = profile.CheckFinancingReconciles(totalEopValue);
            if (reconciliation.IsFailure)
                return Result.Failure<RhshfCreditProfile>(reconciliation.Error);
        }

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

    /// <summary>
    /// Attaches a copy of a document the same FAC supplied on an earlier session, so an unchanged
    /// document (a Credit Appraisal Memorandum, typically) need not be re-uploaded season after
    /// season. The caller copies the bytes first and passes the new storage path — this aggregate
    /// never shares a blob between two cases, so removing one case's copy cannot break the other's.
    /// </summary>
    public Result<RhshfSupportingDocument> AddReusedSupportingDocument(
        RhshfDocumentCategory category, string fileName, string contentType, string storagePath,
        long sizeBytes, Guid sourceDocumentId, string sourceReference, DateTime originalUploadedAt)
    {
        if (CurrentStage != RhshfProfilingStage.SupportingDocuments)
            return Result.Failure<RhshfSupportingDocument>("Case is not on the supporting documents stage.");

        // One carried-over copy per category is the whole point — a second adds nothing and would
        // leave the FAC unable to tell the duplicates apart.
        if (_supportingDocuments.Any(d => d.Category == category))
            return Result.Failure<RhshfSupportingDocument>(
                "This case already has a document in that category. Remove it first to carry a different one over.");

        var document = new RhshfSupportingDocument(Id, category, fileName, contentType, storagePath, sizeBytes);
        document.MarkReusedFrom(sourceDocumentId, sourceReference, originalUploadedAt);
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
            {
                // Named by the requirement's admin-editable Title, not the enum member. The two can
                // differ deliberately — the programme team renames documents (FarmerRegister is
                // titled "Farmers Register") and the FAC must be told the name it was shown on the
                // upload slot, not CRMS's internal identifier.
                var names = missing
                    .Select(c => documentRequirements.FirstOrDefault(r => r.Category == c)?.Title ?? c.ToString())
                    .ToList();
                return Result.Failure($"Required documents are missing: {string.Join(", ", names)}.");
            }
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

        // Both human-driven paths already refuse a total over 100%, so this only catches what an
        // import brought in (the Bank of Agriculture register carries its own percentages). The
        // import is allowed to land — failing it halfway would strand the director list — and the
        // case is stopped here instead, where the FAC can see the figure and correct it.
        //
        // Deliberately NOT a check for exactly 100%: this screen is not a complete ownership
        // register. Non-director shareholders need not be listed and nothing attests that the list
        // is complete, so a FAC whose minority holders are absent would be blocked — and the
        // realistic response to that is inflating a director to 100%, which is worse data than an
        // honest 85%. An under-allocated total is surfaced as advice, not a gate.
        if (expectedCurrentStage == RhshfProfilingStage.ReviewAndSubmit && IsShareholdingOverAllocated)
            return Result.Failure(
                $"Declared shareholding totals {DeclaredShareholdingTotal:0.##}%, which is more than 100%. " +
                "Correct the directors' percentages before submitting.");

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
            AddDomainEvent(new RhshfPortalUpdateEvent(Id, CurrentCycleNumber == 1
                ? RhshfCallbackEventType.SubmittedForReview
                : RhshfCallbackEventType.CorrectionSubmitted));
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
        if (InternalStage != RhshfInternalStage.Appraisal)
            return Result.Failure("Case is not at the appraisal stage.");
        if (_appraisals.Any(a => a.CycleNumber == CurrentCycleNumber))
            return Result.Failure("This cycle has already been appraised.");

        // A Proceed decision requires a saved financial appraisal behind it. NAMP declares exactly
        // this guard (FinDecisionBlocked) and never binds it to anything, so its stage decision can
        // be submitted with no appraisal report at all. Decline and ReturnToFac are unguarded — an
        // officer must always be able to reject or send back a case without first modelling it.
        if (outcome == RhshfAppraisalOutcome.Proceed && GetCurrentCycleFinancialAppraisal() is null)
            return Result.Failure("A financial appraisal must be completed before proceeding to legal clearance.");

        _appraisals.Add(new RhshfAppraisal(Id, CurrentCycleNumber, creditOfficerId, outcome, notes));

        switch (outcome)
        {
            case RhshfAppraisalOutcome.Proceed:
                InternalStage = RhshfInternalStage.LegalClearance;
                RecordTransition("Appraisal completed — sent for legal clearance", creditOfficerId, note: notes);
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

    /// <summary>Risk Officer's review — now the LAST internal look before the case is circulated to
    /// committee, so it runs after legal clearance rather than before it. Must differ from that cycle's
    /// appraising Credit Officer and from its Legal Officer; the InternalStage guard is what enforces
    /// that legal clearance has already happened (a case only reaches RiskReview through it).
    /// legalOfficerId is supplied by the Application layer from that cycle's RhshfLegalClearance, which
    /// is a separate aggregate this one cannot read.</summary>
    public Result ReviewRisk(Guid riskOfficerId, RhshfRiskReviewOutcome outcome, string? notes,
        RhshfProfilingStage? returnToStage = null, Guid? legalOfficerId = null)
    {
        if (Status != RhshfCaseStatus.UnderReview)
            return Result.Failure("Case is not under review.");
        if (InternalStage != RhshfInternalStage.RiskReview)
            return Result.Failure("Case is not at the risk review stage.");

        var appraisal = _appraisals.FirstOrDefault(a => a.CycleNumber == CurrentCycleNumber);
        if (appraisal is null || appraisal.Outcome != RhshfAppraisalOutcome.Proceed)
            return Result.Failure("This cycle has not been appraised with a Proceed outcome yet.");
        if (appraisal.CreditOfficerId == riskOfficerId)
            return Result.Failure("The Risk Officer must be a different person from the Credit Officer who appraised this case.");
        if (legalOfficerId == riskOfficerId)
            return Result.Failure("The Risk Officer must be a different person from the Legal Officer who cleared this case.");
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
        AddDomainEvent(new RhshfPortalUpdateEvent(Id, RhshfCallbackEventType.CorrectionRequired));
    }

    /// <summary>Every ReturnToFac caller's action text starts with this; it is how the history row
    /// that carries the officer's reason is told apart from the FAC's own profiling rows.</summary>
    private const string ReturnedToFacAction = "Returned to the FAC";

    /// <summary>The return the FAC has not yet answered by resubmitting — null when the case is not
    /// back with the FAC after a review. The officer's note is the reason shown to the FAC; the
    /// stage is where the form was reopened. Read from the audit trail rather than stored twice.</summary>
    public RhshfStatusHistory? GetOutstandingReturn()
    {
        if (Status != RhshfCaseStatus.ProfilingInProgress || CurrentCycleNumber == 0)
            return null;

        return _statusHistory
            .Where(h => h.Action.StartsWith(ReturnedToFacAction, StringComparison.Ordinal))
            .OrderByDescending(h => h.ChangedAt)
            .FirstOrDefault();
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

    /// <summary>FAC accepted the offer (design doc §3.6, Phase 7) — advances straight to
    /// PreDeploymentVerification. Legal clearance already happened before the committee voted, so there
    /// is no legal stage left on this side of the decision; the gate's CollateralReview item is where
    /// the security is confirmed still valid. Called by the Application layer once RhshfOffer.Accept()
    /// succeeds, since the offer itself (and its signed-document precondition) lives in a separate
    /// aggregate — the same call site also seeds the pre-deployment checklist.</summary>
    public Result AdvanceToPreDeploymentFromOffer()
    {
        if (Status != RhshfCaseStatus.UnderReview || InternalStage != RhshfInternalStage.AwaitingOfferAcceptance)
            return Result.Failure("Case is not awaiting offer acceptance.");

        InternalStage = RhshfInternalStage.PreDeploymentVerification;
        RecordTransition("Offer accepted — pre-deployment verification opened", actorUserId: null, actorLabel: "FAC");
        AddDomainEvent(new RhshfPortalUpdateEvent(Id, RhshfCallbackEventType.OfferAccepted));
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    /// <summary>Every corrected-offer transition's action text starts with this; OfferVersion counts them.</summary>
    private const string CorrectedOfferAction = "Corrected offer issued";

    /// <summary>
    /// An accepted offer was wrong and a corrected one is being issued in its place. Sends the case
    /// back to awaiting the FAC's acceptance; the credit decision — amount and terms as ratified —
    /// is unchanged, so this is not a fresh ratification. Only before disbursement: once the loan is
    /// booked, the accepted terms are what core banking holds, and a correction there is an
    /// amendment, not a reissue. The pre-deployment items already confirmed stay confirmed; seeding
    /// is idempotent per cycle, so re-acceptance does not duplicate them.
    /// </summary>
    public Result ReopenOfferForCorrection(Guid actorUserId, string reason)
    {
        if (Status != RhshfCaseStatus.UnderReview
            || InternalStage is not (RhshfInternalStage.PreDeploymentVerification or RhshfInternalStage.Disbursement))
            return Result.Failure("A corrected offer can only be issued after the offer is accepted and before disbursement.");
        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure("Say what was wrong with the accepted offer.");

        InternalStage = RhshfInternalStage.AwaitingOfferAcceptance;
        RecordTransition($"{CorrectedOfferAction} — awaiting FAC acceptance", actorUserId, note: reason.Trim());
        AddDomainEvent(new RhshfOfferReadyEvent(Id));
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    /// <summary>Which offer the FAC is looking at: one per ratification that issued an offer, plus one
    /// per correction. Lets the portal tell a corrected offer from the one already accepted.</summary>
    public int OfferVersion =>
        _ratifications.Count(r => r.Outcome == RhshfRatificationOutcome.Ratified)
        + _statusHistory.Count(h => h.Action.StartsWith(CorrectedOfferAction, StringComparison.Ordinal));

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

    /// <summary>That cycle's ratifying Final Approver, if any. A read accessor for the Application
    /// layer's maker-checker exclusion sets; it no longer gates legal clearance, which now runs before
    /// any ratification exists.</summary>
    public Guid? GetCurrentCycleFinalApproverId()
        => _ratifications.Where(r => r.CycleNumber == CurrentCycleNumber)
            .OrderByDescending(r => r.RatifiedAt).FirstOrDefault()?.FinalApproverId;

    /// <summary>Legal cleared the security — hands the case to Risk, the last internal review before
    /// circulation. Called by the Application layer once RhshfLegalClearance.Create() succeeds, since
    /// legal clearance lives in a separate aggregate. Covers both forward outcomes (a clean grant, and
    /// a grant with a documented caveat); the caveat itself rides on the clearance record, which Risk
    /// and then the committee read.</summary>
    public Result AdvanceToRiskReviewFromLegal(Guid? legalOfficerId = null, string? note = null)
    {
        if (Status != RhshfCaseStatus.UnderReview || InternalStage != RhshfInternalStage.LegalClearance)
            return Result.Failure("Case is not at the legal clearance stage.");

        InternalStage = RhshfInternalStage.RiskReview;
        RecordTransition("Legal clearance completed — proceeding to risk review", legalOfficerId, note: note);
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

    /// <summary>
    /// The committee approved on the basis that the security would be perfected before disbursement,
    /// so that obligation becomes a mandatory gate item. Without this the deferral would be a note
    /// nobody enforced and the case would sail to disbursement unperfected. Idempotent per cycle, and
    /// safe to call before or after the templates are seeded.
    /// </summary>
    public void AddPerfectionConditionToPreDeploymentChecklist()
    {
        if (_preDeploymentChecklist.Any(i => i.CycleNumber == CurrentCycleNumber && i.Title == PerfectionConditionTitle))
            return;

        _preDeploymentChecklist.Add(RhshfPreDeploymentChecklistItem.FromCommitteeCondition(
            Id, CurrentCycleNumber, PerfectionConditionTitle,
            "The committee approved this facility on the basis that the security would be perfected before " +
            "disbursement. Confirm the instrument is perfected and the evidence is on file.",
            RhshfPreDeploymentVerificationKind.CollateralReview,
            sortOrder: 15));

        UpdatedAt = DateTime.UtcNow;
    }

    public const string PerfectionConditionTitle = "Collateral Perfection Completed (committee condition)";

    /// <summary>Disbursement Officer confirming (or unconfirming) one gate item — only while the case
    /// is actually at PreDeploymentVerification.</summary>
    public Result ConfirmPreDeploymentChecklistItem(Guid itemId, Guid userId, bool? isConfirmed, string? notes)
    {
        if (Status != RhshfCaseStatus.UnderReview || InternalStage != RhshfInternalStage.PreDeploymentVerification)
            return Result.Failure("Case is not at the pre-deployment verification stage.");

        var item = _preDeploymentChecklist.FirstOrDefault(i => i.Id == itemId && i.CycleNumber == CurrentCycleNumber);
        if (item is null)
            return Result.Failure("Checklist item not found for this cycle.");
        if (!item.IsOfficerConfirmable)
            return Result.Failure("This item is verified automatically from the signed offer documents and can't be confirmed by hand.");

        item.SetConfirmation(userId, isConfirmed, notes);
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    /// <summary>Re-derives the auto (offer-documents) items for the current cycle from whether the signed
    /// offer package is on file. Called by the Application layer (which owns the offer aggregate) before
    /// completion and on display, so these items always reflect reality rather than a stale tick.</summary>
    public void SyncAutoOfferDocumentItems(bool offerDocumentsOnFile)
    {
        foreach (var item in _preDeploymentChecklist.Where(i =>
                     i.CycleNumber == CurrentCycleNumber && i.Kind == RhshfPreDeploymentVerificationKind.OfferDocuments))
            item.SetAutoSatisfaction(offerDocumentsOnFile);
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

    /// <summary>Legal Clearance Returned — goes back to the FAC to fix the security it declared.
    /// Legal now runs before the committee, so the old "return to Ratification" target no longer
    /// exists at this point in the flow: the defect is in what the FAC supplied, and the FAC is who
    /// can correct it. Reserved for fixable problems — falsification and the like are a Decline.</summary>
    public Result ReturnToFacFromLegal(RhshfProfilingStage? returnToStage = null, Guid? legalOfficerId = null, string? note = null)
    {
        if (Status != RhshfCaseStatus.UnderReview || InternalStage != RhshfInternalStage.LegalClearance)
            return Result.Failure("Case is not at the legal clearance stage.");

        ReturnToFac(returnToStage, "Returned to the FAC at legal clearance", legalOfficerId, note);
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
                ? $"Booked as Core Banking loan {fineractLoanAccountNumber}."
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

    /// <summary>
    /// The portal's reconciliation rules 1 to 3, re-checked here on receipt at its request.
    /// <para>
    /// The portal refuses to send a case that fails them, so this is a second pair of eyes rather
    /// than the only one — and that is the point: the approved EOP, this offer letter and the core
    /// banking booking have to describe one financing structure, with nothing financed also deducted
    /// as a charge. A breakdown that does not balance would put an implementation plan in front of a
    /// borrower that does not add up to the amount they are signing for.
    /// </para>
    /// <para>
    /// Tolerance is one kobo. The portal rounds supplier-pack unit prices down and rounds the
    /// per-farmer charges before multiplying out, so two systems will not always agree on the last
    /// kobo — but a naira apart is a real disagreement.
    /// </para>
    /// </summary>
    private Result CheckFinancingReconciles(decimal totalEopValue)
    {
        const decimal tolerance = 0.01m;

        // Rule 1: inputs + packaging excess + charges = approved EOP total. Summed from the rows
        // rather than trusting the portal's own totals — agreeing with itself is not the check.
        var lineAmounts = _financingLines.Sum(l => l.Amount);
        var packagingExcess = _financingLines.Sum(l => l.PackagingExcess);
        var chargeAmounts = _financingCharges.Sum(c => c.Amount);
        var breakdown = lineAmounts + packagingExcess + chargeAmounts;
        var approvedTotal = ApprovedEopTotal ?? totalEopValue;

        if (Math.Abs(approvedTotal - breakdown) > tolerance)
            return Result.Failure(
                $"Financing does not reconcile (rule 1): input lines {lineAmounts:N2} plus packaging excess " +
                $"{packagingExcess:N2} plus charges {chargeAmounts:N2} total {breakdown:N2}, but the approved " +
                $"EOP total is {approvedTotal:N2} (difference {Math.Abs(approvedTotal - breakdown):N2}).");

        // Rule 2: the rows flagged as financed sum to the loan amount. Everything is financed today,
        // so this is the rule that will catch the first case where it is not.
        var financed = _financingLines.Where(l => l.InLoan).Sum(l => l.Amount + l.PackagingExcess)
                       + _financingCharges.Where(c => c.InLoan).Sum(c => c.Amount);
        var loanAmount = FinancedLoanAmount ?? totalEopValue;

        if (Math.Abs(loanAmount - financed) > tolerance)
            return Result.Failure(
                $"Financing does not reconcile (rule 2): the rows marked as financed total {financed:N2}, " +
                $"but the loan amount is {loanAmount:N2} (difference {Math.Abs(loanAmount - financed):N2}).");

        // Rule 3: the loan amount is the figure CRMS profiles and approves. If these ever diverge,
        // the case would be approved for one amount and the letter printed from another.
        if (Math.Abs(loanAmount - totalEopValue) > tolerance)
            return Result.Failure(
                $"Financing does not reconcile (rule 3): the loan amount is {loanAmount:N2}, but the " +
                $"submitted EOP value is {totalEopValue:N2} (difference {Math.Abs(loanAmount - totalEopValue):N2}).");

        return Result.Success();
    }

    /// <summary>
    /// Total shareholding declared across every director/shareholder on the case. Nulls count as
    /// nothing — an unstated holding is unknown, not zero, which is exactly why this total is
    /// advisory below 100% (see <see cref="IsShareholdingOverAllocated"/>).
    /// </summary>
    public decimal DeclaredShareholdingTotal =>
        _directors.Sum(d => d.ShareholdingPercent ?? 0m);

    /// <summary>
    /// Headroom left before the declared total would exceed 100%, optionally ignoring one director —
    /// pass the id of the row being edited, or its own current holding counts against it and
    /// lowering 60% to 55% would be refused against itself. Never negative: an already
    /// over-allocated case (imported from core banking) reports no headroom rather than a negative.
    /// </summary>
    public decimal RemainingShareholding(Guid? excludingDirectorId = null)
    {
        var allocated = _directors
            .Where(d => excludingDirectorId is null || d.Id != excludingDirectorId)
            .Sum(d => d.ShareholdingPercent ?? 0m);
        return Math.Max(0m, 100m - allocated);
    }

    /// <summary>True when the declared total is already past 100% — only reachable through an
    /// import, since both human-driven paths refuse it. Blocks submission rather than the import
    /// itself.</summary>
    public bool IsShareholdingOverAllocated => DeclaredShareholdingTotal > 100m;

    /// <summary>
    /// Adds a director whose declared holding must fit inside what is left of 100%.
    /// <para>
    /// Deliberately NOT the path used by the CAC and core-banking imports — see
    /// <see cref="ImportDirector"/>. The cap belongs on the paths a person drives, where there is
    /// someone to correct the number; failing an import halfway would leave a partial director list
    /// and no way forward.
    /// </para>
    /// </summary>
    public Result AddDeclaredDirector(RhshfDirector director)
    {
        var check = CheckShareholdingFits(director.ShareholdingPercent, excludingDirectorId: null);
        if (check.IsFailure)
            return check;

        _directors.Add(director);
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    /// <summary>
    /// Adds a director from an external record (CAC, or the Bank of Agriculture's own register).
    /// Uncapped on purpose: rejecting the bank's data at the door would strand the import, so an
    /// over-allocated total is allowed in and blocks submission instead, where the FAC can see the
    /// figure and fix it.
    /// </summary>
    public void ImportDirector(RhshfDirector director)
    {
        _directors.Add(director);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Changes one director's declared holding, keeping the case's total within 100%. Lives on the
    /// aggregate because a single director cannot see its siblings' holdings — the whole reason the
    /// old per-director 0-100 check let 60% + 50% through.
    /// </summary>
    public Result UpdateDirectorShareholding(Guid directorId, decimal? shareholdingPercent)
    {
        var director = _directors.FirstOrDefault(d => d.Id == directorId);
        if (director is null)
            return Result.Failure("Director not found on this case.");

        var check = CheckShareholdingFits(shareholdingPercent, excludingDirectorId: directorId);
        if (check.IsFailure)
            return check;

        var result = director.UpdateShareholding(shareholdingPercent);
        if (result.IsFailure)
            return result;

        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    /// <summary>States the arithmetic in the failure: a bare "cannot exceed 100%" leaves the user
    /// guessing which of several directors to change and by how much.</summary>
    private Result CheckShareholdingFits(decimal? proposedPercent, Guid? excludingDirectorId)
    {
        if (!proposedPercent.HasValue)
            return Result.Success();

        var allocatedElsewhere = _directors
            .Where(d => excludingDirectorId is null || d.Id != excludingDirectorId)
            .Sum(d => d.ShareholdingPercent ?? 0m);

        if (allocatedElsewhere + proposedPercent.Value <= 100m)
            return Result.Success();

        return Result.Failure(
            $"Declared shareholding would total {allocatedElsewhere + proposedPercent.Value:0.##}%. " +
            $"The other directors already account for {allocatedElsewhere:0.##}%, so this one cannot exceed " +
            $"{Math.Max(0m, 100m - allocatedElsewhere):0.##}%.");
    }

    public Result RemoveDirector(Guid directorId)
    {
        var director = _directors.FirstOrDefault(d => d.Id == directorId);
        if (director is null)
            return Result.Failure("Director not found.");

        // Staff may not delete what the FAC declared — that would let an officer quietly remove a
        // submitted director (manipulation). Removable: CAC/stale rows left over from the old merging
        // behaviour, and the officer's own manual additions. FAC-declared rows are protected.
        if (director.DeclaredByFac)
            return Result.Failure("Directors declared by the FAC cannot be removed by staff.");

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

/// <summary>Raised at the non-terminal points where the FAC's next step changes but nothing is
/// decided: submitted, returned for correction, resubmitted, offer accepted. Without these the
/// portal only heard about the offer and the final outcome, and a case returned to the FAC sat as
/// "under review" on the portal until someone phoned them.</summary>
public record RhshfPortalUpdateEvent(Guid RhshfCreditProfileId, RhshfCallbackEventType EventType) : DomainEvent;

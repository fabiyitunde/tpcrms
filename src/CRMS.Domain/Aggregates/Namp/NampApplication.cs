using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Namp;

/// <summary>
/// The core NAMP application aggregate. Created when a Loan Officer recalls a NampStagingRecord.
/// Tracks the full lifecycle from Draft through Active / Closed.
/// </summary>
public class NampApplication : AggregateRoot
{
    // ── Identity ───────────────────────────────────────────────────────────
    public string ApplicationNumber { get; private set; } = string.Empty;
    public string ApplicationReference { get; private set; } = string.Empty;  // External PAYS ref
    public NampApplicationStatus Status { get; private set; }

    // ── Product ────────────────────────────────────────────────────────────
    public Guid LoanProductId { get; private set; }

    // ── Applicant ──────────────────────────────────────────────────────────
    public string ApplicantName { get; private set; } = string.Empty;
    public string BoaAccountNumber { get; private set; } = string.Empty;
    public string? ApplicantPhone { get; private set; }
    public string? ApplicantEmail { get; private set; }
    public NampApplicantCategory ApplicantCategory { get; private set; }

    // ── Extended Applicant Info (unpacked from RawPayload at recall) ────────
    public string? Nin { get; private set; }
    public string? Bvn { get; private set; }
    public string? BoaAccountName { get; private set; }
    public string? LoanPurpose { get; private set; }
    public string? IndustrySector { get; private set; }
    public int? RequestedTenorMonths { get; private set; }
    public string? DateOfBirth { get; private set; }
    public string? StateOfResidence { get; private set; }
    public string? LocalGovernmentArea { get; private set; }
    public string? CompanyName { get; private set; }
    public string? RcNumber { get; private set; }

    // ── CAC Company Profile (Agro-Service; fetched from SmartComply at recall/draft) ──
    public string? CacStatus { get; private set; }
    public string? CacEntityType { get; private set; }
    public string? CacRegistrationDate { get; private set; }
    public string? CacNatureOfBusiness { get; private set; }
    public decimal? CacShareCapital { get; private set; }
    public long? CacCompanyId { get; private set; }
    public string? CacAddress { get; private set; }
    public string? CacCity { get; private set; }
    public string? CacState { get; private set; }
    public DateTime? CacFetchedAt { get; private set; }
    public string? CacRawJson { get; private set; }

    // ── Individual Credit Profile (null for MechanisationCompany) ──────────
    public string? Occupation { get; private set; }
    public string? EmployerName { get; private set; }
    public string? EmploymentStatus { get; private set; }
    public int? YearsOfExperience { get; private set; }
    public int? NumberOfDependants { get; private set; }
    public decimal? EstimatedMonthlyIncome { get; private set; }
    public string? OtherIncomeSource { get; private set; }
    public decimal? OtherMonthlyIncome { get; private set; }
    public decimal? MonthlyLivingExpenses { get; private set; }
    public string? OwnedAssetsDescription { get; private set; }
    public decimal? EstimatedNetWorth { get; private set; }
    public string? ExistingLoanObligations { get; private set; }

    // ── Equity & Loan Amounts ──────────────────────────────────────────────
    public decimal? EquityPercent { get; private set; }
    public decimal? CartTotal { get; private set; }
    public decimal? EquityAmount { get; private set; }
    public decimal? LoanAmount { get; private set; }

    // ── Equipment ──────────────────────────────────────────────────────────
    public string EquipmentDescription { get; private set; } = string.Empty;
    public decimal EquipmentValue { get; private set; }
    public string? EquipmentCartJson { get; private set; }

    // ── Branch / Routing ───────────────────────────────────────────────────
    public Guid BranchId { get; private set; }
    public Guid? OfficeId { get; private set; }
    public Guid? LocationId { get; private set; }
    public NampCommitteeTier CommitteeTier { get; private set; }

    // ── Stage 1: Loan Officer ──────────────────────────────────────────────
    public Guid RecalledByUserId { get; private set; }
    public DateTime RecalledAt { get; private set; }
    public DateTime? SubmittedAt { get; private set; }

    // ── Stage 2: Financial Appraisal ──────────────────────────────────────
    public Guid? FinancialAppraisalByUserId { get; private set; }
    public DateTime? FinancialAppraisalAt { get; private set; }
    public string? FinancialAppraisalNote { get; private set; }

    // ── Stage 3: Risk Review ───────────────────────────────────────────────
    public Guid? RiskReviewedByUserId { get; private set; }
    public DateTime? RiskReviewedAt { get; private set; }
    public string? RiskReviewNote { get; private set; }
    public string? RiskReturnNote { get; private set; }
    public string? RiskDeclineNote { get; private set; }

    // ── Stage 4: Committee ────────────────────────────────────────────────
    public Guid? CurrentCommitteeReviewId { get; private set; }
    public DateTime? CommitteeDecisionAt { get; private set; }
    public Guid? CommitteeDecisionByUserId { get; private set; }
    public string? CommitteeDecisionNote { get; private set; }

    // ── Stage 5: Ratification ─────────────────────────────────────────────
    public Guid? RatifiedByUserId { get; private set; }
    public DateTime? RatifiedAt { get; private set; }
    public string? RatificationDeclineNote { get; private set; }

    // ── Stage 5b: Offer ───────────────────────────────────────────────────
    public string? OfferLetterStoragePath { get; private set; }
    public string? LeaseAgreementStoragePath { get; private set; }
    public string? GpsConsentFormStoragePath { get; private set; }
    public DateTime? OfferGeneratedAt { get; private set; }
    public DateTime? OfferAcceptedAt { get; private set; }
    public Guid? OfferAcceptedByUserId { get; private set; }
    public DateTime? OfferLapsedAt { get; private set; }

    // ── Stage 5c: Legal Clearance ─────────────────────────────────────────
    public Guid? LegalClearedByUserId { get; private set; }
    public DateTime? LegalClearedAt { get; private set; }
    public string? LegalClearanceNote { get; private set; }
    public string? LegalDeclineNote { get; private set; }
    public string? LegalReturnNote { get; private set; }

    // ── Stage 6: Pre-Deployment Verification ──────────────────────────────
    public Guid? PreDeploymentVerifiedByUserId { get; private set; }
    public DateTime? PreDeploymentVerifiedAt { get; private set; }
    public string? PreDeploymentNote { get; private set; }

    // ── Stage 6: Deployment ───────────────────────────────────────────────
    public Guid? DeployedByUserId { get; private set; }
    public DateTime? DeployedAt { get; private set; }
    public bool GpsActivated { get; private set; }
    public string? DeploymentNote { get; private set; }

    // ── Fineract Integration ───────────────────────────────────────────────
    public long? FineractClientId { get; private set; }              // Resolved at recall from BOA account lookup
    public int? FineractProductId { get; private set; }              // Resolved at recall from active NAMP loan product
    public string? FineractProductName { get; private set; }         // Resolved at recall
    public decimal? FineractNominalInterestRate { get; private set; } // Annual interest rate from Fineract product catalogue at recall
    public decimal? ApprovedInterestRate { get; private set; }       // Locked at ratification (may differ if committee overrides)
    public long? FineractLoanId { get; private set; }                // Set after deployment booking
    public string? FineractLoanAccountNumber { get; private set; }   // Set after deployment booking

    // ── Collections ───────────────────────────────────────────────────────
    private readonly List<NampDocument> _documents = [];
    private readonly List<NampStatusHistory> _statusHistory = [];
    private readonly List<NampGuarantor> _guarantors = [];
    private readonly List<NampCollateral> _collaterals = [];
    private readonly List<NampFinancialStatement> _financialStatements = [];
    private readonly List<NampPreDeploymentChecklistItem> _preDeploymentChecklist = [];
    private readonly List<NampDirector> _directors = [];

    public IReadOnlyList<NampDocument> Documents => _documents.AsReadOnly();
    public IReadOnlyList<NampStatusHistory> StatusHistory => _statusHistory.AsReadOnly();
    public IReadOnlyList<NampGuarantor> Guarantors => _guarantors.AsReadOnly();
    public IReadOnlyList<NampCollateral> Collaterals => _collaterals.AsReadOnly();
    public IReadOnlyList<NampFinancialStatement> FinancialStatements => _financialStatements.AsReadOnly();
    public IReadOnlyList<NampPreDeploymentChecklistItem> PreDeploymentChecklist => _preDeploymentChecklist.AsReadOnly();
    public IReadOnlyList<NampDirector> Directors => _directors.AsReadOnly();

    private NampApplication() { }

    // ── Factory ───────────────────────────────────────────────────────────

    public static Result<NampApplication> Create(
        string applicationNumber,
        Guid loanProductId,
        string applicationReference,
        string applicantName,
        string boaAccountNumber,
        NampApplicantCategory applicantCategory,
        string equipmentDescription,
        decimal equipmentValue,
        Guid branchId,
        NampCommitteeTier committeeTier,
        Guid recalledByUserId,
        Guid? officeId = null,
        Guid? locationId = null,
        string? applicantPhone = null,
        string? applicantEmail = null)
    {
        if (string.IsNullOrWhiteSpace(applicantName))
            return Result.Failure<NampApplication>("Applicant name is required.");

        if (equipmentValue <= 0)
            return Result.Failure<NampApplication>("Equipment value must be greater than zero.");

        var app = new NampApplication
        {
            ApplicationNumber = applicationNumber,
            ApplicationReference = applicationReference,
            Status = NampApplicationStatus.Draft,
            LoanProductId = loanProductId,
            ApplicantName = applicantName,
            BoaAccountNumber = boaAccountNumber,
            ApplicantCategory = applicantCategory,
            EquipmentDescription = equipmentDescription,
            EquipmentValue = equipmentValue,
            BranchId = branchId,
            OfficeId = officeId,
            LocationId = locationId,
            CommitteeTier = committeeTier,
            RecalledByUserId = recalledByUserId,
            RecalledAt = DateTime.UtcNow,
            ApplicantPhone = applicantPhone,
            ApplicantEmail = applicantEmail,
        };

        app.AddStatusHistory(NampApplicationStatus.Draft, recalledByUserId, "Application recalled from staging by Loan Officer.");
        return Result.Success(app);
    }

    // ── Stage 1: Loan Officer ──────────────────────────────────────────────

    public Result Submit(Guid userId)
    {
        if (Status != NampApplicationStatus.Draft)
            return Result.Failure("Application must be in Draft to submit.");

        // AgroService companies are credit-checked at the company AND director/shareholder level on
        // submission. Every director must have a BVN, otherwise that person cannot be bureau-checked.
        if (ApplicantCategory == NampApplicantCategory.AgroServiceCompany)
        {
            if (_directors.Count == 0)
                return Result.Failure(
                    "Add the company's directors/shareholders (Fetch from CAC or add manually) before submitting — each must be credit-checked.");

            var missingBvn = _directors
                .Where(d => string.IsNullOrWhiteSpace(d.Bvn))
                .Select(d => d.FullName)
                .ToList();
            if (missingBvn.Count > 0)
                return Result.Failure(
                    $"These directors/shareholders are missing a BVN and cannot be credit-checked: {string.Join(", ", missingBvn)}. Complete their BVNs before submitting.");
        }

        Status = NampApplicationStatus.Submitted;
        SubmittedAt = DateTime.UtcNow;
        AddStatusHistory(Status, userId, "Submitted for financial appraisal.");
        return Result.Success();
    }

    // ── Stage 2: Financial Appraisal ──────────────────────────────────────

    public Result SubmitFinancialAppraisal(Guid userId, bool isApproved, string? note)
    {
        if (Status != NampApplicationStatus.FinancialAppraisal && Status != NampApplicationStatus.Submitted)
            return Result.Failure("Application must be in Submitted or FinancialAppraisal status.");

        FinancialAppraisalByUserId = userId;
        FinancialAppraisalAt = DateTime.UtcNow;
        FinancialAppraisalNote = note;

        Status = isApproved ? NampApplicationStatus.RiskReview : NampApplicationStatus.FinancialDeclined;
        var outcome = isApproved ? "Financial appraisal approved — forwarded to Risk Officer for review." : "Financial appraisal declined.";
        AddStatusHistory(Status, userId, $"{outcome}{(note != null ? $" Note: {note}" : "")}");
        return Result.Success();
    }

    // ── Stage 3: Risk Review ──────────────────────────────────────────────

    public Result ApproveRiskReview(Guid userId, string? note)
    {
        if (Status != NampApplicationStatus.RiskReview)
            return Result.Failure("Application must be in Risk Review status.");

        RiskReviewedByUserId = userId;
        RiskReviewedAt = DateTime.UtcNow;
        RiskReviewNote = note;
        Status = TierCirculationStatus();
        AddStatusHistory(Status, userId, $"Risk review approved — circulating to committee.{(note != null ? $" Note: {note}" : "")}");
        return Result.Success();
    }

    public Result ReturnFromRiskReview(Guid userId, string reason)
    {
        if (Status != NampApplicationStatus.RiskReview)
            return Result.Failure("Application must be in Risk Review status.");
        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure("A return reason is required.");

        RiskReviewedByUserId = userId;
        RiskReviewedAt = DateTime.UtcNow;
        RiskReturnNote = reason;
        Status = NampApplicationStatus.FinancialAppraisal;
        AddStatusHistory(Status, userId, $"Returned to Financial Appraisal by Risk Officer. Reason: {reason}");
        return Result.Success();
    }

    public Result DeclineAtRiskReview(Guid userId, string reason)
    {
        if (Status != NampApplicationStatus.RiskReview)
            return Result.Failure("Application must be in Risk Review status.");
        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure("A decline reason is required.");

        RiskReviewedByUserId = userId;
        RiskReviewedAt = DateTime.UtcNow;
        RiskDeclineNote = reason;
        Status = NampApplicationStatus.RiskDeclined;
        AddStatusHistory(Status, userId, $"Declined by Risk Officer. Reason: {reason}");
        return Result.Success();
    }

    public Result ReturnFinancialAppraisalToLoanOfficer(Guid userId, string reason)
    {
        if (Status != NampApplicationStatus.FinancialAppraisal && Status != NampApplicationStatus.Submitted)
            return Result.Failure("Application must be in Financial Appraisal status to return.");

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure("A return reason is required.");

        Status = NampApplicationStatus.Draft;
        AddStatusHistory(Status, userId, $"Returned to Loan Officer by Credit Officer. Reason: {reason}");
        return Result.Success();
    }

    // ── Stage 4: Committee ────────────────────────────────────────────────

    public Result CirculateToCommittee(Guid committeeReviewId, Guid userId)
    {
        var expectedStatus = TierCirculationStatus();
        if (Status != expectedStatus)
            return Result.Failure($"Application must be in {expectedStatus} status to circulate to committee.");

        CurrentCommitteeReviewId = committeeReviewId;
        // Status stays at circulation status (CommitteeReview tracks voting internally)
        AddStatusHistory(Status, userId, $"Committee review created (Id: {committeeReviewId}).");
        return Result.Success();
    }

    public Result RecordCommitteeOutcome(Guid userId, bool isApproved, string? note)
    {
        var expectedStatus = TierCirculationStatus();
        if (Status != expectedStatus)
            return Result.Failure($"Application must be in {expectedStatus} status for committee outcome.");

        CommitteeDecisionAt = DateTime.UtcNow;
        CommitteeDecisionByUserId = userId;
        CommitteeDecisionNote = note;

        Status = isApproved ? NampApplicationStatus.Ratification : TierDeclinedStatus();
        var outcome = isApproved ? "Committee approved — pending ratification." : "Committee declined.";
        AddStatusHistory(Status, userId, $"{outcome}{(note != null ? $" Note: {note}" : "")}");
        return Result.Success();
    }

    // ── Stage 5: Ratification ─────────────────────────────────────────────

    public Result Ratify(
        Guid userId,
        string? offerLetterPath = null,
        string? note = null,
        string? leaseAgreementPath = null,
        string? gpsConsentFormPath = null)
    {
        if (Status != NampApplicationStatus.Ratification)
            return Result.Failure("Application must be in Ratification status.");

        RatifiedByUserId = userId;
        RatifiedAt = DateTime.UtcNow;
        OfferLetterStoragePath = offerLetterPath;
        LeaseAgreementStoragePath = leaseAgreementPath;
        GpsConsentFormStoragePath = gpsConsentFormPath;
        OfferGeneratedAt = DateTime.UtcNow;
        Status = NampApplicationStatus.OfferGenerated;
        var historyNote = string.IsNullOrWhiteSpace(note)
            ? "Decision ratified. Offer documents generated."
            : $"Decision ratified. Offer documents generated. Note: {note}";
        AddStatusHistory(Status, userId, historyNote);
        return Result.Success();
    }

    public Result DeclineRatification(Guid userId, string? note)
    {
        if (Status != NampApplicationStatus.Ratification)
            return Result.Failure("Application must be in Ratification status.");

        RatifiedByUserId = userId;
        RatifiedAt = DateTime.UtcNow;
        RatificationDeclineNote = note;
        Status = NampApplicationStatus.RatificationDeclined;
        AddStatusHistory(Status, userId, $"Ratification declined.{(note != null ? $" Note: {note}" : "")}");
        return Result.Success();
    }

    // ── Stage 5b: Offer ───────────────────────────────────────────────────

    public Result RecordOfferAcceptance(Guid userId)
    {
        if (Status != NampApplicationStatus.OfferGenerated)
            return Result.Failure("Application must be in OfferGenerated status.");

        OfferAcceptedAt = DateTime.UtcNow;
        OfferAcceptedByUserId = userId;
        Status = NampApplicationStatus.OfferAccepted;
        AddStatusHistory(Status, userId, "Offer letter countersigned by applicant.");
        return Result.Success();
    }

    // ── Stage 5c: Legal Clearance ─────────────────────────────────────────

    public Result BeginLegalClearance(Guid userId)
    {
        if (Status != NampApplicationStatus.OfferAccepted)
            return Result.Failure("Application must be in OfferAccepted status.");

        Status = NampApplicationStatus.LegalClearance;
        AddStatusHistory(Status, userId, "Routed to Legal Officer for clearance.");
        return Result.Success();
    }

    public Result GrantLegalClearance(Guid userId, string? note)
    {
        if (Status != NampApplicationStatus.LegalClearance)
            return Result.Failure("Application must be in LegalClearance status.");

        LegalClearedByUserId = userId;
        LegalClearedAt = DateTime.UtcNow;
        LegalClearanceNote = note;
        Status = NampApplicationStatus.PreDeploymentVerification;
        AddStatusHistory(Status, userId, $"Legal clearance granted. Pre-deployment verification started.{(note != null ? $" Note: {note}" : "")}");
        return Result.Success();
    }

    public Result ReturnFromLegal(Guid userId, string note)
    {
        if (Status != NampApplicationStatus.LegalClearance)
            return Result.Failure("Application must be in LegalClearance status.");

        LegalReturnNote = note;
        Status = NampApplicationStatus.LegalReturned;
        AddStatusHistory(Status, userId, $"Returned by Legal Officer. Reason: {note}");
        return Result.Success();
    }

    public Result ResubmitToLegal(Guid userId)
    {
        if (Status != NampApplicationStatus.LegalReturned)
            return Result.Failure("Application must be in LegalReturned status.");

        Status = NampApplicationStatus.LegalClearance;
        AddStatusHistory(Status, userId, "Resubmitted to Legal Officer after remediation.");
        return Result.Success();
    }

    public Result DeclineLegal(Guid userId, string note)
    {
        if (Status != NampApplicationStatus.LegalClearance)
            return Result.Failure("Application must be in LegalClearance status.");

        LegalDeclineNote = note;
        Status = NampApplicationStatus.LegalDeclined;
        AddStatusHistory(Status, userId, $"Declined by Legal Officer. Reason: {note}");
        return Result.Success();
    }

    public Result LapseOffer(Guid userId)
    {
        if (Status != NampApplicationStatus.OfferGenerated)
            return Result.Failure("Application must be in OfferGenerated status.");

        OfferLapsedAt = DateTime.UtcNow;
        Status = NampApplicationStatus.OfferLapsed;
        AddStatusHistory(Status, userId, "Offer lapsed — applicant did not countersign within SLA.");
        return Result.Success();
    }

    // ── Stage 6: Pre-Deployment Verification ──────────────────────────────

    public Result BeginPreDeploymentVerification(Guid userId)
    {
        // LegalClearance is the normal prior state; OfferAccepted accepted for in-flight apps at deploy time.
        if (Status != NampApplicationStatus.LegalClearance && Status != NampApplicationStatus.OfferAccepted)
            return Result.Failure("Application must be in LegalClearance status.");

        Status = NampApplicationStatus.PreDeploymentVerification;
        AddStatusHistory(Status, userId, "Pre-deployment verification started.");
        return Result.Success();
    }

    public void SeedPreDeploymentChecklist(IEnumerable<NampPreDeploymentChecklistTemplate> templates)
    {
        var existingTemplateIds = _preDeploymentChecklist.Select(i => i.TemplateItemId).ToHashSet();
        foreach (var template in templates.Where(t => t.IsActive).OrderBy(t => t.SortOrder))
        {
            if (existingTemplateIds.Contains(template.Id))
                continue;
            _preDeploymentChecklist.Add(NampPreDeploymentChecklistItem.FromTemplate(Id, template));
        }
    }

    public Result ConfirmChecklistItem(Guid itemId, Guid userId, bool? isConfirmed, string? notes)
    {
        if (Status != NampApplicationStatus.PreDeploymentVerification)
            return Result.Failure("Checklist items can only be updated during Pre-Deployment Verification.");

        var item = _preDeploymentChecklist.FirstOrDefault(i => i.Id == itemId);
        if (item is null)
            return Result.Failure("Checklist item not found.");

        item.SetConfirmation(userId, isConfirmed, notes);
        return Result.Success();
    }

    public Result CompletePreDeploymentVerification(Guid userId, string? note)
    {
        if (Status != NampApplicationStatus.PreDeploymentVerification)
            return Result.Failure("Application must be in PreDeploymentVerification status.");

        var blockers = _preDeploymentChecklist.Where(i => i.BlocksCompletion).Select(i => i.Title).ToList();
        if (blockers.Count > 0)
            return Result.Failure($"The following mandatory checklist items are not confirmed: {string.Join(", ", blockers)}.");

        var uploadBlockers = _preDeploymentChecklist
            .Where(i => i.RequiresDocumentUpload
                && i.DocumentCategory.HasValue
                && !_documents.Any(d => d.Category == i.DocumentCategory.Value))
            .Select(i => i.Title)
            .ToList();
        if (uploadBlockers.Count > 0)
            return Result.Failure($"The following checklist items require a document upload: {string.Join(", ", uploadBlockers)}.");

        PreDeploymentVerifiedByUserId = userId;
        PreDeploymentVerifiedAt = DateTime.UtcNow;
        PreDeploymentNote = note;
        Status = NampApplicationStatus.Deployment;
        AddStatusHistory(Status, userId, $"All pre-deployment checklist items verified.{(note != null ? $" Note: {note}" : "")}");
        return Result.Success();
    }

    // ── Stage 6: Deployment ───────────────────────────────────────────────

    public Result ConfirmDeployment(Guid userId, bool gpsActivated, string? note)
    {
        if (Status != NampApplicationStatus.Deployment)
            return Result.Failure("Application must be in Deployment status.");

        DeployedByUserId = userId;
        DeployedAt = DateTime.UtcNow;
        GpsActivated = gpsActivated;
        DeploymentNote = note;
        Status = NampApplicationStatus.Active;
        AddStatusHistory(Status, userId, $"Equipment deployed. GPS activated: {gpsActivated}.{(note != null ? $" Note: {note}" : "")}");
        return Result.Success();
    }

    // ── Stage 7: Active / Closed ──────────────────────────────────────────

    public Result Close(Guid userId)
    {
        if (Status != NampApplicationStatus.Active)
            return Result.Failure("Application must be Active to close.");

        Status = NampApplicationStatus.Closed;
        AddStatusHistory(Status, userId, "Loan fully repaid — marked as Closed.");
        return Result.Success();
    }

    // ── Fineract Integration Setters ──────────────────────────────────────

    public void SetFineractClientId(long clientId) => FineractClientId = clientId;

    public void SetFineractProductDetails(int productId, string? productName, decimal nominalInterestRate)
    {
        FineractProductId = productId;
        FineractProductName = productName;
        FineractNominalInterestRate = nominalInterestRate;
    }

    public void SetApprovedInterestRate(decimal rate) => ApprovedInterestRate = rate;

    public void SetResolvedTenor(int tenorMonths) => RequestedTenorMonths = tenorMonths;

    public void SetFineractLoanResult(long loanId, string loanAccountNumber)
    {
        FineractLoanId = loanId;
        FineractLoanAccountNumber = loanAccountNumber;
    }

    // ── Extended Info Setters (called at recall time) ──────────────────────

    public void SetExtendedApplicantInfo(
        string? nin, string? bvn, string? boaAccountName, string? loanPurpose,
        string? industrySector, int? requestedTenorMonths, string? dateOfBirth,
        string? stateOfResidence, string? localGovernmentArea,
        string? companyName, string? rcNumber)
    {
        Nin = nin;
        Bvn = bvn;
        BoaAccountName = boaAccountName;
        LoanPurpose = loanPurpose;
        IndustrySector = industrySector;
        RequestedTenorMonths = requestedTenorMonths;
        DateOfBirth = dateOfBirth;
        StateOfResidence = stateOfResidence;
        LocalGovernmentArea = localGovernmentArea;
        CompanyName = companyName;
        RcNumber = rcNumber;
    }

    public void SetIndividualCreditProfile(
        string? occupation, string? employerName, string? employmentStatus,
        int? yearsOfExperience, int? numberOfDependants, decimal? estimatedMonthlyIncome,
        string? otherIncomeSource, decimal? otherMonthlyIncome, decimal? monthlyLivingExpenses,
        string? ownedAssetsDescription, decimal? estimatedNetWorth, string? existingLoanObligations)
    {
        Occupation = occupation;
        EmployerName = employerName;
        EmploymentStatus = employmentStatus;
        YearsOfExperience = yearsOfExperience;
        NumberOfDependants = numberOfDependants;
        EstimatedMonthlyIncome = estimatedMonthlyIncome;
        OtherIncomeSource = otherIncomeSource;
        OtherMonthlyIncome = otherMonthlyIncome;
        MonthlyLivingExpenses = monthlyLivingExpenses;
        OwnedAssetsDescription = ownedAssetsDescription;
        EstimatedNetWorth = estimatedNetWorth;
        ExistingLoanObligations = existingLoanObligations;
    }

    public void SetLoanAmounts(decimal? equityPercent, decimal? cartTotal, decimal? equityAmount, decimal? loanAmount)
    {
        EquityPercent = equityPercent;
        CartTotal = cartTotal;
        EquityAmount = equityAmount;
        LoanAmount = loanAmount;
    }

    public void SetEquipmentCartJson(string? cartJson) => EquipmentCartJson = cartJson;

    public void AddGuarantor(NampGuarantor guarantor) => _guarantors.Add(guarantor);
    public void AddCollateral(NampCollateral collateral) => _collaterals.Add(collateral);
    public void AddFinancialStatement(NampFinancialStatement statement) => _financialStatements.Add(statement);

    // ── CAC Company Details & Directors (Agro-Service) ─────────────────────

    /// <summary>Stores the company-level profile returned by the SmartComply CAC lookup.</summary>
    public void SetCacCompanyProfile(
        string? status,
        string? entityType,
        string? registrationDate,
        string? natureOfBusiness,
        decimal? shareCapital,
        long? companyId,
        string? address,
        string? city,
        string? state,
        string? rawJson)
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
    }

    /// <summary>
    /// Finds an existing CAC-sourced director matching the given CAC id (or full name).
    /// Returns null when none match — the caller then creates a new record via the repository.
    /// Manually-added directors (SourcedFromCac == false) are never matched here.
    /// </summary>
    public NampDirector? FindCacDirector(long? cacDirectorId, string fullName) =>
        _directors.FirstOrDefault(d =>
            d.SourcedFromCac &&
            ((cacDirectorId.HasValue && d.CacDirectorId == cacDirectorId) ||
             (!cacDirectorId.HasValue && string.Equals(d.FullName, fullName, StringComparison.OrdinalIgnoreCase))));

    // ── Documents ─────────────────────────────────────────────────────────

    public Result RemoveDocument(Guid documentId)
    {
        var doc = _documents.FirstOrDefault(d => d.Id == documentId);
        if (doc == null)
            return Result.Failure("Document not found.");
        _documents.Remove(doc);
        return Result.Success();
    }

    public NampDocument UploadDocument(
        NampDocumentStage stage,
        string fileName,
        string contentType,
        long fileSize,
        string storagePath,
        Guid uploadedByUserId,
        NampDocumentCategory category = NampDocumentCategory.General,
        string? description = null)
    {
        var doc = NampDocument.Create(Id, stage, fileName, contentType, fileSize, storagePath, uploadedByUserId, category, description);
        _documents.Add(doc);
        return doc;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private NampApplicationStatus TierCirculationStatus() => CommitteeTier switch
    {
        NampCommitteeTier.Branch => NampApplicationStatus.BranchCommitteeCirculation,
        NampCommitteeTier.Zonal => NampApplicationStatus.ZonalCommitteeCirculation,
        NampCommitteeTier.Regional => NampApplicationStatus.RegionalCommitteeCirculation,
        NampCommitteeTier.HeadOffice => NampApplicationStatus.HOCommitteeCirculation,
        _ => throw new InvalidOperationException($"Unknown committee tier: {CommitteeTier}")
    };

    private NampApplicationStatus TierDeclinedStatus() => CommitteeTier switch
    {
        NampCommitteeTier.Branch => NampApplicationStatus.BranchCommitteeDeclined,
        NampCommitteeTier.Zonal => NampApplicationStatus.ZonalCommitteeDeclined,
        NampCommitteeTier.Regional => NampApplicationStatus.RegionalCommitteeDeclined,
        NampCommitteeTier.HeadOffice => NampApplicationStatus.HOCommitteeDeclined,
        _ => throw new InvalidOperationException($"Unknown committee tier: {CommitteeTier}")
    };

    private void AddStatusHistory(NampApplicationStatus status, Guid changedByUserId, string? note)
    {
        _statusHistory.Add(NampStatusHistory.Create(Id, status, changedByUserId, note));
        AddDomainEvent(new NampStatusChangedEvent(Id, status, changedByUserId, note));
    }

}

// Domain Events
public record NampStatusChangedEvent(
    Guid NampApplicationId,
    NampApplicationStatus NewStatus,
    Guid ChangedByUserId,
    string? Note
) : DomainEvent;

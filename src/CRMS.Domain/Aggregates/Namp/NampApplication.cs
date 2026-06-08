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
    public DateTime? OfferGeneratedAt { get; private set; }
    public DateTime? OfferAcceptedAt { get; private set; }
    public Guid? OfferAcceptedByUserId { get; private set; }
    public DateTime? OfferLapsedAt { get; private set; }

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

    public IReadOnlyList<NampDocument> Documents => _documents.AsReadOnly();
    public IReadOnlyList<NampStatusHistory> StatusHistory => _statusHistory.AsReadOnly();
    public IReadOnlyList<NampGuarantor> Guarantors => _guarantors.AsReadOnly();
    public IReadOnlyList<NampCollateral> Collaterals => _collaterals.AsReadOnly();
    public IReadOnlyList<NampFinancialStatement> FinancialStatements => _financialStatements.AsReadOnly();
    public IReadOnlyList<NampPreDeploymentChecklistItem> PreDeploymentChecklist => _preDeploymentChecklist.AsReadOnly();

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

        Status = isApproved ? TierCirculationStatus() : TierDeclinedStatus();
        var outcome = isApproved ? "Financial appraisal approved — circulating to committee." : "Financial appraisal declined.";
        AddStatusHistory(Status, userId, $"{outcome}{(note != null ? $" Note: {note}" : "")}");
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

    public Result Ratify(Guid userId, string? offerLetterPath = null, string? note = null)
    {
        if (Status != NampApplicationStatus.Ratification)
            return Result.Failure("Application must be in Ratification status.");

        RatifiedByUserId = userId;
        RatifiedAt = DateTime.UtcNow;
        OfferLetterStoragePath = offerLetterPath;
        OfferGeneratedAt = DateTime.UtcNow;
        Status = NampApplicationStatus.OfferGenerated;
        var historyNote = string.IsNullOrWhiteSpace(note)
            ? "Decision ratified. Offer letter generated."
            : $"Decision ratified. Offer letter generated. Note: {note}";
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
        if (Status != NampApplicationStatus.OfferAccepted)
            return Result.Failure("Application must be in OfferAccepted status.");

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

    // ── Fineract Integration Setters ──────────────────────────────────────

    public void SetFineractClientId(long clientId) => FineractClientId = clientId;

    public void SetFineractProductDetails(int productId, string? productName, decimal nominalInterestRate)
    {
        FineractProductId = productId;
        FineractProductName = productName;
        FineractNominalInterestRate = nominalInterestRate;
    }

    public void SetApprovedInterestRate(decimal rate) => ApprovedInterestRate = rate;

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

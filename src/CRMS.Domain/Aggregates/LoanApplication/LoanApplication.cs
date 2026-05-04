using CRMS.Domain.Aggregates.ProductCatalog;
using CRMS.Domain.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.ValueObjects;

namespace CRMS.Domain.Aggregates.LoanApplication;

public class LoanApplication : AggregateRoot
{
    public string ApplicationNumber { get; private set; } = string.Empty;
    public LoanApplicationType Type { get; private set; }
    public LoanApplicationStatus Status { get; private set; }
    
    // Product Reference
    public Guid LoanProductId { get; private set; }
    public string ProductCode { get; private set; } = string.Empty;
    
    // Customer/Corporate Reference
    public string AccountNumber { get; private set; } = string.Empty;
    public string CustomerId { get; private set; } = string.Empty;
    public string CustomerName { get; private set; } = string.Empty;
    public string? RegistrationNumber { get; private set; } // RC number for corporate loans
    
    // Loan Details
    public Money RequestedAmount { get; private set; } = null!;
    public int RequestedTenorMonths { get; private set; }
    public decimal InterestRatePerAnnum { get; private set; }
    public InterestRateType InterestRateType { get; private set; }
    public string? Purpose { get; private set; }
    
    // Approved Details (filled after approval)
    public Money? ApprovedAmount { get; private set; }
    public int? ApprovedTenorMonths { get; private set; }
    public decimal? ApprovedInterestRate { get; private set; }
    
    // Branch Info
    public Guid? BranchId { get; private set; }
    public Guid InitiatedByUserId { get; private set; }
    
    // Workflow
    public DateTime? SubmittedAt { get; private set; }
    public DateTime? BranchApprovedAt { get; private set; }
    public Guid? BranchApprovedByUserId { get; private set; }
    public DateTime? FinalApprovedAt { get; private set; }
    public Guid? FinalApprovedByUserId { get; private set; }
    public DateTime? DisbursedAt { get; private set; }
    
    // Corporate Details
    public DateTime? IncorporationDate { get; private set; }
    public string? IndustrySector { get; private set; }

    // Core Banking Reference
    public string? CoreBankingLoanId { get; private set; }

    // Credit Check Tracking
    public int TotalCreditChecksRequired { get; private set; }
    public int CreditChecksCompleted { get; private set; }
    public bool AllCreditChecksCompleted => TotalCreditChecksRequired > 0 && CreditChecksCompleted >= TotalCreditChecksRequired;
    public DateTime? CreditCheckStartedAt { get; private set; }
    public DateTime? CreditCheckCompletedAt { get; private set; }

    // Concurrency control
    public byte[] RowVersion { get; private set; } = [];

    // Offer lifecycle audit
    public DateTime? OfferIssuedAt { get; private set; }
    public Guid? OfferIssuedByUserId { get; private set; }
    public DateTime? OfferAcceptedAt { get; private set; }
    public Guid? OfferAcceptedByUserId { get; private set; }
    public DateTime? CustomerSignedAt { get; private set; }
    public OfferAcceptanceMethod? AcceptanceMethod { get; private set; }
    public bool KfsAcknowledged { get; private set; }

    // Related Entities
    private readonly List<LoanApplicationDocument> _documents = [];
    private readonly List<LoanApplicationParty> _parties = [];
    private readonly List<LoanApplicationComment> _comments = [];
    private readonly List<LoanApplicationStatusHistory> _statusHistory = [];
    private readonly List<DisbursementChecklistItem> _checklistItems = [];
    private readonly List<ApprovalOverrideRecord> _overrideRecords = [];

    public IReadOnlyCollection<LoanApplicationDocument> Documents => _documents.AsReadOnly();
    public IReadOnlyCollection<LoanApplicationParty> Parties => _parties.AsReadOnly();
    public IReadOnlyCollection<LoanApplicationComment> Comments => _comments.AsReadOnly();
    public IReadOnlyCollection<LoanApplicationStatusHistory> StatusHistory => _statusHistory.AsReadOnly();
    public IReadOnlyCollection<DisbursementChecklistItem> ChecklistItems => _checklistItems.AsReadOnly();
    public IReadOnlyCollection<ApprovalOverrideRecord> OverrideRecords => _overrideRecords.AsReadOnly();

    private LoanApplication() { }

    public static Result<LoanApplication> CreateCorporate(
        Guid loanProductId,
        string productCode,
        string accountNumber,
        string customerId,
        string customerName,
        Money requestedAmount,
        int requestedTenorMonths,
        decimal interestRatePerAnnum,
        InterestRateType interestRateType,
        Guid initiatedByUserId,
        Guid? branchId,
        string? purpose = null,
        string? registrationNumber = null,
        DateTime? incorporationDate = null,
        string? industrySector = null)
    {
        if (string.IsNullOrWhiteSpace(accountNumber))
            return Result.Failure<LoanApplication>("Account number is required");

        if (string.IsNullOrWhiteSpace(customerId))
            return Result.Failure<LoanApplication>("Customer ID is required");

        if (requestedAmount.Amount <= 0)
            return Result.Failure<LoanApplication>("Requested amount must be greater than zero");

        if (requestedTenorMonths <= 0)
            return Result.Failure<LoanApplication>("Tenor must be greater than zero");

        var application = new LoanApplication
        {
            ApplicationNumber = GenerateApplicationNumber(),
            Type = LoanApplicationType.Corporate,
            Status = LoanApplicationStatus.Draft,
            LoanProductId = loanProductId,
            ProductCode = productCode,
            AccountNumber = accountNumber,
            CustomerId = customerId,
            CustomerName = customerName,
            RegistrationNumber = registrationNumber,
            IncorporationDate = incorporationDate,
            RequestedAmount = requestedAmount,
            RequestedTenorMonths = requestedTenorMonths,
            InterestRatePerAnnum = interestRatePerAnnum,
            InterestRateType = interestRateType,
            InitiatedByUserId = initiatedByUserId,
            BranchId = branchId,
            Purpose = purpose,
            IndustrySector = industrySector
        };

        application.AddStatusHistory(LoanApplicationStatus.Draft, initiatedByUserId, "Application created");
        application.AddDomainEvent(new LoanApplicationCreatedEvent(application.Id, application.ApplicationNumber, application.Type));

        return Result.Success(application);
    }

    private static string GenerateApplicationNumber()
    {
        return $"LA{DateTime.UtcNow:yyyyMMdd}{Guid.NewGuid().ToString()[..6].ToUpper()}";
    }

    public Result Submit(Guid userId)
    {
        if (Status != LoanApplicationStatus.Draft && Status != LoanApplicationStatus.BranchReturned)
            return Result.Failure("Application can only be submitted from Draft or Returned status");

        // Bank statements are managed via the BankStatement aggregate (Statements tab),
        // not as LoanApplicationDocuments. Cross-aggregate validation is handled by the handler.

        Status = LoanApplicationStatus.Submitted;
        SubmittedAt = DateTime.UtcNow;
        AddStatusHistory(Status, userId, "Application submitted for review");
        AddDomainEvent(new LoanApplicationSubmittedEvent(Id, ApplicationNumber));

        return Result.Success();
    }

    /// <summary>
    /// Validates that statement requirements are met for credit analysis.
    /// Corporate loans require: 1 internal statement (mandatory) + external statements (recommended).
    /// </summary>
    public StatementRequirementResult ValidateStatementRequirements(
        bool hasInternalStatement, 
        bool hasExternalStatements,
        bool allExternalVerified,
        int totalMonthsCovered)
    {
        var warnings = new List<string>();
        var errors = new List<string>();

        // Internal statement is mandatory for corporate loans
        if (Type == LoanApplicationType.Corporate && !hasInternalStatement)
        {
            errors.Add("Internal bank statement (from our core banking) is required for corporate loan assessment");
        }

        // Check period coverage
        if (totalMonthsCovered < 6)
        {
            errors.Add($"Insufficient statement coverage: {totalMonthsCovered} months provided, minimum 6 months required");
        }
        else if (totalMonthsCovered < 12)
        {
            warnings.Add($"Statement coverage of {totalMonthsCovered} months is acceptable but 12 months is recommended for thorough analysis");
        }

        // External statement recommendations
        if (!hasExternalStatements)
        {
            warnings.Add("No external bank statements provided. For complete cashflow picture, consider adding statements from other banks where customer holds accounts");
        }
        else if (!allExternalVerified)
        {
            warnings.Add("Some external bank statements are pending verification");
        }

        return new StatementRequirementResult
        {
            MeetsMinimumRequirements = errors.Count == 0,
            HasInternalStatement = hasInternalStatement,
            HasExternalStatements = hasExternalStatements,
            AllExternalVerified = allExternalVerified,
            TotalMonthsCovered = totalMonthsCovered,
            Errors = errors,
            Warnings = warnings
        };
    }

    public Result StartDataGathering(Guid userId)
    {
        if (Status != LoanApplicationStatus.Submitted)
            return Result.Failure("Can only start data gathering from Submitted status");

        Status = LoanApplicationStatus.DataGathering;
        AddStatusHistory(Status, userId, "Data gathering initiated");

        return Result.Success();
    }

    public Result SubmitForBranchReview(Guid userId)
    {
        if (Status != LoanApplicationStatus.DataGathering && Status != LoanApplicationStatus.Draft && Status != LoanApplicationStatus.Submitted)
            return Result.Failure("Can only submit for branch review from DataGathering, Draft, or Submitted status");

        Status = LoanApplicationStatus.BranchReview;
        AddStatusHistory(Status, userId, "Submitted for branch review");

        return Result.Success();
    }

    public Result ApproveBranch(Guid userId, string? comment = null)
    {
        if (Status != LoanApplicationStatus.BranchReview)
            return Result.Failure("Application must be in BranchReview status");

        Status = LoanApplicationStatus.BranchApproved;
        BranchApprovedAt = DateTime.UtcNow;
        BranchApprovedByUserId = userId;
        AddStatusHistory(Status, userId, comment ?? "Branch approved for processing");
        AddDomainEvent(new LoanApplicationBranchApprovedEvent(Id, ApplicationNumber, userId));

        return Result.Success();
    }

    public Result ReturnFromCreditAnalysis(Guid userId, string reason)
    {
        if (Status != LoanApplicationStatus.CreditAnalysis)
            return Result.Failure("Application must be in CreditAnalysis status");

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure("Return reason is required");

        Status = LoanApplicationStatus.BranchReview;
        AddStatusHistory(Status, userId, reason);
        AddComment(userId, reason, "Credit Analysis Return");

        return Result.Success();
    }

    public Result ReturnFromHOReview(Guid userId, string reason)
    {
        if (Status != LoanApplicationStatus.HOReview)
            return Result.Failure("Application must be in HOReview status");

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure("Return reason is required");

        Status = LoanApplicationStatus.CreditAnalysis;
        AddStatusHistory(Status, userId, reason);
        AddComment(userId, reason, "HO Review Return");

        return Result.Success();
    }

    public Result ReturnFromBranch(Guid userId, string reason)
    {
        if (Status != LoanApplicationStatus.BranchReview)
            return Result.Failure("Application must be in BranchReview status");

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure("Return reason is required");

        Status = LoanApplicationStatus.BranchReturned;
        AddStatusHistory(Status, userId, reason);
        AddComment(userId, reason, "Branch Return");

        return Result.Success();
    }

    public Result RejectBranch(Guid userId, string reason)
    {
        if (Status != LoanApplicationStatus.BranchReview)
            return Result.Failure("Application must be in BranchReview status");

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure("Rejection reason is required");

        Status = LoanApplicationStatus.BranchRejected;
        AddStatusHistory(Status, userId, reason);
        AddComment(userId, reason, "Branch Rejection");

        return Result.Success();
    }

    public Result StartCreditAnalysis(int totalChecksRequired, Guid userId)
    {
        if (Status != LoanApplicationStatus.BranchApproved)
            return Result.Failure("Application must be BranchApproved to start credit analysis");

        if (totalChecksRequired <= 0)
            return Result.Failure("At least one credit check is required");

        Status = LoanApplicationStatus.CreditAnalysis;
        TotalCreditChecksRequired = totalChecksRequired;
        CreditChecksCompleted = 0;
        CreditCheckStartedAt = DateTime.UtcNow;
        AddStatusHistory(Status, userId, $"Credit analysis started for {totalChecksRequired} parties");
        AddDomainEvent(new CreditAnalysisStartedEvent(Id, ApplicationNumber, totalChecksRequired));

        return Result.Success();
    }

    public Result RecordCreditCheckCompleted(Guid userId)
    {
        if (Status != LoanApplicationStatus.CreditAnalysis)
            return Result.Failure("Application must be in CreditAnalysis status");

        CreditChecksCompleted++;

        // Use == (not >=) so the event fires exactly once — when the counter first reaches the total.
        // Using >= would re-fire the event on subsequent over-increments (e.g. re-running a NotFound check
        // for a party whose BVN was corrected after the initial run).
        if (CreditChecksCompleted == TotalCreditChecksRequired)
        {
            CreditCheckCompletedAt = DateTime.UtcNow;
            AddStatusHistory(Status, userId, $"All {TotalCreditChecksRequired} credit checks completed");
            AddDomainEvent(new AllCreditChecksCompletedEvent(Id, ApplicationNumber));
        }

        return Result.Success();
    }

    /// <summary>
    /// Resets the credit check progress counter so all checks can be re-run from scratch.
    /// Used when a force-refresh is needed (e.g. to fix bad data from a previous run).
    /// Does NOT change Status or TotalCreditChecksRequired.
    /// </summary>
    public Result ResetCreditCheckProgress(Guid userId)
    {
        if (Status != LoanApplicationStatus.CreditAnalysis)
            return Result.Failure("Application must be in CreditAnalysis status to reset credit checks");

        CreditChecksCompleted = 0;
        CreditCheckCompletedAt = null;

        return Result.Success();
    }

    public Result MoveToHOReview(Guid userId)
    {
        if (Status != LoanApplicationStatus.CreditAnalysis)
            return Result.Failure("Application must be in CreditAnalysis status with all checks completed");

        if (!AllCreditChecksCompleted)
            return Result.Failure($"All credit checks must be completed. {CreditChecksCompleted}/{TotalCreditChecksRequired} done.");

        Status = LoanApplicationStatus.HOReview;
        AddStatusHistory(Status, userId, "Moved to Head Office review");

        return Result.Success();
    }

    public Result MoveToLegalReview(Guid userId)
    {
        if (Status != LoanApplicationStatus.HOReview)
            return Result.Failure("Application must be in HOReview status");

        Status = LoanApplicationStatus.LegalReview;
        AddStatusHistory(Status, userId, "Referred to Legal for opinion");

        return Result.Success();
    }

    public Result ReturnFromLegalReview(Guid userId, string reason)
    {
        if (Status != LoanApplicationStatus.LegalReview)
            return Result.Failure("Application must be in LegalReview status");

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure("Return reason is required");

        Status = LoanApplicationStatus.HOReview;
        AddStatusHistory(Status, userId, reason);
        AddComment(userId, reason, "Legal Return");

        return Result.Success();
    }

    public Result SubmitLegalOpinion(Guid userId, string? comment = null)
    {
        if (Status != LoanApplicationStatus.LegalReview)
            return Result.Failure("Application must be in LegalReview status");

        Status = LoanApplicationStatus.LegalApproval;
        AddStatusHistory(Status, userId, comment ?? "Legal opinion submitted — awaiting Head of Legal countersignature");

        return Result.Success();
    }

    public Result ApproveLegalReview(Guid userId, string? comment = null)
    {
        if (Status != LoanApplicationStatus.LegalApproval)
            return Result.Failure("Application must be in LegalApproval status");

        Status = LoanApplicationStatus.CommitteeCirculation;
        AddStatusHistory(Status, userId, comment ?? "Legal opinion countersigned — circulating to committee");

        return Result.Success();
    }

    public Result ReturnFromLegalApproval(Guid userId, string reason)
    {
        if (Status != LoanApplicationStatus.LegalApproval)
            return Result.Failure("Application must be in LegalApproval status");

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure("Return reason is required");

        Status = LoanApplicationStatus.LegalReview;
        AddStatusHistory(Status, userId, reason);
        AddComment(userId, reason, "Legal Return");

        return Result.Success();
    }

    public Result MoveToCommittee(Guid userId)
    {
        if (Status != LoanApplicationStatus.LegalApproval)
            return Result.Failure("Application must be in LegalApproval status");

        Status = LoanApplicationStatus.CommitteeCirculation;
        AddStatusHistory(Status, userId, "Circulated to committee");

        return Result.Success();
    }

    public Result ApproveCommittee(Guid userId, Money approvedAmount, int approvedTenor, decimal approvedRate, string? comment = null)
    {
        if (Status != LoanApplicationStatus.CommitteeCirculation)
            return Result.Failure("Application must be in CommitteeCirculation status");

        Status = LoanApplicationStatus.CommitteeApproved;
        ApprovedAmount = approvedAmount;
        ApprovedTenorMonths = approvedTenor;
        ApprovedInterestRate = approvedRate;
        AddStatusHistory(Status, userId, comment ?? "Committee approved");

        return Result.Success();
    }

    public Result RejectCommittee(Guid userId, string reason)
    {
        if (Status != LoanApplicationStatus.CommitteeCirculation)
            return Result.Failure("Application must be in CommitteeCirculation status");

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure("Rejection reason is required");

        Status = LoanApplicationStatus.CommitteeRejected;
        AddStatusHistory(Status, userId, reason);
        AddComment(userId, reason, "Committee Rejection");

        return Result.Success();
    }

    public Result DeferFromCommittee(Guid userId, string rationale)
    {
        if (Status != LoanApplicationStatus.CommitteeCirculation)
            return Result.Failure("Application must be in CommitteeCirculation status");

        Status = LoanApplicationStatus.HOReview;
        AddStatusHistory(Status, userId, $"Committee deferred: {rationale}");

        return Result.Success();
    }

    public Result MoveToFinalApproval(Guid userId)
    {
        if (Status != LoanApplicationStatus.CommitteeApproved)
            return Result.Failure("Application must be in CommitteeApproved status");

        Status = LoanApplicationStatus.FinalApproval;
        AddStatusHistory(Status, userId, "Moved to final approval — awaiting MD/CEO sign-off");

        return Result.Success();
    }

    public Result FinalApprove(Guid userId, string? comment = null)
    {
        if (Status != LoanApplicationStatus.FinalApproval)
            return Result.Failure("Application must be in FinalApproval status");

        Status = LoanApplicationStatus.Approved;
        FinalApprovedAt = DateTime.UtcNow;
        FinalApprovedByUserId = userId;
        AddStatusHistory(Status, userId, comment ?? "Final approval granted");
        AddDomainEvent(new LoanApplicationApprovedEvent(Id, ApplicationNumber, ApprovedAmount!.Amount));

        return Result.Success();
    }

    // -------------------------------------------------------------------------
    // Post-approval: offer issuance, checklist seeding, acceptance
    // -------------------------------------------------------------------------

    public Result IssueOfferLetter(Guid userId)
    {
        if (Status != LoanApplicationStatus.Approved)
            return Result.Failure("Offer letter can only be issued from Approved status");

        Status = LoanApplicationStatus.OfferGenerated;
        OfferIssuedAt = DateTime.UtcNow;
        OfferIssuedByUserId = userId;
        AddStatusHistory(Status, userId, "Offer letter issued to customer — awaiting signed acceptance");
        AddDomainEvent(new OfferLetterIssuedEvent(Id, ApplicationNumber));

        return Result.Success();
    }

    /// <summary>
    /// Seeds the disbursement checklist from the product template items.
    /// Called by IssueOfferLetterHandler immediately after IssueOfferLetter().
    /// Clears any previously seeded items so re-issuance produces a clean slate
    /// (only safe to call before any items are satisfied or waived).
    /// </summary>
    public void SeedChecklistItems(
        IEnumerable<ProductCatalog.DisbursementChecklistTemplate> templateItems)
    {
        // Only clear items that are still Pending — preserve any already actioned
        var actionableStatuses = new[]
        {
            Enums.ChecklistItemStatus.Satisfied,
            Enums.ChecklistItemStatus.Waived,
            Enums.ChecklistItemStatus.PendingLegalReview,
            Enums.ChecklistItemStatus.WaiverPending
        };
        var hasActionedItems = _checklistItems.Any(i => actionableStatuses.Contains(i.Status));

        if (!hasActionedItems)
            _checklistItems.Clear();

        var existingTemplateIds = _checklistItems.Select(i => i.TemplateItemId).ToHashSet();

        foreach (var template in templateItems.Where(t => t.IsActive).OrderBy(t => t.SortOrder))
        {
            if (existingTemplateIds.Contains(template.Id))
                continue; // Already seeded and actioned — do not duplicate

            _checklistItems.Add(DisbursementChecklistItem.FromTemplate(
                Id,
                template.Id,
                template.ItemName,
                template.Description,
                template.IsMandatory,
                template.ConditionType,
                template.SubsequentDueDays,
                template.RequiresDocumentUpload,
                template.RequiresLegalRatification,
                template.CanBeWaived,
                template.SortOrder));
        }
    }

    public Result AcceptOffer(Guid userId, DateTime customerSignedAt, OfferAcceptanceMethod acceptanceMethod, bool kfsAcknowledged)
    {
        if (Status != LoanApplicationStatus.OfferGenerated)
            return Result.Failure("Offer can only be accepted from OfferGenerated status");

        if (!kfsAcknowledged)
            return Result.Failure("KFS acknowledgement is required before recording acceptance");

        // Validate all mandatory CP items are resolved
        var blockers = _checklistItems
            .Where(i => i.BlocksDisbursement)
            .Select(i => i.ItemName)
            .ToList();

        if (blockers.Count > 0)
            return Result.Failure(
                $"The following mandatory conditions precedent are unresolved: {string.Join(", ", blockers)}");

        Status = LoanApplicationStatus.OfferAccepted;
        OfferAcceptedAt = DateTime.UtcNow;
        OfferAcceptedByUserId = userId;
        CustomerSignedAt = customerSignedAt;
        AcceptanceMethod = acceptanceMethod;
        KfsAcknowledged = kfsAcknowledged;
        AddStatusHistory(Status, userId, $"Customer acceptance confirmed via {acceptanceMethod} — all conditions precedent resolved");
        AddDomainEvent(new OfferAcceptedEvent(Id, ApplicationNumber));

        return Result.Success();
    }

    // -------------------------------------------------------------------------
    // Security Perfection + Disbursement maker-checker chain
    // -------------------------------------------------------------------------

    public Result MoveToSecurityPerfection(Guid userId)
    {
        if (Status != LoanApplicationStatus.OfferAccepted)
            return Result.Failure("Application must be in OfferAccepted status");

        Status = LoanApplicationStatus.SecurityPerfection;
        AddStatusHistory(Status, userId, "Referred to Legal for security perfection");

        return Result.Success();
    }

    public Result SubmitSecurityDocuments(Guid userId, string? comment = null)
    {
        if (Status != LoanApplicationStatus.SecurityPerfection)
            return Result.Failure("Application must be in SecurityPerfection status");

        Status = LoanApplicationStatus.SecurityApproval;
        AddStatusHistory(Status, userId, comment ?? "Security documents submitted — awaiting Head of Legal countersignature");

        return Result.Success();
    }

    public Result ApproveSecurityPerfection(Guid userId, string? comment = null)
    {
        if (Status != LoanApplicationStatus.SecurityApproval)
            return Result.Failure("Application must be in SecurityApproval status");

        Status = LoanApplicationStatus.DisbursementPending;
        AddStatusHistory(Status, userId, comment ?? "Security perfection confirmed — referred to Operations for disbursement memo");

        return Result.Success();
    }

    public Result ReturnFromSecurityApproval(Guid userId, string reason)
    {
        if (Status != LoanApplicationStatus.SecurityApproval)
            return Result.Failure("Application must be in SecurityApproval status");

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure("Return reason is required");

        Status = LoanApplicationStatus.SecurityPerfection;
        AddStatusHistory(Status, userId, reason);
        AddComment(userId, reason, "Security Return");

        return Result.Success();
    }

    public Result PrepareDisbursementMemo(Guid userId, string? comment = null)
    {
        if (Status != LoanApplicationStatus.DisbursementPending)
            return Result.Failure("Application must be in DisbursementPending status");

        Status = LoanApplicationStatus.DisbursementBranchApproval;
        AddStatusHistory(Status, userId, comment ?? "Disbursement memo prepared — submitted for branch authorisation");

        return Result.Success();
    }

    public Result ApproveDisbursementBranch(Guid userId, string? comment = null)
    {
        if (Status != LoanApplicationStatus.DisbursementBranchApproval)
            return Result.Failure("Application must be in DisbursementBranchApproval status");

        Status = LoanApplicationStatus.DisbursementHQApproval;
        AddStatusHistory(Status, userId, comment ?? "Branch authorisation granted — referred to GM Finance for final release");

        return Result.Success();
    }

    public Result ReturnFromDisbursementBranch(Guid userId, string reason)
    {
        if (Status != LoanApplicationStatus.DisbursementBranchApproval)
            return Result.Failure("Application must be in DisbursementBranchApproval status");

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure("Return reason is required");

        Status = LoanApplicationStatus.DisbursementPending;
        AddStatusHistory(Status, userId, reason);
        AddComment(userId, reason, "Disbursement Return");

        return Result.Success();
    }

    public Result RecordDisbursement(string coreBankingLoanId, Guid userId)
    {
        if (Status != LoanApplicationStatus.DisbursementHQApproval
            && Status != LoanApplicationStatus.Approved
            && Status != LoanApplicationStatus.OfferAccepted)
            return Result.Failure("Application must be in DisbursementHQApproval status");

        Status = LoanApplicationStatus.Disbursed;
        CoreBankingLoanId = coreBankingLoanId;
        DisbursedAt = DateTime.UtcNow;

        // Set due dates on all Subsequent checklist items
        foreach (var item in _checklistItems.Where(i => i.ConditionType == Enums.ConditionType.Subsequent))
            item.SetDueDate(DisbursedAt.Value);

        AddStatusHistory(Status, userId, $"Loan disbursed. Core Banking ID: {coreBankingLoanId}");
        AddDomainEvent(new LoanApplicationDisbursedEvent(Id, ApplicationNumber, coreBankingLoanId));

        return Result.Success();
    }

    public Result<LoanApplicationDocument> AddDocument(
        DocumentCategory category,
        string fileName,
        string filePath,
        long fileSize,
        string contentType,
        Guid uploadedByUserId,
        string? description = null)
    {
        var document = LoanApplicationDocument.Create(
            Id, category, fileName, filePath, fileSize, contentType, uploadedByUserId, description);

        if (document.IsFailure)
            return document;

        _documents.Add(document.Value);
        return document;
    }

    public Result RemoveDocument(Guid documentId)
    {
        var document = _documents.FirstOrDefault(d => d.Id == documentId);
        if (document == null)
            return Result.Failure("Document not found");

        if (document.Status == DocumentStatus.Verified)
            return Result.Failure("Cannot remove verified document");

        _documents.Remove(document);
        return Result.Success();
    }

    public Result<LoanApplicationParty> AddParty(
        PartyType partyType,
        string fullName,
        string? bvn,
        string? email,
        string? phoneNumber,
        string? designation,
        decimal? shareholdingPercent)
    {
        var party = LoanApplicationParty.Create(
            Id, partyType, fullName, bvn, email, phoneNumber, designation, shareholdingPercent);

        if (party.IsFailure)
            return party;

        _parties.Add(party.Value);
        return party;
    }

    public void AddComment(Guid userId, string content, string? category = null)
    {
        _comments.Add(new LoanApplicationComment(Id, userId, content, category));
    }

    private void AddStatusHistory(LoanApplicationStatus status, Guid userId, string? comment)
    {
        _statusHistory.Add(new LoanApplicationStatusHistory(Id, status, userId, comment));
    }

    public Result UpdatePartyFields(Guid partyId, string? bvn, decimal? shareholdingPercent)
    {
        var party = _parties.FirstOrDefault(p => p.Id == partyId);
        if (party == null) return Result.Failure("Party not found");
        if (Status != LoanApplicationStatus.Draft)
            return Result.Failure("Party info can only be updated in Draft status");
        if (bvn != null) party.UpdateBVN(bvn);
        if (shareholdingPercent.HasValue) party.UpdateShareholdingPercent(shareholdingPercent.Value);
        return Result.Success();
    }

    public Result UpdateLoanDetails(Money requestedAmount, int requestedTenorMonths, decimal interestRate, string? purpose)
    {
        if (Status != LoanApplicationStatus.Draft && Status != LoanApplicationStatus.BranchReturned)
            return Result.Failure("Can only update details in Draft or Returned status");

        RequestedAmount = requestedAmount;
        RequestedTenorMonths = requestedTenorMonths;
        InterestRatePerAnnum = interestRate;
        Purpose = purpose;

        return Result.Success();
    }
}

// Domain Events
public record LoanApplicationCreatedEvent(Guid ApplicationId, string ApplicationNumber, LoanApplicationType Type) : DomainEvent;
public record LoanApplicationSubmittedEvent(Guid ApplicationId, string ApplicationNumber) : DomainEvent;
public record LoanApplicationBranchApprovedEvent(Guid ApplicationId, string ApplicationNumber, Guid ApprovedByUserId) : DomainEvent;
public record CreditAnalysisStartedEvent(Guid ApplicationId, string ApplicationNumber, int TotalChecks) : DomainEvent;
public record AllCreditChecksCompletedEvent(Guid ApplicationId, string ApplicationNumber) : DomainEvent;
public record LoanApplicationApprovedEvent(Guid ApplicationId, string ApplicationNumber, decimal ApprovedAmount) : DomainEvent;
public record OfferLetterIssuedEvent(Guid ApplicationId, string ApplicationNumber) : DomainEvent;
public record OfferAcceptedEvent(Guid ApplicationId, string ApplicationNumber) : DomainEvent;
public record LoanApplicationDisbursedEvent(Guid ApplicationId, string ApplicationNumber, string CoreBankingLoanId) : DomainEvent;

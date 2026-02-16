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
    
    // Core Banking Reference
    public string? CoreBankingLoanId { get; private set; }

    // Related Entities
    private readonly List<LoanApplicationDocument> _documents = [];
    private readonly List<LoanApplicationParty> _parties = [];
    private readonly List<LoanApplicationComment> _comments = [];
    private readonly List<LoanApplicationStatusHistory> _statusHistory = [];

    public IReadOnlyCollection<LoanApplicationDocument> Documents => _documents.AsReadOnly();
    public IReadOnlyCollection<LoanApplicationParty> Parties => _parties.AsReadOnly();
    public IReadOnlyCollection<LoanApplicationComment> Comments => _comments.AsReadOnly();
    public IReadOnlyCollection<LoanApplicationStatusHistory> StatusHistory => _statusHistory.AsReadOnly();

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
        string? purpose = null)
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
            RequestedAmount = requestedAmount,
            RequestedTenorMonths = requestedTenorMonths,
            InterestRatePerAnnum = interestRatePerAnnum,
            InterestRateType = interestRateType,
            InitiatedByUserId = initiatedByUserId,
            BranchId = branchId,
            Purpose = purpose
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

        if (!_documents.Any(d => d.Category == DocumentCategory.BankStatement && d.Status == DocumentStatus.Uploaded))
            return Result.Failure("At least one bank statement is required");

        Status = LoanApplicationStatus.Submitted;
        SubmittedAt = DateTime.UtcNow;
        AddStatusHistory(Status, userId, "Application submitted for review");
        AddDomainEvent(new LoanApplicationSubmittedEvent(Id, ApplicationNumber));

        return Result.Success();
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
        if (Status != LoanApplicationStatus.DataGathering && Status != LoanApplicationStatus.Draft)
            return Result.Failure("Can only submit for branch review from DataGathering or Draft status");

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
        AddDomainEvent(new LoanApplicationBranchApprovedEvent(Id, ApplicationNumber));

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

    public Result MoveToHOReview(Guid userId)
    {
        if (Status != LoanApplicationStatus.BranchApproved && Status != LoanApplicationStatus.CreditAnalysis)
            return Result.Failure("Application must be BranchApproved or in CreditAnalysis");

        Status = LoanApplicationStatus.HOReview;
        AddStatusHistory(Status, userId, "Moved to Head Office review");

        return Result.Success();
    }

    public Result MoveToCommittee(Guid userId)
    {
        if (Status != LoanApplicationStatus.HOReview)
            return Result.Failure("Application must be in HOReview status");

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

    public Result FinalApprove(Guid userId, string? comment = null)
    {
        if (Status != LoanApplicationStatus.CommitteeApproved)
            return Result.Failure("Application must be CommitteeApproved");

        Status = LoanApplicationStatus.Approved;
        FinalApprovedAt = DateTime.UtcNow;
        FinalApprovedByUserId = userId;
        AddStatusHistory(Status, userId, comment ?? "Final approval granted");
        AddDomainEvent(new LoanApplicationApprovedEvent(Id, ApplicationNumber, ApprovedAmount!.Amount));

        return Result.Success();
    }

    public Result RecordDisbursement(string coreBankingLoanId, Guid userId)
    {
        if (Status != LoanApplicationStatus.Approved && Status != LoanApplicationStatus.OfferAccepted)
            return Result.Failure("Application must be Approved or OfferAccepted");

        Status = LoanApplicationStatus.Disbursed;
        CoreBankingLoanId = coreBankingLoanId;
        DisbursedAt = DateTime.UtcNow;
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
public record LoanApplicationBranchApprovedEvent(Guid ApplicationId, string ApplicationNumber) : DomainEvent;
public record LoanApplicationApprovedEvent(Guid ApplicationId, string ApplicationNumber, decimal ApprovedAmount) : DomainEvent;
public record LoanApplicationDisbursedEvent(Guid ApplicationId, string ApplicationNumber, string CoreBankingLoanId) : DomainEvent;

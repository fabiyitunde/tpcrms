using CRMS.Domain.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.ValueObjects;

namespace CRMS.Domain.Aggregates.Guarantor;

public class Guarantor : AggregateRoot
{
    public Guid LoanApplicationId { get; private set; }
    public string GuarantorReference { get; private set; } = string.Empty;
    public GuarantorType Type { get; private set; }
    public GuarantorStatus Status { get; private set; }
    public GuaranteeType GuaranteeType { get; private set; }
    
    // Identity (Individual)
    public string FullName { get; private set; } = string.Empty;
    public string? BVN { get; private set; }
    public string? NIN { get; private set; }
    public DateTime? DateOfBirth { get; private set; }
    public string? Gender { get; private set; }
    
    // Identity (Corporate)
    public string? CompanyName { get; private set; }
    public string? RegistrationNumber { get; private set; }
    public string? TaxId { get; private set; }
    
    // Contact
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? Address { get; private set; }
    
    // Relationship
    public string? RelationshipToApplicant { get; private set; }
    public bool IsDirector { get; private set; }
    public bool IsShareHolder { get; private set; }
    public decimal? ShareholdingPercentage { get; private set; }
    
    // Financial Standing
    public Money? DeclaredNetWorth { get; private set; }
    public Money? VerifiedNetWorth { get; private set; }
    public string? Occupation { get; private set; }
    public string? EmployerName { get; private set; }
    public Money? MonthlyIncome { get; private set; }
    
    // Guarantee Details
    public Money? GuaranteeLimit { get; private set; }
    public bool IsUnlimited { get; private set; }
    public DateTime? GuaranteeStartDate { get; private set; }
    public DateTime? GuaranteeEndDate { get; private set; }
    
    // Credit Check
    public Guid? BureauReportId { get; private set; }
    public int? CreditScore { get; private set; }
    public string? CreditScoreGrade { get; private set; }
    public DateTime? CreditCheckDate { get; private set; }
    public bool HasCreditIssues { get; private set; }
    public string? CreditIssuesSummary { get; private set; }
    
    // Existing Guarantees
    public int ExistingGuaranteeCount { get; private set; }
    public Money? TotalExistingGuarantees { get; private set; }
    
    // Documents
    public bool HasSignedGuaranteeAgreement { get; private set; }
    public DateTime? AgreementSignedDate { get; private set; }
    public string? AgreementDocumentPath { get; private set; }
    
    // Audit
    public Guid CreatedByUserId { get; private set; }
    public new DateTime CreatedAt { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public DateTime? ApprovedAt { get; private set; }
    public string? RejectionReason { get; private set; }
    public string? Notes { get; private set; }

    private readonly List<GuarantorDocument> _documents = [];
    public IReadOnlyCollection<GuarantorDocument> Documents => _documents.AsReadOnly();

    private Guarantor() { }

    public static Result<Guarantor> CreateIndividual(
        Guid loanApplicationId,
        string fullName,
        string? bvn,
        GuaranteeType guaranteeType,
        Guid createdByUserId,
        string? relationshipToApplicant = null,
        string? email = null,
        string? phone = null,
        string? address = null,
        Money? guaranteeLimit = null)
    {
        if (loanApplicationId == Guid.Empty)
            return Result.Failure<Guarantor>("Loan application ID is required");

        if (string.IsNullOrWhiteSpace(fullName))
            return Result.Failure<Guarantor>("Guarantor name is required");

        var guarantor = new Guarantor
        {
            LoanApplicationId = loanApplicationId,
            GuarantorReference = GenerateReference(),
            Type = GuarantorType.Individual,
            Status = GuarantorStatus.Proposed,
            GuaranteeType = guaranteeType,
            FullName = fullName,
            BVN = bvn,
            Email = email,
            Phone = phone,
            Address = address,
            RelationshipToApplicant = relationshipToApplicant,
            GuaranteeLimit = guaranteeLimit,
            IsUnlimited = guaranteeType == GuaranteeType.Unlimited,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow
        };

        guarantor.AddDomainEvent(new GuarantorProposedEvent(guarantor.Id, loanApplicationId, fullName, GuarantorType.Individual));

        return Result.Success(guarantor);
    }

    public static Result<Guarantor> CreateCorporate(
        Guid loanApplicationId,
        string companyName,
        string registrationNumber,
        GuaranteeType guaranteeType,
        Guid createdByUserId,
        string? taxId = null,
        string? email = null,
        string? phone = null,
        string? address = null,
        Money? guaranteeLimit = null)
    {
        if (loanApplicationId == Guid.Empty)
            return Result.Failure<Guarantor>("Loan application ID is required");

        if (string.IsNullOrWhiteSpace(companyName))
            return Result.Failure<Guarantor>("Company name is required");

        var guarantor = new Guarantor
        {
            LoanApplicationId = loanApplicationId,
            GuarantorReference = GenerateReference(),
            Type = GuarantorType.Corporate,
            Status = GuarantorStatus.Proposed,
            GuaranteeType = guaranteeType,
            FullName = companyName,
            CompanyName = companyName,
            RegistrationNumber = registrationNumber,
            TaxId = taxId,
            Email = email,
            Phone = phone,
            Address = address,
            GuaranteeLimit = guaranteeLimit,
            IsUnlimited = guaranteeType == GuaranteeType.Unlimited,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow
        };

        guarantor.AddDomainEvent(new GuarantorProposedEvent(guarantor.Id, loanApplicationId, companyName, GuarantorType.Corporate));

        return Result.Success(guarantor);
    }

    private static string GenerateReference()
    {
        return $"GR{DateTime.UtcNow:yyyyMMddHHmmss}{Guid.NewGuid().ToString()[..4].ToUpper()}";
    }

    public Result SetFinancialDetails(
        Money? declaredNetWorth,
        string? occupation,
        string? employerName,
        Money? monthlyIncome)
    {
        DeclaredNetWorth = declaredNetWorth;
        Occupation = occupation;
        EmployerName = employerName;
        MonthlyIncome = monthlyIncome;
        return Result.Success();
    }

    public Result SetDirectorDetails(bool isDirector, bool isShareholder, decimal? shareholdingPercentage)
    {
        IsDirector = isDirector;
        IsShareHolder = isShareholder;
        ShareholdingPercentage = shareholdingPercentage;
        
        if (isDirector)
            Type = GuarantorType.Director;
        else if (isShareholder)
            Type = GuarantorType.Shareholder;
            
        return Result.Success();
    }

    public Result RecordCreditCheck(
        Guid bureauReportId,
        int? creditScore,
        string? creditScoreGrade,
        bool hasCreditIssues,
        string? creditIssuesSummary,
        int existingGuaranteeCount,
        Money? totalExistingGuarantees)
    {
        BureauReportId = bureauReportId;
        CreditScore = creditScore;
        CreditScoreGrade = creditScoreGrade;
        CreditCheckDate = DateTime.UtcNow;
        HasCreditIssues = hasCreditIssues;
        CreditIssuesSummary = creditIssuesSummary;
        ExistingGuaranteeCount = existingGuaranteeCount;
        TotalExistingGuarantees = totalExistingGuarantees;
        Status = GuarantorStatus.CreditCheckCompleted;

        AddDomainEvent(new GuarantorCreditCheckCompletedEvent(Id, LoanApplicationId, creditScore, hasCreditIssues));

        return Result.Success();
    }

    public Result Approve(Guid approvedByUserId, Money? verifiedNetWorth = null)
    {
        if (Status != GuarantorStatus.CreditCheckCompleted && Status != GuarantorStatus.PendingVerification)
            return Result.Failure("Guarantor must complete credit check or be verified before approval");

        Status = GuarantorStatus.Approved;
        ApprovedByUserId = approvedByUserId;
        ApprovedAt = DateTime.UtcNow;
        VerifiedNetWorth = verifiedNetWorth ?? DeclaredNetWorth;
        GuaranteeStartDate = DateTime.UtcNow;

        AddDomainEvent(new GuarantorApprovedEvent(Id, LoanApplicationId, FullName, GuaranteeLimit));

        return Result.Success();
    }

    public Result Reject(Guid rejectedByUserId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure("Rejection reason is required");

        Status = GuarantorStatus.Rejected;
        ApprovedByUserId = rejectedByUserId;
        ApprovedAt = DateTime.UtcNow;
        RejectionReason = reason;

        return Result.Success();
    }

    public Result Activate()
    {
        if (Status != GuarantorStatus.Approved)
            return Result.Failure("Guarantor must be approved before activation");

        if (!HasSignedGuaranteeAgreement)
            return Result.Failure("Guarantor must sign guarantee agreement before activation");

        Status = GuarantorStatus.Active;
        return Result.Success();
    }

    public Result RecordAgreementSigned(string documentPath)
    {
        HasSignedGuaranteeAgreement = true;
        AgreementSignedDate = DateTime.UtcNow;
        AgreementDocumentPath = documentPath;
        return Result.Success();
    }

    public Result Release(string reason)
    {
        if (Status != GuarantorStatus.Active)
            return Result.Failure("Only active guarantors can be released");

        Status = GuarantorStatus.Released;
        GuaranteeEndDate = DateTime.UtcNow;
        Notes = reason;

        AddDomainEvent(new GuarantorReleasedEvent(Id, LoanApplicationId, FullName, reason));

        return Result.Success();
    }

    public void AddDocument(GuarantorDocument document)
    {
        _documents.Add(document);
    }

    public bool CanGuarantee(Money loanAmount)
    {
        if (IsUnlimited) return true;
        if (GuaranteeLimit == null) return false;
        
        var availableLimit = GuaranteeLimit.Amount - (TotalExistingGuarantees?.Amount ?? 0);
        return availableLimit >= loanAmount.Amount;
    }
}

// Domain Events
public record GuarantorProposedEvent(Guid GuarantorId, Guid LoanApplicationId, string Name, GuarantorType Type) : DomainEvent;
public record GuarantorCreditCheckCompletedEvent(Guid GuarantorId, Guid LoanApplicationId, int? CreditScore, bool HasIssues) : DomainEvent;
public record GuarantorApprovedEvent(Guid GuarantorId, Guid LoanApplicationId, string Name, Money? GuaranteeLimit) : DomainEvent;
public record GuarantorReleasedEvent(Guid GuarantorId, Guid LoanApplicationId, string Name, string Reason) : DomainEvent;

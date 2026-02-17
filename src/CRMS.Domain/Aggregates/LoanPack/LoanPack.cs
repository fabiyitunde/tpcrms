using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.LoanPack;

/// <summary>
/// Represents a generated loan pack document for committee review.
/// Tracks versions and generation history.
/// </summary>
public class LoanPack : AggregateRoot
{
    public Guid LoanApplicationId { get; private set; }
    public string ApplicationNumber { get; private set; } = string.Empty;
    public int Version { get; private set; }
    public LoanPackStatus Status { get; private set; }
    
    // Generation metadata
    public DateTime GeneratedAt { get; private set; }
    public Guid GeneratedByUserId { get; private set; }
    public string GeneratedByUserName { get; private set; } = string.Empty;
    
    // Document storage
    public string FileName { get; private set; } = string.Empty;
    public string StoragePath { get; private set; } = string.Empty;
    public long FileSizeBytes { get; private set; }
    public string ContentHash { get; private set; } = string.Empty;
    
    // Content summary (for quick reference without loading PDF)
    public decimal RequestedAmount { get; private set; }
    public decimal? RecommendedAmount { get; private set; }
    public string CustomerName { get; private set; } = string.Empty;
    public string ProductName { get; private set; } = string.Empty;
    public int? OverallRiskScore { get; private set; }
    public string? RiskRating { get; private set; }
    
    // Sections included
    public bool IncludesExecutiveSummary { get; private set; }
    public bool IncludesBureauReports { get; private set; }
    public bool IncludesFinancialAnalysis { get; private set; }
    public bool IncludesCashflowAnalysis { get; private set; }
    public bool IncludesCollateralDetails { get; private set; }
    public bool IncludesGuarantorDetails { get; private set; }
    public bool IncludesAIAdvisory { get; private set; }
    public bool IncludesWorkflowHistory { get; private set; }
    public bool IncludesCommitteeComments { get; private set; }
    
    // Counts for reference
    public int DirectorCount { get; private set; }
    public int BureauReportCount { get; private set; }
    public int CollateralCount { get; private set; }
    public int GuarantorCount { get; private set; }

    private LoanPack() { }

    public static Result<LoanPack> Create(
        Guid loanApplicationId,
        string applicationNumber,
        Guid generatedByUserId,
        string generatedByUserName,
        string customerName,
        string productName,
        decimal requestedAmount)
    {
        if (loanApplicationId == Guid.Empty)
            return Result.Failure<LoanPack>("Loan application ID is required");

        if (string.IsNullOrWhiteSpace(applicationNumber))
            return Result.Failure<LoanPack>("Application number is required");

        var pack = new LoanPack
        {
            LoanApplicationId = loanApplicationId,
            ApplicationNumber = applicationNumber,
            Version = 1,
            Status = LoanPackStatus.Generating,
            GeneratedAt = DateTime.UtcNow,
            GeneratedByUserId = generatedByUserId,
            GeneratedByUserName = generatedByUserName,
            CustomerName = customerName,
            ProductName = productName,
            RequestedAmount = requestedAmount
        };

        pack.AddDomainEvent(new LoanPackGenerationStartedEvent(
            pack.Id, loanApplicationId, applicationNumber, generatedByUserId));

        return Result.Success(pack);
    }

    public Result SetDocument(string fileName, string storagePath, long fileSizeBytes, string contentHash)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return Result.Failure("File name is required");

        if (string.IsNullOrWhiteSpace(storagePath))
            return Result.Failure("Storage path is required");

        FileName = fileName;
        StoragePath = storagePath;
        FileSizeBytes = fileSizeBytes;
        ContentHash = contentHash;
        Status = LoanPackStatus.Generated;

        AddDomainEvent(new LoanPackGeneratedEvent(
            Id, LoanApplicationId, ApplicationNumber, Version, fileName, fileSizeBytes));

        return Result.Success();
    }

    public void SetContentSummary(
        decimal? recommendedAmount,
        int? overallRiskScore,
        string? riskRating,
        int directorCount,
        int bureauReportCount,
        int collateralCount,
        int guarantorCount)
    {
        RecommendedAmount = recommendedAmount;
        OverallRiskScore = overallRiskScore;
        RiskRating = riskRating;
        DirectorCount = directorCount;
        BureauReportCount = bureauReportCount;
        CollateralCount = collateralCount;
        GuarantorCount = guarantorCount;
    }

    public void SetIncludedSections(
        bool executiveSummary,
        bool bureauReports,
        bool financialAnalysis,
        bool cashflowAnalysis,
        bool collateralDetails,
        bool guarantorDetails,
        bool aiAdvisory,
        bool workflowHistory,
        bool committeeComments)
    {
        IncludesExecutiveSummary = executiveSummary;
        IncludesBureauReports = bureauReports;
        IncludesFinancialAnalysis = financialAnalysis;
        IncludesCashflowAnalysis = cashflowAnalysis;
        IncludesCollateralDetails = collateralDetails;
        IncludesGuarantorDetails = guarantorDetails;
        IncludesAIAdvisory = aiAdvisory;
        IncludesWorkflowHistory = workflowHistory;
        IncludesCommitteeComments = committeeComments;
    }

    public Result MarkAsFailed(string errorMessage)
    {
        Status = LoanPackStatus.Failed;
        
        AddDomainEvent(new LoanPackGenerationFailedEvent(
            Id, LoanApplicationId, ApplicationNumber, errorMessage));

        return Result.Success();
    }

    public Result Archive()
    {
        if (Status != LoanPackStatus.Generated)
            return Result.Failure("Only generated packs can be archived");

        Status = LoanPackStatus.Archived;
        return Result.Success();
    }

    public Result<LoanPack> CreateNewVersion(Guid generatedByUserId, string generatedByUserName)
    {
        var newPack = new LoanPack
        {
            LoanApplicationId = LoanApplicationId,
            ApplicationNumber = ApplicationNumber,
            Version = Version + 1,
            Status = LoanPackStatus.Generating,
            GeneratedAt = DateTime.UtcNow,
            GeneratedByUserId = generatedByUserId,
            GeneratedByUserName = generatedByUserName,
            CustomerName = CustomerName,
            ProductName = ProductName,
            RequestedAmount = RequestedAmount
        };

        // Archive current version
        Archive();

        newPack.AddDomainEvent(new LoanPackGenerationStartedEvent(
            newPack.Id, LoanApplicationId, ApplicationNumber, generatedByUserId));

        return Result.Success(newPack);
    }
}

// Domain Events
public record LoanPackGenerationStartedEvent(
    Guid LoanPackId, Guid LoanApplicationId, string ApplicationNumber, Guid GeneratedByUserId) : DomainEvent;

public record LoanPackGeneratedEvent(
    Guid LoanPackId, Guid LoanApplicationId, string ApplicationNumber, 
    int Version, string FileName, long FileSizeBytes) : DomainEvent;

public record LoanPackGenerationFailedEvent(
    Guid LoanPackId, Guid LoanApplicationId, string ApplicationNumber, string ErrorMessage) : DomainEvent;

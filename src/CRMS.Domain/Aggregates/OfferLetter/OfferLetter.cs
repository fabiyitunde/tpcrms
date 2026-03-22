using CRMS.Domain.Common;

namespace CRMS.Domain.Aggregates.OfferLetter;

public enum OfferLetterStatus
{
    Generating,
    Generated,
    Failed,
    Archived,
    Superseded
}

/// <summary>
/// Represents a generated offer letter document for a loan application.
/// Tracks versions, schedule summary, and generation history.
/// </summary>
public class OfferLetter : AggregateRoot
{
    public Guid LoanApplicationId { get; private set; }
    public string ApplicationNumber { get; private set; } = string.Empty;
    public int Version { get; private set; }
    public OfferLetterStatus Status { get; private set; }

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
    public string CustomerName { get; private set; } = string.Empty;
    public string ProductName { get; private set; } = string.Empty;
    public decimal ApprovedAmount { get; private set; }
    public int ApprovedTenorMonths { get; private set; }
    public decimal ApprovedInterestRate { get; private set; }

    // Schedule summary
    public decimal TotalInterest { get; private set; }
    public decimal TotalRepayment { get; private set; }
    public decimal MonthlyInstallment { get; private set; }
    public int InstallmentCount { get; private set; }
    public DateTime? ExpectedDisbursementDate { get; private set; }
    public string ScheduleSource { get; private set; } = string.Empty;

    private OfferLetter() { }

    public static Result<OfferLetter> Create(
        Guid loanApplicationId,
        string applicationNumber,
        Guid generatedByUserId,
        string generatedByUserName,
        string customerName,
        string productName,
        decimal approvedAmount,
        int approvedTenorMonths,
        decimal approvedInterestRate)
    {
        if (loanApplicationId == Guid.Empty)
            return Result.Failure<OfferLetter>("Loan application ID is required");

        if (string.IsNullOrWhiteSpace(applicationNumber))
            return Result.Failure<OfferLetter>("Application number is required");

        var letter = new OfferLetter
        {
            LoanApplicationId = loanApplicationId,
            ApplicationNumber = applicationNumber,
            Version = 1,
            Status = OfferLetterStatus.Generating,
            GeneratedAt = DateTime.UtcNow,
            GeneratedByUserId = generatedByUserId,
            GeneratedByUserName = generatedByUserName,
            CustomerName = customerName,
            ProductName = productName,
            ApprovedAmount = approvedAmount,
            ApprovedTenorMonths = approvedTenorMonths,
            ApprovedInterestRate = approvedInterestRate
        };

        letter.AddDomainEvent(new OfferLetterGeneratedEvent(
            letter.Id, loanApplicationId, applicationNumber, 1));

        return Result.Success(letter);
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
        Status = OfferLetterStatus.Generated;

        return Result.Success();
    }

    public Result SetScheduleSummary(
        decimal totalInterest,
        decimal totalRepayment,
        decimal monthlyInstallment,
        int installmentCount,
        string scheduleSource,
        DateTime? expectedDisbursementDate)
    {
        if (string.IsNullOrWhiteSpace(scheduleSource))
            return Result.Failure("Schedule source is required");

        TotalInterest = totalInterest;
        TotalRepayment = totalRepayment;
        MonthlyInstallment = monthlyInstallment;
        InstallmentCount = installmentCount;
        ScheduleSource = scheduleSource;
        ExpectedDisbursementDate = expectedDisbursementDate;

        return Result.Success();
    }

    public Result MarkAsFailed(string errorMessage)
    {
        Status = OfferLetterStatus.Failed;

        return Result.Success();
    }

    public Result Archive()
    {
        if (Status != OfferLetterStatus.Generated)
            return Result.Failure("Only generated offer letters can be archived");

        Status = OfferLetterStatus.Archived;
        return Result.Success();
    }

    public Result<OfferLetter> CreateNewVersion(Guid generatedByUserId, string generatedByUserName)
    {
        var newLetter = new OfferLetter
        {
            LoanApplicationId = LoanApplicationId,
            ApplicationNumber = ApplicationNumber,
            Version = Version + 1,
            Status = OfferLetterStatus.Generating,
            GeneratedAt = DateTime.UtcNow,
            GeneratedByUserId = generatedByUserId,
            GeneratedByUserName = generatedByUserName,
            CustomerName = CustomerName,
            ProductName = ProductName,
            ApprovedAmount = ApprovedAmount,
            ApprovedTenorMonths = ApprovedTenorMonths,
            ApprovedInterestRate = ApprovedInterestRate
        };

        // Supersede current version
        Status = OfferLetterStatus.Superseded;

        newLetter.AddDomainEvent(new OfferLetterGeneratedEvent(
            newLetter.Id, LoanApplicationId, ApplicationNumber, newLetter.Version));

        return Result.Success(newLetter);
    }
}

// Domain Events
public record OfferLetterGeneratedEvent(
    Guid OfferLetterId, Guid LoanApplicationId, string ApplicationNumber, int Version) : DomainEvent;

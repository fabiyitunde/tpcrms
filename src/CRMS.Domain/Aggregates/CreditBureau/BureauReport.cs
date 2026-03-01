using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.CreditBureau;

public class BureauReport : AggregateRoot
{
    public Guid? LoanApplicationId { get; private set; }
    public CreditBureauProvider Provider { get; private set; }
    public SubjectType SubjectType { get; private set; }
    public BureauReportStatus Status { get; private set; }
    
    // Consent Reference (required for NDPA compliance)
    public Guid ConsentRecordId { get; private set; }
    
    // Subject Identifiers
    public string? BVN { get; private set; }
    public string? RegistryId { get; private set; }
    public string? TaxId { get; private set; }
    public string SubjectName { get; private set; } = string.Empty;
    
    // Party Reference (for linking back to director/signatory/guarantor)
    public Guid? PartyId { get; private set; }
    public string? PartyType { get; private set; }
    
    // Report Data
    public int? CreditScore { get; private set; }
    public string? ScoreGrade { get; private set; }
    public DateTime? ReportDate { get; private set; }
    public string? RawResponseJson { get; private set; }
    public string? PdfReportBase64 { get; private set; }
    
    // Summary Metrics
    public int TotalAccounts { get; private set; }
    public int ActiveLoans { get; private set; } // Currently open facilities (active, not closed)
    public int PerformingAccounts { get; private set; } // Open facilities with no delinquency
    public int NonPerformingAccounts { get; private set; }
    public int ClosedAccounts { get; private set; }
    public decimal TotalOutstandingBalance { get; private set; }
    public decimal TotalOverdue { get; private set; }
    public decimal TotalCreditLimit { get; private set; }
    public int MaxDelinquencyDays { get; private set; }
    public bool HasLegalActions { get; private set; }
    
    // Fraud Check Results (from SmartComply Loan Fraud Check)
    public int? FraudRiskScore { get; private set; }
    public string? FraudRecommendation { get; private set; }
    public string? FraudCheckRawJson { get; private set; }
    
    // Request Tracking
    public string? RequestReference { get; private set; }
    public Guid RequestedByUserId { get; private set; }
    public DateTime RequestedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? ErrorMessage { get; private set; }

    // Related Entities
    private readonly List<BureauAccount> _accounts = [];
    private readonly List<BureauScoreFactor> _scoreFactors = [];

    public IReadOnlyCollection<BureauAccount> Accounts => _accounts.AsReadOnly();
    public IReadOnlyCollection<BureauScoreFactor> ScoreFactors => _scoreFactors.AsReadOnly();

    private BureauReport() { }

    public static Result<BureauReport> Create(
        CreditBureauProvider provider,
        SubjectType subjectType,
        string subjectName,
        string? bvn,
        Guid requestedByUserId,
        Guid consentRecordId,
        Guid? loanApplicationId = null,
        string? taxId = null,
        Guid? partyId = null,
        string? partyType = null)
    {
        if (string.IsNullOrWhiteSpace(subjectName))
            return Result.Failure<BureauReport>("Subject name is required");

        if (subjectType == SubjectType.Individual && string.IsNullOrWhiteSpace(bvn))
            return Result.Failure<BureauReport>("BVN is required for individual credit checks");

        if (consentRecordId == Guid.Empty)
            return Result.Failure<BureauReport>("Valid consent record is required for credit bureau checks (NDPA compliance)");

        var report = new BureauReport
        {
            Provider = provider,
            SubjectType = subjectType,
            SubjectName = subjectName,
            BVN = bvn,
            TaxId = taxId,
            Status = BureauReportStatus.Pending,
            RequestedByUserId = requestedByUserId,
            RequestedAt = DateTime.UtcNow,
            LoanApplicationId = loanApplicationId,
            ConsentRecordId = consentRecordId,
            RequestReference = GenerateRequestReference(),
            PartyId = partyId,
            PartyType = partyType
        };

        report.AddDomainEvent(new BureauReportRequestedEvent(report.Id, provider, subjectName, bvn, consentRecordId));

        return Result.Success(report);
    }

    /// <summary>
    /// Creates a BureauReport record for a consent failure (NDPA audit trail requirement).
    /// This records the attempt even when no valid consent exists.
    /// </summary>
    public static Result<BureauReport> CreateForConsentFailure(
        CreditBureauProvider provider,
        SubjectType subjectType,
        string subjectName,
        string? bvn,
        string? taxId,
        Guid requestedByUserId,
        Guid? loanApplicationId,
        Guid? partyId,
        string? partyType,
        string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(subjectName))
            return Result.Failure<BureauReport>("Subject name is required");

        var report = new BureauReport
        {
            Provider = provider,
            SubjectType = subjectType,
            SubjectName = subjectName,
            BVN = bvn,
            TaxId = taxId,
            Status = BureauReportStatus.Failed,
            RequestedByUserId = requestedByUserId,
            RequestedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            LoanApplicationId = loanApplicationId,
            ConsentRecordId = Guid.Empty, // No consent - that's the failure reason
            RequestReference = GenerateRequestReference(),
            PartyId = partyId,
            PartyType = partyType,
            ErrorMessage = errorMessage
        };

        // Note: No BureauReportRequestedEvent since no actual bureau access was attempted
        return Result.Success(report);
    }

    private static string GenerateRequestReference()
    {
        return $"BR{DateTime.UtcNow:yyyyMMddHHmmss}{Guid.NewGuid().ToString()[..6].ToUpper()}";
    }

    public void MarkProcessing()
    {
        Status = BureauReportStatus.Processing;
    }

    public void CompleteWithData(
        string registryId,
        int? creditScore,
        string? scoreGrade,
        DateTime reportDate,
        string? rawResponseJson,
        string? pdfReportBase64,
        int totalAccounts,
        int activeLoans,
        int performingAccounts,
        int nonPerformingAccounts,
        int closedAccounts,
        decimal totalOutstandingBalance,
        decimal totalOverdue,
        decimal totalCreditLimit,
        int maxDelinquencyDays,
        bool hasLegalActions)
    {
        RegistryId = registryId;
        CreditScore = creditScore;
        ScoreGrade = scoreGrade;
        ReportDate = reportDate;
        RawResponseJson = rawResponseJson;
        PdfReportBase64 = pdfReportBase64;
        TotalAccounts = totalAccounts;
        ActiveLoans = activeLoans;
        PerformingAccounts = performingAccounts;
        NonPerformingAccounts = nonPerformingAccounts;
        ClosedAccounts = closedAccounts;
        TotalOutstandingBalance = totalOutstandingBalance;
        TotalOverdue = totalOverdue;
        TotalCreditLimit = totalCreditLimit;
        MaxDelinquencyDays = maxDelinquencyDays;
        HasLegalActions = hasLegalActions;
        Status = BureauReportStatus.Completed;
        CompletedAt = DateTime.UtcNow;

        AddDomainEvent(new BureauReportCompletedEvent(Id, Provider, SubjectName, creditScore));
    }

    public void MarkFailed(string errorMessage)
    {
        Status = BureauReportStatus.Failed;
        ErrorMessage = errorMessage;
        CompletedAt = DateTime.UtcNow;
    }

    public void MarkNotFound()
    {
        Status = BureauReportStatus.NotFound;
        CompletedAt = DateTime.UtcNow;
    }

    public void AddAccount(BureauAccount account)
    {
        _accounts.Add(account);
    }

    public void AddScoreFactor(BureauScoreFactor factor)
    {
        _scoreFactors.Add(factor);
    }

    /// <summary>
    /// Records fraud check results from SmartComply Loan Fraud Check API.
    /// Called after the main credit report is completed.
    /// </summary>
    public void RecordFraudCheckResults(int? fraudRiskScore, string? fraudRecommendation, string? rawJson = null)
    {
        FraudRiskScore = fraudRiskScore;
        FraudRecommendation = fraudRecommendation;
        FraudCheckRawJson = rawJson;
    }
}

// Domain Events
public record BureauReportRequestedEvent(Guid ReportId, CreditBureauProvider Provider, string SubjectName, string? BVN, Guid ConsentRecordId) : DomainEvent;
public record BureauReportCompletedEvent(Guid ReportId, CreditBureauProvider Provider, string SubjectName, int? CreditScore) : DomainEvent;

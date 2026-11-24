using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.CreditBureau;

public class BureauReport : AggregateRoot
{
    // One nullable FK per product track. RH-SHF joined these in Phase B of the NAMP-alignment work:
    // it previously kept a single flat bureau result on RhshfCreditProfile, which capped it at one
    // company-level subject and no score/grade/fraud data.
    public Guid? LoanApplicationId { get; private set; }
    public Guid? NampApplicationId { get; private set; }
    public Guid? RhshfCreditProfileId { get; private set; }
    public CreditBureauProvider Provider { get; private set; }
    public SubjectType SubjectType { get; private set; }
    public BureauReportStatus Status { get; private set; }

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
    public int DelinquentFacilities { get; private set; }
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
        Guid? loanApplicationId = null,
        Guid? nampApplicationId = null,
        string? taxId = null,
        Guid? partyId = null,
        string? partyType = null,
        Guid? rhshfCreditProfileId = null)
    {
        if (string.IsNullOrWhiteSpace(subjectName))
            return Result.Failure<BureauReport>("Subject name is required");

        if (subjectType == SubjectType.Individual && string.IsNullOrWhiteSpace(bvn))
            return Result.Failure<BureauReport>("BVN is required for individual credit checks");

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
            NampApplicationId = nampApplicationId,
            RhshfCreditProfileId = rhshfCreditProfileId,
            RequestReference = GenerateRequestReference(),
            PartyId = partyId,
            PartyType = partyType
        };

        report.AddDomainEvent(new BureauReportRequestedEvent(report.Id, provider, subjectName, bvn));

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

    /// <summary>The bureau that actually answered, once known — a pull that starts at CRC may be
    /// answered by First Central instead.</summary>
    public void RecordProvider(CreditBureauProvider provider)
    {
        Provider = provider;
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
        int delinquentFacilities,
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
        DelinquentFacilities = delinquentFacilities;
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

    /// <summary>The longest bureau answer kept, so an oversized reply (an HTML error page, say)
    /// cannot stop the report itself from being saved.</summary>
    public const int MaxErrorMessageLength = 4000;

    public void MarkFailed(string errorMessage)
    {
        Status = BureauReportStatus.Failed;
        ErrorMessage = Cap(errorMessage);
        CompletedAt = DateTime.UtcNow;
    }

    private static string? Cap(string? text) =>
        text is null || text.Length <= MaxErrorMessageLength ? text : text[..(MaxErrorMessageLength - 1)] + "…";

    /// <summary>No bureau holds a file. <paramref name="detail"/> keeps what the bureaus actually
    /// answered, so staff can tell a genuine "no record" from a check that only looked like one.</summary>
    public void MarkNotFound(string? detail = null)
    {
        Status = BureauReportStatus.NotFound;
        ErrorMessage = Cap(detail);
        CompletedAt = DateTime.UtcNow;
    }

    /// <summary>The grade written on a completed individual report whose bureau score could not be
    /// had. The score itself is left empty: the CRMS no longer makes one up.</summary>
    public const string ScoreUnavailableGrade = "UNAVAILABLE";

    /// <summary>The grade older reports carry, from when a missing score was replaced by an estimate.</summary>
    public const string LegacyDerivedGrade = "DERIVED";

    /// <summary>A completed individual report without a real bureau score: the report is kept, the
    /// score is fetched again on the next run of the checks.</summary>
    public bool IsScoreMissing =>
        Status == BureauReportStatus.Completed && SubjectType == SubjectType.Individual
        && ScoreGrade is ScoreUnavailableGrade or LegacyDerivedGrade;

    /// <summary>Why a completed report has no score, in the bureau's own words. Shown on the card.</summary>
    public void NoteScoreUnavailable(string reason)
    {
        ErrorMessage = Cap(reason);
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
public record BureauReportRequestedEvent(Guid ReportId, CreditBureauProvider Provider, string SubjectName, string? BVN) : DomainEvent;
public record BureauReportCompletedEvent(Guid ReportId, CreditBureauProvider Provider, string SubjectName, int? CreditScore) : DomainEvent;

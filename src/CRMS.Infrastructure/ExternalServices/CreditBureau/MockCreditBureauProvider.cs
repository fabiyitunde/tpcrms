using CRMS.Domain.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Infrastructure.ExternalServices.CreditBureau;

public class MockCreditBureauProvider : ICreditBureauProvider
{
    public CreditBureauProvider ProviderType => CreditBureauProvider.CreditRegistry;

    private readonly Dictionary<string, MockSubject> _subjects = new()
    {
        ["22234567890"] = new MockSubject(
            RegistryId: "REG001",
            FullName: "John Adebayo",
            BVN: "22234567890",
            DateOfBirth: "1975-03-20",
            Gender: "M",
            Phone: "+2348012345001",
            Email: "john.adebayo@email.com",
            Address: "45 Victoria Island, Lagos",
            SubjectType: SubjectType.Individual,
            CreditScore: 720,
            ScoreGrade: "A",
            Accounts: new List<MockAccount>
            {
                new("ACC001", "First Bank", "Term Loan", "Performing", 0, 5000000, 1500000, DateTime.Now.AddYears(-2), "000000000000"),
                new("ACC002", "GTBank", "Credit Card", "Performing", 0, 500000, 150000, DateTime.Now.AddYears(-1), "000000001111"),
            }
        ),
        ["22234567891"] = new MockSubject(
            RegistryId: "REG002",
            FullName: "Amina Ibrahim",
            BVN: "22234567891",
            DateOfBirth: "1980-07-10",
            Gender: "F",
            Phone: "+2348012345002",
            Email: "amina.ibrahim@email.com",
            Address: "22 Ikoyi, Lagos",
            SubjectType: SubjectType.Individual,
            CreditScore: 650,
            ScoreGrade: "B",
            Accounts: new List<MockAccount>
            {
                new("ACC003", "Access Bank", "Mortgage", "Performing", 0, 25000000, 18000000, DateTime.Now.AddYears(-3), "000000000000"),
                new("ACC004", "UBA", "Personal Loan", "NonPerforming", 60, 2000000, 1800000, DateTime.Now.AddMonths(-6), "001122223333"),
            }
        ),
        ["22234567892"] = new MockSubject(
            RegistryId: "REG003",
            FullName: "Chukwuma Okonkwo",
            BVN: "22234567892",
            DateOfBirth: "1982-11-05",
            Gender: "M",
            Phone: "+2348012345003",
            Email: "chukwuma.o@email.com",
            Address: "10 Lekki Phase 1, Lagos",
            SubjectType: SubjectType.Individual,
            CreditScore: 580,
            ScoreGrade: "C",
            Accounts: new List<MockAccount>
            {
                new("ACC005", "Zenith Bank", "Business Loan", "NonPerforming", 90, 10000000, 9500000, DateTime.Now.AddYears(-1), "001112223333"),
                new("ACC006", "FCMB", "Overdraft", "WrittenOff", 360, 1000000, 1000000, DateTime.Now.AddYears(-2), "888888888888"),
            }
        ),
        ["22212345678"] = new MockSubject(
            RegistryId: "REG004",
            FullName: "Oluwaseun Bakare",
            BVN: "22212345678",
            DateOfBirth: "1990-08-15",
            Gender: "M",
            Phone: "+2348098765432",
            Email: "seun.bakare@email.com",
            Address: "55 Surulere, Lagos",
            SubjectType: SubjectType.Individual,
            CreditScore: 780,
            ScoreGrade: "A+",
            Accounts: new List<MockAccount>
            {
                new("ACC007", "Sterling Bank", "Salary Loan", "Closed", 0, 500000, 0, DateTime.Now.AddYears(-2), "000000000000"),
            }
        )
    };

    public Task<Result<BureauSearchResult>> SearchByBVNAsync(string bvn, CancellationToken ct = default)
    {
        if (_subjects.TryGetValue(bvn, out var subject))
        {
            return Task.FromResult(Result.Success(new BureauSearchResult(
                Found: true,
                RegistryId: subject.RegistryId,
                FullName: subject.FullName,
                BVN: subject.BVN,
                DateOfBirth: subject.DateOfBirth,
                Gender: subject.Gender,
                Phone: subject.Phone,
                Email: subject.Email,
                Address: subject.Address,
                SubjectType: subject.SubjectType
            )));
        }

        return Task.FromResult(Result.Success(new BureauSearchResult(
            false, null, null, bvn, null, null, null, null, null, SubjectType.Individual)));
    }

    public Task<Result<BureauSearchResult>> SearchByNameAsync(string firstName, string lastName, DateTime? dateOfBirth, CancellationToken ct = default)
    {
        var fullName = $"{firstName} {lastName}".ToLowerInvariant();
        var subject = _subjects.Values.FirstOrDefault(s => s.FullName.ToLowerInvariant().Contains(fullName));

        if (subject != null)
        {
            return Task.FromResult(Result.Success(new BureauSearchResult(
                Found: true,
                RegistryId: subject.RegistryId,
                FullName: subject.FullName,
                BVN: subject.BVN,
                DateOfBirth: subject.DateOfBirth,
                Gender: subject.Gender,
                Phone: subject.Phone,
                Email: subject.Email,
                Address: subject.Address,
                SubjectType: subject.SubjectType
            )));
        }

        return Task.FromResult(Result.Success(new BureauSearchResult(
            false, null, null, null, null, null, null, null, null, SubjectType.Individual)));
    }

    public Task<Result<BureauSearchResult>> SearchByTaxIdAsync(string taxId, CancellationToken ct = default)
    {
        return Task.FromResult(Result.Success(new BureauSearchResult(
            false, null, null, null, null, null, null, null, null, SubjectType.Business)));
    }

    public Task<Result<BureauCreditReport>> GetCreditReportAsync(string registryId, bool includePdf = false, CancellationToken ct = default)
    {
        var subject = _subjects.Values.FirstOrDefault(s => s.RegistryId == registryId);
        if (subject == null)
            return Task.FromResult(Result.Failure<BureauCreditReport>($"Subject not found: {registryId}"));

        var accounts = subject.Accounts.Select(a => new BureauAccountData(
            AccountNumber: a.AccountNumber,
            CreditorName: a.CreditorName,
            AccountType: a.AccountType,
            Status: a.Status,
            DelinquencyDays: a.DelinquencyDays,
            CreditLimit: a.CreditLimit,
            Balance: a.Balance,
            MinimumPayment: a.CreditLimit / 24,
            DateOpened: a.DateOpened,
            DateClosed: a.Status == "Closed" ? DateTime.Now.AddMonths(-1) : null,
            LastPaymentDate: DateTime.Now.AddDays(-15),
            LastPaymentAmount: a.CreditLimit / 24,
            PaymentProfile: a.PaymentProfile,
            LegalStatus: null,
            LegalStatusDate: null,
            Currency: "NGN",
            LastUpdated: DateTime.Now
        )).ToList();

        var scoreFactors = new List<BureauScoreFactorData>
        {
            new("F001", "Payment History", "Positive", 1),
            new("F002", "Credit Utilization", subject.CreditScore > 700 ? "Positive" : "Negative", 2),
            new("F003", "Length of Credit History", "Positive", 3),
            new("F004", "Credit Mix", "Neutral", 4)
        };

        var performingCount = accounts.Count(a => a.Status == "Performing");
        var nonPerformingCount = accounts.Count(a => a.Status == "NonPerforming");
        var closedCount = accounts.Count(a => a.Status == "Closed");
        var writtenOffCount = accounts.Count(a => a.Status == "WrittenOff");

        var summary = new BureauReportSummary(
            TotalAccounts: accounts.Count,
            PerformingAccounts: performingCount,
            NonPerformingAccounts: nonPerformingCount,
            ClosedAccounts: closedCount,
            WrittenOffAccounts: writtenOffCount,
            TotalOutstandingBalance: accounts.Sum(a => a.Balance),
            TotalCreditLimit: accounts.Sum(a => a.CreditLimit),
            MaxDelinquencyDays: accounts.Any() ? accounts.Max(a => a.DelinquencyDays) : 0,
            HasLegalActions: false,
            EnquiriesLast30Days: 2,
            EnquiriesLast90Days: 5
        );

        return Task.FromResult(Result.Success(new BureauCreditReport(
            RegistryId: registryId,
            FullName: subject.FullName,
            CreditScore: subject.CreditScore,
            ScoreGrade: subject.ScoreGrade,
            ReportDate: DateTime.UtcNow,
            RawJson: null,
            PdfBase64: null,
            Summary: summary,
            Accounts: accounts,
            ScoreFactors: scoreFactors
        )));
    }

    public Task<Result<BureauCreditScore>> GetCreditScoreAsync(string registryId, CancellationToken ct = default)
    {
        var subject = _subjects.Values.FirstOrDefault(s => s.RegistryId == registryId);
        if (subject == null)
            return Task.FromResult(Result.Failure<BureauCreditScore>($"Subject not found: {registryId}"));

        var factors = new List<BureauScoreFactorData>
        {
            new("F001", "Payment History", "Positive", 1),
            new("F002", "Credit Utilization", subject.CreditScore > 700 ? "Positive" : "Negative", 2)
        };

        return Task.FromResult(Result.Success(new BureauCreditScore(
            RegistryId: registryId,
            Score: subject.CreditScore,
            Grade: subject.ScoreGrade,
            GeneratedDate: DateTime.UtcNow,
            Factors: factors
        )));
    }

    private record MockSubject(
        string RegistryId,
        string FullName,
        string BVN,
        string DateOfBirth,
        string Gender,
        string Phone,
        string Email,
        string Address,
        SubjectType SubjectType,
        int CreditScore,
        string ScoreGrade,
        List<MockAccount> Accounts
    );

    private record MockAccount(
        string AccountNumber,
        string CreditorName,
        string AccountType,
        string Status,
        int DelinquencyDays,
        decimal CreditLimit,
        decimal Balance,
        DateTime DateOpened,
        string PaymentProfile
    );
}

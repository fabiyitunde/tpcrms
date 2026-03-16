using CRMS.Domain.Common;
using CRMS.Domain.Interfaces;

namespace CRMS.Infrastructure.ExternalServices.CoreBanking;

/// <summary>
/// Mock implementation of ICoreBankingService for development/testing.
/// Mirrors the real CBS API shape: a single fulldetailsbynuban response provides
/// clientDetails + directors[] + signatories[] in one call, and a transactions endpoint
/// returns account transactions with Deposit/Withdrawal types.
///
/// Real API endpoints:
///   1. GET /core/account/fulldetailsbynuban/{nuban} → client + directors + signatories
///   2. GET /core/transactions/{nuban}?startDate=DD-MM-YYYY&endDate=DD-MM-YYYY → transactions
///
/// Fields NOT returned by the real CBS API (but present in domain records for SmartComply):
///   - Directors: Nationality, ShareholdingPercent (come from SmartComply CAC)
///   - Signatories: MandateType, Designation (not in CBS; set to defaults)
///   - CorporateInfo: Industry, IncorporationDate, TaxId (not in CBS)
/// </summary>
public class MockCoreBankingService : ICoreBankingService
{
    // Simulates the single fulldetailsbynuban response per NUBAN
    private readonly Dictionary<string, MockAccountData> _accounts = new();

    public MockCoreBankingService()
    {
        SeedMockData();
    }

    private void SeedMockData()
    {
        // Corporate account — matches the CBS API response shape
        _accounts["1234567890"] = new MockAccountData
        {
            ClientType = "BUSINESS",
            ClientId = 1643,
            FullName = "Acme Industries Ltd",
            Bvn = "09191919190",
            MobileNo = "+2348012345678",
            Status = "Active",
            IncorporationNumber = "RC123456",
            ActivationDate = "01-06-2010",
            AddressLine1 = "123 Industrial Avenue",
            City = "Lagos",
            State = "Lagos",
            Directors =
            [
                new MockParty
                {
                    Id = 28,
                    Firstname = "John",
                    Lastname = "Adebayo",
                    Bvn = "22234567890",
                    Email = "john.adebayo@acmeindustries.com",
                    MobileNo = "+2348012345001",
                    DateOfBirth = "20-03-1975",
                    AddressLine1 = "45 Victoria Island",
                    City = "Lagos",
                    Status = "Active"
                },
                new MockParty
                {
                    Id = 29,
                    Firstname = "Amina",
                    Lastname = "Ibrahim",
                    Bvn = "22234567891",
                    Email = "amina.ibrahim@acmeindustries.com",
                    MobileNo = "+2348012345002",
                    DateOfBirth = "10-07-1980",
                    AddressLine1 = "22 Ikoyi",
                    City = "Lagos",
                    Status = "Active"
                },
                new MockParty
                {
                    Id = 30,
                    Firstname = "Chukwuma",
                    Lastname = "Okonkwo",
                    Bvn = "22234567892",
                    Email = "chukwuma.o@acmeindustries.com",
                    MobileNo = "+2348012345003",
                    DateOfBirth = "05-11-1982",
                    AddressLine1 = "10 Lekki Phase 1",
                    City = "Lagos",
                    Status = "Active"
                }
            ],
            Signatories =
            [
                new MockParty
                {
                    Id = 28,
                    Firstname = "John",
                    Lastname = "Adebayo",
                    Bvn = "22234567890",
                    Email = "john.adebayo@acmeindustries.com",
                    MobileNo = "+2348012345001",
                    DateOfBirth = "20-03-1975",
                    AddressLine1 = "45 Victoria Island",
                    City = "Lagos",
                    Status = "Active"
                },
                new MockParty
                {
                    Id = 31,
                    Firstname = "Fatima",
                    Lastname = "Bello",
                    Bvn = "22234567893",
                    Email = "fatima.bello@acmeindustries.com",
                    MobileNo = "+2348012345004",
                    DateOfBirth = "15-09-1985",
                    AddressLine1 = "8 Admiralty Way",
                    City = "Lagos",
                    Status = "Active"
                }
            ]
        };

        // Individual account
        _accounts["0987654321"] = new MockAccountData
        {
            ClientType = "PERSON",
            ClientId = 1644,
            FullName = "Oluwaseun Bakare",
            Bvn = "22212345678",
            MobileNo = "+2348098765432",
            Status = "Active",
            ActivationDate = "10-03-2018",
            AddressLine1 = "55 Surulere",
            City = "Lagos",
            State = "Lagos",
            Directors = [],
            Signatories = []
        };
    }

    public Task<Result<CustomerInfo>> GetCustomerByAccountNumberAsync(string accountNumber, CancellationToken ct = default)
    {
        if (!_accounts.TryGetValue(accountNumber, out var data))
            return Task.FromResult(Result.Failure<CustomerInfo>($"Customer with account {accountNumber} not found"));

        var customerType = string.Equals(data.ClientType, "BUSINESS", StringComparison.OrdinalIgnoreCase)
            ? CustomerType.Corporate
            : CustomerType.Individual;

        return Task.FromResult(Result.Success(new CustomerInfo(
            CustomerId: data.ClientId.ToString(),
            FullName: data.FullName,
            CustomerType: customerType,
            Email: null, // CBS API does not return email at client level
            PhoneNumber: data.MobileNo,
            BVN: data.Bvn,
            DateOfBirth: null,
            Address: BuildAddress(data.AddressLine1, data.City, data.State)
        )));
    }

    public Task<Result<CustomerInfo>> GetCustomerByIdAsync(string customerId, CancellationToken ct = default)
    {
        return Task.FromResult(Result.Failure<CustomerInfo>(
            "GetCustomerByIdAsync is not supported by the core banking API. Use GetCustomerByAccountNumberAsync."));
    }

    public Task<Result<string>> CreateCustomerAsync(CreateCustomerRequest request, CancellationToken ct = default)
    {
        return Task.FromResult(Result.Failure<string>(
            "CreateCustomerAsync is not implemented — accounts are created externally in core banking."));
    }

    public Task<Result<CorporateInfo>> GetCorporateInfoAsync(string accountNumber, CancellationToken ct = default)
    {
        if (!_accounts.TryGetValue(accountNumber, out var data))
            return Task.FromResult(Result.Failure<CorporateInfo>($"Corporate info for account {accountNumber} not found"));

        return Task.FromResult(Result.Success(new CorporateInfo(
            CorporateId: data.ClientId.ToString(),
            CompanyName: data.FullName,
            RegistrationNumber: data.IncorporationNumber, // CBS: incorporationNumber
            Industry: null,            // not available from CBS
            IncorporationDate: null,   // not available from CBS
            RegisteredAddress: BuildAddress(data.AddressLine1, data.City, data.State),
            TaxIdentificationNumber: null // not available from CBS
        )));
    }

    public Task<Result<IReadOnlyList<DirectorInfo>>> GetDirectorsAsync(string corporateId, CancellationToken ct = default)
    {
        // In the real CBS API, directors come from fulldetailsbynuban (keyed by NUBAN).
        // The code passes customer.CustomerId here. Match by client ID.
        var data = _accounts.Values.FirstOrDefault(a => a.ClientId.ToString() == corporateId);
        if (data == null)
            return Task.FromResult(Result.Success<IReadOnlyList<DirectorInfo>>(new List<DirectorInfo>()));

        var directors = data.Directors.Select(d => new DirectorInfo(
            DirectorId: d.Id.ToString(),
            FullName: $"{d.Firstname} {d.Lastname}".Trim(),
            BVN: d.Bvn,
            Email: d.Email,
            PhoneNumber: d.MobileNo,
            Address: BuildAddress(d.AddressLine1, d.City, null),
            DateOfBirth: ParseDate(d.DateOfBirth),
            Nationality: null,           // not available from CBS
            ShareholdingPercent: null     // not available from CBS (comes from SmartComply CAC)
        )).ToList();

        return Task.FromResult(Result.Success<IReadOnlyList<DirectorInfo>>(directors));
    }

    public Task<Result<IReadOnlyList<SignatoryInfo>>> GetSignatoriesAsync(string accountNumber, CancellationToken ct = default)
    {
        if (!_accounts.TryGetValue(accountNumber, out var data))
            return Task.FromResult(Result.Success<IReadOnlyList<SignatoryInfo>>(new List<SignatoryInfo>()));

        var signatories = data.Signatories.Select(s => new SignatoryInfo(
            SignatoryId: s.Id.ToString(),
            FullName: $"{s.Firstname} {s.Lastname}".Trim(),
            BVN: s.Bvn,
            Email: s.Email,
            PhoneNumber: s.MobileNo,
            MandateType: "A",      // CBS does not provide mandate type; default
            Designation: null      // CBS does not provide designation
        )).ToList();

        return Task.FromResult(Result.Success<IReadOnlyList<SignatoryInfo>>(signatories));
    }

    public Task<Result<AccountInfo>> GetAccountInfoAsync(string accountNumber, CancellationToken ct = default)
    {
        if (!_accounts.TryGetValue(accountNumber, out var data))
            return Task.FromResult(Result.Failure<AccountInfo>($"Account {accountNumber} not found"));

        return Task.FromResult(Result.Success(new AccountInfo(
            AccountNumber: accountNumber,
            AccountName: data.FullName,
            AccountType: data.ClientType ?? "Unknown",
            Currency: "NGN",
            CurrentBalance: 0m,       // CBS fulldetailsbynuban does not return balance
            AvailableBalance: 0m,
            Status: data.Status ?? "Unknown",
            OpenedDate: ParseDate(data.ActivationDate) ?? DateTime.MinValue
        )));
    }

    public Task<Result<AccountStatement>> GetStatementAsync(string accountNumber, DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        if (!_accounts.TryGetValue(accountNumber, out var data))
            return Task.FromResult(Result.Failure<AccountStatement>($"Account {accountNumber} not found"));

        var transactions = GenerateMockTransactions(fromDate, toDate);
        var totalCredits = transactions.Where(t => t.Type == TransactionType.Credit).Sum(t => t.Amount);
        var totalDebits = transactions.Where(t => t.Type == TransactionType.Debit).Sum(t => t.Amount);
        var openingBalance = transactions.Count > 0
            ? transactions[0].RunningBalance + (transactions[0].Type == TransactionType.Debit ? transactions[0].Amount : -transactions[0].Amount)
            : 0m;
        var closingBalance = transactions.Count > 0 ? transactions[^1].RunningBalance : 0m;

        return Task.FromResult(Result.Success(new AccountStatement(
            AccountNumber: accountNumber,
            FromDate: fromDate,
            ToDate: toDate,
            OpeningBalance: openingBalance,
            ClosingBalance: closingBalance,
            TotalCredits: totalCredits,
            TotalDebits: totalDebits,
            Transactions: transactions
        )));
    }

    public Task<Result<decimal>> GetAccountBalanceAsync(string accountNumber, CancellationToken ct = default)
    {
        return Task.FromResult(Result.Failure<decimal>(
            "GetAccountBalanceAsync is not supported by the available CBS endpoints."));
    }

    // Loan operations — not available from real CBS API
    public Task<Result<string>> CreateLoanAsync(CreateLoanRequest request, CancellationToken ct = default)
        => Task.FromResult(Result.Failure<string>("Loan booking is done manually in core banking. Automated API not implemented."));

    public Task<Result> ApproveLoanAsync(string loanId, CancellationToken ct = default)
        => Task.FromResult(Result.Failure("Loan approval is done manually in core banking."));

    public Task<Result> DisburseLoanAsync(DisbursementRequest request, CancellationToken ct = default)
        => Task.FromResult(Result.Failure("Disbursement is done manually in core banking."));

    public Task<Result<LoanInfo>> GetLoanInfoAsync(string loanId, CancellationToken ct = default)
        => Task.FromResult(Result.Failure<LoanInfo>("Loan info query not available from current CBS API."));

    public Task<Result<RepaymentSchedule>> GetRepaymentScheduleAsync(string loanId, CancellationToken ct = default)
        => Task.FromResult(Result.Failure<RepaymentSchedule>("Repayment schedule query not available from current CBS API."));

    public Task<Result<LoanStatus>> GetLoanStatusAsync(string loanId, CancellationToken ct = default)
        => Task.FromResult(Result.Failure<LoanStatus>("Loan status query not available from current CBS API."));

    // TODO: Replace with real CBS API call once endpoint is specified by core banking provider.
    // Returns mock zero exposure so AI advisory can run without blocking on this gap.
    public Task<Result<CustomerExposure>> GetCustomerExposureAsync(string accountNumber, CancellationToken ct = default)
    {
        var exposure = new CustomerExposure(
            accountNumber,
            _accounts.TryGetValue(accountNumber, out var acct) ? acct.FullName : "Unknown",
            ActiveFacilitiesCount: 0,
            TotalOutstandingBalance: 0m,
            TotalApprovedLimit: 0m,
            Facilities: Array.Empty<FacilitySummary>()
        );
        return Task.FromResult(Result.Success(exposure));
    }

    #region Helpers

    private static string? BuildAddress(string? line1, string? city, string? state)
    {
        var parts = new[] { line1, city, state }.Where(p => !string.IsNullOrWhiteSpace(p));
        return parts.Any() ? string.Join(", ", parts) : null;
    }

    private static DateTime? ParseDate(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr)) return null;
        if (DateTime.TryParseExact(dateStr, "dd-MM-yyyy", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var dt))
            return dt;
        return null;
    }

    /// <summary>
    /// Generates mock transactions matching the real CBS /core/transactions response shape.
    /// Transaction types: "Deposit" → Credit, "Withdrawal" → Debit.
    /// Includes realistic note patterns from the CBS sandbox.
    /// </summary>
    private static List<StatementTransaction> GenerateMockTransactions(DateTime fromDate, DateTime toDate)
    {
        var transactions = new List<StatementTransaction>();
        var random = new Random(42);
        decimal runningBalance = 1_000_000m;
        var currentDate = fromDate;

        var depositNotes = new[]
        {
            "DEPOSIT - {0}",
            "REVERSAL - SMS CHARGES",
            "Transfer In - Client Payment"
        };

        var withdrawalNotes = new[]
        {
            "{0} - SMS CHARGES",
            "ELECTRICITY FROM {1} FOR 0000000000 - ID : {2}",
            "AIRTIME FROM {1} TO 08096886444 - ID : {2}",
            "ACCOUNT MAINTENANCE FEE",
            "Vendor Payment - {0}"
        };

        int txnId = 1;
        while (currentDate <= toDate && transactions.Count < 60)
        {
            var isDeposit = random.Next(100) < 35; // ~35% deposits
            var amount = Math.Round((decimal)(random.NextDouble() * 500_000 + 1_000), 2);
            var idSuffix = Guid.NewGuid().ToString("N")[..32].ToUpper();

            string note;
            TransactionType type;

            if (isDeposit)
            {
                type = TransactionType.Credit;
                note = string.Format(depositNotes[random.Next(depositNotes.Length)], "Client");
                runningBalance += amount;
            }
            else
            {
                type = TransactionType.Debit;
                note = string.Format(withdrawalNotes[random.Next(withdrawalNotes.Length)],
                    "Acme Industries Ltd", "1234567890", idSuffix);
                runningBalance -= amount;
                if (runningBalance < 0) runningBalance = 100_000m;
            }

            transactions.Add(new StatementTransaction(
                TransactionId: txnId.ToString(),
                Date: currentDate,
                Description: note,
                Amount: amount,
                Type: type,
                RunningBalance: runningBalance,
                Reference: random.Next(100) < 30 ? $"999992{currentDate:yyMMddHHmmss}{random.Next(100000, 999999)}" : null
            ));

            txnId++;
            currentDate = currentDate.AddDays(random.Next(1, 4)).AddHours(random.Next(6, 18));
        }

        return transactions;
    }

    #endregion

    #region Mock Data Models

    private class MockAccountData
    {
        public string? ClientType { get; set; }
        public int ClientId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? Bvn { get; set; }
        public string? MobileNo { get; set; }
        public string? Status { get; set; }
        public string? IncorporationNumber { get; set; }
        public string? ActivationDate { get; set; }
        public string? AddressLine1 { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public List<MockParty> Directors { get; set; } = [];
        public List<MockParty> Signatories { get; set; } = [];
    }

    private class MockParty
    {
        public int Id { get; set; }
        public string? Firstname { get; set; }
        public string? Lastname { get; set; }
        public string? Bvn { get; set; }
        public string? Email { get; set; }
        public string? MobileNo { get; set; }
        public string? DateOfBirth { get; set; }
        public string? AddressLine1 { get; set; }
        public string? City { get; set; }
        public string? Status { get; set; }
    }

    #endregion
}

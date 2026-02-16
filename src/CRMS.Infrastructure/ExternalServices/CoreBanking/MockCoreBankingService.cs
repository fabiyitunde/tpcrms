using CRMS.Domain.Common;
using CRMS.Domain.Interfaces;

namespace CRMS.Infrastructure.ExternalServices.CoreBanking;

public class MockCoreBankingService : ICoreBankingService
{
    private readonly Dictionary<string, CustomerInfo> _customers = new();
    private readonly Dictionary<string, CorporateInfo> _corporates = new();
    private readonly Dictionary<string, AccountInfo> _accounts = new();
    private readonly Dictionary<string, LoanInfo> _loans = new();
    private readonly Dictionary<string, List<DirectorInfo>> _directors = new();
    private readonly Dictionary<string, List<SignatoryInfo>> _signatories = new();

    public MockCoreBankingService()
    {
        SeedMockData();
    }

    private void SeedMockData()
    {
        // Seed a corporate customer
        var corporateCustomerId = "CUST001";
        var corporateAccountNumber = "1234567890";

        _customers[corporateAccountNumber] = new CustomerInfo(
            CustomerId: corporateCustomerId,
            FullName: "Acme Industries Ltd",
            CustomerType: CustomerType.Corporate,
            Email: "info@acmeindustries.com",
            PhoneNumber: "+2348012345678",
            BVN: null,
            DateOfBirth: null,
            Address: "123 Industrial Avenue, Lagos"
        );

        _corporates[corporateAccountNumber] = new CorporateInfo(
            CorporateId: corporateCustomerId,
            CompanyName: "Acme Industries Ltd",
            RegistrationNumber: "RC123456",
            Industry: "Manufacturing",
            IncorporationDate: new DateTime(2010, 5, 15),
            RegisteredAddress: "123 Industrial Avenue, Lagos",
            TaxIdentificationNumber: "12345678-0001"
        );

        _accounts[corporateAccountNumber] = new AccountInfo(
            AccountNumber: corporateAccountNumber,
            AccountName: "Acme Industries Ltd",
            AccountType: "Current",
            Currency: "NGN",
            CurrentBalance: 50_000_000m,
            AvailableBalance: 48_000_000m,
            Status: "Active",
            OpenedDate: new DateTime(2010, 6, 1)
        );

        _directors[corporateCustomerId] =
        [
            new DirectorInfo(
                DirectorId: "DIR001",
                FullName: "John Adebayo",
                BVN: "22234567890",
                Email: "john.adebayo@acmeindustries.com",
                PhoneNumber: "+2348012345001",
                Address: "45 Victoria Island, Lagos",
                DateOfBirth: new DateTime(1975, 3, 20),
                Nationality: "Nigerian",
                ShareholdingPercent: 40m
            ),
            new DirectorInfo(
                DirectorId: "DIR002",
                FullName: "Amina Ibrahim",
                BVN: "22234567891",
                Email: "amina.ibrahim@acmeindustries.com",
                PhoneNumber: "+2348012345002",
                Address: "22 Ikoyi, Lagos",
                DateOfBirth: new DateTime(1980, 7, 10),
                Nationality: "Nigerian",
                ShareholdingPercent: 35m
            ),
            new DirectorInfo(
                DirectorId: "DIR003",
                FullName: "Chukwuma Okonkwo",
                BVN: "22234567892",
                Email: "chukwuma.o@acmeindustries.com",
                PhoneNumber: "+2348012345003",
                Address: "10 Lekki Phase 1, Lagos",
                DateOfBirth: new DateTime(1982, 11, 5),
                Nationality: "Nigerian",
                ShareholdingPercent: 25m
            )
        ];

        _signatories[corporateAccountNumber] =
        [
            new SignatoryInfo(
                SignatoryId: "SIG001",
                FullName: "John Adebayo",
                BVN: "22234567890",
                Email: "john.adebayo@acmeindustries.com",
                PhoneNumber: "+2348012345001",
                MandateType: "A",
                Designation: "Managing Director"
            ),
            new SignatoryInfo(
                SignatoryId: "SIG002",
                FullName: "Fatima Bello",
                BVN: "22234567893",
                Email: "fatima.bello@acmeindustries.com",
                PhoneNumber: "+2348012345004",
                MandateType: "B",
                Designation: "Finance Director"
            )
        ];

        // Seed an individual customer
        var individualAccountNumber = "0987654321";
        _customers[individualAccountNumber] = new CustomerInfo(
            CustomerId: "CUST002",
            FullName: "Oluwaseun Bakare",
            CustomerType: CustomerType.Individual,
            Email: "seun.bakare@email.com",
            PhoneNumber: "+2348098765432",
            BVN: "22212345678",
            DateOfBirth: new DateTime(1990, 8, 15),
            Address: "55 Surulere, Lagos"
        );

        _accounts[individualAccountNumber] = new AccountInfo(
            AccountNumber: individualAccountNumber,
            AccountName: "Oluwaseun Bakare",
            AccountType: "Savings",
            Currency: "NGN",
            CurrentBalance: 2_500_000m,
            AvailableBalance: 2_500_000m,
            Status: "Active",
            OpenedDate: new DateTime(2018, 3, 10)
        );
    }

    public Task<Result<CustomerInfo>> GetCustomerByAccountNumberAsync(string accountNumber, CancellationToken ct = default)
    {
        if (_customers.TryGetValue(accountNumber, out var customer))
            return Task.FromResult(Result.Success(customer));

        return Task.FromResult(Result.Failure<CustomerInfo>($"Customer with account {accountNumber} not found"));
    }

    public Task<Result<CustomerInfo>> GetCustomerByIdAsync(string customerId, CancellationToken ct = default)
    {
        var customer = _customers.Values.FirstOrDefault(c => c.CustomerId == customerId);
        if (customer != null)
            return Task.FromResult(Result.Success(customer));

        return Task.FromResult(Result.Failure<CustomerInfo>($"Customer {customerId} not found"));
    }

    public Task<Result<string>> CreateCustomerAsync(CreateCustomerRequest request, CancellationToken ct = default)
    {
        var customerId = $"CUST{Guid.NewGuid().ToString()[..8].ToUpper()}";
        var customer = new CustomerInfo(
            customerId,
            request.FullName,
            request.CustomerType,
            request.Email,
            request.PhoneNumber,
            request.BVN,
            request.DateOfBirth,
            request.Address
        );

        var accountNumber = GenerateAccountNumber();
        _customers[accountNumber] = customer;

        return Task.FromResult(Result.Success(customerId));
    }

    public Task<Result<CorporateInfo>> GetCorporateInfoAsync(string accountNumber, CancellationToken ct = default)
    {
        if (_corporates.TryGetValue(accountNumber, out var corporate))
            return Task.FromResult(Result.Success(corporate));

        return Task.FromResult(Result.Failure<CorporateInfo>($"Corporate info for account {accountNumber} not found"));
    }

    public Task<Result<IReadOnlyList<DirectorInfo>>> GetDirectorsAsync(string corporateId, CancellationToken ct = default)
    {
        if (_directors.TryGetValue(corporateId, out var directors))
            return Task.FromResult(Result.Success<IReadOnlyList<DirectorInfo>>(directors));

        return Task.FromResult(Result.Success<IReadOnlyList<DirectorInfo>>([]));
    }

    public Task<Result<IReadOnlyList<SignatoryInfo>>> GetSignatoriesAsync(string accountNumber, CancellationToken ct = default)
    {
        if (_signatories.TryGetValue(accountNumber, out var signatories))
            return Task.FromResult(Result.Success<IReadOnlyList<SignatoryInfo>>(signatories));

        return Task.FromResult(Result.Success<IReadOnlyList<SignatoryInfo>>([]));
    }

    public Task<Result<AccountInfo>> GetAccountInfoAsync(string accountNumber, CancellationToken ct = default)
    {
        if (_accounts.TryGetValue(accountNumber, out var account))
            return Task.FromResult(Result.Success(account));

        return Task.FromResult(Result.Failure<AccountInfo>($"Account {accountNumber} not found"));
    }

    public Task<Result<AccountStatement>> GetStatementAsync(string accountNumber, DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        if (!_accounts.TryGetValue(accountNumber, out var account))
            return Task.FromResult(Result.Failure<AccountStatement>($"Account {accountNumber} not found"));

        var transactions = GenerateMockTransactions(fromDate, toDate, account.CurrentBalance);
        var totalCredits = transactions.Where(t => t.Type == TransactionType.Credit).Sum(t => t.Amount);
        var totalDebits = transactions.Where(t => t.Type == TransactionType.Debit).Sum(t => t.Amount);

        var statement = new AccountStatement(
            AccountNumber: accountNumber,
            FromDate: fromDate,
            ToDate: toDate,
            OpeningBalance: account.CurrentBalance - totalCredits + totalDebits,
            ClosingBalance: account.CurrentBalance,
            TotalCredits: totalCredits,
            TotalDebits: totalDebits,
            Transactions: transactions
        );

        return Task.FromResult(Result.Success(statement));
    }

    public Task<Result<decimal>> GetAccountBalanceAsync(string accountNumber, CancellationToken ct = default)
    {
        if (_accounts.TryGetValue(accountNumber, out var account))
            return Task.FromResult(Result.Success(account.AvailableBalance));

        return Task.FromResult(Result.Failure<decimal>($"Account {accountNumber} not found"));
    }

    public Task<Result<string>> CreateLoanAsync(CreateLoanRequest request, CancellationToken ct = default)
    {
        var loanId = $"LN{Guid.NewGuid().ToString()[..8].ToUpper()}";

        var loan = new LoanInfo(
            LoanId: loanId,
            CustomerId: request.CustomerId,
            AccountNumber: request.AccountNumber,
            PrincipalAmount: request.PrincipalAmount,
            OutstandingBalance: request.PrincipalAmount,
            InterestRate: request.InterestRatePerAnnum,
            TenorMonths: request.TenorMonths,
            DisbursementDate: null,
            MaturityDate: null,
            Status: LoanStatus.PendingApproval
        );

        _loans[loanId] = loan;
        return Task.FromResult(Result.Success(loanId));
    }

    public Task<Result> ApproveLoanAsync(string loanId, CancellationToken ct = default)
    {
        if (!_loans.TryGetValue(loanId, out var loan))
            return Task.FromResult(Result.Failure($"Loan {loanId} not found"));

        if (loan.Status != LoanStatus.PendingApproval)
            return Task.FromResult(Result.Failure($"Loan {loanId} cannot be approved in status {loan.Status}"));

        _loans[loanId] = loan with { Status = LoanStatus.Approved };
        return Task.FromResult(Result.Success());
    }

    public Task<Result> DisburseLoanAsync(DisbursementRequest request, CancellationToken ct = default)
    {
        if (!_loans.TryGetValue(request.LoanId, out var loan))
            return Task.FromResult(Result.Failure($"Loan {request.LoanId} not found"));

        if (loan.Status != LoanStatus.Approved)
            return Task.FromResult(Result.Failure($"Loan {request.LoanId} must be approved before disbursement"));

        var maturityDate = request.DisbursementDate.AddMonths(loan.TenorMonths);
        _loans[request.LoanId] = loan with
        {
            Status = LoanStatus.Active,
            DisbursementDate = request.DisbursementDate,
            MaturityDate = maturityDate
        };

        return Task.FromResult(Result.Success());
    }

    public Task<Result<LoanInfo>> GetLoanInfoAsync(string loanId, CancellationToken ct = default)
    {
        if (_loans.TryGetValue(loanId, out var loan))
            return Task.FromResult(Result.Success(loan));

        return Task.FromResult(Result.Failure<LoanInfo>($"Loan {loanId} not found"));
    }

    public Task<Result<RepaymentSchedule>> GetRepaymentScheduleAsync(string loanId, CancellationToken ct = default)
    {
        if (!_loans.TryGetValue(loanId, out var loan))
            return Task.FromResult(Result.Failure<RepaymentSchedule>($"Loan {loanId} not found"));

        if (!loan.DisbursementDate.HasValue)
            return Task.FromResult(Result.Failure<RepaymentSchedule>($"Loan {loanId} has not been disbursed"));

        var installments = GenerateRepaymentSchedule(loan);
        return Task.FromResult(Result.Success(new RepaymentSchedule(loanId, installments)));
    }

    public Task<Result<LoanStatus>> GetLoanStatusAsync(string loanId, CancellationToken ct = default)
    {
        if (_loans.TryGetValue(loanId, out var loan))
            return Task.FromResult(Result.Success(loan.Status));

        return Task.FromResult(Result.Failure<LoanStatus>($"Loan {loanId} not found"));
    }

    private static string GenerateAccountNumber()
    {
        var random = new Random();
        return string.Concat(Enumerable.Range(0, 10).Select(_ => random.Next(0, 10).ToString()));
    }

    private static List<StatementTransaction> GenerateMockTransactions(DateTime fromDate, DateTime toDate, decimal currentBalance)
    {
        var transactions = new List<StatementTransaction>();
        var random = new Random(42);
        var runningBalance = currentBalance;
        var currentDate = toDate;

        var descriptions = new[]
        {
            ("Salary Credit", TransactionType.Credit),
            ("Transfer from Client", TransactionType.Credit),
            ("Sales Revenue", TransactionType.Credit),
            ("Vendor Payment", TransactionType.Debit),
            ("Utility Bill", TransactionType.Debit),
            ("Salary Payment", TransactionType.Debit),
            ("Equipment Purchase", TransactionType.Debit),
            ("Loan Repayment", TransactionType.Debit)
        };

        for (int i = 0; i < 30 && currentDate >= fromDate; i++)
        {
            var (description, type) = descriptions[random.Next(descriptions.Length)];
            var amount = Math.Round((decimal)(random.NextDouble() * 5_000_000 + 100_000), 2);

            if (type == TransactionType.Debit)
                runningBalance += amount;
            else
                runningBalance -= amount;

            transactions.Add(new StatementTransaction(
                TransactionId: $"TXN{Guid.NewGuid().ToString()[..8].ToUpper()}",
                Date: currentDate,
                Description: description,
                Amount: amount,
                Type: type,
                RunningBalance: runningBalance,
                Reference: $"REF{random.Next(100000, 999999)}"
            ));

            currentDate = currentDate.AddDays(-random.Next(1, 5));
        }

        transactions.Reverse();
        return transactions;
    }

    private static List<RepaymentInstallment> GenerateRepaymentSchedule(LoanInfo loan)
    {
        var installments = new List<RepaymentInstallment>();
        var monthlyRate = loan.InterestRate / 100 / 12;
        var monthlyPayment = loan.PrincipalAmount *
            (monthlyRate * (decimal)Math.Pow((double)(1 + monthlyRate), loan.TenorMonths)) /
            ((decimal)Math.Pow((double)(1 + monthlyRate), loan.TenorMonths) - 1);

        var outstanding = loan.PrincipalAmount;
        var dueDate = loan.DisbursementDate!.Value.AddMonths(1);

        for (int i = 1; i <= loan.TenorMonths; i++)
        {
            var interestAmount = Math.Round(outstanding * monthlyRate, 2);
            var principalAmount = Math.Round(monthlyPayment - interestAmount, 2);

            if (i == loan.TenorMonths)
                principalAmount = outstanding;

            outstanding -= principalAmount;

            installments.Add(new RepaymentInstallment(
                InstallmentNumber: i,
                DueDate: dueDate,
                PrincipalAmount: principalAmount,
                InterestAmount: interestAmount,
                TotalAmount: principalAmount + interestAmount,
                OutstandingAfter: Math.Max(0, outstanding),
                Status: InstallmentStatus.Pending
            ));

            dueDate = dueDate.AddMonths(1);
        }

        return installments;
    }
}

using CRMS.Domain.Interfaces;
using CRMS.Infrastructure.ExternalServices.CoreBanking;
using CRMS.Infrastructure.ExternalServices.FineractDirect;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace CRMS.Infrastructure.Tests;

/// <summary>
/// Live integration tests for the Core Banking API.
/// These tests call the REAL CBS API endpoints.
/// 
/// Configuration (in order of priority):
///   1. Environment variables (CBS_BASE_URL, CBS_CLIENT_ID, etc.)
///   2. appsettings.test.json file in the test project
/// 
/// Required settings:
///   - CoreBanking:BaseUrl - Base URL of the core banking API
///   - CoreBanking:ClientId - OAuth2 client ID
///   - CoreBanking:ClientSecret - OAuth2 client secret
///   - CoreBanking:TestNuban - A valid NUBAN for testing (corporate account)
/// 
/// Tests are skipped if BaseUrl is not configured.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "LiveAPI")]
public class CoreBankingServiceLiveIntegrationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly CoreBankingSettings _settings;
    private readonly string _testNuban;
    private readonly string _testIndividualNuban;
    private ICoreBankingService? _service;

    // Fineract Direct
    private readonly FineractDirectSettings _fineractSettings;
    private readonly long _fineractTestClientId;
    private readonly int _fineractTestProductId;
    private readonly decimal _fineractTestPrincipal;
    private readonly int _fineractTestTenorMonths;
    private readonly decimal _fineractTestInterestRatePerAnnum;
    private readonly string _fineractTestRepaymentAccount;
    private readonly long _fineractTestLoanId;
    private IFineractDirectService? _fineractService;

    public CoreBankingServiceLiveIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        
        // Build configuration from appsettings.test.json + environment variables
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.test.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables() // Environment variables override JSON settings
            .Build();

        var cbsSection = configuration.GetSection("CoreBanking");
        
        _settings = new CoreBankingSettings
        {
            BaseUrl = cbsSection["BaseUrl"] ?? "",
            ClientId = cbsSection["ClientId"] ?? "",
            ClientSecret = cbsSection["ClientSecret"] ?? "",
            TokenEndpoint = cbsSection["TokenEndpoint"] ?? "/oauth/token",
            TimeoutSeconds = int.TryParse(cbsSection["TimeoutSeconds"], out var timeout) ? timeout : 30,
            UseMock = false
        };
        
        _testNuban = cbsSection["TestNuban"] ?? "";
        _testIndividualNuban = cbsSection["TestIndividualNuban"] ?? "";

        // Fineract Direct settings
        var fineractSection = configuration.GetSection("FineractDirect");
        _fineractSettings = new FineractDirectSettings
        {
            BaseUrl        = fineractSection["BaseUrl"] ?? "",
            Username       = fineractSection["Username"] ?? "",
            Password       = fineractSection["Password"] ?? "",
            TenantId       = fineractSection["TenantId"] ?? "bankofagriculture",
            TimeoutSeconds = int.TryParse(fineractSection["TimeoutSeconds"], out var fts) ? fts : 60,
            UseMock        = false
        };

        long.TryParse(fineractSection["TestClientId"], out _fineractTestClientId);
        int.TryParse(fineractSection["TestProductId"], out _fineractTestProductId);
        decimal.TryParse(fineractSection["TestPrincipal"], out _fineractTestPrincipal);
        int.TryParse(fineractSection["TestTenorMonths"], out _fineractTestTenorMonths);
        decimal.TryParse(fineractSection["TestInterestRatePerAnnum"], out _fineractTestInterestRatePerAnnum);
        _fineractTestRepaymentAccount = fineractSection["TestRepaymentAccountNumber"] ?? "";
        long.TryParse(fineractSection["TestLoanId"], out _fineractTestLoanId);
    }

    public Task InitializeAsync()
    {
        // Initialise CoreBankingService if configured
        if (!string.IsNullOrEmpty(_settings.BaseUrl))
        {
            var authHandler = new CoreBankingAuthHandler(
                Options.Create(_settings),
                new Mock<ILogger<CoreBankingAuthHandler>>().Object)
            {
                InnerHandler = new HttpClientHandler()
            };

            var httpClient = new HttpClient(authHandler)
            {
                BaseAddress = new Uri(_settings.BaseUrl),
                Timeout     = TimeSpan.FromSeconds(_settings.TimeoutSeconds)
            };

            _service = new CoreBankingService(
                httpClient,
                Options.Create(_settings),
                new Mock<ILogger<CoreBankingService>>().Object);

            _output.WriteLine($"Initialized CoreBankingService with BaseUrl: {_settings.BaseUrl}");
        }
        else
        {
            _output.WriteLine("CBS_BASE_URL not configured - CoreBanking live tests will be skipped");
        }

        // Initialise FineractDirectService if configured (independent of CoreBanking)
        if (!string.IsNullOrEmpty(_fineractSettings.BaseUrl))
        {
            var fineractAuthHandler = new FineractDirectAuthHandler(
                Options.Create(_fineractSettings),
                new Mock<ILogger<FineractDirectAuthHandler>>().Object)
            {
                InnerHandler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (_, _, _, errors) =>
                        errors == System.Net.Security.SslPolicyErrors.None ||
                        errors == System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch
                }
            };

            var baseUrl = _fineractSettings.BaseUrl.TrimEnd('/') + "/";
            var fineractHttpClient = new HttpClient(fineractAuthHandler)
            {
                BaseAddress = new Uri(baseUrl),
                Timeout     = TimeSpan.FromSeconds(_fineractSettings.TimeoutSeconds)
            };

            _fineractService = new FineractDirectService(
                fineractHttpClient,
                new Mock<ILogger<FineractDirectService>>().Object);

            _output.WriteLine($"Initialized FineractDirectService with BaseUrl: {_fineractSettings.BaseUrl}");
        }
        else
        {
            _output.WriteLine("FineractDirect:BaseUrl not configured - Fineract live tests will be skipped");
        }

        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private void SkipIfNotConfigured()
    {
        Skip.If(string.IsNullOrEmpty(_settings.BaseUrl), "CBS_BASE_URL not configured");
        Skip.If(string.IsNullOrEmpty(_testNuban), "CBS_TEST_NUBAN not configured");
    }

    #region Customer Operations

    [SkippableFact]
    public async Task GetCustomerByAccountNumberAsync_WithValidAccount_ReturnsCustomerInfo()
    {
        SkipIfNotConfigured();

        var result = await _service!.GetCustomerByAccountNumberAsync(_testNuban);

        _output.WriteLine($"Result: IsSuccess={result.IsSuccess}, Error={result.Error}");
        if (result.IsSuccess)
        {
            _output.WriteLine($"Customer: {result.Value?.FullName}, Type: {result.Value?.CustomerType}");
        }

        Assert.True(result.IsSuccess, $"Expected success but got: {result.Error}");
        Assert.NotNull(result.Value);
        Assert.NotEmpty(result.Value.FullName);
        Assert.NotEmpty(result.Value.CustomerId);
    }

    [SkippableFact]
    public async Task GetCustomerByAccountNumberAsync_WithInvalidAccount_ReturnsFailure()
    {
        SkipIfNotConfigured();

        var result = await _service!.GetCustomerByAccountNumberAsync("0000000000");

        _output.WriteLine($"Result: IsSuccess={result.IsSuccess}, Error={result.Error}");

        Assert.False(result.IsSuccess);
    }

    #endregion

    #region Corporate Operations

    [SkippableFact]
    public async Task GetCorporateInfoAsync_WithValidAccount_ReturnsCorporateInfo()
    {
        SkipIfNotConfigured();

        var result = await _service!.GetCorporateInfoAsync(_testNuban);

        _output.WriteLine($"Result: IsSuccess={result.IsSuccess}, Error={result.Error}");
        if (result.IsSuccess)
        {
            _output.WriteLine($"Company: {result.Value?.CompanyName}, RC: {result.Value?.RegistrationNumber}");
        }

        Assert.True(result.IsSuccess, $"Expected success but got: {result.Error}");
        Assert.NotNull(result.Value);
        Assert.NotEmpty(result.Value.CompanyName);
    }

    [SkippableFact]
    public async Task GetSignatoriesAsync_WithValidAccount_ReturnsSignatoryList()
    {
        SkipIfNotConfigured();

        var result = await _service!.GetSignatoriesAsync(_testNuban);

        _output.WriteLine($"Result: IsSuccess={result.IsSuccess}, Error={result.Error}");
        if (result.IsSuccess)
        {
            _output.WriteLine($"Signatories count: {result.Value?.Count}");
            foreach (var sig in result.Value ?? [])
            {
                _output.WriteLine($"  - {sig.FullName} (BVN: {sig.BVN?[..4]}***)");
            }
        }

        Assert.True(result.IsSuccess, $"Expected success but got: {result.Error}");
        Assert.NotNull(result.Value);
    }

    [SkippableFact]
    public async Task GetDirectorsAsync_AfterCustomerLookup_ReturnsDirectors()
    {
        SkipIfNotConfigured();

        // First get customer to populate cache
        var customerResult = await _service!.GetCustomerByAccountNumberAsync(_testNuban);
        Assert.True(customerResult.IsSuccess);

        var result = await _service.GetDirectorsAsync(customerResult.Value!.CustomerId);

        _output.WriteLine($"Result: IsSuccess={result.IsSuccess}, Error={result.Error}");
        if (result.IsSuccess)
        {
            _output.WriteLine($"Directors count: {result.Value?.Count}");
            foreach (var dir in result.Value ?? [])
            {
                _output.WriteLine($"  - {dir.FullName} (BVN: {dir.BVN?[..4] ?? "N/A"}***)");
            }
        }

        Assert.True(result.IsSuccess, $"Expected success but got: {result.Error}");
        Assert.NotNull(result.Value);
    }

    #endregion

    #region Account Operations

    [SkippableFact]
    public async Task GetAccountInfoAsync_WithValidAccount_ReturnsAccountInfo()
    {
        SkipIfNotConfigured();

        var result = await _service!.GetAccountInfoAsync(_testNuban);

        _output.WriteLine($"Result: IsSuccess={result.IsSuccess}, Error={result.Error}");
        if (result.IsSuccess)
        {
            _output.WriteLine($"Account: {result.Value?.AccountNumber}, Name: {result.Value?.AccountName}");
            _output.WriteLine($"Type: {result.Value?.AccountType}, Status: {result.Value?.Status}");
        }

        Assert.True(result.IsSuccess, $"Expected success but got: {result.Error}");
        Assert.NotNull(result.Value);
        Assert.Equal(_testNuban, result.Value.AccountNumber);
    }

    [SkippableFact]
    public async Task GetStatementAsync_WithValidAccount_ReturnsStatementWithTransactions()
    {
        SkipIfNotConfigured();

        var fromDate = DateTime.UtcNow.AddMonths(-3);
        var toDate = DateTime.UtcNow;

        var result = await _service!.GetStatementAsync(_testNuban, fromDate, toDate);

        _output.WriteLine($"Result: IsSuccess={result.IsSuccess}, Error={result.Error}");
        if (result.IsSuccess)
        {
            _output.WriteLine($"Statement: {result.Value?.AccountNumber}");
            _output.WriteLine($"Period: {result.Value?.FromDate:yyyy-MM-dd} to {result.Value?.ToDate:yyyy-MM-dd}");
            _output.WriteLine($"Transactions: {result.Value?.Transactions.Count}");
            _output.WriteLine($"Total Credits: {result.Value?.TotalCredits:N2}");
            _output.WriteLine($"Total Debits: {result.Value?.TotalDebits:N2}");
        }

        Assert.True(result.IsSuccess, $"Expected success but got: {result.Error}");
        Assert.NotNull(result.Value);
        Assert.Equal(_testNuban, result.Value.AccountNumber);
    }

    [SkippableFact]
    public async Task GetStatementAsync_TransactionsHaveCorrectStructure()
    {
        SkipIfNotConfigured();

        var fromDate = DateTime.UtcNow.AddMonths(-1);
        var toDate = DateTime.UtcNow;

        var result = await _service!.GetStatementAsync(_testNuban, fromDate, toDate);

        Assert.True(result.IsSuccess, $"Expected success but got: {result.Error}");
        
        if (result.Value!.Transactions.Any())
        {
            var tx = result.Value.Transactions.First();
            _output.WriteLine($"Sample transaction: ID={tx.TransactionId}, Date={tx.Date}, Amount={tx.Amount:N2}, Type={tx.Type}");
            
            Assert.NotNull(tx.TransactionId);
            Assert.True(tx.Amount > 0);
            Assert.True(tx.Type == TransactionType.Credit || tx.Type == TransactionType.Debit);
        }
        else
        {
            _output.WriteLine("No transactions found in the period");
        }
    }

    #endregion

    #region Error Handling

    [SkippableFact]
    public async Task GetCustomerByIdAsync_ReturnsNotSupported()
    {
        SkipIfNotConfigured();

        var result = await _service!.GetCustomerByIdAsync("12345");

        Assert.False(result.IsSuccess);
        Assert.Contains("not supported", result.Error ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task GetAccountBalanceAsync_ReturnsNotSupported()
    {
        SkipIfNotConfigured();

        var result = await _service!.GetAccountBalanceAsync(_testNuban);

        Assert.False(result.IsSuccess);
        Assert.Contains("not supported", result.Error ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task CreateLoanAsync_ReturnsManualProcess()
    {
        SkipIfNotConfigured();

        var request = new CreateLoanRequest(
            CustomerId: "1643",
            AccountNumber: _testNuban,
            ProductCode: "CORP001",
            PrincipalAmount: 10000000m,
            TenorMonths: 12,
            InterestRatePerAnnum: 15m,
            ExpectedDisbursementDate: DateTime.UtcNow.AddDays(7),
            RepaymentFrequency: "Monthly",
            IdempotencyKey: Guid.NewGuid().ToString()
        );

        var result = await _service!.CreateLoanAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Contains("manually", result.Error ?? "", StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Fineract Loan Booking

    private void SkipIfFineractNotConfigured()
    {
        Skip.If(string.IsNullOrEmpty(_fineractSettings.BaseUrl),   "FineractDirect:BaseUrl not configured");
        Skip.If(string.IsNullOrEmpty(_fineractSettings.Username),  "FineractDirect:Username not configured");
        Skip.If(_fineractTestClientId <= 0,                        "FineractDirect:TestClientId not configured");
        Skip.If(_fineractTestProductId <= 0,                       "FineractDirect:TestProductId not configured");
        Skip.If(_fineractTestPrincipal <= 0,                       "FineractDirect:TestPrincipal not configured");
        Skip.If(_fineractTestTenorMonths <= 0,                     "FineractDirect:TestTenorMonths not configured");
        Skip.If(string.IsNullOrEmpty(_fineractTestRepaymentAccount),"FineractDirect:TestRepaymentAccountNumber not configured");
    }

    /// <summary>
    /// Live end-to-end test for BookApprovedLoanAsync.
    /// Creates, approves, and disburses a loan in the configured Fineract environment.
    ///
    /// ⚠️  THIS TEST WRITES DATA TO FINERACT. Run only against a sandbox/test tenant.
    ///
    /// Required in appsettings.test.json (or environment variables):
    ///   FineractDirect:BaseUrl                  — e.g. https://tpapi.bankofagriculture.com/core_banking/api/v1
    ///   FineractDirect:Username                 — Fineract username
    ///   FineractDirect:Password                 — Fineract password
    ///   FineractDirect:TenantId                 — e.g. bankofagriculture
    ///   FineractDirect:TestClientId             — existing Fineract client ID (long)
    ///   FineractDirect:TestProductId            — NAMP loan product ID (int)
    ///   FineractDirect:TestPrincipal            — loan amount, e.g. 500000
    ///   FineractDirect:TestTenorMonths          — e.g. 12
    ///   FineractDirect:TestInterestRatePerAnnum — e.g. 9
    ///   FineractDirect:TestRepaymentAccountNumber — BOA savings account number
    /// </summary>
    [SkippableFact]
    public async Task BookApprovedLoanAsync_WithValidRequest_BooksApprovesAndDisburses()
    {
        SkipIfFineractNotConfigured();

        var valueDate = DateTime.UtcNow.Date;
        var request = new FineractLoanBookingRequest(
            ClientId:                _fineractTestClientId,
            ProductId:               _fineractTestProductId,
            Principal:               _fineractTestPrincipal,
            TenorMonths:             _fineractTestTenorMonths,
            InterestRatePerAnnum:    _fineractTestInterestRatePerAnnum > 0 ? _fineractTestInterestRatePerAnnum : 9m,
            ValueDate:               valueDate,
            RepaymentAccountNumber:  _fineractTestRepaymentAccount,
            DisburseToSavings:       false
        );

        _output.WriteLine("=== Fineract Loan Booking ===");
        _output.WriteLine($"ClientId:            {request.ClientId}");
        _output.WriteLine($"ProductId:           {request.ProductId}");
        _output.WriteLine($"Principal:           {request.Principal:N2}");
        _output.WriteLine($"TenorMonths:         {request.TenorMonths}");
        _output.WriteLine($"InterestRate (p.a.): {request.InterestRatePerAnnum:N2}%");
        _output.WriteLine($"ValueDate:           {request.ValueDate:yyyy-MM-dd}");
        _output.WriteLine($"RepaymentAccount:    {request.RepaymentAccountNumber}");

        var result = await _fineractService!.BookApprovedLoanAsync(request);

        _output.WriteLine($"\nResult: IsSuccess={result.IsSuccess}, Error={result.Error}");

        if (result.IsSuccess)
        {
            _output.WriteLine($"LoanId:            {result.Value.LoanId}");
            _output.WriteLine($"LoanAccountNumber: {result.Value.LoanAccountNumber}");
            _output.WriteLine($"Booked:            {result.Value.Booked}");
            _output.WriteLine($"Approved:          {result.Value.Approved}");
            _output.WriteLine($"Disbursed:         {result.Value.Disbursed}");
            _output.WriteLine($"Status:            {result.Value.Status}");
        }

        Assert.True(result.IsSuccess, $"BookApprovedLoanAsync failed: {result.Error}");
        Assert.True(result.Value.LoanId > 0, "Expected a positive LoanId");
        Assert.False(string.IsNullOrWhiteSpace(result.Value.LoanAccountNumber), "Expected a non-empty LoanAccountNumber");
        Assert.True(result.Value.Booked,    "Expected Booked = true");
        Assert.True(result.Value.Approved,  "Expected Approved = true");
        Assert.True(result.Value.Disbursed, "Expected Disbursed = true");
    }

    /// <summary>
    /// Verifies that the product catalogue is reachable and the configured test product exists.
    /// A safe read-only check to run before attempting the destructive booking test.
    /// </summary>
    [SkippableFact]
    public async Task GetLoanProductsAsync_ConfiguredProductExists()
    {
        Skip.If(string.IsNullOrEmpty(_fineractSettings.BaseUrl), "FineractDirect:BaseUrl not configured");
        Skip.If(_fineractTestProductId <= 0, "FineractDirect:TestProductId not configured");

        var result = await _fineractService!.GetLoanProductsAsync(activeOnly: false);

        _output.WriteLine($"Result: IsSuccess={result.IsSuccess}, Error={result.Error}");

        Assert.True(result.IsSuccess, $"GetLoanProductsAsync failed: {result.Error}");
        Assert.NotNull(result.Value);
        Assert.NotEmpty(result.Value);

        _output.WriteLine($"Total products returned: {result.Value.Count}");
        foreach (var p in result.Value)
            _output.WriteLine($"  [{p.Id}] {p.Name} — {p.AnnualInterestRate:N2}% p.a., active={p.IsActive}");

        var testProduct = result.Value.FirstOrDefault(p => p.Id == _fineractTestProductId);
        Assert.NotNull(testProduct);
        _output.WriteLine($"\nTest product found: [{testProduct!.Id}] {testProduct.Name} — {testProduct.AnnualInterestRate:N2}% p.a.");
    }

    /// <summary>
    /// Fetches full loan detail (repayment schedule + summary) for an existing Fineract loan.
    /// Read-only — does not modify any data.
    ///
    /// Required in appsettings.test.json (or environment variables):
    ///   FineractDirect:BaseUrl     — e.g. https://tpapi.bankofagriculture.com/core_banking/api/v1
    ///   FineractDirect:Username    — Fineract username
    ///   FineractDirect:Password    — Fineract password
    ///   FineractDirect:TenantId    — e.g. bankofagriculture
    ///   FineractDirect:TestLoanId  — ID of an existing loan (long), e.g. 42
    /// </summary>
    [SkippableFact]
    public async Task GetLoanDetailAsync_WithValidLoanId_ReturnsDetailWithSchedule()
    {
        Skip.If(string.IsNullOrEmpty(_fineractSettings.BaseUrl), "FineractDirect:BaseUrl not configured");
        Skip.If(string.IsNullOrEmpty(_fineractSettings.Username), "FineractDirect:Username not configured");
        Skip.If(_fineractTestLoanId <= 0, "FineractDirect:TestLoanId not configured");

        _output.WriteLine($"=== Get Loan Detail (LoanId: {_fineractTestLoanId}) ===");

        var result = await _fineractService!.GetLoanDetailAsync(_fineractTestLoanId);

        _output.WriteLine($"Result: IsSuccess={result.IsSuccess}, Error={result.Error}");

        Assert.True(result.IsSuccess, $"GetLoanDetailAsync failed: {result.Error}");
        Assert.NotNull(result.Value);

        var loan = result.Value;

        _output.WriteLine($"\n--- Loan Header ---");
        _output.WriteLine($"  Account No:        {loan.AccountNo}");
        _output.WriteLine($"  Product:           {loan.ProductName}");
        _output.WriteLine($"  Status:            {loan.Status} (code={loan.StatusCode})");
        _output.WriteLine($"  Principal:         ₦{loan.Principal:N2}");
        _output.WriteLine($"  Approved Principal:₦{loan.ApprovedPrincipal:N2}");
        _output.WriteLine($"  Interest Rate:     {loan.InterestRate:N2}% p.a.");
        _output.WriteLine($"  Repayments:        {loan.NumberOfRepayments}");
        _output.WriteLine($"  Disbursement Date: {loan.DisbursementDate:yyyy-MM-dd}");
        _output.WriteLine($"  Maturity Date:     {loan.MaturityDate:yyyy-MM-dd}");

        _output.WriteLine($"\n--- Summary ---");
        _output.WriteLine($"  Total Expected:    ₦{loan.Summary.TotalExpectedRepayment:N2}");
        _output.WriteLine($"  Total Repaid:      ₦{loan.Summary.TotalRepayment:N2}");
        _output.WriteLine($"  Total Outstanding: ₦{loan.Summary.TotalOutstanding:N2}");
        _output.WriteLine($"  Principal Paid:    ₦{loan.Summary.PrincipalPaid:N2}");
        _output.WriteLine($"  Principal Outstd:  ₦{loan.Summary.PrincipalOutstanding:N2}");
        _output.WriteLine($"  Interest Charged:  ₦{loan.Summary.InterestCharged:N2}");
        _output.WriteLine($"  Interest Paid:     ₦{loan.Summary.InterestPaid:N2}");
        _output.WriteLine($"  Interest Outstd:   ₦{loan.Summary.InterestOutstanding:N2}");
        if (loan.Summary.PenaltyChargesOutstanding > 0)
            _output.WriteLine($"  ⚠ Penalty Outstd: ₦{loan.Summary.PenaltyChargesOutstanding:N2}");

        _output.WriteLine($"\n--- Repayment Schedule ({loan.RepaymentSchedule.Count} rows) ---");
        foreach (var p in loan.RepaymentSchedule)
        {
            if (p.Period == 0) continue; // disbursement row, not a repayment
            var flag = p.Complete ? "✓" : p.DueDate < DateTime.UtcNow ? "⚠" : " ";
            _output.WriteLine(
                $"  [{flag}] #{p.Period:D2} {p.DueDate:yyyy-MM-dd}  " +
                $"Due=₦{p.TotalDue:N0}  Paid=₦{p.TotalPaid:N0}  Outstd=₦{p.TotalOutstanding:N0}");
        }

        // Structural assertions
        Assert.False(string.IsNullOrWhiteSpace(loan.AccountNo), "AccountNo should not be empty");
        Assert.False(string.IsNullOrWhiteSpace(loan.ProductName), "ProductName should not be empty");
        Assert.False(string.IsNullOrWhiteSpace(loan.Status), "Status should not be empty");
        Assert.True(loan.Principal > 0, "Principal should be positive");
        Assert.True(loan.NumberOfRepayments > 0, "NumberOfRepayments should be positive");

        // Summary sanity checks
        Assert.True(loan.Summary.PrincipalDisbursed >= 0);
        Assert.True(loan.Summary.TotalOutstanding >= 0);
        Assert.True(loan.Summary.TotalOutstanding <= loan.Summary.TotalExpectedRepayment,
            "Outstanding cannot exceed total expected repayment");

        // Schedule
        Assert.NotEmpty(loan.RepaymentSchedule);
        var repaymentRows = loan.RepaymentSchedule.Where(p => p.Period > 0).ToList();
        Assert.NotEmpty(repaymentRows);
        Assert.All(repaymentRows, p => Assert.True(p.TotalDue >= 0));
        Assert.All(repaymentRows, p => Assert.True(p.TotalPaid >= 0));
        Assert.All(repaymentRows, p => Assert.True(p.TotalOutstanding >= 0));
    }

    #endregion

    #region End-to-End Flow

    [SkippableFact]
    public async Task FullCorporateLookupFlow_ReturnsCompleteData()
    {
        SkipIfNotConfigured();

        _output.WriteLine("=== Full Corporate Lookup Flow ===");

        // Step 1: Get customer info
        _output.WriteLine("\n1. Getting customer info...");
        var customerResult = await _service!.GetCustomerByAccountNumberAsync(_testNuban);
        Assert.True(customerResult.IsSuccess, $"Customer lookup failed: {customerResult.Error}");
        _output.WriteLine($"   Customer: {customerResult.Value?.FullName} ({customerResult.Value?.CustomerType})");

        // Step 2: Get corporate info
        _output.WriteLine("\n2. Getting corporate info...");
        var corpResult = await _service.GetCorporateInfoAsync(_testNuban);
        Assert.True(corpResult.IsSuccess, $"Corporate lookup failed: {corpResult.Error}");
        _output.WriteLine($"   Company: {corpResult.Value?.CompanyName}");
        _output.WriteLine($"   RC Number: {corpResult.Value?.RegistrationNumber}");

        // Step 3: Get signatories
        _output.WriteLine("\n3. Getting signatories...");
        var sigResult = await _service.GetSignatoriesAsync(_testNuban);
        Assert.True(sigResult.IsSuccess, $"Signatories lookup failed: {sigResult.Error}");
        _output.WriteLine($"   Signatories: {sigResult.Value?.Count}");

        // Step 4: Get directors
        _output.WriteLine("\n4. Getting directors...");
        var dirResult = await _service.GetDirectorsAsync(customerResult.Value!.CustomerId);
        Assert.True(dirResult.IsSuccess, $"Directors lookup failed: {dirResult.Error}");
        _output.WriteLine($"   Directors: {dirResult.Value?.Count}");

        // Step 5: Get statement
        _output.WriteLine("\n5. Getting 3-month statement...");
        var stmtResult = await _service.GetStatementAsync(_testNuban, DateTime.UtcNow.AddMonths(-3), DateTime.UtcNow);
        Assert.True(stmtResult.IsSuccess, $"Statement lookup failed: {stmtResult.Error}");
        _output.WriteLine($"   Transactions: {stmtResult.Value?.Transactions.Count}");
        _output.WriteLine($"   Credits: {stmtResult.Value?.TotalCredits:N2}");
        _output.WriteLine($"   Debits: {stmtResult.Value?.TotalDebits:N2}");

        _output.WriteLine("\n=== Flow Complete ===");
    }

    #endregion
}

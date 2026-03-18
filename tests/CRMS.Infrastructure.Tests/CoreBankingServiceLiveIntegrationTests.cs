using CRMS.Domain.Interfaces;
using CRMS.Infrastructure.ExternalServices.CoreBanking;
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
    }

    public Task InitializeAsync()
    {
        if (string.IsNullOrEmpty(_settings.BaseUrl))
        {
            _output.WriteLine("CBS_BASE_URL not configured - live tests will be skipped");
            return Task.CompletedTask;
        }

        var loggerMock = new Mock<ILogger<CoreBankingService>>();
        var authHandlerLogger = new Mock<ILogger<CoreBankingAuthHandler>>();
        
        var authHandler = new CoreBankingAuthHandler(
            Options.Create(_settings),
            authHandlerLogger.Object)
        {
            InnerHandler = new HttpClientHandler()
        };
        
        var httpClient = new HttpClient(authHandler)
        {
            BaseAddress = new Uri(_settings.BaseUrl),
            Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds)
        };
        
        _service = new CoreBankingService(
            httpClient,
            Options.Create(_settings),
            loggerMock.Object);
            
        _output.WriteLine($"Initialized CoreBankingService with BaseUrl: {_settings.BaseUrl}");
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

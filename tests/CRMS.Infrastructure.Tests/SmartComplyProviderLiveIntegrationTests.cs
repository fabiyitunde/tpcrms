using CRMS.Domain.Interfaces;
using CRMS.Infrastructure.ExternalServices.SmartComply;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace CRMS.Infrastructure.Tests;

/// <summary>
/// Live integration tests for the SmartComply/Adhere API.
/// These tests call the REAL SmartComply API endpoints.
/// 
/// Configuration (in order of priority):
///   1. Environment variables (SmartComply__ApiKey, SmartComply__BaseUrl, etc.)
///   2. appsettings.test.json file in the test project
/// 
/// Required settings:
///   - SmartComply:ApiKey - API key for SmartComply
///   - SmartComply:BaseUrl - Base URL (default: https://adhere-api.smartcomply.com)
///   - SmartComply:TestBvn - A valid BVN for testing
///   - SmartComply:TestRcNumber - A valid RC number for business testing
/// 
/// Tests are skipped if ApiKey is not configured.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "LiveAPI")]
public class SmartComplyProviderLiveIntegrationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly SmartComplySettings _settings;
    private readonly string _testBvn;
    private readonly string _testRcNumber;
    private ISmartComplyProvider? _provider;

    public SmartComplyProviderLiveIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        
        // Build configuration from appsettings.test.json + environment variables
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.test.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables() // Environment variables override JSON settings
            .Build();

        var scSection = configuration.GetSection("SmartComply");
        
        _settings = new SmartComplySettings
        {
            BaseUrl = scSection["BaseUrl"] ?? "https://adhere-api.smartcomply.com",
            ApiKey = scSection["ApiKey"] ?? "",
            TimeoutSeconds = int.TryParse(scSection["TimeoutSeconds"], out var timeout) ? timeout : 60,
            UseMock = false
        };
        
        _testBvn = scSection["TestBvn"] ?? "";
        _testRcNumber = scSection["TestRcNumber"] ?? "";
    }

    public Task InitializeAsync()
    {
        if (string.IsNullOrEmpty(_settings.ApiKey))
        {
            _output.WriteLine("SMARTCOMPLY_API_KEY not configured - live tests will be skipped");
            return Task.CompletedTask;
        }

        var loggerMock = new Mock<ILogger<SmartComplyProvider>>();
        
        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds)
        };
        
        _provider = new SmartComplyProvider(
            httpClient,
            Options.Create(_settings),
            loggerMock.Object);
            
        _output.WriteLine($"Initialized SmartComplyProvider with BaseUrl: {_settings.BaseUrl}");
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private void SkipIfNotConfigured()
    {
        Skip.If(string.IsNullOrEmpty(_settings.ApiKey), "SMARTCOMPLY_API_KEY not configured");
    }

    private void SkipIfNoBvn()
    {
        SkipIfNotConfigured();
        Skip.If(string.IsNullOrEmpty(_testBvn), "SMARTCOMPLY_TEST_BVN not configured");
    }

    private void SkipIfNoRcNumber()
    {
        SkipIfNotConfigured();
        Skip.If(string.IsNullOrEmpty(_testRcNumber), "SMARTCOMPLY_TEST_RC not configured");
    }

    #region Individual Credit Reports

    [SkippableFact]
    public async Task GetCRCFullAsync_WithValidBvn_ReturnsIndividualCreditReport()
    {
        SkipIfNoBvn();

        var result = await _provider!.GetCRCFullAsync(_testBvn);

        _output.WriteLine($"Result: IsSuccess={result.IsSuccess}, Error={result.Error}");
        if (result.IsSuccess)
        {
            _output.WriteLine($"Name: {result.Value?.Name}");
            _output.WriteLine($"BVN: {result.Value?.Bvn}");
            _output.WriteLine($"Total Loans: {result.Value?.Summary.TotalNoOfLoans}");
            _output.WriteLine($"Active Loans: {result.Value?.Summary.TotalNoOfActiveLoans}");
            _output.WriteLine($"Total Outstanding: {result.Value?.Summary.TotalOutstanding:N2}");
        }

        Assert.True(result.IsSuccess, $"Expected success but got: {result.Error}");
        Assert.NotNull(result.Value);
        Assert.NotNull(result.Value.Summary);
    }

    [SkippableFact]
    public async Task GetCRCFullAsync_WithInvalidBvn_ReturnsFailure()
    {
        SkipIfNotConfigured();

        var result = await _provider!.GetCRCFullAsync("00000000000");

        _output.WriteLine($"Result: IsSuccess={result.IsSuccess}, Error={result.Error}");

        Assert.False(result.IsSuccess);
    }

    [SkippableFact]
    public async Task GetFirstCentralFullAsync_WithValidBvn_ReturnsReport()
    {
        SkipIfNoBvn();

        var result = await _provider!.GetFirstCentralFullAsync(_testBvn);

        _output.WriteLine($"Result: IsSuccess={result.IsSuccess}, Error={result.Error}");
        if (result.IsSuccess)
        {
            _output.WriteLine($"Name: {result.Value?.Name}");
            _output.WriteLine($"Total Loans: {result.Value?.Summary.TotalNoOfLoans}");
        }

        Assert.True(result.IsSuccess, $"Expected success but got: {result.Error}");
        Assert.NotNull(result.Value);
    }

    [SkippableFact]
    public async Task GetCreditRegistryFullAsync_WithValidBvn_ReturnsReport()
    {
        SkipIfNoBvn();

        var result = await _provider!.GetCreditRegistryFullAsync(_testBvn);

        _output.WriteLine($"Result: IsSuccess={result.IsSuccess}, Error={result.Error}");

        Assert.True(result.IsSuccess, $"Expected success but got: {result.Error}");
        Assert.NotNull(result.Value);
    }

    [SkippableFact]
    public async Task GetCRCScoreAsync_WithValidBvn_ReturnsCreditScore()
    {
        SkipIfNoBvn();

        var result = await _provider!.GetCRCScoreAsync(_testBvn);

        _output.WriteLine($"Result: IsSuccess={result.IsSuccess}, Error={result.Error}");
        if (result.IsSuccess)
        {
            _output.WriteLine($"Score: {result.Value?.Score}");
            _output.WriteLine($"Grade: {result.Value?.Grade}");
            _output.WriteLine($"Provider: {result.Value?.Provider}");
        }

        Assert.True(result.IsSuccess, $"Expected success but got: {result.Error}");
        Assert.NotNull(result.Value);
        Assert.True(result.Value.Score >= 0);
    }

    #endregion

    #region Business Credit Reports

    [SkippableFact]
    public async Task GetCRCBusinessHistoryAsync_WithValidRcNumber_ReturnsBusinessReport()
    {
        SkipIfNoRcNumber();

        var result = await _provider!.GetCRCBusinessHistoryAsync(_testRcNumber);

        _output.WriteLine($"Result: IsSuccess={result.IsSuccess}, Error={result.Error}");
        if (result.IsSuccess)
        {
            _output.WriteLine($"Name: {result.Value?.Name}");
            _output.WriteLine($"RC: {result.Value?.BusinessRegNo}");
            _output.WriteLine($"Total Loans: {result.Value?.Summary.TotalNoOfLoans}");
            _output.WriteLine($"Active Loans: {result.Value?.Summary.TotalNoOfActiveLoans}");
            _output.WriteLine($"Total Outstanding: {result.Value?.Summary.TotalOutstanding:N2}");
        }

        Assert.True(result.IsSuccess, $"Expected success but got: {result.Error}");
        Assert.NotNull(result.Value);
        Assert.NotNull(result.Value.Summary);
    }

    [SkippableFact]
    public async Task GetCRCBusinessHistoryAsync_WithInvalidRcNumber_ReturnsFailure()
    {
        SkipIfNotConfigured();

        var result = await _provider!.GetCRCBusinessHistoryAsync("RC000000INVALID");

        _output.WriteLine($"Result: IsSuccess={result.IsSuccess}, Error={result.Error}");

        Assert.False(result.IsSuccess);
    }

    [SkippableFact]
    public async Task GetFirstCentralBusinessAsync_WithValidRc_ReturnsReport()
    {
        SkipIfNoRcNumber();

        var result = await _provider!.GetFirstCentralBusinessAsync(_testRcNumber);

        _output.WriteLine($"Result: IsSuccess={result.IsSuccess}, Error={result.Error}");

        Assert.True(result.IsSuccess, $"Expected success but got: {result.Error}");
        Assert.NotNull(result.Value);
    }

    [SkippableFact]
    public async Task GetPremiumBusinessAsync_WithValidRc_ReturnsReport()
    {
        SkipIfNoRcNumber();

        var result = await _provider!.GetPremiumBusinessAsync(_testRcNumber);

        _output.WriteLine($"Result: IsSuccess={result.IsSuccess}, Error={result.Error}");

        Assert.True(result.IsSuccess, $"Expected success but got: {result.Error}");
        Assert.NotNull(result.Value);
    }

    #endregion

    #region Loan Fraud Check

    [SkippableFact]
    public async Task CheckIndividualLoanFraudAsync_WithValidRequest_ReturnsFraudResult()
    {
        SkipIfNoBvn();

        var request = new SmartComplyIndividualLoanRequest(
            FirstName: "Test",
            LastName: "User",
            OtherName: null,
            DateOfBirth: "1985-01-15",
            Gender: "Male",
            Country: "Nigeria",
            City: "Lagos",
            CurrentAddress: "Test Address",
            Bvn: _testBvn,
            PhoneNumber: "+2348012345678",
            EmailAddress: "test@example.com",
            EmploymentType: "Employed",
            JobRole: "Manager",
            EmployerName: "Test Company",
            AnnualIncome: 15000000m,
            BankName: "First Bank",
            AccountNumber: "1234567890",
            LoanAmountRequested: 5000000m,
            PurposeOfLoan: "Business Expansion",
            LoanRepaymentDurationType: "Months",
            LoanRepaymentDurationValue: 12,
            CollateralRequired: false,
            CollateralValue: null,
            RunAmlCheck: false
        );

        var result = await _provider!.CheckIndividualLoanFraudAsync(request);

        _output.WriteLine($"Result: IsSuccess={result.IsSuccess}, Error={result.Error}");
        if (result.IsSuccess)
        {
            _output.WriteLine($"Fraud Risk Score: {result.Value?.FraudRiskScore}");
            _output.WriteLine($"Recommendation: {result.Value?.Recommendation}");
            _output.WriteLine($"Status: {result.Value?.Status}");
        }

        Assert.True(result.IsSuccess, $"Expected success but got: {result.Error}");
        Assert.NotNull(result.Value);
        Assert.InRange(result.Value.FraudRiskScore, 0, 100);
    }

    [SkippableFact]
    public async Task CheckBusinessLoanFraudAsync_WithValidRequest_ReturnsFraudResult()
    {
        SkipIfNoRcNumber();

        var request = new SmartComplyBusinessLoanRequest(
            BusinessName: "Test Business Ltd",
            BusinessAddress: "123 Test Street",
            RcNumber: _testRcNumber,
            City: "Lagos",
            Country: "Nigeria",
            PhoneNumber: "+2348012345678",
            EmailAddress: "info@testbusiness.com",
            AnnualRevenue: 500000000m,
            BankName: "First Bank",
            AccountNumber: "1234567890",
            LoanAmountRequested: 50000000m,
            PurposeOfLoan: "Expansion",
            LoanRepaymentDurationType: "Months",
            LoanRepaymentDurationValue: 24,
            CollateralRequired: true,
            CollateralValue: 75000000m,
            RunAmlCheck: false
        );

        var result = await _provider!.CheckBusinessLoanFraudAsync(request);

        _output.WriteLine($"Result: IsSuccess={result.IsSuccess}, Error={result.Error}");
        if (result.IsSuccess)
        {
            _output.WriteLine($"Fraud Risk Score: {result.Value?.FraudRiskScore}");
            _output.WriteLine($"Recommendation: {result.Value?.Recommendation}");
        }

        Assert.True(result.IsSuccess, $"Expected success but got: {result.Error}");
        Assert.NotNull(result.Value);
        Assert.InRange(result.Value.FraudRiskScore, 0, 100);
    }

    #endregion

    #region KYC/Identity Verification

    [SkippableFact]
    public async Task VerifyBvnAsync_WithValidBvn_ReturnsBasicInfo()
    {
        SkipIfNoBvn();

        var result = await _provider!.VerifyBvnAsync(_testBvn);

        _output.WriteLine($"Result: IsSuccess={result.IsSuccess}, Error={result.Error}");
        if (result.IsSuccess)
        {
            _output.WriteLine($"Name: {result.Value?.FirstName} {result.Value?.LastName}");
            _output.WriteLine($"DOB: {result.Value?.DateOfBirth}");
        }

        Assert.True(result.IsSuccess, $"Expected success but got: {result.Error}");
        Assert.NotNull(result.Value);
    }

    [SkippableFact]
    public async Task VerifyBvnAsync_WithInvalidBvn_ReturnsFailure()
    {
        SkipIfNotConfigured();

        var result = await _provider!.VerifyBvnAsync("00000000000");

        _output.WriteLine($"Result: IsSuccess={result.IsSuccess}, Error={result.Error}");

        Assert.False(result.IsSuccess);
    }

    [SkippableFact]
    public async Task VerifyBvnAdvancedAsync_WithValidBvn_ReturnsExtendedInfo()
    {
        SkipIfNoBvn();

        var result = await _provider!.VerifyBvnAdvancedAsync(_testBvn);

        _output.WriteLine($"Result: IsSuccess={result.IsSuccess}, Error={result.Error}");
        if (result.IsSuccess)
        {
            _output.WriteLine($"BVN: {result.Value?.Bvn}");
            _output.WriteLine($"Name: {result.Value?.FirstName} {result.Value?.LastName}");
            _output.WriteLine($"Gender: {result.Value?.Gender}");
            _output.WriteLine($"State of Origin: {result.Value?.StateOfOrigin}");
            _output.WriteLine($"Watch Listed: {result.Value?.WatchListed}");
        }

        Assert.True(result.IsSuccess, $"Expected success but got: {result.Error}");
        Assert.NotNull(result.Value);
    }

    [SkippableFact]
    public async Task VerifyCacAsync_WithValidRcNumber_ReturnsCompanyInfo()
    {
        SkipIfNoRcNumber();

        var result = await _provider!.VerifyCacAsync(_testRcNumber);

        _output.WriteLine($"Result: IsSuccess={result.IsSuccess}, Error={result.Error}");
        if (result.IsSuccess)
        {
            _output.WriteLine($"Company: {result.Value?.CompanyName}");
            _output.WriteLine($"RC: {result.Value?.RcNumber}");
            _output.WriteLine($"Type: {result.Value?.CompanyType}");
            _output.WriteLine($"Status: {result.Value?.Status}");
        }

        Assert.True(result.IsSuccess, $"Expected success but got: {result.Error}");
        Assert.NotNull(result.Value);
    }

    [SkippableFact]
    public async Task VerifyCacAdvancedAsync_ReturnsDirectors()
    {
        SkipIfNoRcNumber();

        var result = await _provider!.VerifyCacAdvancedAsync(_testRcNumber);

        _output.WriteLine($"Result: IsSuccess={result.IsSuccess}, Error={result.Error}");
        if (result.IsSuccess)
        {
            _output.WriteLine($"Company: {result.Value?.CompanyName}");
            _output.WriteLine($"Directors: {result.Value?.Directors.Count}");
            foreach (var dir in result.Value?.Directors ?? [])
            {
                _output.WriteLine($"  - {dir.FullName} ({dir.AffiliateType}) - Shares: {dir.NumSharesAlloted}");
            }
        }

        Assert.True(result.IsSuccess, $"Expected success but got: {result.Error}");
        Assert.NotNull(result.Value);
    }

    #endregion

    #region End-to-End Flow

    [SkippableFact]
    public async Task FullIndividualCreditCheckFlow_ReturnsCompleteData()
    {
        SkipIfNoBvn();

        _output.WriteLine("=== Full Individual Credit Check Flow ===");

        // Step 1: BVN Verification
        _output.WriteLine("\n1. BVN Verification...");
        var bvnResult = await _provider!.VerifyBvnAsync(_testBvn);
        Assert.True(bvnResult.IsSuccess, $"BVN verification failed: {bvnResult.Error}");
        _output.WriteLine($"   Name: {bvnResult.Value?.FirstName} {bvnResult.Value?.LastName}");

        // Step 2: Credit Report
        _output.WriteLine("\n2. Credit Report (CRC Full)...");
        var creditResult = await _provider.GetCRCFullAsync(_testBvn);
        Assert.True(creditResult.IsSuccess, $"Credit report failed: {creditResult.Error}");
        _output.WriteLine($"   Total Loans: {creditResult.Value?.Summary.TotalNoOfLoans}");
        _output.WriteLine($"   Active Loans: {creditResult.Value?.Summary.TotalNoOfActiveLoans}");
        _output.WriteLine($"   Outstanding: {creditResult.Value?.Summary.TotalOutstanding:N2}");

        // Step 3: Credit Score
        _output.WriteLine("\n3. Credit Score...");
        var scoreResult = await _provider.GetCRCScoreAsync(_testBvn);
        Assert.True(scoreResult.IsSuccess, $"Credit score failed: {scoreResult.Error}");
        _output.WriteLine($"   Score: {scoreResult.Value?.Score}");
        _output.WriteLine($"   Grade: {scoreResult.Value?.Grade}");

        _output.WriteLine("\n=== Flow Complete ===");
    }

    [SkippableFact]
    public async Task FullBusinessCreditCheckFlow_ReturnsCompleteData()
    {
        SkipIfNoRcNumber();

        _output.WriteLine("=== Full Business Credit Check Flow ===");

        // Step 1: CAC Verification
        _output.WriteLine("\n1. CAC Verification...");
        var cacResult = await _provider!.VerifyCacAdvancedAsync(_testRcNumber);
        Assert.True(cacResult.IsSuccess, $"CAC verification failed: {cacResult.Error}");
        _output.WriteLine($"   Company: {cacResult.Value?.CompanyName}");
        _output.WriteLine($"   Directors: {cacResult.Value?.Directors.Count}");

        // Step 2: Business Credit Report
        _output.WriteLine("\n2. Business Credit Report...");
        var creditResult = await _provider.GetCRCBusinessHistoryAsync(_testRcNumber);
        Assert.True(creditResult.IsSuccess, $"Business credit report failed: {creditResult.Error}");
        _output.WriteLine($"   Total Loans: {creditResult.Value?.Summary.TotalNoOfLoans}");
        _output.WriteLine($"   Active Loans: {creditResult.Value?.Summary.TotalNoOfActiveLoans}");
        _output.WriteLine($"   Outstanding: {creditResult.Value?.Summary.TotalOutstanding:N2}");

        // Step 3: Business Fraud Check
        _output.WriteLine("\n3. Business Fraud Check...");
        var fraudRequest = new SmartComplyBusinessLoanRequest(
            BusinessName: cacResult.Value!.CompanyName ?? "Test",
            BusinessAddress: cacResult.Value.Address ?? "",
            RcNumber: _testRcNumber,
            City: cacResult.Value.City ?? "Lagos",
            Country: "Nigeria",
            PhoneNumber: null,
            EmailAddress: cacResult.Value.Email,
            AnnualRevenue: 100000000m,
            BankName: null,
            AccountNumber: null,
            LoanAmountRequested: 10000000m,
            PurposeOfLoan: "Working Capital",
            LoanRepaymentDurationType: "Months",
            LoanRepaymentDurationValue: 12,
            CollateralRequired: false,
            CollateralValue: null,
            RunAmlCheck: false
        );
        var fraudResult = await _provider.CheckBusinessLoanFraudAsync(fraudRequest);
        Assert.True(fraudResult.IsSuccess, $"Fraud check failed: {fraudResult.Error}");
        _output.WriteLine($"   Risk Score: {fraudResult.Value?.FraudRiskScore}");
        _output.WriteLine($"   Recommendation: {fraudResult.Value?.Recommendation}");

        _output.WriteLine("\n=== Flow Complete ===");
    }

    #endregion
}

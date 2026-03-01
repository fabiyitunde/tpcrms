using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CRMS.Domain.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CRMS.Infrastructure.ExternalServices.CreditBureau;

public class CreditRegistryProvider : ICreditBureauProvider
{
    private readonly HttpClient _httpClient;
    private readonly CreditRegistrySettings _settings;
    private readonly ILogger<CreditRegistryProvider> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private string? _sessionCode;
    private DateTime _sessionExpiry;

    public CreditBureauProvider ProviderType => CreditBureauProvider.CreditRegistry;

    public CreditRegistryProvider(
        HttpClient httpClient,
        IOptions<CreditRegistrySettings> settings,
        ILogger<CreditRegistryProvider> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);
    }

    private async Task<Result<string>> EnsureAuthenticatedAsync(CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(_sessionCode) && DateTime.UtcNow < _sessionExpiry)
            return Result.Success(_sessionCode);

        var loginRequest = new
        {
            SubscriberID = _settings.SubscriberId,
            AgentUserID = _settings.AgentUserId
        };

        try
        {
            var response = await PostAsync<LoginResponse>("/api/Login", loginRequest, ct);
            if (response == null || !response.Success)
                return Result.Failure<string>($"Login failed: {string.Join(", ", response?.Errors ?? [])}");

            _sessionCode = response.SessionCode!;
            _sessionExpiry = DateTime.UtcNow.AddMinutes(response.TimeoutMinutes - 5);

            return Result.Success(_sessionCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreditRegistry login failed");
            return Result.Failure<string>($"Authentication failed: {ex.Message}");
        }
    }

    public async Task<Result<BureauSearchResult>> SearchByBVNAsync(string bvn, CancellationToken ct = default)
    {
        var authResult = await EnsureAuthenticatedAsync(ct);
        if (authResult.IsFailure)
            return Result.Failure<BureauSearchResult>(authResult.Error);

        var request = new
        {
            SessionCode = _sessionCode,
            BVN = bvn
        };

        try
        {
            var response = await PostAsync<FindSummaryResponse>("/api/FindSummary", request, ct);
            if (response == null || !response.Success)
                return Result.Failure<BureauSearchResult>($"Search failed: {string.Join(", ", response?.Errors ?? [])}");

            if (response.SearchResults == null || response.SearchResults.Count == 0)
                return Result.Success(new BureauSearchResult(false, null, null, bvn, null, null, null, null, null, SubjectType.Individual));

            var match = response.SearchResults.First();
            return Result.Success(new BureauSearchResult(
                Found: true,
                RegistryId: match.RegistryID,
                FullName: $"{match.FirstName} {match.Surname}".Trim(),
                BVN: match.BVN,
                DateOfBirth: match.DateOfBirth,
                Gender: match.Gender,
                Phone: match.MobileNo,
                Email: match.Email,
                Address: match.Address,
                SubjectType: SubjectType.Individual
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreditRegistry BVN search failed for {BVN}", bvn);
            return Result.Failure<BureauSearchResult>($"Search failed: {ex.Message}");
        }
    }

    public async Task<Result<BureauSearchResult>> SearchByNameAsync(string firstName, string lastName, DateTime? dateOfBirth, CancellationToken ct = default)
    {
        var authResult = await EnsureAuthenticatedAsync(ct);
        if (authResult.IsFailure)
            return Result.Failure<BureauSearchResult>(authResult.Error);

        var request = new
        {
            SessionCode = _sessionCode,
            FirstName = firstName,
            Surname = lastName,
            DateOfBirth = dateOfBirth?.ToString("yyyy-MM-dd")
        };

        try
        {
            var response = await PostAsync<FindSummaryResponse>("/api/FindSummary", request, ct);
            if (response == null || !response.Success)
                return Result.Failure<BureauSearchResult>($"Search failed: {string.Join(", ", response?.Errors ?? [])}");

            if (response.SearchResults == null || response.SearchResults.Count == 0)
                return Result.Success(new BureauSearchResult(false, null, null, null, null, null, null, null, null, SubjectType.Individual));

            var match = response.SearchResults.First();
            return Result.Success(new BureauSearchResult(
                Found: true,
                RegistryId: match.RegistryID,
                FullName: $"{match.FirstName} {match.Surname}".Trim(),
                BVN: match.BVN,
                DateOfBirth: match.DateOfBirth,
                Gender: match.Gender,
                Phone: match.MobileNo,
                Email: match.Email,
                Address: match.Address,
                SubjectType: SubjectType.Individual
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreditRegistry name search failed");
            return Result.Failure<BureauSearchResult>($"Search failed: {ex.Message}");
        }
    }

    public async Task<Result<BureauSearchResult>> SearchByTaxIdAsync(string taxId, CancellationToken ct = default)
    {
        var authResult = await EnsureAuthenticatedAsync(ct);
        if (authResult.IsFailure)
            return Result.Failure<BureauSearchResult>(authResult.Error);

        var request = new
        {
            SessionCode = _sessionCode,
            TaxIDNo = taxId
        };

        try
        {
            var response = await PostAsync<FindSummaryResponse>("/api/FindSummary", request, ct);
            if (response == null || !response.Success)
                return Result.Failure<BureauSearchResult>($"Search failed: {string.Join(", ", response?.Errors ?? [])}");

            if (response.SearchResults == null || response.SearchResults.Count == 0)
                return Result.Success(new BureauSearchResult(false, null, null, null, null, null, null, null, null, SubjectType.Business));

            var match = response.SearchResults.First();
            return Result.Success(new BureauSearchResult(
                Found: true,
                RegistryId: match.RegistryID,
                FullName: match.BusinessName ?? $"{match.FirstName} {match.Surname}".Trim(),
                BVN: match.BVN,
                DateOfBirth: match.DateOfBirth,
                Gender: match.Gender,
                Phone: match.MobileNo,
                Email: match.Email,
                Address: match.Address,
                SubjectType: string.IsNullOrEmpty(match.BusinessName) ? SubjectType.Individual : SubjectType.Business
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreditRegistry TaxID search failed");
            return Result.Failure<BureauSearchResult>($"Search failed: {ex.Message}");
        }
    }

    public async Task<Result<BureauCreditReport>> GetCreditReportAsync(string registryId, bool includePdf = false, CancellationToken ct = default)
    {
        var authResult = await EnsureAuthenticatedAsync(ct);
        if (authResult.IsFailure)
            return Result.Failure<BureauCreditReport>(authResult.Error);

        // Use GetReport201 for Account data + SMARTScore + Score Factors
        var request = new
        {
            SessionCode = _sessionCode,
            RegistryID = registryId,
            IncludePDF = includePdf
        };

        try
        {
            var response = await PostAsync<GetReport201Response>("/api/GetReport201", request, ct);
            if (response == null || !response.Success)
                return Result.Failure<BureauCreditReport>($"Report retrieval failed: {string.Join(", ", response?.Errors ?? [])}");

            var accounts = MapAccounts(response);
            var scoreFactors = MapScoreFactors(response);
            var summary = CalculateSummary(accounts, response);

            return Result.Success(new BureauCreditReport(
                RegistryId: registryId,
                FullName: response.FullName ?? "Unknown",
                CreditScore: response.SMARTScore?.Score,
                ScoreGrade: response.SMARTScore?.Grade,
                ReportDate: DateTime.UtcNow,
                RawJson: JsonSerializer.Serialize(response, _jsonOptions),
                PdfBase64: response.PDFReport,
                Summary: summary,
                Accounts: accounts,
                ScoreFactors: scoreFactors
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreditRegistry report retrieval failed for {RegistryId}", registryId);
            return Result.Failure<BureauCreditReport>($"Report retrieval failed: {ex.Message}");
        }
    }

    public async Task<Result<BureauCreditScore>> GetCreditScoreAsync(string registryId, CancellationToken ct = default)
    {
        var authResult = await EnsureAuthenticatedAsync(ct);
        if (authResult.IsFailure)
            return Result.Failure<BureauCreditScore>(authResult.Error);

        // Use GetReport202 for SMARTScore + Score Factors only
        var request = new
        {
            SessionCode = _sessionCode,
            RegistryID = registryId
        };

        try
        {
            var response = await PostAsync<GetReport202Response>("/api/GetReport202", request, ct);
            if (response == null || !response.Success)
                return Result.Failure<BureauCreditScore>($"Score retrieval failed: {string.Join(", ", response?.Errors ?? [])}");

            var factors = response.ScoreFactors?.Select(f => new BureauScoreFactorData(
                f.FactorCode ?? "",
                f.Description ?? "",
                f.Impact ?? "",
                f.Rank
            )).ToList() ?? [];

            return Result.Success(new BureauCreditScore(
                RegistryId: registryId,
                Score: response.SMARTScore?.Score ?? 0,
                Grade: response.SMARTScore?.Grade,
                GeneratedDate: DateTime.UtcNow,
                Factors: factors
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreditRegistry score retrieval failed for {RegistryId}", registryId);
            return Result.Failure<BureauCreditScore>($"Score retrieval failed: {ex.Message}");
        }
    }

    private async Task<T?> PostAsync<T>(string endpoint, object request, CancellationToken ct) where T : class
    {
        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(endpoint, content, ct);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<T>(responseJson, _jsonOptions);
    }

    private static List<BureauAccountData> MapAccounts(GetReport201Response response)
    {
        var accounts = new List<BureauAccountData>();

        if (response.PerformingAccounts != null)
        {
            accounts.AddRange(response.PerformingAccounts.Select(a => MapAccount(a, "Performing")));
        }
        if (response.NonPerformingAccounts != null)
        {
            accounts.AddRange(response.NonPerformingAccounts.Select(a => MapAccount(a, "NonPerforming")));
        }
        if (response.ClosedAccounts != null)
        {
            accounts.AddRange(response.ClosedAccounts.Select(a => MapAccount(a, "Closed")));
        }
        if (response.WrittenOffAccounts != null)
        {
            accounts.AddRange(response.WrittenOffAccounts.Select(a => MapAccount(a, "WrittenOff")));
        }

        return accounts;
    }

    private static BureauAccountData MapAccount(AccountResult a, string status)
    {
        return new BureauAccountData(
            AccountNumber: a.Account_No ?? a.Abbreviated_Account_No ?? "",
            CreditorName: a.CreditorName,
            AccountType: a.Account_Type_Description ?? a.Account_Type,
            Status: status,
            DelinquencyDays: ParseDelinquencyDays(a.PaymentProfile),
            CreditLimit: (decimal)(a.Credit_Limit ?? 0),
            Balance: (decimal)(a.Balance ?? 0),
            MinimumPayment: a.Minimum_Installment.HasValue ? (decimal)a.Minimum_Installment : null,
            DateOpened: a.Date_Opened,
            DateClosed: null,
            LastPaymentDate: a.Payment_Date,
            LastPaymentAmount: a.Payment.HasValue ? (decimal)a.Payment : null,
            PaymentProfile: a.PaymentProfile,
            LegalStatus: a.Legal_Status,
            LegalStatusDate: a.Legal_Status_Date,
            Currency: a.Currency,
            LastUpdated: a.LastUpdated
        );
    }

    private static int ParseDelinquencyDays(string? paymentProfile)
    {
        if (string.IsNullOrEmpty(paymentProfile)) return 0;
        
        // Payment profile: 0=Current, 1=<30, 2=30-60, 3=61-90, etc.
        var firstChar = paymentProfile[0];
        return firstChar switch
        {
            '0' => 0,
            '1' => 30,
            '2' => 60,
            '3' => 90,
            '4' => 120,
            '5' => 150,
            '6' => 180,
            '7' => 360,
            '8' => 999,
            _ => 0
        };
    }

    private static List<BureauScoreFactorData> MapScoreFactors(GetReport201Response response)
    {
        if (response.ScoreFactors == null) return [];

        return response.ScoreFactors.Select(f => new BureauScoreFactorData(
            f.FactorCode ?? "",
            f.Description ?? "",
            f.Impact ?? "",
            f.Rank
        )).ToList();
    }

    private static BureauReportSummary CalculateSummary(List<BureauAccountData> accounts, GetReport201Response response)
    {
        var performingCount = accounts.Count(a => a.Status == "Performing");
        var nonPerformingCount = accounts.Count(a => a.Status == "NonPerforming");
        var closedCount = accounts.Count(a => a.Status == "Closed");
        var writtenOffCount = accounts.Count(a => a.Status == "WrittenOff");
        var activeCount = accounts.Count - closedCount - writtenOffCount; // Active = not closed and not written off
        var maxDelinquency = accounts.Any() ? accounts.Max(a => a.DelinquencyDays) : 0;
        var hasLegal = accounts.Any(a => !string.IsNullOrEmpty(a.LegalStatus) && a.LegalStatus != "None");

        return new BureauReportSummary(
            TotalAccounts: accounts.Count,
            ActiveLoans: activeCount,
            PerformingAccounts: performingCount,
            NonPerformingAccounts: nonPerformingCount,
            ClosedAccounts: closedCount,
            WrittenOffAccounts: writtenOffCount,
            TotalOutstandingBalance: accounts.Sum(a => a.Balance),
            TotalOverdue: accounts.Where(a => a.DelinquencyDays > 0).Sum(a => a.Balance),
            TotalCreditLimit: accounts.Sum(a => a.CreditLimit),
            MaxDelinquencyDays: maxDelinquency,
            HasLegalActions: hasLegal,
            EnquiriesLast30Days: response.EnquiriesLast30Days ?? 0,
            EnquiriesLast90Days: response.EnquiriesLast90Days ?? 0
        );
    }
}

// CreditRegistry API Response DTOs
public class BaseApiResponse
{
    public bool Success { get; set; }
    public List<string>? Errors { get; set; }
}

public class LoginResponse : BaseApiResponse
{
    public string? SessionCode { get; set; }
    public int TimeoutMinutes { get; set; } = 30;
}

public class FindSummaryResponse : BaseApiResponse
{
    public List<SearchResult>? SearchResults { get; set; }
}

public class SearchResult
{
    public string? RegistryID { get; set; }
    public string? FirstName { get; set; }
    public string? Surname { get; set; }
    public string? BusinessName { get; set; }
    public string? BVN { get; set; }
    public string? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? MobileNo { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
}

public class GetReport201Response : BaseApiResponse
{
    public string? FullName { get; set; }
    public SMARTScoreResult? SMARTScore { get; set; }
    public List<ScoreFactorResult>? ScoreFactors { get; set; }
    public List<AccountResult>? PerformingAccounts { get; set; }
    public List<AccountResult>? NonPerformingAccounts { get; set; }
    public List<AccountResult>? ClosedAccounts { get; set; }
    public List<AccountResult>? WrittenOffAccounts { get; set; }
    public int? EnquiriesLast30Days { get; set; }
    public int? EnquiriesLast90Days { get; set; }
    public string? PDFReport { get; set; }
}

public class GetReport202Response : BaseApiResponse
{
    public SMARTScoreResult? SMARTScore { get; set; }
    public List<ScoreFactorResult>? ScoreFactors { get; set; }
}

public class SMARTScoreResult
{
    public int Score { get; set; }
    public string? Grade { get; set; }
}

public class ScoreFactorResult
{
    public string? FactorCode { get; set; }
    public string? Description { get; set; }
    public string? Impact { get; set; }
    public int? Rank { get; set; }
}

public class AccountResult
{
    public string? Account_No { get; set; }
    public string? Abbreviated_Account_No { get; set; }
    public string? CreditorName { get; set; }
    public string? Account_Type { get; set; }
    public string? Account_Type_Description { get; set; }
    public double? Credit_Limit { get; set; }
    public double? Balance { get; set; }
    public double? Minimum_Installment { get; set; }
    public DateTime? Date_Opened { get; set; }
    public DateTime? Payment_Date { get; set; }
    public double? Payment { get; set; }
    public string? PaymentProfile { get; set; }
    public string? Legal_Status { get; set; }
    public DateTime? Legal_Status_Date { get; set; }
    public string? Currency { get; set; }
    public DateTime LastUpdated { get; set; }
}

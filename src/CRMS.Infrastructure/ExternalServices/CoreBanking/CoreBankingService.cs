using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using CRMS.Domain.Common;
using CRMS.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CRMS.Infrastructure.ExternalServices.CoreBanking;

/// <summary>
/// Real core banking service that calls the CBS REST API.
/// Uses two endpoints:
///   1. GET /core/account/fulldetailsbynuban/{nuban} — account details + directors + signatories
///   2. GET /core/transactions/{nuban}?startDate=DD-MM-YYYY&endDate=DD-MM-YYYY — statement
/// Auth: OAuth 2.0 Client Credentials (bearer token).
/// </summary>
public class CoreBankingService : ICoreBankingService
{
    private readonly HttpClient _httpClient;
    private readonly CoreBankingSettings _settings;
    private readonly ILogger<CoreBankingService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    // Per-request cache: avoids duplicate API calls when multiple domain methods
    // (GetCustomerByAccountNumberAsync, GetCorporateInfoAsync, GetDirectorsAsync, GetSignatoriesAsync)
    // all need the same fulldetailsbynuban response.
    private readonly Dictionary<string, FullDetailsByNubanResponse> _detailsCache = new();

    public CoreBankingService(
        HttpClient httpClient,
        IOptions<CoreBankingSettings> settings,
        ILogger<CoreBankingService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    #region Core Methods — Backed by Real API

    private async Task<Result<FullDetailsByNubanResponse>> GetFullDetailsAsync(string nuban, CancellationToken ct)
    {
        if (_detailsCache.TryGetValue(nuban, out var cached))
            return Result.Success(cached);

        try
        {
            var url = $"/core/account/fulldetailsbynuban/{nuban}";
            _logger.LogInformation("CBS: GET {Url}", url);

            var response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("CBS fulldetailsbynuban failed ({Status}): {Body}", response.StatusCode, errorBody);
                return Result.Failure<FullDetailsByNubanResponse>($"Core banking returned {(int)response.StatusCode}: {errorBody}");
            }

            var result = await response.Content.ReadFromJsonAsync<FullDetailsByNubanResponse>(_jsonOptions, ct);
            if (result == null)
                return Result.Failure<FullDetailsByNubanResponse>("Empty response from core banking");

            _detailsCache[nuban] = result;
            return Result.Success(result);
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CBS fulldetailsbynuban error for {Nuban}", nuban);
            return Result.Failure<FullDetailsByNubanResponse>($"Core banking error: {ex.Message}");
        }
    }

    public async Task<Result<CustomerInfo>> GetCustomerByAccountNumberAsync(string accountNumber, CancellationToken ct = default)
    {
        var detailsResult = await GetFullDetailsAsync(accountNumber, ct);
        if (detailsResult.IsFailure)
            return Result.Failure<CustomerInfo>(detailsResult.Error);

        var details = detailsResult.Value;
        var client = details.ClientDetails;
        if (client == null)
            return Result.Failure<CustomerInfo>("No client details in CBS response");

        var customerType = string.Equals(details.ClientType, "BUSINESS", StringComparison.OrdinalIgnoreCase)
            ? CustomerType.Corporate
            : CustomerType.Individual;

        return Result.Success(new CustomerInfo(
            CustomerId: client.Id.ToString(),
            FullName: client.FullName ?? "Unknown",
            CustomerType: customerType,
            Email: null,
            PhoneNumber: client.MobileNo,
            BVN: client.Bvn,
            DateOfBirth: ParseCbsDate(client.DateOfBirth),
            Address: BuildAddress(client)
        ));
    }

    public Task<Result<CustomerInfo>> GetCustomerByIdAsync(string customerId, CancellationToken ct = default)
    {
        // Not supported by the real CBS API — only lookup by NUBAN is available
        return Task.FromResult(Result.Failure<CustomerInfo>("GetCustomerByIdAsync is not supported by the core banking API. Use GetCustomerByAccountNumberAsync."));
    }

    public Task<Result<string>> CreateCustomerAsync(CreateCustomerRequest request, CancellationToken ct = default)
    {
        // Phase 2: Automated account creation not implemented yet
        return Task.FromResult(Result.Failure<string>("CreateCustomerAsync is not implemented — accounts are created externally in core banking."));
    }

    public async Task<Result<CorporateInfo>> GetCorporateInfoAsync(string accountNumber, CancellationToken ct = default)
    {
        var detailsResult = await GetFullDetailsAsync(accountNumber, ct);
        if (detailsResult.IsFailure)
            return Result.Failure<CorporateInfo>(detailsResult.Error);

        var client = detailsResult.Value.ClientDetails;
        if (client == null)
            return Result.Failure<CorporateInfo>("No client details in CBS response");

        return Result.Success(new CorporateInfo(
            CorporateId: client.Id.ToString(),
            CompanyName: client.FullName ?? "Unknown",
            RegistrationNumber: client.IncorporationNumber,
            Industry: null,
            IncorporationDate: null,
            RegisteredAddress: BuildAddress(client),
            TaxIdentificationNumber: null
        ));
    }

    public Task<Result<IReadOnlyList<DirectorInfo>>> GetDirectorsAsync(string corporateId, CancellationToken ct = default)
    {
        // corporateId in the current flow is the CBS client ID; but fulldetailsbynuban needs the NUBAN.
        // When called from InitiateCorporateLoanCommand, it passes customer.CustomerId (CBS client ID).
        // This is the legacy fallback path. In the real API we only have NUBAN-based lookup.
        // The details should already be cached from the preceding GetCustomerByAccountNumberAsync call.
        var cached = _detailsCache.Values.FirstOrDefault();
        if (cached == null)
            return Task.FromResult(Result.Success<IReadOnlyList<DirectorInfo>>(new List<DirectorInfo>()));

        var directors = (cached.Directors ?? [])
            .Select((d, i) => new DirectorInfo(
                DirectorId: d.Id.ToString(),
                FullName: d.FullName,
                BVN: d.Bvn,
                Email: d.Email,
                PhoneNumber: d.MobileNo,
                Address: d.Address,
                DateOfBirth: ParseCbsDate(d.DateOfBirth),
                Nationality: null,
                ShareholdingPercent: null
            ))
            .ToList();

        return Task.FromResult(Result.Success<IReadOnlyList<DirectorInfo>>(directors));
    }

    public async Task<Result<IReadOnlyList<SignatoryInfo>>> GetSignatoriesAsync(string accountNumber, CancellationToken ct = default)
    {
        var detailsResult = await GetFullDetailsAsync(accountNumber, ct);
        if (detailsResult.IsFailure)
            return Result.Failure<IReadOnlyList<SignatoryInfo>>(detailsResult.Error);

        var signatories = (detailsResult.Value.Signatories ?? [])
            .Select(s => new SignatoryInfo(
                SignatoryId: s.Id.ToString(),
                FullName: s.FullName,
                BVN: s.Bvn,
                Email: s.Email,
                PhoneNumber: s.MobileNo,
                MandateType: "A",
                Designation: null
            ))
            .ToList();

        return Result.Success<IReadOnlyList<SignatoryInfo>>(signatories);
    }

    public async Task<Result<AccountInfo>> GetAccountInfoAsync(string accountNumber, CancellationToken ct = default)
    {
        // The fulldetailsbynuban endpoint doesn't return balance information,
        // so we provide basic account info from the client details.
        var detailsResult = await GetFullDetailsAsync(accountNumber, ct);
        if (detailsResult.IsFailure)
            return Result.Failure<AccountInfo>(detailsResult.Error);

        var client = detailsResult.Value.ClientDetails;
        if (client == null)
            return Result.Failure<AccountInfo>("No client details in CBS response");

        return Result.Success(new AccountInfo(
            AccountNumber: accountNumber,
            AccountName: client.FullName ?? "Unknown",
            AccountType: client.ClientType ?? "Unknown",
            Currency: "NGN",
            CurrentBalance: 0m,
            AvailableBalance: 0m,
            Status: client.Status ?? "Unknown",
            OpenedDate: ParseCbsDate(client.ActivationDate) ?? DateTime.MinValue
        ));
    }

    public async Task<Result<AccountStatement>> GetStatementAsync(string accountNumber, DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        try
        {
            var startStr = fromDate.ToString("dd-MM-yyyy");
            var endStr = toDate.ToString("dd-MM-yyyy");
            var url = $"/core/transactions/{accountNumber}?startDate={startStr}&endDate={endStr}";

            _logger.LogInformation("CBS: GET {Url}", url);

            var response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("CBS transactions failed ({Status}): {Body}", response.StatusCode, errorBody);
                return Result.Failure<AccountStatement>($"Core banking transactions returned {(int)response.StatusCode}: {errorBody}");
            }

            var transactions = await response.Content.ReadFromJsonAsync<List<CbsTransaction>>(_jsonOptions, ct)
                ?? new List<CbsTransaction>();

            var mapped = transactions
                .OrderBy(t => t.CreatedDate)
                .Select(t => new StatementTransaction(
                    TransactionId: t.Id.ToString(),
                    Date: t.CreatedDate,
                    Description: t.Note ?? string.Empty,
                    Amount: t.Amount,
                    Type: string.Equals(t.TransactionType, "Deposit", StringComparison.OrdinalIgnoreCase)
                        ? TransactionType.Credit
                        : TransactionType.Debit,
                    RunningBalance: t.Balance,
                    Reference: t.TransactionReference ?? t.SessionId
                ))
                .ToList();

            var totalCredits = mapped.Where(t => t.Type == TransactionType.Credit).Sum(t => t.Amount);
            var totalDebits = mapped.Where(t => t.Type == TransactionType.Debit).Sum(t => t.Amount);
            var openingBalance = mapped.Count > 0 ? mapped[0].RunningBalance + (mapped[0].Type == TransactionType.Debit ? mapped[0].Amount : -mapped[0].Amount) : 0m;
            var closingBalance = mapped.Count > 0 ? mapped[^1].RunningBalance : 0m;

            return Result.Success(new AccountStatement(
                AccountNumber: accountNumber,
                FromDate: fromDate,
                ToDate: toDate,
                OpeningBalance: openingBalance,
                ClosingBalance: closingBalance,
                TotalCredits: totalCredits,
                TotalDebits: totalDebits,
                Transactions: mapped
            ));
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CBS transactions error for {Nuban}", accountNumber);
            return Result.Failure<AccountStatement>($"Core banking transactions error: {ex.Message}");
        }
    }

    public Task<Result<decimal>> GetAccountBalanceAsync(string accountNumber, CancellationToken ct = default)
    {
        // Not directly available from the CBS API endpoints
        return Task.FromResult(Result.Failure<decimal>("GetAccountBalanceAsync is not supported by the available CBS endpoints."));
    }

    #endregion

    #region Loan Operations — Phase 2 (Not Implemented)

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

    // TODO: Implement once CBS provider confirms the exposure endpoint specification.
    // See ICoreBankingService.GetCustomerExposureAsync for expected contract.
    // Until then, returns a not-implemented failure so callers can fall back gracefully.
    public Task<Result<CustomerExposure>> GetCustomerExposureAsync(string accountNumber, CancellationToken ct = default)
        => Task.FromResult(Result.Failure<CustomerExposure>("Customer exposure endpoint not yet available — pending CBS API specification from provider."));

    #endregion

    #region Helpers

    private static DateTime? ParseCbsDate(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr))
            return null;
        // CBS uses "dd-MM-yyyy" format
        if (DateTime.TryParseExact(dateStr, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return dt;
        // Fallback: try ISO
        if (DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
            return dt;
        return null;
    }

    private static string? BuildAddress(CbsClientDetails client)
    {
        var parts = new[] { client.AddressLine1, client.AddressLine2, client.AddressLine3, client.City, client.State }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        return parts.Any() ? string.Join(", ", parts) : null;
    }

    #endregion
}

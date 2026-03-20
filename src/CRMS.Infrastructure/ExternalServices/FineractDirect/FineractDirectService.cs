using System.Net.Http.Json;
using System.Text.Json;
using CRMS.Domain.Common;
using CRMS.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace CRMS.Infrastructure.ExternalServices.FineractDirect;

/// <summary>
/// Real Fineract direct API client. Uses Basic Auth + tenant header.
/// Hybrid approach: tries Fineract API first, falls back to in-house calculation.
/// Endpoints:
///   1. POST /loans?command=calculateLoanSchedule — schedule preview
///   2. GET /clients/{clientId}/accounts — all accounts for a client
///   3. GET /loans/{loanId}?associations=repaymentSchedule — loan detail
/// </summary>
public class FineractDirectService : IFineractDirectService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FineractDirectService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly MockFineractDirectService _fallbackCalculator;

    private static readonly int[] ActiveLoanStatusCodes = [300]; // ACTIVE = 300

    public FineractDirectService(
        HttpClient httpClient,
        ILogger<FineractDirectService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        _fallbackCalculator = new MockFineractDirectService(logger);
    }

    public async Task<Result<ProposedRepaymentSchedule>> CalculateRepaymentScheduleAsync(
        ScheduleCalculationRequest request, CancellationToken ct = default)
    {
        // If no Fineract product ID, use in-house calculation directly
        if (request.ProductId <= 0)
        {
            _logger.LogInformation("Fineract: No ProductId provided, using in-house schedule calculation");
            return await _fallbackCalculator.CalculateRepaymentScheduleAsync(request, ct);
        }

        // Try Fineract API first
        var fineractResult = await CalculateViaFineractAsync(request, ct);
        if (fineractResult.IsSuccess)
            return fineractResult;

        // Fall back to in-house calculation
        _logger.LogWarning("Fineract API failed ({Error}), falling back to in-house schedule calculation", fineractResult.Error);
        return await _fallbackCalculator.CalculateRepaymentScheduleAsync(request, ct);
    }

    private async Task<Result<ProposedRepaymentSchedule>> CalculateViaFineractAsync(
        ScheduleCalculationRequest request, CancellationToken ct)
    {
        try
        {
            var loanTermFrequency = request.NumberOfRepayments * request.RepaymentEvery;

            var body = new Dictionary<string, object>
            {
                ["dateFormat"] = "dd MMMM yyyy",
                ["locale"] = "en",
                ["productId"] = request.ProductId,
                ["principal"] = request.Principal,
                ["loanTermFrequency"] = loanTermFrequency,
                ["loanTermFrequencyType"] = request.RepaymentFrequencyType,
                ["numberOfRepayments"] = request.NumberOfRepayments,
                ["repaymentEvery"] = request.RepaymentEvery,
                ["repaymentFrequencyType"] = request.RepaymentFrequencyType,
                ["interestRatePerPeriod"] = request.InterestRatePerPeriod,
                ["interestRateFrequencyType"] = request.InterestRateFrequencyType,
                ["amortizationType"] = request.AmortizationType,
                ["interestType"] = request.InterestType,
                ["interestCalculationPeriodType"] = request.InterestCalculationPeriodType,
                ["expectedDisbursementDate"] = request.ExpectedDisbursementDate.ToString("dd MMMM yyyy"),
                ["transactionProcessingStrategyId"] = request.TransactionProcessingStrategyId,
                ["loanType"] = request.LoanType
            };

            if (request.GraceOnPrincipalPayment.HasValue)
                body["graceOnPrincipalPayment"] = request.GraceOnPrincipalPayment.Value;
            if (request.GraceOnInterestPayment.HasValue)
                body["graceOnInterestPayment"] = request.GraceOnInterestPayment.Value;

            _logger.LogInformation("Fineract: POST /loans?command=calculateLoanSchedule (principal={Principal}, repayments={Repayments})",
                request.Principal, request.NumberOfRepayments);

            var response = await _httpClient.PostAsJsonAsync("/loans?command=calculateLoanSchedule", body, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Fineract calculateLoanSchedule failed ({Status}): {Body}", response.StatusCode, errorBody);
                return Result.Failure<ProposedRepaymentSchedule>($"Fineract returned {(int)response.StatusCode}: {errorBody}");
            }

            var scheduleResponse = await response.Content.ReadFromJsonAsync<FineractScheduleResponse>(_jsonOptions, ct);
            if (scheduleResponse == null)
                return Result.Failure<ProposedRepaymentSchedule>("Empty response from Fineract");

            var installments = (scheduleResponse.Periods ?? [])
                .Where(p => p.Period.HasValue && p.Period.Value > 0) // Skip disbursement period (period=null or 0)
                .Select(p => new ProposedInstallment(
                    PeriodNumber: p.Period!.Value,
                    FromDate: ParseFineractDate(p.FromDate) ?? DateTime.MinValue,
                    DueDate: ParseFineractDate(p.DueDate) ?? DateTime.MinValue,
                    PrincipalDue: p.PrincipalDue,
                    InterestDue: p.InterestDue,
                    FeesDue: p.FeeChargesDue + p.PenaltyChargesDue,
                    TotalDue: p.TotalDueForPeriod,
                    OutstandingBalance: p.PrincipalLoanBalanceOutstanding
                ))
                .ToList();

            return Result.Success(new ProposedRepaymentSchedule(
                TotalPrincipal: scheduleResponse.TotalPrincipalExpected,
                TotalInterest: scheduleResponse.TotalInterestCharged,
                TotalFees: scheduleResponse.TotalFeeChargesCharged + scheduleResponse.TotalPenaltyChargesCharged,
                TotalRepayment: scheduleResponse.TotalRepaymentExpected,
                Installments: installments
            ));
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fineract calculateLoanSchedule error");
            return Result.Failure<ProposedRepaymentSchedule>($"Fineract error: {ex.Message}");
        }
    }

    public async Task<Result<ClientAccountSummary>> GetClientAccountsAsync(long clientId, CancellationToken ct = default)
    {
        try
        {
            var url = $"/clients/{clientId}/accounts";
            _logger.LogInformation("Fineract: GET {Url}", url);

            var response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Fineract client accounts failed ({Status}): {Body}", response.StatusCode, errorBody);
                return Result.Failure<ClientAccountSummary>($"Fineract returned {(int)response.StatusCode}: {errorBody}");
            }

            var accountsResponse = await response.Content.ReadFromJsonAsync<FineractClientAccountsResponse>(_jsonOptions, ct);
            if (accountsResponse == null)
                return Result.Failure<ClientAccountSummary>("Empty response from Fineract");

            var loanAccounts = (accountsResponse.LoanAccounts ?? [])
                .Select(la => new ClientLoanAccountSummary(
                    Id: la.Id,
                    AccountNo: la.AccountNo ?? "",
                    ProductName: la.ProductName ?? "",
                    ProductId: la.ProductId,
                    Status: la.Status?.Value ?? "",
                    StatusCode: la.Status?.Id ?? 0,
                    LoanType: la.LoanType?.Value ?? ""
                ))
                .ToList();

            var savingsAccounts = (accountsResponse.SavingsAccounts ?? [])
                .Select(sa => new ClientSavingsAccountSummary(
                    Id: sa.Id,
                    AccountNo: sa.AccountNo ?? "",
                    ProductName: sa.ProductName ?? "",
                    Status: sa.Status?.Value ?? "",
                    AccountBalance: sa.AccountBalance
                ))
                .ToList();

            return Result.Success(new ClientAccountSummary(
                ClientId: clientId,
                LoanAccounts: loanAccounts,
                SavingsAccounts: savingsAccounts
            ));
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fineract client accounts error for clientId={ClientId}", clientId);
            return Result.Failure<ClientAccountSummary>($"Fineract error: {ex.Message}");
        }
    }

    public async Task<Result<FineractLoanDetail>> GetLoanDetailAsync(long loanId, CancellationToken ct = default)
    {
        try
        {
            var url = $"/loans/{loanId}?associations=repaymentSchedule";
            _logger.LogInformation("Fineract: GET {Url}", url);

            var response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Fineract loan detail failed ({Status}): {Body}", response.StatusCode, errorBody);
                return Result.Failure<FineractLoanDetail>($"Fineract returned {(int)response.StatusCode}: {errorBody}");
            }

            var loanResponse = await response.Content.ReadFromJsonAsync<FineractLoanDetailResponse>(_jsonOptions, ct);
            if (loanResponse == null)
                return Result.Failure<FineractLoanDetail>("Empty response from Fineract");

            var summary = loanResponse.Summary != null
                ? new FineractLoanSummary(
                    TotalExpectedRepayment: loanResponse.Summary.TotalExpectedRepayment,
                    TotalRepayment: loanResponse.Summary.TotalRepayment,
                    TotalOutstanding: loanResponse.Summary.TotalOutstanding,
                    PrincipalDisbursed: loanResponse.Summary.PrincipalDisbursed,
                    PrincipalPaid: loanResponse.Summary.PrincipalPaid,
                    PrincipalOutstanding: loanResponse.Summary.PrincipalOutstanding,
                    InterestCharged: loanResponse.Summary.InterestCharged,
                    InterestPaid: loanResponse.Summary.InterestPaid,
                    InterestOutstanding: loanResponse.Summary.InterestOutstanding,
                    FeeChargesCharged: loanResponse.Summary.FeeChargesCharged,
                    FeeChargesPaid: loanResponse.Summary.FeeChargesPaid,
                    FeeChargesOutstanding: loanResponse.Summary.FeeChargesOutstanding,
                    PenaltyChargesCharged: loanResponse.Summary.PenaltyChargesCharged,
                    PenaltyChargesPaid: loanResponse.Summary.PenaltyChargesPaid,
                    PenaltyChargesOutstanding: loanResponse.Summary.PenaltyChargesOutstanding
                )
                : new FineractLoanSummary(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

            var schedulePeriods = (loanResponse.RepaymentSchedule?.Periods ?? [])
                .Where(p => p.Period.HasValue && p.Period.Value > 0)
                .Select(p => new FineractSchedulePeriod(
                    Period: p.Period!.Value,
                    FromDate: ParseFineractDate(p.FromDate) ?? DateTime.MinValue,
                    DueDate: ParseFineractDate(p.DueDate) ?? DateTime.MinValue,
                    PrincipalDue: p.PrincipalDue,
                    PrincipalPaid: 0, // Not in schedule period DTO — derived from transactions
                    PrincipalOutstanding: p.PrincipalLoanBalanceOutstanding,
                    InterestDue: p.InterestDue,
                    InterestPaid: 0,
                    InterestOutstanding: 0,
                    FeeChargesDue: p.FeeChargesDue,
                    PenaltyChargesDue: p.PenaltyChargesDue,
                    TotalDue: p.TotalDueForPeriod,
                    TotalPaid: 0,
                    TotalOutstanding: p.TotalOutstandingForPeriod,
                    Complete: p.Complete
                ))
                .ToList();

            return Result.Success(new FineractLoanDetail(
                Id: loanResponse.Id,
                AccountNo: loanResponse.AccountNo ?? "",
                ProductName: loanResponse.ProductName ?? "",
                Status: loanResponse.Status?.Value ?? "",
                StatusCode: loanResponse.Status?.Id ?? 0,
                Principal: loanResponse.Principal,
                ApprovedPrincipal: loanResponse.ApprovedPrincipal,
                InterestRate: loanResponse.AnnualInterestRate,
                NumberOfRepayments: loanResponse.NumberOfRepayments,
                DisbursementDate: ParseFineractDate(loanResponse.Timeline?.ActualDisbursementDate),
                MaturityDate: ParseFineractDate(loanResponse.Timeline?.ExpectedMaturityDate),
                Summary: summary,
                RepaymentSchedule: schedulePeriods
            ));
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fineract loan detail error for loanId={LoanId}", loanId);
            return Result.Failure<FineractLoanDetail>($"Fineract error: {ex.Message}");
        }
    }

    public async Task<Result<CustomerExposure>> GetCustomerExposureAsync(
        long clientId, string accountNumber, string customerName, CancellationToken ct = default)
    {
        var accountsResult = await GetClientAccountsAsync(clientId, ct);
        if (accountsResult.IsFailure)
            return Result.Failure<CustomerExposure>(accountsResult.Error);

        var activeLoanIds = accountsResult.Value.LoanAccounts
            .Where(la => ActiveLoanStatusCodes.Contains(la.StatusCode))
            .ToList();

        var facilities = new List<FacilitySummary>();
        decimal totalOutstanding = 0;
        decimal totalApprovedLimit = 0;

        foreach (var loanAccount in activeLoanIds)
        {
            var loanResult = await GetLoanDetailAsync(loanAccount.Id, ct);
            if (loanResult.IsFailure)
            {
                _logger.LogWarning("Fineract: Could not fetch loan {LoanId} for exposure — {Error}", loanAccount.Id, loanResult.Error);
                continue;
            }

            var loan = loanResult.Value;
            totalOutstanding += loan.Summary.TotalOutstanding;
            totalApprovedLimit += loan.ApprovedPrincipal;

            var status = loan.Summary.TotalOutstanding > 0 && loan.Summary.PenaltyChargesOutstanding > 0
                ? "Overdue"
                : "Active";

            facilities.Add(new FacilitySummary(
                FacilityId: loan.AccountNo,
                ProductType: loan.ProductName,
                ApprovedAmount: loan.ApprovedPrincipal,
                OutstandingBalance: loan.Summary.TotalOutstanding,
                Status: status,
                MaturityDate: loan.MaturityDate
            ));
        }

        return Result.Success(new CustomerExposure(
            AccountNumber: accountNumber,
            CustomerName: customerName,
            ActiveFacilitiesCount: facilities.Count,
            TotalOutstandingBalance: totalOutstanding,
            TotalApprovedLimit: totalApprovedLimit,
            Facilities: facilities
        ));
    }

    /// <summary>
    /// Fineract returns dates as [year, month, day] integer arrays.
    /// </summary>
    private static DateTime? ParseFineractDate(List<int>? dateParts)
    {
        if (dateParts == null || dateParts.Count < 3)
            return null;
        try
        {
            return new DateTime(dateParts[0], dateParts[1], dateParts[2]);
        }
        catch
        {
            return null;
        }
    }
}

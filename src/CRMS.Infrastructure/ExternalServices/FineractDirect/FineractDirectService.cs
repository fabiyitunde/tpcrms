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

            var response = await _httpClient.PostAsJsonAsync("loans?command=calculateLoanSchedule", body, ct);

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
            var url = $"clients/{clientId}/accounts";
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
            var url = $"loans/{loanId}?associations=repaymentSchedule";
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

    public async Task<Result<FineractClientInfo>> GetClientByIdAsync(long clientId, CancellationToken ct = default)
    {
        try
        {
            var url = $"clients/{clientId}";
            _logger.LogInformation("Fineract: GET {Url}", url);

            var response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Fineract client detail failed ({Status}): {Body}", response.StatusCode, errorBody);
                return Result.Failure<FineractClientInfo>($"Fineract returned {(int)response.StatusCode}: {errorBody}");
            }

            var client = await response.Content.ReadFromJsonAsync<FineractClientDetailResponse>(_jsonOptions, ct);
            if (client == null)
                return Result.Failure<FineractClientInfo>("Empty response from Fineract.");

            if (string.IsNullOrWhiteSpace(client.OfficeName))
                return Result.Failure<FineractClientInfo>($"Client {clientId} has no office name in Fineract.");

            return Result.Success(new FineractClientInfo(
                client.Id,
                client.OfficeId,
                client.OfficeName,
                client.DisplayName ?? $"{client.Firstname} {client.Lastname}".Trim()));
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fineract client detail error for clientId={ClientId}", clientId);
            return Result.Failure<FineractClientInfo>($"Fineract error: {ex.Message}");
        }
    }

    public async Task<Result<IReadOnlyList<FineractLoanProduct>>> GetLoanProductsAsync(
        bool activeOnly = true, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Fineract: GET /loanproducts (activeOnly={ActiveOnly})", activeOnly);

            var response = await _httpClient.GetAsync("loanproducts", ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Fineract loanproducts failed ({Status}): {Body}", response.StatusCode, errorBody);
                return Result.Failure<IReadOnlyList<FineractLoanProduct>>($"Fineract returned {(int)response.StatusCode}: {errorBody}");
            }

            var dtos = await response.Content.ReadFromJsonAsync<List<FineractLoanProductDto>>(_jsonOptions, ct);
            if (dtos == null)
                return Result.Failure<IReadOnlyList<FineractLoanProduct>>("Empty response from Fineract");

            var today = DateTime.UtcNow.Date;
            var products = dtos
                .Select(dto =>
                {
                    var closeDate = ParseFineractDate(dto.CloseDate);
                    var isActive = closeDate == null || closeDate.Value.Date >= today;
                    return new FineractLoanProduct(
                        Id: dto.Id,
                        Name: dto.Name ?? "",
                        ShortName: dto.ShortName ?? "",
                        Description: dto.Description,
                        CurrencyCode: dto.Currency?.Code ?? "",
                        CurrencySymbol: dto.Currency?.DisplaySymbol ?? dto.Currency?.Code ?? "",
                        MinPrincipal: dto.MinPrincipal,
                        DefaultPrincipal: dto.Principal,
                        MaxPrincipal: dto.MaxPrincipal,
                        DefaultInterestRatePerPeriod: dto.InterestRatePerPeriod,
                        MinInterestRatePerPeriod: dto.MinInterestRatePerPeriod,
                        MaxInterestRatePerPeriod: dto.MaxInterestRatePerPeriod,
                        AnnualInterestRate: dto.AnnualInterestRate,
                        InterestRateFrequencyType: dto.InterestRateFrequencyType?.Value ?? "",
                        DefaultNumberOfRepayments: dto.NumberOfRepayments,
                        MinNumberOfRepayments: dto.MinNumberOfRepayments,
                        MaxNumberOfRepayments: dto.MaxNumberOfRepayments,
                        RepaymentEvery: dto.RepaymentEvery,
                        RepaymentFrequencyType: dto.RepaymentFrequencyType?.Value ?? "",
                        AmortizationType: dto.AmortizationType?.Value ?? "",
                        InterestType: dto.InterestType?.Value ?? "",
                        TransactionProcessingStrategyId: dto.TransactionProcessingStrategyId,
                        StartDate: ParseFineractDate(dto.StartDate),
                        CloseDate: closeDate,
                        IsActive: isActive
                    );
                })
                .Where(p => !activeOnly || p.IsActive)
                .ToList();

            return Result.Success<IReadOnlyList<FineractLoanProduct>>(products);
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fineract loanproducts error");
            return Result.Failure<IReadOnlyList<FineractLoanProduct>>($"Fineract error: {ex.Message}");
        }
    }

    public async Task<Result<FineractBookingResult>> BookApprovedLoanAsync(
        FineractLoanBookingRequest request, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Fineract: Initiating automated booking for client {ClientId}, product {ProductId}, principal {Principal}",
                request.ClientId, request.ProductId, request.Principal);

            // Step 1: Fetch Fineract Loan Product config to map settings
            var productsResult = await GetLoanProductsAsync(activeOnly: false, ct);
            if (productsResult.IsFailure)
            {
                return Result.Failure<FineractBookingResult>($"Failed to retrieve Fineract loan products: {productsResult.Error}");
            }

            var fineractProduct = productsResult.Value.FirstOrDefault(p => p.Id == request.ProductId);
            if (fineractProduct == null)
            {
                return Result.Failure<FineractBookingResult>($"Fineract loan product with ID {request.ProductId} was not found.");
            }

            // Step 2: Resolve the linked repayment savings account.
            // RepaymentAccountNumber is the BOA account (a Fineract external id), so resolve it via the
            // byexternalId lookup — that returns the savings account's internal id, which is what Fineract's
            // linkAccountId expects (NOT the accountNo). The linked account is required both for disburse-to-savings
            // and for the auto-debit repayment standing instruction.
            long? linkAccountId = null;
            if (!string.IsNullOrWhiteSpace(request.RepaymentAccountNumber))
            {
                var savingsResult = await GetNampBoaAccountAsync(request.RepaymentAccountNumber, ct);
                if (savingsResult.IsSuccess)
                {
                    var savings = savingsResult.Value;
                    if (savings.ClientId != request.ClientId)
                    {
                        _logger.LogWarning(
                            "Fineract: Repayment account '{Account}' resolved to client {ResolvedClient} but booking is for client {RequestClient}; not linking.",
                            request.RepaymentAccountNumber, savings.ClientId, request.ClientId);
                    }
                    else
                    {
                        linkAccountId = savings.SavingsAccountId;
                        _logger.LogInformation(
                            "Fineract: Resolved repayment savings account id {SavingsId} (accountNo {AccountNo}) for BOA externalId {ExternalId}",
                            linkAccountId, savings.SavingsAccountNo, request.RepaymentAccountNumber);
                    }
                }
                else
                {
                    _logger.LogWarning("Fineract: Could not resolve repayment savings account for BOA externalId '{ExternalId}': {Error}",
                        request.RepaymentAccountNumber, savingsResult.Error);
                }
            }

            // A linked savings account is mandatory for disburse-to-savings and for the repayment standing
            // instruction. If we cannot resolve it, fail the booking rather than disburse a loan that has no
            // working repayment-collection mechanism (audit-critical).
            if (linkAccountId == null && (request.DisburseToSavings || request.CreateRepaymentStandingInstruction))
            {
                var purpose = request.DisburseToSavings ? "disburse to savings" : "create the repayment standing instruction";
                return Result.Failure<FineractBookingResult>(
                    $"Cannot {purpose}: a linked savings account for BOA account '{request.RepaymentAccountNumber}' could not be resolved for client {request.ClientId}. " +
                    "Confirm the applicant's BOA savings account exists in Fineract and belongs to this client before booking.");
            }

            // Step 3: Map product settings to Fineract loan application payload
            var isMonthlyInterest = fineractProduct.InterestRateFrequencyType.Contains("month", StringComparison.OrdinalIgnoreCase);
            var interestRateFrequencyType = isMonthlyInterest ? 2 : 3; // 2 = Per Month, 3 = Per Year
            var interestRatePerPeriod = isMonthlyInterest ? request.InterestRatePerAnnum / 12m : request.InterestRatePerAnnum;

            var isEmi = fineractProduct.AmortizationType.Contains("installment", StringComparison.OrdinalIgnoreCase);
            var amortizationType = isEmi ? 1 : 0; // 1 = Equal Installments, 0 = Equal Principal

            var isFlat = fineractProduct.InterestType.Contains("flat", StringComparison.OrdinalIgnoreCase);
            var interestType = isFlat ? 1 : 0; // 1 = Flat, 0 = Declining Balance

            var repaymentFrequencyType = 2; // Default to Months
            if (fineractProduct.RepaymentFrequencyType.Contains("week", StringComparison.OrdinalIgnoreCase))
                repaymentFrequencyType = 1;
            else if (fineractProduct.RepaymentFrequencyType.Contains("day", StringComparison.OrdinalIgnoreCase))
                repaymentFrequencyType = 0;

            var formattedDate = request.ValueDate.ToString("dd MMMM yyyy");

            var loanBody = new Dictionary<string, object>
            {
                ["dateFormat"] = "dd MMMM yyyy",
                ["locale"] = "en",
                ["clientId"] = request.ClientId,
                ["productId"] = request.ProductId,
                ["principal"] = request.Principal,
                ["loanTermFrequency"] = request.TenorMonths * fineractProduct.RepaymentEvery,
                ["loanTermFrequencyType"] = repaymentFrequencyType,
                ["numberOfRepayments"] = request.TenorMonths,
                ["repaymentEvery"] = fineractProduct.RepaymentEvery,
                ["repaymentFrequencyType"] = repaymentFrequencyType,
                ["interestRatePerPeriod"] = interestRatePerPeriod,
                ["interestRateFrequencyType"] = interestRateFrequencyType,
                ["amortizationType"] = amortizationType,
                ["interestType"] = interestType,
                ["interestCalculationPeriodType"] = 1, // Same as repayment period
                ["expectedDisbursementDate"] = formattedDate,
                ["submittedOnDate"] = formattedDate,
                ["loanType"] = "individual",
                ["transactionProcessingStrategyId"] = fineractProduct.TransactionProcessingStrategyId
            };

            if (linkAccountId.HasValue)
            {
                loanBody["linkAccountId"] = linkAccountId.Value;

                // Ask Fineract to auto-create a savings -> loan repayment standing instruction at disbursement.
                // Fineract creates a DUES / AS_PER_DUES standing instruction (the scheduled EXECUTE_STANDING_INSTRUCTIONS
                // job then auto-debits the linked savings account for each due) — the same behaviour as ticking the
                // box in the Fineract UI. This fires for plain `disburse` (to vendor) too, not just disburseToSavings.
                if (request.CreateRepaymentStandingInstruction)
                {
                    loanBody["createStandingInstructionAtDisbursement"] = true;
                    _logger.LogInformation("Fineract: createStandingInstructionAtDisbursement=true with linked savings id {SavingsId}", linkAccountId.Value);
                }
            }

            // Step 4: Book the loan
            _logger.LogInformation("Fineract: Booking loan (POST /loans)");
            var bookResponse = await _httpClient.PostAsJsonAsync("loans", loanBody, ct);
            if (!bookResponse.IsSuccessStatusCode)
            {
                var errorBody = await bookResponse.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Fineract: Booking loan failed ({Status}): {Body}", bookResponse.StatusCode, errorBody);
                return Result.Failure<FineractBookingResult>($"Fineract booking failed: {errorBody}");
            }

            var createResponse = await bookResponse.Content.ReadFromJsonAsync<FineractCreateLoanResponse>(_jsonOptions, ct);
            if (createResponse == null || createResponse.LoanId <= 0)
            {
                return Result.Failure<FineractBookingResult>("Empty or invalid booking response from Fineract.");
            }

            var loanId = createResponse.LoanId;
            _logger.LogInformation("Fineract: Loan successfully booked with ID {LoanId}", loanId);

            // Step 5: Approve the loan
            _logger.LogInformation("Fineract: Approving loan {LoanId} (POST /loans/{LoanId}?command=approve)", loanId, loanId);
            var approveBody = new Dictionary<string, object>
            {
                ["dateFormat"] = "dd MMMM yyyy",
                ["locale"] = "en",
                ["approvedOnDate"] = formattedDate,
                ["approvedLoanAmount"] = request.Principal,
                ["expectedDisbursementDate"] = formattedDate,
                ["note"] = "Approved automatically by CRMS booking workflow"
            };

            var approveResponse = await _httpClient.PostAsJsonAsync($"loans/{loanId}?command=approve", approveBody, ct);
            if (!approveResponse.IsSuccessStatusCode)
            {
                var errorBody = await approveResponse.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Fineract: Approving loan {LoanId} failed ({Status}): {Body}", loanId, approveResponse.StatusCode, errorBody);
                return Result.Success(new FineractBookingResult(
                    LoanId: loanId,
                    LoanAccountNumber: "",
                    Booked: true,
                    Approved: false,
                    Disbursed: false,
                    Status: "Submitted and pending approval"
                ));
            }

            _logger.LogInformation("Fineract: Loan {LoanId} successfully approved", loanId);

            // Step 6: Disburse the loan
            var command = request.DisburseToSavings ? "disburseToSavings" : "disburse";
            _logger.LogInformation("Fineract: Disbursing loan {LoanId} using command {Command}", loanId, command);

            var disburseBody = new Dictionary<string, object>
            {
                ["dateFormat"] = "dd MMMM yyyy",
                ["locale"] = "en",
                ["actualDisbursementDate"] = formattedDate,
                ["transactionAmount"] = request.Principal,
                ["note"] = $"Disbursed automatically by CRMS booking workflow ({command})"
            };

            var disburseResponse = await _httpClient.PostAsJsonAsync($"loans/{loanId}?command={command}", disburseBody, ct);
            if (!disburseResponse.IsSuccessStatusCode)
            {
                var errorBody = await disburseResponse.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Fineract: Disbursing loan {LoanId} failed ({Status}): {Body}", loanId, disburseResponse.StatusCode, errorBody);
                return Result.Success(new FineractBookingResult(
                    LoanId: loanId,
                    LoanAccountNumber: "",
                    Booked: true,
                    Approved: true,
                    Disbursed: false,
                    Status: "Approved"
                ));
            }

            _logger.LogInformation("Fineract: Loan {LoanId} successfully disbursed", loanId);

            // Step 7: Retrieve the final loan details to get the generated loan account number
            var loanNo = "";
            var statusStr = "Active";
            var detailResult = await GetLoanDetailAsync(loanId, ct);
            if (detailResult.IsSuccess)
            {
                loanNo = detailResult.Value.AccountNo;
                statusStr = detailResult.Value.Status;
            }

            return Result.Success(new FineractBookingResult(
                LoanId: loanId,
                LoanAccountNumber: loanNo,
                Booked: true,
                Approved: true,
                Disbursed: true,
                Status: statusStr
            ));
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fineract automated booking error");
            return Result.Failure<FineractBookingResult>($"Fineract automated booking error: {ex.Message}");
        }
    }

    public async Task<Result<NampBoaAccountInfo>> GetNampBoaAccountAsync(string boaAccountNumber, CancellationToken ct = default)
    {
        try
        {
            var url = $"tp/savingsaccounts/byexternalId/{Uri.EscapeDataString(boaAccountNumber)}";
            _logger.LogInformation("Fineract NAMP: GET {Url}", url);

            var response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Fineract NAMP savings account lookup failed ({Status}): {Body}", response.StatusCode, errorBody);
                return Result.Failure<NampBoaAccountInfo>($"Account lookup returned {(int)response.StatusCode}: {errorBody}");
            }

            var result = await response.Content.ReadFromJsonAsync<NampSavingsAccountResponse>(_jsonOptions, ct);
            if (result == null)
                return Result.Failure<NampBoaAccountInfo>("Empty response from Fineract.");

            return Result.Success(new NampBoaAccountInfo(result.ClientId, result.Status?.Value ?? "Unknown", result.Id, result.AccountNo));
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fineract NAMP savings account lookup error for {Account}", boaAccountNumber);
            return Result.Failure<NampBoaAccountInfo>($"Fineract error: {ex.Message}");
        }
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


using CRMS.Domain.Common;
using CRMS.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace CRMS.Infrastructure.ExternalServices.FineractDirect;

/// <summary>
/// Mock Fineract direct service for development/testing.
/// Generates realistic repayment schedules using standard financial math.
/// </summary>
public class MockFineractDirectService : IFineractDirectService
{
    private readonly ILogger _logger;

    public MockFineractDirectService(ILogger<MockFineractDirectService> logger) : this((ILogger)logger) { }

    public MockFineractDirectService(ILogger logger)
    {
        _logger = logger;
    }

    public Task<Result<ProposedRepaymentSchedule>> CalculateRepaymentScheduleAsync(
        ScheduleCalculationRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("MockFineract: Calculating schedule (principal={Principal}, repayments={N}, rate={Rate})",
            request.Principal, request.NumberOfRepayments, request.InterestRatePerPeriod);

        var installments = new List<ProposedInstallment>();
        var principal = request.Principal;
        var numRepayments = request.NumberOfRepayments;

        // Convert rate to periodic rate
        decimal periodicRate;
        if (request.InterestRateFrequencyType == 3) // Per Year
            periodicRate = request.InterestRatePerPeriod / 100m / 12m;
        else // Per Month
            periodicRate = request.InterestRatePerPeriod / 100m;

        decimal totalInterest = 0;
        decimal totalFees = 0;
        var outstanding = principal;
        var startDate = request.ExpectedDisbursementDate;

        if (request.InterestType == 1) // Flat
        {
            totalInterest = principal * periodicRate * numRepayments;
            var principalPerPeriod = Math.Round(principal / numRepayments, 2);
            var interestPerPeriod = Math.Round(totalInterest / numRepayments, 2);

            for (int i = 1; i <= numRepayments; i++)
            {
                var fromDate = startDate.AddMonths((i - 1) * request.RepaymentEvery);
                var dueDate = startDate.AddMonths(i * request.RepaymentEvery);

                var principalDue = i == numRepayments ? outstanding : principalPerPeriod;
                outstanding -= principalDue;

                installments.Add(new ProposedInstallment(
                    PeriodNumber: i,
                    FromDate: fromDate,
                    DueDate: dueDate,
                    PrincipalDue: principalDue,
                    InterestDue: interestPerPeriod,
                    FeesDue: 0,
                    TotalDue: principalDue + interestPerPeriod,
                    OutstandingBalance: Math.Max(0, outstanding)
                ));
            }
        }
        else // Declining Balance
        {
            if (request.AmortizationType == 1) // Equal Installments (EMI)
            {
                var emi = periodicRate > 0
                    ? principal * periodicRate / (1 - (decimal)Math.Pow((double)(1 + periodicRate), -numRepayments))
                    : principal / numRepayments;
                emi = Math.Round(emi, 2);

                for (int i = 1; i <= numRepayments; i++)
                {
                    var fromDate = startDate.AddMonths((i - 1) * request.RepaymentEvery);
                    var dueDate = startDate.AddMonths(i * request.RepaymentEvery);

                    var interestDue = Math.Round(outstanding * periodicRate, 2);
                    var principalDue = i == numRepayments ? outstanding : Math.Round(emi - interestDue, 2);
                    principalDue = Math.Min(principalDue, outstanding);

                    outstanding -= principalDue;
                    totalInterest += interestDue;

                    installments.Add(new ProposedInstallment(
                        PeriodNumber: i,
                        FromDate: fromDate,
                        DueDate: dueDate,
                        PrincipalDue: principalDue,
                        InterestDue: interestDue,
                        FeesDue: 0,
                        TotalDue: principalDue + interestDue,
                        OutstandingBalance: Math.Max(0, outstanding)
                    ));
                }
            }
            else // Equal Principal
            {
                var principalPerPeriod = Math.Round(principal / numRepayments, 2);

                for (int i = 1; i <= numRepayments; i++)
                {
                    var fromDate = startDate.AddMonths((i - 1) * request.RepaymentEvery);
                    var dueDate = startDate.AddMonths(i * request.RepaymentEvery);

                    var interestDue = Math.Round(outstanding * periodicRate, 2);
                    var principalDue = i == numRepayments ? outstanding : principalPerPeriod;

                    outstanding -= principalDue;
                    totalInterest += interestDue;

                    installments.Add(new ProposedInstallment(
                        PeriodNumber: i,
                        FromDate: fromDate,
                        DueDate: dueDate,
                        PrincipalDue: principalDue,
                        InterestDue: interestDue,
                        FeesDue: 0,
                        TotalDue: principalDue + interestDue,
                        OutstandingBalance: Math.Max(0, outstanding)
                    ));
                }
            }
        }

        var result = new ProposedRepaymentSchedule(
            TotalPrincipal: principal,
            TotalInterest: request.InterestType == 1 ? totalInterest : installments.Sum(i => i.InterestDue),
            TotalFees: totalFees,
            TotalRepayment: installments.Sum(i => i.TotalDue),
            Installments: installments
        );

        return Task.FromResult(Result.Success(result));
    }

    public Task<Result<ClientAccountSummary>> GetClientAccountsAsync(long clientId, CancellationToken ct = default)
    {
        _logger.LogInformation("MockFineract: GetClientAccounts for clientId={ClientId}", clientId);

        var result = new ClientAccountSummary(
            ClientId: clientId,
            LoanAccounts: new List<ClientLoanAccountSummary>
            {
                new(Id: 1001, AccountNo: "LN-000001", ProductName: "Corporate Term Loan", ProductId: 1,
                    Status: "Active", StatusCode: 300, LoanType: "individual"),
                new(Id: 1002, AccountNo: "LN-000002", ProductName: "Working Capital Facility", ProductId: 2,
                    Status: "Active", StatusCode: 300, LoanType: "individual"),
                new(Id: 1003, AccountNo: "LN-000003", ProductName: "LPO Financing", ProductId: 3,
                    Status: "Closed", StatusCode: 600, LoanType: "individual")
            },
            SavingsAccounts: new List<ClientSavingsAccountSummary>
            {
                new(Id: 2001, AccountNo: "SA-000001", ProductName: "Corporate Current Account", Status: "Active", AccountBalance: 15_500_000m),
                new(Id: 2002, AccountNo: "SA-000002", ProductName: "Fixed Deposit", Status: "Active", AccountBalance: 50_000_000m)
            }
        );

        return Task.FromResult(Result.Success(result));
    }

    public Task<Result<FineractLoanDetail>> GetLoanDetailAsync(long loanId, CancellationToken ct = default)
    {
        _logger.LogInformation("MockFineract: GetLoanDetail for loanId={LoanId}", loanId);

        var (principal, rate, tenor, productName) = loanId switch
        {
            1001 => (100_000_000m, 18m, 24, "Corporate Term Loan"),
            1002 => (50_000_000m, 22m, 12, "Working Capital Facility"),
            _ => (25_000_000m, 20m, 6, "LPO Financing")
        };

        var monthlyRate = rate / 100m / 12m;
        var emi = principal * monthlyRate / (1 - (decimal)Math.Pow((double)(1 + monthlyRate), -tenor));
        var outstanding = principal;
        var totalInterest = 0m;
        var totalPaid = 0m;

        // Simulate partial repayment (50% through term)
        var paidInstallments = tenor / 2;
        for (int i = 0; i < paidInstallments; i++)
        {
            var interest = outstanding * monthlyRate;
            var principalPortion = emi - interest;
            outstanding -= principalPortion;
            totalInterest += interest;
            totalPaid += emi;
        }

        var result = new FineractLoanDetail(
            Id: loanId,
            AccountNo: $"LN-{loanId:D6}",
            ProductName: productName,
            Status: "Active",
            StatusCode: 300,
            Principal: principal,
            ApprovedPrincipal: principal,
            InterestRate: rate,
            NumberOfRepayments: tenor,
            DisbursementDate: DateTime.Today.AddMonths(-paidInstallments),
            MaturityDate: DateTime.Today.AddMonths(tenor - paidInstallments),
            Summary: new FineractLoanSummary(
                TotalExpectedRepayment: emi * tenor,
                TotalRepayment: totalPaid,
                TotalOutstanding: Math.Round(outstanding + (emi * (tenor - paidInstallments) - outstanding), 2),
                PrincipalDisbursed: principal,
                PrincipalPaid: principal - outstanding,
                PrincipalOutstanding: Math.Round(outstanding, 2),
                InterestCharged: Math.Round(totalInterest + outstanding * monthlyRate * (tenor - paidInstallments), 2),
                InterestPaid: Math.Round(totalInterest, 2),
                InterestOutstanding: Math.Round(outstanding * monthlyRate * (tenor - paidInstallments), 2),
                FeeChargesCharged: 0, FeeChargesPaid: 0, FeeChargesOutstanding: 0,
                PenaltyChargesCharged: 0, PenaltyChargesPaid: 0, PenaltyChargesOutstanding: 0
            ),
            RepaymentSchedule: new List<FineractSchedulePeriod>()
        );

        return Task.FromResult(Result.Success(result));
    }

    public Task<Result<FineractClientInfo>> GetClientByIdAsync(long clientId, CancellationToken ct = default)
    {
        _logger.LogInformation("MockFineract: GetClientById for clientId={ClientId}", clientId);

        // Mock: clientId 1643 → Enugu Branch (matching the mock BOA account 1234567890)
        // Mock: clientId 1001 → Lagos Main Branch (matching NAMP test BOA account 0000000029 / TEST*)
        var mockClients = new Dictionary<long, FineractClientInfo>
        {
            [1643] = new(Id: 1643, OfficeId: 5, OfficeName: "Enugu Branch",      DisplayName: "Acme Industries Ltd"),
            [1001] = new(Id: 1001, OfficeId: 1, OfficeName: "Lagos Main Branch", DisplayName: "Adewale Okafor"),
        };

        if (mockClients.TryGetValue(clientId, out var info))
            return Task.FromResult(Result.Success(info));

        return Task.FromResult(Result.Failure<FineractClientInfo>(
            $"MockFineract: client {clientId} not found."));
    }

    public Task<Result<IReadOnlyList<FineractLoanProduct>>> GetLoanProductsAsync(
        bool activeOnly = true, CancellationToken ct = default)
    {
        _logger.LogInformation("MockFineract: GetLoanProducts (activeOnly={ActiveOnly})", activeOnly);

        var products = new List<FineractLoanProduct>
        {
            new(Id: 1, Name: "Corporate Term Loan", ShortName: "CTL",
                Description: "Medium to long-term financing for capital expenditure and business expansion",
                CurrencyCode: "NGN", CurrencySymbol: "₦",
                MinPrincipal: 5_000_000m, DefaultPrincipal: 50_000_000m, MaxPrincipal: 500_000_000m,
                DefaultInterestRatePerPeriod: 1.5m, MinInterestRatePerPeriod: 1.0m, MaxInterestRatePerPeriod: 2.5m,
                AnnualInterestRate: 18m, InterestRateFrequencyType: "Per month",
                DefaultNumberOfRepayments: 24, MinNumberOfRepayments: 6, MaxNumberOfRepayments: 60,
                RepaymentEvery: 1, RepaymentFrequencyType: "Months",
                AmortizationType: "Equal installments", InterestType: "Declining Balance",
                TransactionProcessingStrategyId: 1,
                StartDate: new DateTime(2023, 1, 1), CloseDate: null, IsActive: true),

            new(Id: 2, Name: "Working Capital Facility", ShortName: "WCF",
                Description: "Short-term revolving credit facility to finance day-to-day operations",
                CurrencyCode: "NGN", CurrencySymbol: "₦",
                MinPrincipal: 2_000_000m, DefaultPrincipal: 20_000_000m, MaxPrincipal: 200_000_000m,
                DefaultInterestRatePerPeriod: 1.8m, MinInterestRatePerPeriod: 1.5m, MaxInterestRatePerPeriod: 2.5m,
                AnnualInterestRate: 21.6m, InterestRateFrequencyType: "Per month",
                DefaultNumberOfRepayments: 12, MinNumberOfRepayments: 3, MaxNumberOfRepayments: 12,
                RepaymentEvery: 1, RepaymentFrequencyType: "Months",
                AmortizationType: "Equal installments", InterestType: "Flat",
                TransactionProcessingStrategyId: 1,
                StartDate: new DateTime(2023, 1, 1), CloseDate: null, IsActive: true),

            new(Id: 3, Name: "LPO Financing", ShortName: "LPO",
                Description: "Local Purchase Order financing for businesses with confirmed purchase orders",
                CurrencyCode: "NGN", CurrencySymbol: "₦",
                MinPrincipal: 1_000_000m, DefaultPrincipal: 10_000_000m, MaxPrincipal: 100_000_000m,
                DefaultInterestRatePerPeriod: 2.0m, MinInterestRatePerPeriod: 1.5m, MaxInterestRatePerPeriod: 3.0m,
                AnnualInterestRate: 24m, InterestRateFrequencyType: "Per month",
                DefaultNumberOfRepayments: 6, MinNumberOfRepayments: 1, MaxNumberOfRepayments: 6,
                RepaymentEvery: 1, RepaymentFrequencyType: "Months",
                AmortizationType: "Equal principal payments", InterestType: "Declining Balance",
                TransactionProcessingStrategyId: 1,
                StartDate: new DateTime(2023, 1, 1), CloseDate: null, IsActive: true),

            new(Id: 4, Name: "Asset Finance", ShortName: "AF",
                Description: "Equipment and vehicle acquisition financing",
                CurrencyCode: "NGN", CurrencySymbol: "₦",
                MinPrincipal: 5_000_000m, DefaultPrincipal: 30_000_000m, MaxPrincipal: 300_000_000m,
                DefaultInterestRatePerPeriod: 1.6m, MinInterestRatePerPeriod: 1.2m, MaxInterestRatePerPeriod: 2.2m,
                AnnualInterestRate: 19.2m, InterestRateFrequencyType: "Per month",
                DefaultNumberOfRepayments: 36, MinNumberOfRepayments: 12, MaxNumberOfRepayments: 60,
                RepaymentEvery: 1, RepaymentFrequencyType: "Months",
                AmortizationType: "Equal installments", InterestType: "Declining Balance",
                TransactionProcessingStrategyId: 1,
                StartDate: new DateTime(2023, 1, 1), CloseDate: null, IsActive: true),

            new(Id: 5, Name: "RHNAMP Agricultural Equipment Loan", ShortName: "NAMP-AEL",
                Description: "Renewed Hope NAMP — Pay-As-You-Sell (PAYS) agricultural equipment financing for youth, women agripreneurs and agro-service companies",
                CurrencyCode: "NGN", CurrencySymbol: "₦",
                MinPrincipal: 500_000m, DefaultPrincipal: 5_000_000m, MaxPrincipal: 100_000_000m,
                DefaultInterestRatePerPeriod: 0.416m, MinInterestRatePerPeriod: 0.416m, MaxInterestRatePerPeriod: 0.416m,
                AnnualInterestRate: 5m, InterestRateFrequencyType: "Per month",
                DefaultNumberOfRepayments: 36, MinNumberOfRepayments: 12, MaxNumberOfRepayments: 60,
                RepaymentEvery: 1, RepaymentFrequencyType: "Months",
                AmortizationType: "Equal installments", InterestType: "Declining Balance",
                TransactionProcessingStrategyId: 1,
                StartDate: new DateTime(2024, 1, 1), CloseDate: null, IsActive: true),

            new(Id: 6, Name: "Invoice Discounting (Legacy)", ShortName: "ID-LEG",
                Description: "Discontinued invoice discounting product",
                CurrencyCode: "NGN", CurrencySymbol: "₦",
                MinPrincipal: 1_000_000m, DefaultPrincipal: 5_000_000m, MaxPrincipal: 50_000_000m,
                DefaultInterestRatePerPeriod: 2.5m, MinInterestRatePerPeriod: null, MaxInterestRatePerPeriod: null,
                AnnualInterestRate: 30m, InterestRateFrequencyType: "Per month",
                DefaultNumberOfRepayments: 3, MinNumberOfRepayments: null, MaxNumberOfRepayments: null,
                RepaymentEvery: 1, RepaymentFrequencyType: "Months",
                AmortizationType: "Equal installments", InterestType: "Flat",
                TransactionProcessingStrategyId: 1,
                StartDate: new DateTime(2020, 1, 1), CloseDate: new DateTime(2022, 12, 31), IsActive: false),
        };

        var result = activeOnly ? products.Where(p => p.IsActive).ToList() : products;
        return Task.FromResult(Result.Success<IReadOnlyList<FineractLoanProduct>>(result));
    }

    public async Task<Result<CustomerExposure>> GetCustomerExposureAsync(
        long clientId, string accountNumber, string customerName, CancellationToken ct = default)
    {
        var accountsResult = await GetClientAccountsAsync(clientId, ct);
        if (accountsResult.IsFailure)
            return Result.Failure<CustomerExposure>(accountsResult.Error);

        var activeLoanIds = accountsResult.Value.LoanAccounts
            .Where(la => la.StatusCode == 300) // Active
            .ToList();

        var facilities = new List<FacilitySummary>();
        decimal totalOutstanding = 0;
        decimal totalApproved = 0;

        foreach (var loanAccount in activeLoanIds)
        {
            var loanResult = await GetLoanDetailAsync(loanAccount.Id, ct);
            if (loanResult.IsFailure) continue;

            var loan = loanResult.Value;
            totalOutstanding += loan.Summary.PrincipalOutstanding;
            totalApproved += loan.ApprovedPrincipal;

            facilities.Add(new FacilitySummary(
                FacilityId: loan.AccountNo,
                ProductType: loan.ProductName,
                ApprovedAmount: loan.ApprovedPrincipal,
                OutstandingBalance: loan.Summary.PrincipalOutstanding,
                Status: "Active",
                MaturityDate: loan.MaturityDate
            ));
        }

        return Result.Success(new CustomerExposure(
            AccountNumber: accountNumber,
            CustomerName: customerName,
            ActiveFacilitiesCount: facilities.Count,
            TotalOutstandingBalance: totalOutstanding,
            TotalApprovedLimit: totalApproved,
            Facilities: facilities
        ));
    }

    public Task<Result<NampBoaAccountInfo>> GetNampBoaAccountAsync(string boaAccountNumber, CancellationToken ct = default)
    {
        // Simulate a known test account; any other account returns not found
        if (boaAccountNumber == "0000000029" || boaAccountNumber.StartsWith("TEST"))
            return Task.FromResult(Result.Success(new NampBoaAccountInfo(
                ClientId: 1001L, AccountStatus: "Active", SavingsAccountId: 2001L, SavingsAccountNo: boaAccountNumber)));

        return Task.FromResult(Result.Failure<NampBoaAccountInfo>(
            $"NAMP BOA account '{boaAccountNumber}' not found in mock data."));
    }

    public Task<Result<FineractBookingResult>> BookApprovedLoanAsync(
        FineractLoanBookingRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("MockFineract: Initiating automated booking for client {ClientId}, product {ProductId}, principal {Principal}",
            request.ClientId, request.ProductId, request.Principal);

        var random = new Random();
        var loanId = 3000L + random.Next(1, 1000);
        var loanAccountNumber = $"LN-{loanId:D6}";

        _logger.LogInformation("MockFineract: Successfully booked loan application (LoanId={LoanId}, AccountNo={AccountNo})",
            loanId, loanAccountNumber);

        if (request.CreateRepaymentStandingInstruction && !string.IsNullOrWhiteSpace(request.RepaymentAccountNumber))
            _logger.LogInformation("MockFineract: Would create savings -> loan repayment standing instruction from account {Account}",
                request.RepaymentAccountNumber);

        var result = new FineractBookingResult(
            LoanId: loanId,
            LoanAccountNumber: loanAccountNumber,
            Booked: true,
            Approved: true,
            Disbursed: true,
            Status: "Active"
        );

        return Task.FromResult(Result.Success(result));
    }
}


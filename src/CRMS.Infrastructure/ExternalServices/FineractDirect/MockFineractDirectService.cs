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
}

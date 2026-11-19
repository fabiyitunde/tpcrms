using System.Net;
using System.Text;
using CRMS.Infrastructure.ExternalServices.FineractDirect;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CRMS.Infrastructure.Tests;

/// <summary>
/// A booked loan as core banking reports it: what has been paid on each instalment, and whether the loan
/// is in arrears. Overdue is core banking's flag and overdue amount, not whether a penalty was charged.
/// </summary>
public class FineractLoanDetailTests
{
    private sealed class StubHandler(Func<string, string?> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(respond(request.RequestUri!.PathAndQuery) ?? "{}", Encoding.UTF8, "application/json")
            });
    }

    private static FineractDirectService Build(Func<string, string?> respond) =>
        new(new HttpClient(new StubHandler(respond)) { BaseAddress = new Uri("https://core-banking.test/core_banking/api/") },
            NullLogger<FineractDirectService>.Instance);

    // In arrears with no penalty charged: the case the penalty test missed.
    private const string LoanInArrearsJson =
        """
        {"id":901,"accountNo":"LN-000901","status":{"id":300,"value":"Active"},"approvedPrincipal":1200000,"inArrears":true,
         "summary":{"totalOutstanding":900000,"principalOutstanding":900000,"penaltyChargesCharged":0,"penaltyChargesOutstanding":0,
                    "totalOverdue":336000,"overdueSinceDate":[2026,9,1]},
         "repaymentSchedule":{"periods":[{"period":null},
           {"period":1,"dueDate":[2026,6,1],"principalDue":300000,"interestDue":36000,"totalDueForPeriod":336000,
            "principalPaid":300000,"interestPaid":36000,"interestOutstanding":0,"totalPaidForPeriod":336000,"totalOutstandingForPeriod":0,"complete":true},
           {"period":2,"dueDate":[2026,9,1],"principalDue":300000,"interestDue":36000,"totalDueForPeriod":336000,
            "principalPaid":0,"interestPaid":0,"interestOutstanding":36000,"totalPaidForPeriod":0,"totalOutstandingForPeriod":336000,"complete":false}]}}
        """;

    [Fact]
    public async Task LoanDetail_CarriesWhatWasPaidOnEachInstalment_AndTheArrearsFlag()
    {
        var service = Build(path => path.Contains("loans/901") ? LoanInArrearsJson : null);

        var result = await service.GetLoanDetailAsync(901);

        Assert.True(result.IsSuccess);
        var loan = result.Value;
        Assert.True(loan.InArrears);
        Assert.Equal(336000m, loan.Summary.TotalOverdue);
        Assert.Equal(new DateTime(2026, 9, 1), loan.Summary.OverdueSince);
        Assert.Equal(30, loan.Summary.DaysOverdue(new DateTime(2026, 10, 1)));

        var paid = loan.RepaymentSchedule.Single(p => p.Period == 1);
        Assert.Equal(300000m, paid.PrincipalPaid);
        Assert.Equal(36000m, paid.InterestPaid);
        Assert.Equal(336000m, paid.TotalPaid);
        var unpaid = loan.RepaymentSchedule.Single(p => p.Period == 2);
        Assert.Equal(0m, unpaid.TotalPaid);
        Assert.Equal(36000m, unpaid.InterestOutstanding);
    }

    [Fact]
    public async Task Exposure_ShowsALoanInArrearsAsOverdue_EvenWithNoPenaltyCharged()
    {
        var service = Build(path => path switch
        {
            var p when p.Contains("clients/555/accounts") =>
                """{"loanAccounts":[{"id":901,"accountNo":"LN-000901","status":{"id":300,"value":"Active"}}]}""",
            var p when p.Contains("loans/901") => LoanInArrearsJson,
            _ => null
        });

        var result = await service.GetCustomerExposureAsync(555, "0123456789", "Test Farm Ltd");

        Assert.True(result.IsSuccess);
        Assert.Equal("Overdue", Assert.Single(result.Value.Facilities).Status);
    }
}

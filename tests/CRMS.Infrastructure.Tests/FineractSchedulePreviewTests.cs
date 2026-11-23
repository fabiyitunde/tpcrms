using System.Net;
using System.Text;
using System.Text.Json;
using CRMS.Domain.Interfaces;
using CRMS.Infrastructure.ExternalServices.FineractDirect;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CRMS.Infrastructure.Tests;

/// <summary>
/// The offer letter's schedule is the schedule of the loan booking creates: core banking calculates it
/// with the booking's own terms (taken from the product), for a real customer. Without a customer, or if
/// core banking refuses, the CRMS's estimate stands in and says so. The tenor is in months and is split
/// by the product's repayment interval, for the preview and the booking alike.
/// </summary>
public class FineractSchedulePreviewTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, string, HttpResponseMessage?> respond) : HttpMessageHandler
    {
        public List<(string Method, string Path, string? Body)> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.PathAndQuery;
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            Calls.Add((request.Method.Method, path, body));
            return respond(request, path) ?? Json("{}");
        }

        public JsonElement BodyOf(string pathFragment) =>
            JsonDocument.Parse(Calls.Last(c => c.Method == "POST" && c.Path.Contains(pathFragment)).Body!).RootElement;
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    // Quarterly repayments, equal principal, flat interest: none of it the old preview's hard-coded
    // monthly / equal-instalment / declining-balance.
    private const string QuarterlyProductJson =
        """[{"id":7,"name":"Agri Term","repaymentEvery":3,"numberOfRepayments":4,"annualInterestRate":12,"transactionProcessingStrategyId":2,"repaymentFrequencyType":{"value":"Months"},"interestRateFrequencyType":{"value":"Per year"},"amortizationType":{"value":"Equal principal payments"},"interestType":{"value":"Flat"}}]""";

    private const string WeeklyProductJson =
        """[{"id":7,"name":"Weekly","repaymentEvery":1,"numberOfRepayments":12,"repaymentFrequencyType":{"value":"Weeks"}}]""";

    /// <summary>The product as either read returns it: the catalogue (a list) or, for booking,
    /// GET loanproducts/{id} (the single product, the only read that carries charges).</summary>
    private static HttpResponseMessage Product(string path, string listJson) =>
        Json(path.Contains("loanproducts/") ? listJson.Trim()[1..^1] : listJson);

    private const string ScheduleJson =
        """{"totalPrincipalExpected":1200000,"totalInterestCharged":144000,"totalRepaymentExpected":1344000,"periods":[{"period":null},{"period":1,"dueDate":[2027,3,1],"principalDue":300000,"interestDue":36000,"totalDueForPeriod":336000}]}""";

    private static (FineractDirectService service, StubHandler handler) Build(
        Func<HttpRequestMessage, string, HttpResponseMessage?> respond)
    {
        var handler = new StubHandler(respond);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://core-banking.test/core_banking/api/") };
        return (new FineractDirectService(client, NullLogger<FineractDirectService>.Instance), handler);
    }

    private static LoanSchedulePreviewRequest Preview(long clientId = 555) =>
        new(clientId, ProductId: 7, Principal: 1_200_000m, TenorMonths: 12, InterestRatePerAnnum: 12m,
            ExpectedDisbursementDate: DateTime.Today.AddDays(14));

    [Fact]
    public async Task Preview_SendsTheBookingsTerms_ForTheCustomer_AndIsMarkedAsCoreBanking()
    {
        var (service, handler) = Build((_, path) => path switch
        {
            var p when p.Contains("loanproducts") => Json(QuarterlyProductJson),
            var p when p.Contains("calculateLoanSchedule") => Json(ScheduleJson),
            _ => null
        });

        var result = await service.PreviewBookingScheduleAsync(Preview());

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.FromCoreBanking);
        Assert.Equal("Fineract", result.Value.SourceLabel);
        Assert.Equal(144000m, result.Value.TotalInterest);

        var body = handler.BodyOf("calculateLoanSchedule");
        Assert.Equal(555, body.GetProperty("clientId").GetInt64());
        Assert.True(body.TryGetProperty("submittedOnDate", out _));
        Assert.Equal(12, body.GetProperty("loanTermFrequency").GetInt32());
        Assert.Equal(4, body.GetProperty("numberOfRepayments").GetInt32());
        Assert.Equal(3, body.GetProperty("repaymentEvery").GetInt32());
        Assert.Equal(0, body.GetProperty("amortizationType").GetInt32()); // equal principal, from the product
        Assert.Equal(1, body.GetProperty("interestType").GetInt32());     // flat, from the product
        Assert.Equal(2, body.GetProperty("transactionProcessingStrategyId").GetInt32());
    }

    [Fact]
    public async Task Preview_WithoutACoreBankingCustomer_IsTheCrmsEstimate_AndNotSent()
    {
        var (service, handler) = Build((_, path) => path.Contains("loanproducts") ? Json(QuarterlyProductJson) : null);

        var result = await service.PreviewBookingScheduleAsync(Preview(clientId: 0));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.FromCoreBanking);
        Assert.Equal("InHouse", result.Value.SourceLabel);
        Assert.Equal(4, result.Value.Installments.Count); // still the product's quarterly pattern
        Assert.DoesNotContain(handler.Calls, c => c.Path.Contains("calculateLoanSchedule"));
    }

    [Fact]
    public async Task Preview_RefusedByCoreBanking_FallsBackToTheEstimate_MarkedAsSuch()
    {
        var (service, _) = Build((_, path) => path switch
        {
            var p when p.Contains("loanproducts") => Json(QuarterlyProductJson),
            var p when p.Contains("calculateLoanSchedule") => Json("""{"errors":[]}""", HttpStatusCode.BadRequest),
            _ => null
        });

        var result = await service.PreviewBookingScheduleAsync(Preview());

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.FromCoreBanking);
    }

    [Fact]
    public async Task Booking_SplitsTheTenorInMonthsByTheProductsRepaymentInterval()
    {
        var (service, handler) = Build((req, path) => (req.Method.Method, path) switch
        {
            ("GET", var p) when p.Contains("loanproducts") => Product(p, QuarterlyProductJson),
            ("POST", var p) when p.EndsWith("loans") => Json("""{"loanId":901,"resourceId":901}"""),
            ("POST", var p) when p.Contains("command=") => Json("""{"loanId":901}"""),
            ("GET", var p) when p.Contains("loans/901") => Json("""{"id":901,"accountNo":"LN-000901","status":{"id":300,"value":"Active"}}"""),
            _ => null
        });

        var result = await service.BookApprovedLoanAsync(BookingRequest(tenorMonths: 12));

        Assert.True(result.IsSuccess);
        var body = JsonDocument.Parse(handler.Calls.First(c => c.Method == "POST" && c.Path.EndsWith("loans")).Body!).RootElement;
        Assert.Equal(12, body.GetProperty("loanTermFrequency").GetInt32());
        Assert.Equal(4, body.GetProperty("numberOfRepayments").GetInt32());
        Assert.Equal(3, body.GetProperty("repaymentEvery").GetInt32());
    }

    [Fact]
    public async Task Booking_ATenorThatDoesNotDivideIntoTheInterval_IsRefusedBeforeAnythingIsSent()
    {
        var (service, handler) = Build((_, path) => path.Contains("loanproducts") ? Product(path, QuarterlyProductJson) : null);

        var result = await service.BookApprovedLoanAsync(BookingRequest(tenorMonths: 10));

        Assert.True(result.IsFailure);
        Assert.Contains("does not divide", result.Error);
        Assert.DoesNotContain(handler.Calls, c => c.Method == "POST");
    }

    [Fact]
    public async Task Booking_AProductNotRepaidInMonths_IsRefused()
    {
        var (service, handler) = Build((_, path) => path.Contains("loanproducts") ? Product(path, WeeklyProductJson) : null);

        var result = await service.BookApprovedLoanAsync(BookingRequest(tenorMonths: 3));

        Assert.True(result.IsFailure);
        Assert.Contains("months", result.Error);
        Assert.DoesNotContain(handler.Calls, c => c.Method == "POST");
    }

    private static FineractLoanBookingRequest BookingRequest(int tenorMonths) => new(
        ClientId: 555,
        ProductId: 7,
        Principal: 1_200_000m,
        TenorMonths: tenorMonths,
        InterestRatePerAnnum: 12m,
        ValueDate: new DateTime(2026, 11, 19),
        RepaymentAccountNumber: "",
        DisburseToSavings: false,
        CreateRepaymentStandingInstruction: false);
}

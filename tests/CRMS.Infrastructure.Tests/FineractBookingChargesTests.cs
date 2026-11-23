using System.Net;
using System.Text;
using System.Text.Json;
using CRMS.Domain.Interfaces;
using CRMS.Infrastructure.ExternalServices.FineractDirect;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CRMS.Infrastructure.Tests;

/// <summary>
/// Core banking puts on a loan only the charges listed on POST /loans; it never copies the
/// product's own (LoanChargeAssembler.fromParsedJson). Loans booked from the CRMS therefore carried
/// no fees while the same product booked in core banking's own app did. These pin what booking now
/// sends.
/// </summary>
public class FineractBookingChargesTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        public Func<string, string, HttpResponseMessage?> Respond { get; init; } = (_, _) => null;
        public List<(string Method, string Path, string? Body)> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.PathAndQuery;
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            Calls.Add((request.Method.Method, path, body));
            return Respond(request.Method.Method, path) ?? Json("{}");
        }
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static string Charge(int id, string name, decimal amount, int timeTypeId, string timeType) =>
        $$$"""{"id":{{{id}}},"name":"{{{name}}}","amount":{{{amount}}},"chargeCalculationType":{"id":2,"value":"% Approved Amount"},"chargeTimeType":{"id":{{{timeTypeId}}},"value":"{{{timeType}}}"},"currency":{"code":"NGN"}}""";

    private static string Product(params string[] charges) =>
        $$"""{"id":7,"name":"RH-SHF Inputs","shortName":"RHSH","repaymentEvery":9,"numberOfRepayments":1,"repaymentFrequencyType":{"value":"Months"},"annualInterestRate":9,"transactionProcessingStrategyId":1,"charges":[{{string.Join(",", charges)}}]}""";

    private static (FineractDirectService Service, StubHandler Handler) Build(string productJson)
    {
        var handler = new StubHandler
        {
            Respond = (method, path) => (method, path) switch
            {
                ("GET", var p) when p.Contains("loanproducts/7") => Json(productJson),
                ("GET", var p) when p.Contains("loans?externalId") => Json("""{"totalFilteredRecords":0,"pageItems":[]}"""),
                ("GET", var p) when p.Contains("savingsaccounts/byexternalId") =>
                    Json("""{"id":2001,"accountNo":"SAV-001","clientId":555,"status":{"id":300,"value":"Active"}}"""),
                ("POST", var p) when p.EndsWith("loans") => Json("""{"loanId":901,"resourceId":901}"""),
                ("GET", var p) when p.Contains("loans/901") => Json("""{"id":901,"accountNo":"LN-000901","status":{"id":300,"value":"Active"}}"""),
                _ => null
            }
        };
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://core-banking.test/api/") };
        return (new FineractDirectService(client, NullLogger<FineractDirectService>.Instance), handler);
    }

    private static FineractLoanBookingRequest Request(IReadOnlyCollection<int>? excluded = null) => new(
        ClientId: 555, ProductId: 7, Principal: 61_185_500m, TenorMonths: 9, InterestRatePerAnnum: 9m,
        ValueDate: new DateTime(2026, 11, 20), RepaymentAccountNumber: "0123456789", DisburseToSavings: false,
        ExternalId: "RHSHF-2026-714036-C5", ExcludedChargeIds: excluded);

    private static JsonElement CreateBody(StubHandler handler)
    {
        var body = handler.Calls.Single(c => c.Method == "POST" && c.Path.EndsWith("loans")).Body!;
        return JsonDocument.Parse(body).RootElement;
    }

    [Fact]
    public async Task Product_charges_are_sent_on_the_create_request()
    {
        var (service, handler) = Build(Product(
            Charge(101, "Appraisal Fee", 1m, 1, "Disbursement"),
            Charge(102, "Monitoring Fee", 0.5m, 1, "Disbursement")));

        var result = await service.BookApprovedLoanAsync(Request());

        Assert.True(result.IsSuccess);
        var charges = CreateBody(handler).GetProperty("charges").EnumerateArray().ToList();
        Assert.Equal([101, 102], charges.Select(c => c.GetProperty("chargeId").GetInt32()));
        Assert.Equal(1m, charges[0].GetProperty("amount").GetDecimal());
        Assert.All(charges, c => Assert.False(c.TryGetProperty("dueDate", out _)));
    }

    [Fact]
    public async Task Booking_reads_the_single_product_not_the_catalogue()
    {
        var (service, handler) = Build(Product(Charge(101, "Appraisal Fee", 1m, 1, "Disbursement")));

        await service.BookApprovedLoanAsync(Request());

        Assert.Contains(handler.Calls, c => c.Method == "GET" && c.Path.Contains("loanproducts/7"));
        Assert.DoesNotContain(handler.Calls, c => c.Method == "GET" && c.Path.EndsWith("loanproducts"));
    }

    [Fact]
    public async Task Excluded_overdue_and_zero_charges_are_left_off()
    {
        var (service, handler) = Build(Product(
            Charge(101, "Appraisal Fee", 1m, 1, "Disbursement"),
            Charge(103, "Insurance", 4m, 1, "Disbursement"),
            Charge(104, "Late Payment Penalty", 2m, 9, "Overdue Fees"),
            Charge(105, "Unpriced Fee", 0m, 1, "Disbursement")));

        await service.BookApprovedLoanAsync(Request(excluded: [103]));

        var ids = CreateBody(handler).GetProperty("charges").EnumerateArray()
            .Select(c => c.GetProperty("chargeId").GetInt32());
        Assert.Equal([101], ids);
    }

    [Fact]
    public async Task A_specified_due_date_charge_falls_due_on_the_booking_date()
    {
        var (service, handler) = Build(Product(Charge(106, "Documentation Fee", 5000m, 2, "Specified due date")));

        await service.BookApprovedLoanAsync(Request());

        var charge = CreateBody(handler).GetProperty("charges").EnumerateArray().Single();
        Assert.Equal("20 November 2026", charge.GetProperty("dueDate").GetString());
    }

    [Fact]
    public async Task A_product_without_charges_sends_no_charges_key()
    {
        var (service, handler) = Build(Product());

        await service.BookApprovedLoanAsync(Request());

        Assert.False(CreateBody(handler).TryGetProperty("charges", out _));
    }
}

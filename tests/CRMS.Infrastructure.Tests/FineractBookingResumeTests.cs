using System.Net;
using System.Text;
using CRMS.Domain.Interfaces;
using CRMS.Infrastructure.ExternalServices.FineractDirect;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CRMS.Infrastructure.Tests;

/// <summary>
/// Covers the externalId contract core banking asked for (CRMS-Core-Banking-Interface-Brief §3.1): the
/// CRMS reference goes out on POST /loans, and a retry resumes the loan an earlier attempt created
/// instead of booking a second one for the same application. Also pins the partial-booking result
/// (created but not disbursed → Disbursed=false with the loanId kept), which the three track handlers
/// now branch on before recording that money moved.
/// </summary>
public class FineractBookingResumeTests
{
    private const string ExternalId = "RHSHF-2026-0042-C1";

    /// <summary>Routes requests by method + path fragment, and records every call in order.</summary>
    private sealed class RouteStubHandler : HttpMessageHandler
    {
        private readonly List<(string Method, string Path, string? Body)> _calls = [];
        public IReadOnlyList<(string Method, string Path, string? Body)> Calls => _calls;

        public Func<HttpRequestMessage, string, HttpResponseMessage?>? Respond { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.PathAndQuery;
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            _calls.Add((request.Method.Method, path, body));

            var response = Respond?.Invoke(request, path);
            if (response is not null) return response;

            return Json("{}");
        }

        public bool Called(string method, string pathFragment) =>
            _calls.Any(c => c.Method == method && c.Path.Contains(pathFragment, StringComparison.OrdinalIgnoreCase));

        public string? BodyOf(string method, string pathFragment) =>
            _calls.FirstOrDefault(c => c.Method == method && c.Path.Contains(pathFragment, StringComparison.OrdinalIgnoreCase)).Body;
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private const string ProductsJson =
        """[{"id":7,"name":"RH-SHF Inputs","shortName":"RHSH","repaymentEvery":1,"numberOfRepayments":6,"annualInterestRate":9.5,"transactionProcessingStrategyId":1}]""";

    private const string SavingsJson =
        """{"id":2001,"accountNo":"SAV-001","externalId":"0123456789","clientId":555,"status":{"id":300,"value":"Active"}}""";

    private static string LoanLookupJson(long id, int statusCode, string status, string accountNo = "LN-000900") =>
        $$"""{"totalFilteredRecords":1,"pageItems":[{"id":{{id}},"accountNo":"{{accountNo}}","externalId":"{{ExternalId}}","status":{"id":{{statusCode}},"value":"{{status}}"},"principal":5000000.00}]}""";

    private const string EmptyLookupJson = """{"totalFilteredRecords":0,"pageItems":[]}""";

    private static (FineractDirectService service, RouteStubHandler handler) Build(
        Func<HttpRequestMessage, string, HttpResponseMessage?> respond)
    {
        var handler = new RouteStubHandler { Respond = respond };
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://core-banking.test/fineract-provider/api/v1/") };
        return (new FineractDirectService(client, NullLogger<FineractDirectService>.Instance), handler);
    }

    private static FineractLoanBookingRequest Request() => new(
        ClientId: 555,
        ProductId: 7,
        Principal: 5_000_000m,
        TenorMonths: 6,
        InterestRatePerAnnum: 9.5m,
        ValueDate: new DateTime(2026, 10, 9),
        RepaymentAccountNumber: "0123456789",
        DisburseToSavings: false,
        CreateRepaymentStandingInstruction: true,
        ExternalId: ExternalId);

    /// <summary>The common pre-booking reads (product catalogue + repayment savings account).</summary>
    private static HttpResponseMessage? CommonReads(string path) => path switch
    {
        var p when p.Contains("loanproducts") => Json(ProductsJson),
        var p when p.Contains("savingsaccounts/byexternalId") => Json(SavingsJson),
        _ => null
    };

    [Fact]
    public async Task Create_SendsCrmsReferenceAsExternalId_AndDisburses()
    {
        var (service, handler) = Build((req, path) => CommonReads(path) ?? (req.Method.Method, path) switch
        {
            ("GET", var p) when p.Contains("loans?externalId") => Json(EmptyLookupJson),
            ("POST", var p) when p.EndsWith("loans") => Json("""{"loanId":901,"resourceId":901,"clientId":555}"""),
            ("POST", var p) when p.Contains("command=approve") => Json("""{"loanId":901}"""),
            ("POST", var p) when p.Contains("command=disburse") => Json("""{"loanId":901}"""),
            ("GET", var p) when p.Contains("loans/901") => Json("""{"id":901,"accountNo":"LN-000901","status":{"id":300,"value":"Active"}}"""),
            _ => null
        });

        var result = await service.BookApprovedLoanAsync(Request());

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Disbursed);
        Assert.False(result.Value.Resumed);
        Assert.Equal(901, result.Value.LoanId);
        Assert.Equal("LN-000901", result.Value.LoanAccountNumber);
        Assert.Contains($"\"externalId\":\"{ExternalId}\"", handler.BodyOf("POST", "loans")!);
    }

    [Fact]
    public async Task ApproveFails_ReportsNotDisbursed_AndKeepsTheLoanId()
    {
        var (service, _) = Build((req, path) => CommonReads(path) ?? (req.Method.Method, path) switch
        {
            ("GET", var p) when p.Contains("loans?externalId") => Json(EmptyLookupJson),
            ("POST", var p) when p.EndsWith("loans") => Json("""{"loanId":901,"resourceId":901}"""),
            ("POST", var p) when p.Contains("command=approve") => Json("""{"errors":[{"developerMessage":"approval denied"}]}""", HttpStatusCode.Forbidden),
            _ => null
        });

        var result = await service.BookApprovedLoanAsync(Request());

        // Success carrying a partial booking — that is the contract the callers branch on.
        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Booked);
        Assert.False(result.Value.Approved);
        Assert.False(result.Value.Disbursed);
        Assert.Equal(901, result.Value.LoanId);
    }

    [Fact]
    public async Task DisburseFails_ReportsNotDisbursed_AndKeepsTheLoanId()
    {
        var (service, _) = Build((req, path) => CommonReads(path) ?? (req.Method.Method, path) switch
        {
            ("GET", var p) when p.Contains("loans?externalId") => Json(EmptyLookupJson),
            ("POST", var p) when p.EndsWith("loans") => Json("""{"loanId":901,"resourceId":901}"""),
            ("POST", var p) when p.Contains("command=approve") => Json("""{"loanId":901}"""),
            ("POST", var p) when p.Contains("command=disburse") => Json("""{"errors":[{"developerMessage":"insufficient funds in office"}]}""", HttpStatusCode.Forbidden),
            _ => null
        });

        var result = await service.BookApprovedLoanAsync(Request());

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Approved);
        Assert.False(result.Value.Disbursed);
        Assert.Equal(901, result.Value.LoanId);
        Assert.Equal("", result.Value.LoanAccountNumber);
    }

    [Fact]
    public async Task Retry_PendingLoanFoundByExternalId_IsApprovedAndDisbursed_NotRecreated()
    {
        var (service, handler) = Build((req, path) => CommonReads(path) ?? (req.Method.Method, path) switch
        {
            ("GET", var p) when p.Contains("loans?externalId") => Json(LoanLookupJson(901, 100, "Submitted and pending approval")),
            ("POST", var p) when p.Contains("command=approve") => Json("""{"loanId":901}"""),
            ("POST", var p) when p.Contains("command=disburse") => Json("""{"loanId":901}"""),
            ("GET", var p) when p.Contains("loans/901") => Json("""{"id":901,"accountNo":"LN-000901","status":{"id":300,"value":"Active"}}"""),
            _ => null
        });

        var result = await service.BookApprovedLoanAsync(Request());

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Disbursed);
        Assert.True(result.Value.Resumed);
        Assert.Equal(901, result.Value.LoanId);
        Assert.DoesNotContain(handler.Calls, c => c.Method == "POST" && c.Path.EndsWith("loans"));
        Assert.True(handler.Called("POST", "command=approve"));
        Assert.True(handler.Called("POST", "command=disburse"));
    }

    [Fact]
    public async Task Retry_ApprovedLoanFoundByExternalId_IsOnlyDisbursed()
    {
        var (service, handler) = Build((req, path) => CommonReads(path) ?? (req.Method.Method, path) switch
        {
            ("GET", var p) when p.Contains("loans?externalId") => Json(LoanLookupJson(901, 200, "Approved")),
            ("POST", var p) when p.Contains("command=disburse") => Json("""{"loanId":901}"""),
            ("GET", var p) when p.Contains("loans/901") => Json("""{"id":901,"accountNo":"LN-000901","status":{"id":300,"value":"Active"}}"""),
            _ => null
        });

        var result = await service.BookApprovedLoanAsync(Request());

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Disbursed);
        Assert.True(result.Value.Resumed);
        Assert.False(handler.Called("POST", "command=approve"));
        Assert.True(handler.Called("POST", "command=disburse"));
    }

    [Fact]
    public async Task Retry_AlreadyActiveLoan_ReportsDisbursed_WithoutTouchingIt()
    {
        var (service, handler) = Build((req, path) => CommonReads(path) ?? (req.Method.Method, path) switch
        {
            ("GET", var p) when p.Contains("loans?externalId") => Json(LoanLookupJson(901, 300, "Active", "LN-000901")),
            _ => null
        });

        var result = await service.BookApprovedLoanAsync(Request());

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Disbursed);
        Assert.True(result.Value.Resumed);
        Assert.Equal("LN-000901", result.Value.LoanAccountNumber);
        Assert.DoesNotContain(handler.Calls, c => c.Method == "POST");
    }

    [Fact]
    public async Task Retry_RejectedLoan_FailsLoudly_AndDoesNotBookASecondLoan()
    {
        var (service, handler) = Build((req, path) => CommonReads(path) ?? (req.Method.Method, path) switch
        {
            ("GET", var p) when p.Contains("loans?externalId") => Json(LoanLookupJson(901, 500, "Rejected")),
            _ => null
        });

        var result = await service.BookApprovedLoanAsync(Request());

        Assert.True(result.IsFailure);
        Assert.Contains("Rejected", result.Error);
        Assert.DoesNotContain(handler.Calls, c => c.Method == "POST");
    }

    [Fact]
    public async Task DuplicateExternalIdOnCreate_ResumesTheExistingLoan()
    {
        // The probe misses the loan (lookup itself errors), so the create is attempted and core banking's
        // uniqueness constraint catches it — the path that actually guarantees no duplicate loan.
        var lookupCalls = 0;
        var (service, handler) = Build((req, path) => CommonReads(path) ?? (req.Method.Method, path) switch
        {
            ("GET", var p) when p.Contains("loans?externalId") =>
                ++lookupCalls == 1
                    ? Json("{}", HttpStatusCode.InternalServerError)
                    : Json(LoanLookupJson(901, 200, "Approved")),
            ("POST", var p) when p.EndsWith("loans") =>
                Json("""{"errors":[{"developerMessage":"Loan with externalId 'RHSHF-2026-0042-C1' already exists"}]}""", HttpStatusCode.Forbidden),
            ("POST", var p) when p.Contains("command=disburse") => Json("""{"loanId":901}"""),
            ("GET", var p) when p.Contains("loans/901") => Json("""{"id":901,"accountNo":"LN-000901","status":{"id":300,"value":"Active"}}"""),
            _ => null
        });

        var result = await service.BookApprovedLoanAsync(Request());

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Disbursed);
        Assert.True(result.Value.Resumed);
        Assert.Equal(901, result.Value.LoanId);
        Assert.Equal(1, handler.Calls.Count(c => c.Method == "POST" && c.Path.EndsWith("loans")));
    }

    [Fact]
    public async Task BookingWithoutAnExternalId_SkipsTheLookupEntirely()
    {
        var (service, handler) = Build((req, path) => CommonReads(path) ?? (req.Method.Method, path) switch
        {
            ("POST", var p) when p.EndsWith("loans") => Json("""{"loanId":901,"resourceId":901}"""),
            ("POST", var p) when p.Contains("command=approve") => Json("""{"loanId":901}"""),
            ("POST", var p) when p.Contains("command=disburse") => Json("""{"loanId":901}"""),
            ("GET", var p) when p.Contains("loans/901") => Json("""{"id":901,"accountNo":"LN-000901","status":{"id":300,"value":"Active"}}"""),
            _ => null
        });

        var result = await service.BookApprovedLoanAsync(Request() with { ExternalId = null });

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Disbursed);
        Assert.False(handler.Called("GET", "loans?externalId"));
        Assert.DoesNotContain("externalId", handler.BodyOf("POST", "loans")!);
    }

    [Fact]
    public async Task Mock_BookingTheSameReferenceTwice_ReturnsTheSameLoan()
    {
        var mock = new MockFineractDirectService(NullLogger<MockFineractDirectService>.Instance);
        var request = Request() with { ExternalId = $"MOCK-{Guid.NewGuid()}" };

        var first = await mock.BookApprovedLoanAsync(request);
        var second = await mock.BookApprovedLoanAsync(request);

        Assert.True(first.IsSuccess && second.IsSuccess);
        Assert.Equal(first.Value.LoanId, second.Value.LoanId);
        Assert.False(first.Value.Resumed);
        Assert.True(second.Value.Resumed);
    }
}

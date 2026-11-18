using System.Net;
using CRMS.Infrastructure.ExternalServices.SmartComply;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CRMS.Infrastructure.Tests;

/// <summary>
/// The Diagnostics individual credit probe, against a stubbed HTTP handler: it hits the right
/// endpoint per bureau, reads the body through the same parser the loan checks use, and lists the
/// response fields the mapping drops.
/// </summary>
public class SmartComplyIndividualProbeTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public string Body { get; set; } = "";
        public List<string> Paths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Paths.Add(request.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent(Body, System.Text.Encoding.UTF8, "application/json") });
        }
    }

    private static (SmartComplyProvider Provider, StubHandler Handler) Build()
    {
        var handler = new StubHandler();
        var settings = Options.Create(new SmartComplySettings { BaseUrl = "https://smartcomply.test", ApiKey = "k", UseMock = false });
        return (new SmartComplyProvider(new HttpClient(handler), settings, NullLogger<SmartComplyProvider>.Instance), handler);
    }

    private const string ReportBody = """
        {"status":"success","message":"ok","data":{
          "_id":"abc","bvn":"22222222222","name":"ADA OBI","gender":"Female","dateOfBirth":"1990-01-01",
          "score":{"totalNoOfLoans":3,"totalNoOfActiveLoans":2,"totalOutstanding":"150000.50","totalOverdue":0,
                   "maxNoOfDays":12,"crcReportOrderNumber":"R-1",
                   "loanPerformance":[{"loanProvider":"BANK A","loanAmount":100000,"outstandingBalance":50000,"mysteryField":1}]},
          "brandNewTopLevel":"x"}}
        """;

    [Theory]
    [InlineData("crc", "report", "/api/onboarding/individual/crc_summary/")]
    [InlineData("first_central", "report", "/api/onboarding/individual/first_central_full/")]
    [InlineData("first_central", "score", "/api/onboarding/individual/credit_scores_first_central/")]
    [InlineData("credit_registry", "report", "/api/onboarding/individual/credit_registry_full/")]
    [InlineData("premium", "report", "/api/onboarding/individual/premium/")]
    public async Task Each_bureau_hits_its_own_endpoint(string bureau, string kind, string path)
    {
        var (provider, handler) = Build();
        handler.Body = ReportBody;

        await provider.ProbeIndividualCreditAsync(bureau, kind, "22222222222");

        Assert.Equal(path, Assert.Single(handler.Paths));
    }

    [Fact]
    public async Task A_report_is_read_as_the_loan_checks_read_it_and_dropped_fields_are_listed()
    {
        var (provider, handler) = Build();
        handler.Body = ReportBody;

        var probe = (await provider.ProbeIndividualCreditAsync("first_central", "report", "22222222222")).Value;

        Assert.Null(probe.ParseError);
        Assert.Equal("FirstCentral", probe.Bureau);
        Assert.Equal("ADA OBI", probe.Report!.Name);
        Assert.Equal(3, probe.Report.Summary.TotalNoOfLoans);
        Assert.Equal(150000.50m, probe.Report.Summary.TotalOutstanding);
        Assert.Equal("R-1", probe.Report.Summary.ReportOrderNumber);
        Assert.Equal(50000m, Assert.Single(probe.Report.LoanPerformance).OutstandingBalance);
        Assert.Equal(["data.brandNewTopLevel", "data.score.loanPerformance[].mysteryField"], probe.UnreadFields);
    }

    // Layout of a real first_central_full response (captured 2026-11-17 against the live API),
    // with the personal details replaced and the figures made non-zero so each mapping is tested.
    private const string FirstCentralBody = """
        {"status":"success","message":"First central credit report fetched successfully","data":{
          "bvn":"22222222222","name":"ADA  OBI","email":"","phone":"08000000000",
          "score":{"rating":"0","hasLoans":"Yes","amount_arrear":"12500.75","total_accounts":"5",
                   "total_judgements":"0","total_dishonoured":"0","total_account_arrear":"1",
                   "total_judgement_amount":"0","total_outstanding_debt":"340000.00",
                   "total_monthly_instalment":"28000.00","total_accounts_bad_condition":"2",
                   "total_accounts_good_condition":"3"},
          "gender":"Male","address":"1 TEST STREET","employer":"TEST LTD","dateOfBirth":"1985-03-04",
          "nationality":"Nigeria","passport_no":"","searchedDate":"2026-11-17 10:15:00","marital_status":"",
          "has_credit_history":true,
          "credit_account_rating":{"NoOfRetailAccountsGood":3,"NoOfRetailAccountsBad":2},
          "credit_account_summary":{"TotalAccounts":"5","TotalOutstandingdebt":"340000.00","Amountarrear":"12500.75"}}}
        """;

    [Fact]
    public async Task A_First_Central_report_is_read_from_its_own_field_names()
    {
        var (provider, handler) = Build();
        handler.Body = FirstCentralBody;

        var probe = (await provider.ProbeIndividualCreditAsync("first_central", "report", "22222222222")).Value;
        var s = probe.Report!.Summary;

        Assert.Null(probe.ParseError);
        Assert.Equal("ADA  OBI", probe.Report.Name);
        Assert.Equal("1985-03-04", probe.Report.DateOfBirth);
        Assert.Equal(5, s.TotalNoOfLoans);
        Assert.Equal(3, s.TotalNoOfPerformingLoans);
        Assert.Equal(2, s.TotalNoOfDelinquentFacilities);   // larger of bad-condition (2) and in-arrears (1)
        Assert.Equal(340000.00m, s.TotalOutstanding);
        Assert.Equal(12500.75m, s.TotalOverdue);
        Assert.Equal(28000.00m, s.TotalMonthlyInstallment);
        Assert.Empty(probe.Report.LoanPerformance);         // First Central sends no per-loan rows
        Assert.DoesNotContain(probe.UnreadFields, f => f.StartsWith("data.score."));
    }

    [Fact]
    public async Task A_CRC_report_is_not_overridden_by_First_Central_names()
    {
        var (provider, handler) = Build();
        handler.Body = """
            {"status":"success","data":{"score":{"totalNoOfLoans":4,"totalOutstanding":900,
              "total_accounts":"99","total_outstanding_debt":"1"}}}
            """;

        var s = (await provider.ProbeIndividualCreditAsync("crc", "report", "22222222222")).Value.Report!.Summary;

        Assert.Equal(4, s.TotalNoOfLoans);
        Assert.Equal(900m, s.TotalOutstanding);
    }

    // Real no-record answers captured 2026-11-18: both business endpoints say HTTP 200 "success" for a
    // company they hold nothing on; only has_credit_history gives it away. RC numbers replaced.
    private const string CrcBusinessNoRecord = """
        {"status":"success","message":"CRC Business report retrieved successfully","data":{
          "has_credit_history":false,
          "message":"No credit history found for this registration number (RC0000000). The subject may not have any credit records in the bureau."}}
        """;

    private const string FirstCentralBusinessNoRecord = """
        {"status":"success","message":"First Central Business report retrieved successfully","data":{
          "SubjectList":[],"BusinessData":[],"DirectorInformation":[],
          "FacilityPerformanceSummary":[{"TotalAccounts":"0","TotalOutstandingdebt":"0.00","Amountarrear":"0.00"}],
          "HighestDelinquencyRating":[{"HighestDelinquencyRating":"-1"}],
          "EnquiryDetails":[{"SubscriberEnquiryResultID":"000000000","ProductID":46,"MatchingRate":0,"SubscriberEnquiryEngineID":"00000000"}],
          "_id":"","businessId":"","businessName":"","registrationNumber":"RC0000000","name":"","searchedDate":"2026-11-18 11:49:45",
          "score":{"totalNoOfDelinquentFacilities":0,"lastReportedDate":"","totalNoOfLoans":0,"totalNoOfInstitutions":0,
                   "totalNoOfActiveLoans":0,"totalBorrowed":"0","totalOutstanding":0,"totalOverdue":0,"maxNoOfDays":"0",
                   "totalNoOfClosedLoans":0,"reportOrderNumber":"","loanHistory":[],"totalMonthlyInstallment":0,"totalNoOfPerformingLoans":0},
          "has_credit_history":false,
          "message":"No credit history found. The company may not have any credit records in the bureau."}}
        """;

    [Theory]
    [InlineData(CrcBusinessNoRecord)]
    [InlineData(FirstCentralBusinessNoRecord)]
    public async Task A_business_success_with_no_credit_history_reads_as_not_found(string body)
    {
        var (provider, handler) = Build();
        handler.Body = body;

        var result = await provider.GetFirstCentralBusinessAsync("RC0000000");

        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No credit history found", result.Error);
    }

    [Fact]
    public async Task An_individual_success_with_no_credit_history_reads_as_not_found()
    {
        var (provider, handler) = Build();
        handler.Body = """{"status":"success","data":{"bvn":"22222222222","has_credit_history":false,"message":"No credit history found.","score":{}}}""";

        var result = await provider.GetCRCFullAsync("22222222222");

        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_report_with_credit_history_is_still_read()
    {
        var (provider, handler) = Build();
        handler.Body = """{"status":"success","data":{"has_credit_history":true,"score":{"totalNoOfLoans":3}}}""";

        var result = await provider.GetCRCFullAsync("22222222222");

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Summary.TotalNoOfLoans);
    }

    // Layout of a real CRC business response with credit history (captured 2026-11-18); identifiers
    // replaced, figures and formats kept — including "NGN   2,500,000" amounts and the month-object
    // payment history that company reports use.
    private const string CrcBusinessWithHistory = """
        {"status":"success","message":"CRC Business report retrieved successfully","data":{
          "_id":"x","name":"TEST LTD","rcNumber":"RC0000000","businessName":"TEST LTD","searchedDate":"2026-11-17 12:53:08",
          "has_credit_history":true,
          "score":{"maxNoOfDays":"7","totalOverdue":0,"totalBorrowed":2500193,"totalNoOfLoans":4,
            "totalOutstanding":2514891,"highestLoanAmount":2500000,"crcReportOrderNumber":"W-0000000000/2026",
            "totalNoOfActiveLoans":2,"totalNoOfClosedLoans":2,"totalNoOfInstitutions":4,
            "totalNoOfPerformingLoans":4,"totalNoOfDelinquentFacilities":0,"lastReportedDate":"17/Nov/2026",
            "loanPerformance":[
              {"type":"","status":"Open","loanCount":1,"loanAmount":"NGN   2,500,000","loanProvider":"TEST MICROFINANCE BANK",
               "accountNumber":"0000000001","lastUpdatedAt":"","overdueAmount":"0","performanceStatus":"Performing",
               "outstandingBalance":"2,514,891","noOfPerformingLoans":1,"noOfNonPerformingLoans":0}],
            "loanHistory":[
              {"type":"Others","reason":"Open","currency":"NGN","collateral":"Unsecured","loanAmount":"2500000","loanStatus":"A",
               "dateReported":"31-Dec-2020","loanProvider":"TEST MICROFINANCE BANK","maturityDate":"27-May-2023",
               "accountNumber":"0000000001","disbursedDate":"27-May-2020","overdueAmount":"0",
               "paymentHistory":{"MONTH1":{"month":"Dec-20","daysOverdue":"OK"},"MONTH2":{"month":"Nov-20","daysOverdue":"30"}},
               "lastPaymentDate":"","performanceStatus":"Performing","outstandingBalance":"2,514,891","numberOfOverdueDays":"0"}]}}}
        """;

    [Fact]
    public async Task A_company_report_keeps_its_per_loan_rows()
    {
        var (provider, handler) = Build();
        handler.Body = CrcBusinessWithHistory;

        var report = (await provider.GetCRCBusinessHistoryAsync("RC0000000")).Value;

        Assert.Equal(4, report.Summary.TotalNoOfLoans);
        Assert.Equal(7, report.Summary.MaxNoOfDays);
        var perf = Assert.Single(report.LoanPerformance!);
        Assert.Equal("TEST MICROFINANCE BANK", perf.LoanProvider);
        Assert.Equal(2_500_000m, perf.LoanAmount);              // "NGN   2,500,000"
        Assert.Equal(2_514_891m, perf.OutstandingBalance);      // "2,514,891"
        Assert.Equal("Performing", perf.PerformanceStatus);
        var hist = Assert.Single(report.LoanHistory!);
        Assert.Equal("A", hist.Status);                          // company reports use loanStatus
        Assert.Equal(new DateTime(2020, 5, 27), hist.DisbursedDate!.Value.Date);
        Assert.Equal("Dec-20: OK; Nov-20: 30", hist.PaymentHistory);
    }

    [Fact]
    public async Task An_individual_payment_profile_string_is_unchanged()
    {
        var (provider, handler) = Build();
        handler.Body = """{"status":"success","data":{"score":{"loanHistory":[{"loanProvider":"BANK A","paymentHistory":"NNNN0N0"}]}}}""";

        var report = (await provider.GetCRCFullAsync("22222222222")).Value;

        Assert.Equal("NNNN0N0", Assert.Single(report.LoanHistory).PaymentHistory);
    }

    [Fact]
    public async Task A_score_is_read_from_the_fico_block()
    {
        var (provider, handler) = Build();
        handler.Body = """{"status":"success","data":{"score":{"ficoScore":{"score":702,"rating":"GOOD"}}}}""";

        var probe = (await provider.ProbeIndividualCreditAsync("crc", "score", "22222222222")).Value;

        Assert.Equal(702, probe.Score!.Score);
        Assert.Equal("GOOD", probe.Score.Grade);
        Assert.Empty(probe.UnreadFields);
    }

    [Fact]
    public async Task A_refused_call_is_a_failure_carrying_the_bureau_answer()
    {
        var (provider, handler) = Build();
        handler.Status = HttpStatusCode.BadRequest;
        handler.Body = """{"status":"failed","message":"No credit data available"}""";

        var result = await provider.ProbeIndividualCreditAsync("first_central", "report", "22222222222");

        Assert.True(result.IsFailure);
        Assert.Contains("No credit data available", result.Error);
    }

    [Fact]
    public async Task An_unknown_bureau_is_refused_without_a_call()
    {
        var (provider, handler) = Build();

        var result = await provider.ProbeIndividualCreditAsync("premium", "score", "22222222222");

        Assert.True(result.IsFailure);
        Assert.Empty(handler.Paths);
    }
}

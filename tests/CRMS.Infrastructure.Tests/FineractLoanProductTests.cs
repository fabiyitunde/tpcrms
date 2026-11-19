using System.Net;
using System.Text;
using CRMS.Infrastructure.ExternalServices.FineractDirect;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CRMS.Infrastructure.Tests;

/// <summary>
/// Core banking leaves charges out of GET /loanproducts and returns them only on GET
/// /loanproducts/{id}. The RH-SHF letter, its double-financing check and the disbursement all read
/// the product's fees, so they go through the single-product read.
/// </summary>
public class FineractLoanProductTests
{
    private sealed class StubHandler(Func<string, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.PathAndQuery;
            Paths.Add(path);
            return Task.FromResult(respond(path));
        }
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private const string ListJson =
        """[{"id":7,"name":"RH-SHF Inputs","repaymentEvery":9,"numberOfRepayments":1,"annualInterestRate":9,"repaymentFrequencyType":{"value":"Months"}}]""";

    private const string SingleJson =
        """{"id":7,"name":"RH-SHF Inputs","repaymentEvery":9,"numberOfRepayments":1,"annualInterestRate":9,"repaymentFrequencyType":{"value":"Months"},"charges":[{"id":3,"name":"Management Fee","amount":1.0,"chargeCalculationType":{"value":"% Amount"},"chargeTimeType":{"value":"Disbursement"},"currency":{"code":"NGN"}}]}""";

    private static (FineractDirectService service, StubHandler handler) Build(Func<string, HttpResponseMessage> respond)
    {
        var handler = new StubHandler(respond);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://core-banking.test/core_banking/api/") };
        return (new FineractDirectService(client, NullLogger<FineractDirectService>.Instance), handler);
    }

    [Fact]
    public async Task SingleProductRead_CarriesTheChargesTheListLeavesOut()
    {
        var (service, handler) = Build(path => path.EndsWith("loanproducts/7") ? Json(SingleJson) : Json(ListJson));

        var list = await service.GetLoanProductsAsync(activeOnly: false);
        var single = await service.GetLoanProductAsync(7);

        Assert.Empty(Assert.Single(list.Value).ProductCharges);
        Assert.True(single.IsSuccess);
        var charge = Assert.Single(single.Value.ProductCharges);
        Assert.Equal("Management Fee", charge.Name);
        Assert.Equal("Disbursement", charge.TimeType);
        Assert.Contains(handler.Paths, p => p.EndsWith("loanproducts/7"));
    }

    [Fact]
    public async Task UnknownProduct_FailsAsNotFound()
    {
        var (service, _) = Build(_ => Json("""{"errors":[]}""", HttpStatusCode.NotFound));

        var result = await service.GetLoanProductAsync(42);

        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.Error);
    }
}

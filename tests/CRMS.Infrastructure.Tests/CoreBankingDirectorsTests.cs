using System.Net;
using System.Text;
using CRMS.Infrastructure.ExternalServices.CoreBanking;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CRMS.Infrastructure.Tests;

/// <summary>
/// Directors come from the account details an earlier lookup cached, so they must be the requested
/// customer's — not whichever account happened to be read first — and "not read yet" is a failure, not
/// an empty list.
/// </summary>
public class CoreBankingDirectorsTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            var body = path.EndsWith("1111111111") ? Details(101, "Alpha Farms Ltd", "Ade", "Alpha")
                : path.EndsWith("2222222222") ? Details(202, "Beta Agro Ltd", "Bola", "Beta")
                : "{}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }

        private static string Details(int clientId, string name, string first, string last) =>
            $$$"""{"clientType":"BUSINESS","clientDetails":{"id":{{{clientId}}},"fullName":"{{{name}}}"},"directors":[{"id":{{{clientId * 10}}},"firstname":"{{{first}}}","lastname":"{{{last}}}"}]}""";
    }

    private static CoreBankingService Build() =>
        new(new HttpClient(new StubHandler()) { BaseAddress = new Uri("https://gateway.test") },
            Options.Create(new CoreBankingSettings()), NullLogger<CoreBankingService>.Instance);

    [Fact]
    public async Task Directors_AreTheRequestedCustomers_NotTheFirstAccountRead()
    {
        var service = Build();
        await service.GetCustomerByAccountNumberAsync("1111111111");
        var beta = await service.GetCustomerByAccountNumberAsync("2222222222");

        var directors = await service.GetDirectorsAsync(beta.Value.CustomerId);

        Assert.True(directors.IsSuccess);
        Assert.Equal("2020", Assert.Single(directors.Value).DirectorId);
    }

    [Fact]
    public async Task Directors_OfACustomerNotYetRead_FailRatherThanComeBackEmpty()
    {
        var service = Build();
        await service.GetCustomerByAccountNumberAsync("1111111111");

        var directors = await service.GetDirectorsAsync("202");

        Assert.True(directors.IsFailure);
        Assert.Contains("not been read", directors.Error);
    }
}

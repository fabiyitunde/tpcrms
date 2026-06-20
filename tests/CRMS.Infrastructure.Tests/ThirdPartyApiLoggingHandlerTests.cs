using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using CRMS.Infrastructure.ExternalServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CRMS.Infrastructure.Tests;

public class ThirdPartyApiLoggingHandlerTests
{
    private sealed class CapturingLogger : ILogger<ThirdPartyApiLoggingHandler>
    {
        public List<string> Messages { get; } = [];
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => new Dummy();
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? ex, Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, ex));
        private sealed class Dummy : IDisposable { public void Dispose() { } }
    }

    private sealed class StubInnerHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"access_token\":\"super-secret-token\",\"ok\":true}", Encoding.UTF8, "application/json")
            });
    }

    private static (CapturingLogger log, HttpClient client) Build(bool enabled)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ThirdPartyApi:LogPayloads"] = enabled.ToString() })
            .Build();
        var log = new CapturingLogger();
        var handler = new ThirdPartyApiLoggingHandler(config, log) { InnerHandler = new StubInnerHandler() };
        return (log, new HttpClient(handler));
    }

    [Fact]
    public async Task Logs_Request_And_Response_Bodies()
    {
        var (log, client) = Build(enabled: true);

        await client.PostAsJsonAsync("https://api.example.com/verify", new { rcNumber = "RC123456" });

        Assert.Contains(log.Messages, m => m.Contains("RC123456"));            // request body logged
        Assert.Contains(log.Messages, m => m.Contains("\"ok\":true"));          // response body logged
        Assert.Contains(log.Messages, m => m.Contains("200"));                  // status logged
    }

    [Fact]
    public async Task Redacts_Secrets_In_Bodies()
    {
        var (log, client) = Build(enabled: true);

        await client.PostAsJsonAsync("https://api.example.com/token", new { password = "hunter2", clientId = "abc" });

        Assert.DoesNotContain(log.Messages, m => m.Contains("hunter2"));        // request secret redacted
        Assert.DoesNotContain(log.Messages, m => m.Contains("super-secret-token")); // response token redacted
        Assert.Contains(log.Messages, m => m.Contains("\"***\""));
    }

    [Fact]
    public async Task Disabled_LogsNothing()
    {
        var (log, client) = Build(enabled: false);

        await client.PostAsJsonAsync("https://api.example.com/verify", new { rcNumber = "RC123456" });

        Assert.Empty(log.Messages);
    }
}

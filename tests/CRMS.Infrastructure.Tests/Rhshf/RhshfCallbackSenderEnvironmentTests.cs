using CRMS.Infrastructure.BackgroundServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace CRMS.Infrastructure.Tests.Rhshf;

/// <summary>
/// Only Production hosts (TEST and production) send queued portal updates. A local run shares TEST's
/// database and outbox but signs with a placeholder secret, so it must leave sending to TEST (CRMSQA#2).
/// </summary>
public class RhshfCallbackSenderEnvironmentTests
{
    private sealed class Env(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "CRMS";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static IConfiguration Config(string? senderEnabled = null) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(senderEnabled is null ? [] : [new("Rhshf:CallbackSenderEnabled", senderEnabled)])
            .Build();

    [Theory]
    [InlineData("Production", true)]
    [InlineData("Local", false)]
    [InlineData("Development", false)]
    public void SendsOnlyOnProductionHosts(string environment, bool sends) =>
        Assert.Equal(sends, RhshfCallbackBackgroundService.IsEnabled(Config(), new Env(environment)));

    [Fact]
    public void ASettingOverridesTheEnvironment_EitherWay()
    {
        Assert.True(RhshfCallbackBackgroundService.IsEnabled(Config("true"), new Env("Local")));
        Assert.False(RhshfCallbackBackgroundService.IsEnabled(Config("false"), new Env("Production")));
    }
}

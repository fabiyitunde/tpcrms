using CRMS.Application.Notification.Interfaces;
using CRMS.Domain.Enums;
using CRMS.Infrastructure.ExternalServices.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit.Abstractions;

namespace CRMS.Infrastructure.Tests;

/// <summary>
/// Tests for the email notification channel.
///
/// MockEmailSender_ReturnsSuccess always runs (no external calls).
///
/// The live SES / SMTP tests actually SEND an email and are skipped unless configured.
/// Configuration (env vars override appsettings.test.json):
///   Email:FromAddress    — a verified SES identity, e.g. "noreply@bankofagriculture.com"
///   Email:TestRecipient  — where the test email is sent (REQUIRED to run the live tests)
///   Email:Region         — e.g. "eu-west-1"
///   Email:AccessKey / Email:SecretKey   — optional; omit to use the default AWS credential chain
///   Email:Smtp:Username / Email:Smtp:Password — REQUIRED to run the SMTP test (SES SMTP credentials)
///
/// Run only these:  dotnet test --filter "FullyQualifiedName~EmailSenderLiveIntegrationTests"
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "LiveAPI")]
public class EmailSenderLiveIntegrationTests
{
    private readonly ITestOutputHelper _output;
    private readonly IConfiguration _configuration;
    private readonly string _fromAddress;
    private readonly string _testRecipient;

    public EmailSenderLiveIntegrationTests(ITestOutputHelper output)
    {
        _output = output;

        _configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.test.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        _fromAddress = _configuration["Email:FromAddress"] ?? "";
        _testRecipient = _configuration["Email:TestRecipient"] ?? "";
    }

    private NotificationMessage BuildTestMessage(string via)
    {
        var stamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
        return new NotificationMessage(
            RecipientAddress: _testRecipient,
            Subject: $"CRMS email test ({via}) — {stamp}",
            Body: $"This is a plain-text test email sent from the CRMS {via} sender at {stamp}.",
            BodyHtml: $"<html><body><h2>CRMS Email Test</h2>" +
                      $"<p>This is an <strong>HTML</strong> test email sent via the CRMS <em>{via}</em> sender.</p>" +
                      $"<p>Timestamp: {stamp}</p></body></html>",
            Priority: NotificationPriority.Normal);
    }

    // ── Mock sender — always runs, no external dependency ─────────────────────

    [Fact]
    public async Task MockEmailSender_ReturnsSuccess()
    {
        var sender = new MockEmailSender(new Mock<ILogger<MockEmailSender>>().Object);

        Assert.Equal(NotificationChannel.Email, sender.Channel);

        var result = await sender.SendAsync(new NotificationMessage(
            "someone@example.com", "Subject", "Body text"));

        Assert.True(result.IsSuccess);
        Assert.False(string.IsNullOrEmpty(result.ExternalMessageId));
        _output.WriteLine($"MockEmailSender succeeded with id {result.ExternalMessageId} ✓");
    }

    // ── AWS SES API ───────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task SesEmailSender_SendsRealEmail()
    {
        Skip.If(string.IsNullOrEmpty(_fromAddress) || string.IsNullOrEmpty(_testRecipient),
            "Email:FromAddress and Email:TestRecipient must be configured to run the live SES test.");

        var sender = new SesEmailSender(
            _configuration, new Mock<ILogger<SesEmailSender>>().Object);

        _output.WriteLine($"Sending SES test email: {_fromAddress} -> {_testRecipient}");
        var result = await sender.SendAsync(BuildTestMessage("SES API"));

        _output.WriteLine($"IsSuccess={result.IsSuccess}, MessageId={result.ExternalMessageId}, " +
                          $"Error={result.ErrorMessage}, Provider={result.ProviderName}");

        Assert.True(result.IsSuccess,
            $"SES send failed: {result.ErrorMessage} | {result.ProviderResponse}");
        Assert.False(string.IsNullOrEmpty(result.ExternalMessageId), "Expected a SES MessageId");
        _output.WriteLine($"SES email sent ✓ (MessageId {result.ExternalMessageId})");

        sender.Dispose();
    }

    // ── SMTP (e.g. SES SMTP interface) ────────────────────────────────────────

    [SkippableFact]
    public async Task SmtpEmailSender_SendsRealEmail()
    {
        Skip.If(string.IsNullOrEmpty(_fromAddress) || string.IsNullOrEmpty(_testRecipient),
            "Email:FromAddress and Email:TestRecipient must be configured to run the live SMTP test.");
        Skip.If(string.IsNullOrEmpty(_configuration["Email:Smtp:Username"]),
            "Email:Smtp:Username/Password must be configured to run the live SMTP test.");

        var sender = new SmtpEmailSender(
            _configuration, new Mock<ILogger<SmtpEmailSender>>().Object);

        _output.WriteLine($"Sending SMTP test email: {_fromAddress} -> {_testRecipient}");
        var result = await sender.SendAsync(BuildTestMessage("SMTP"));

        _output.WriteLine($"IsSuccess={result.IsSuccess}, Error={result.ErrorMessage}, " +
                          $"Provider={result.ProviderName}, Response={result.ProviderResponse}");

        Assert.True(result.IsSuccess,
            $"SMTP send failed: {result.ErrorMessage} | {result.ProviderResponse}");
        _output.WriteLine("SMTP email sent ✓");
    }
}

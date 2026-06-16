using CRMS.Application.Notification.Interfaces;
using CRMS.Domain.Enums;
using CRMS.Infrastructure.ExternalServices.Notifications;
using CRMS.Infrastructure.Persistence;
using CRMS.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit.Abstractions;

namespace CRMS.Infrastructure.Tests;

/// <summary>
/// Renders the REAL seeded email templates (account created, password changed, password reset) with
/// sample data and sends them via AWS SES to your own inbox, so you can see how each looks when delivered.
///
/// Configure in appsettings.test.json (or env vars), then run:
///   dotnet test --filter "FullyQualifiedName~EmailTemplatePreviewTests"
///
/// Required:
///   Email:FromAddress    — verified SES identity
///   Email:TestRecipient  — your inbox (REQUIRED to run; otherwise skipped)
///   Email:Region, Email:AccessKey/SecretKey (or rely on the default AWS credential chain)
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "LiveAPI")]
public class EmailTemplatePreviewTests
{
    private readonly ITestOutputHelper _output;
    private readonly IConfiguration _configuration;
    private readonly string _recipient;

    public EmailTemplatePreviewTests(ITestOutputHelper output)
    {
        _output = output;
        _configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.test.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();
        _recipient = _configuration["Email:TestRecipient"] ?? "";
    }

    [SkippableFact]
    public async Task SendAllTemplates_ToMyInbox()
    {
        Skip.If(string.IsNullOrEmpty(_recipient),
            "Set Email:TestRecipient (and SES creds) in appsettings.test.json to run this preview.");

        // Seed the real templates into an in-memory DB (same seeder as production).
        var options = new DbContextOptionsBuilder<CRMSDbContext>()
            .UseInMemoryDatabase("TemplatePreview_" + Guid.NewGuid()).Options;
        using var context = new CRMSDbContext(options);
        await SeedData.SeedAsync(context, NullLogger.Instance);
        var templates = new NotificationTemplateRepository(context);

        var sender = new SesEmailSender(_configuration, new Mock<ILogger<SesEmailSender>>().Object);

        var sampleResetLink =
            "https://crms.bankofagriculture.com/reset-password?token=PREVIEW-TOKEN-EXAMPLE-1234567890";

        var cases = new (string Code, Dictionary<string, string> Vars)[]
        {
            ("ACCOUNT_CREATED", new()
            {
                ["RecipientName"] = "Jane Doe",
                ["LoginEmail"] = "jane.doe@bankofagriculture.com",
                ["TempPassword"] = "Welcome@1234"
            }),
            ("PASSWORD_CHANGED", new()
            {
                ["RecipientName"] = "Jane Doe",
                ["ChangedAt"] = DateTime.UtcNow.ToString("dd MMM yyyy HH:mm 'UTC'")
            }),
            ("PASSWORD_RESET", new()
            {
                ["RecipientName"] = "Jane Doe",
                ["ResetLink"] = sampleResetLink,
                ["ExpiryMinutes"] = "60"
            }),
        };

        foreach (var (code, vars) in cases)
        {
            var template = await templates.GetByCodeAsync(code, NotificationChannel.Email, "en");
            Assert.NotNull(template);

            var (subject, body, bodyHtml) = template!.Render(vars);
            var result = await sender.SendAsync(new NotificationMessage(
                RecipientAddress: _recipient,
                Subject: subject,
                Body: body,
                BodyHtml: bodyHtml));

            _output.WriteLine($"{code}: IsSuccess={result.IsSuccess}, MessageId={result.ExternalMessageId}, Error={result.ErrorMessage}");
            Assert.True(result.IsSuccess, $"{code} send failed: {result.ErrorMessage} | {result.ProviderResponse}");
        }

        sender.Dispose();
        _output.WriteLine($"Sent {cases.Length} template preview emails to {_recipient} — check your inbox.");
    }
}

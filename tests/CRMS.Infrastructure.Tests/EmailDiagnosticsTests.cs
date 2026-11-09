using CRMS.Application.Notification.Commands;
using CRMS.Application.Notification.Interfaces;
using CRMS.Application.Notification.Services;
using CRMS.Domain.Aggregates.Notification;
using CRMS.Domain.Enums;
using CRMS.Infrastructure.Persistence;
using CRMS.Infrastructure.Persistence.Repositories;
using CRMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CRMS.Infrastructure.Tests;

/// <summary>
/// Admin > Diagnostics > Email: a template goes to a chosen address through the real queue and
/// orchestrator, with a stand-in sender, and the administrator hears the provider's answer at once.
/// </summary>
public class EmailDiagnosticsTests
{
    private sealed class ScriptedSender : INotificationSender
    {
        public NotificationChannel Channel => NotificationChannel.Email;
        public NotificationSendResult Answer { get; set; } = new(true, "msg-42", "AmazonSES", "MessageId=msg-42");
        public List<NotificationMessage> Sent { get; } = [];
        public Task<NotificationSendResult> SendAsync(NotificationMessage message, CancellationToken ct = default)
        {
            Sent.Add(message);
            return Task.FromResult(Answer);
        }
    }

    private sealed class RecordingAudit : IOutboundEmailAudit
    {
        public List<OutboundEmailAuditEntry> Entries { get; } = [];
        public Task RecordAsync(OutboundEmailAuditEntry entry, CancellationToken ct = default) { Entries.Add(entry); return Task.CompletedTask; }
    }

    private sealed class FixedSenderInfo : IEmailSenderInfo
    {
        public EmailSenderInfoDto Get() => new("AWS SES", false, "noreply@bank.test", "Bank", "eu-west-1", "https://crms.bank.test");
    }

    private static (CRMSDbContext Db, SendDiagnosticEmailHandler Handler, ScriptedSender Sender, RecordingAudit Audit) Build(string name)
    {
        var db = new CRMSDbContext(new DbContextOptionsBuilder<CRMSDbContext>().UseInMemoryDatabase(name).Options);
        db.NotificationTemplates.Add(NotificationTemplate.Create(
            "PASSWORD_RESET", "Password Reset", "d", NotificationType.PasswordReset, NotificationChannel.Email,
            "Dear {{RecipientName}}, open {{ResetLink}} within {{ExpiryMinutes}} minutes.", Guid.Empty,
            subject: "Reset your password", bodyHtmlTemplate: "<a href=\"{{ResetLink}}\">{{RecipientName}}</a> {{ExpiryMinutes}} {{Reference}}").Value);
        db.SaveChanges();
        var sender = new ScriptedSender();
        var audit = new RecordingAudit();
        var queue = new NotificationRepository(db);
        var templates = new NotificationTemplateRepository(db);
        var orchestrator = new NotificationOrchestrator(queue, templates, [sender], db);
        return (db, new SendDiagnosticEmailHandler(templates, orchestrator, queue, new FixedSenderInfo(), audit), sender, audit);
    }

    [Fact]
    public async Task A_test_email_goes_through_the_real_sender_with_every_placeholder_filled()
    {
        var (db, handler, sender, audit) = Build(nameof(A_test_email_goes_through_the_real_sender_with_every_placeholder_filled));

        var result = await handler.Handle(new SendDiagnosticEmailCommand("ada@bank.test", "PASSWORD_RESET", Guid.NewGuid(), "Ada Admin"));

        Assert.True(result.IsSuccess, result.Error);
        var r = result.Data!;
        Assert.True(r.Accepted);
        Assert.Equal("Sent", r.Status);
        Assert.Equal("AmazonSES", r.Provider);
        Assert.Equal("msg-42", r.MessageId);
        Assert.Equal("[Test] Reset your password", r.Subject);
        Assert.Equal("ada@bank.test", r.ToAddress);

        var message = Assert.Single(sender.Sent);
        Assert.Equal("ada@bank.test", message.RecipientAddress);
        Assert.DoesNotContain("{{", message.Body);
        Assert.DoesNotContain("{{", message.BodyHtml!);
        Assert.Contains("Dear Ada Admin", message.Body);
        Assert.Contains("https://crms.bank.test/reset-password?token=diagnostic-test", message.Body);
        Assert.Contains("within 60 minutes", message.Body);
        Assert.Contains("TEST-0001", message.BodyHtml!);          // the generic Reference sample

        var row = await db.Notifications.SingleAsync();
        Assert.Equal(NotificationStatus.Sent, row.Status);        // visible under Outbound Email
        Assert.Equal("PASSWORD_RESET", row.TemplateCode);
        var entry = Assert.Single(audit.Entries);
        Assert.Equal(OutboundEmailAuditEntry.TestSent, entry.Event);
        Assert.Contains("accepted by AmazonSES", entry.Description);
    }

    [Fact]
    public async Task A_refusal_comes_back_with_the_providers_reason()
    {
        var (_, handler, sender, audit) = Build(nameof(A_refusal_comes_back_with_the_providers_reason));
        sender.Answer = new NotificationSendResult(false, ProviderName: "AmazonSES",
            ProviderResponse: "User: arn:aws:iam::1:user/crms-ses-smtp-user is not authorized to perform: ses:SendEmail",
            ErrorMessage: "SES error (Forbidden).", CanRetry: false);

        var result = await handler.Handle(new SendDiagnosticEmailCommand("ada@bank.test", "PASSWORD_RESET", Guid.NewGuid(), "Ada Admin"));

        Assert.True(result.IsSuccess, result.Error);               // the diagnostic itself worked; the send did not
        var r = result.Data!;
        Assert.False(r.Accepted);
        Assert.Equal("Failed", r.Status);
        Assert.Contains("ses:SendEmail", r.FailureReason);
        Assert.Null(r.NextRetryAt);
        Assert.Contains("not sent", Assert.Single(audit.Entries).Description);
    }

    [Fact]
    public async Task A_transient_refusal_says_when_the_system_will_try_again()
    {
        var (_, handler, sender, _) = Build(nameof(A_transient_refusal_says_when_the_system_will_try_again));
        sender.Answer = new NotificationSendResult(false, ProviderName: "AmazonSES", ErrorMessage: "SES throttled the request.", CanRetry: true);

        var r = (await handler.Handle(new SendDiagnosticEmailCommand("ada@bank.test", "PASSWORD_RESET", Guid.NewGuid(), "Ada"))).Data!;

        Assert.False(r.Accepted);
        Assert.Equal("Retry", r.Status);
        Assert.NotNull(r.NextRetryAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ada")]
    [InlineData("ada@bank")]
    [InlineData("ada@bank.test, bola@bank.test")]
    [InlineData("Ada <ada@bank.test>")]
    public async Task Only_one_plain_address_is_accepted(string address)
    {
        var (_, handler, sender, _) = Build(nameof(Only_one_plain_address_is_accepted) + address.Length);

        var result = await handler.Handle(new SendDiagnosticEmailCommand(address, "PASSWORD_RESET", Guid.NewGuid(), "Ada"));

        Assert.False(result.IsSuccess);
        Assert.Contains("one valid email address", result.Error);
        Assert.Empty(sender.Sent);
    }

    [Fact]
    public async Task An_unknown_or_inactive_template_is_refused_before_anything_is_sent()
    {
        var (db, handler, sender, _) = Build(nameof(An_unknown_or_inactive_template_is_refused_before_anything_is_sent));

        var unknown = await handler.Handle(new SendDiagnosticEmailCommand("ada@bank.test", "NO_SUCH_TEMPLATE", Guid.NewGuid(), "Ada"));
        var template = await db.NotificationTemplates.SingleAsync();
        template.Deactivate();
        await db.SaveChangesAsync();
        var inactive = await handler.Handle(new SendDiagnosticEmailCommand("ada@bank.test", "PASSWORD_RESET", Guid.NewGuid(), "Ada"));

        Assert.False(unknown.IsSuccess);
        Assert.False(inactive.IsSuccess);
        Assert.Contains("not found or is not active", inactive.Error);
        Assert.Empty(sender.Sent);
    }

    [Fact]
    public void The_sender_info_follows_the_same_rules_as_the_wiring()
    {
        static EmailSenderInfoDto Info(params (string Key, string? Value)[] settings) =>
            new EmailSenderInfo(new ConfigurationBuilder().AddInMemoryCollection(settings.ToDictionary(s => s.Key, s => s.Value)).Build()).Get();

        Assert.True(Info(("Email:UseMock", "true"), ("Email:FromAddress", "x@y.test")).IsMock);
        Assert.True(Info(("Other:Thing", "1")).IsMock);                 // no Email section at all
        var ses = Info(("Email:UseMock", "false"), ("Email:Provider", "Ses"), ("Email:FromAddress", "noreply@bank.test"), ("Email:FromName", "Bank"), ("Email:Region", "eu-west-1"), ("WebsiteUrl", "https://crms.bank.test/"));
        Assert.False(ses.IsMock);
        Assert.Equal("AWS SES", ses.Provider);
        Assert.Equal("eu-west-1", ses.Region);
        Assert.Equal("https://crms.bank.test/", ses.SiteUrl);
        var smtp = Info(("Email:UseMock", "false"), ("Email:Provider", "Smtp"), ("Email:FromAddress", "noreply@bank.test"), ("Email:Smtp:Host", "email-smtp.eu-west-1.amazonaws.com"));
        Assert.Equal("SMTP", smtp.Provider);
        Assert.Equal("email-smtp.eu-west-1.amazonaws.com", smtp.Region);
    }

    [Fact]
    public void Placeholders_are_found_in_both_spellings_and_each_gets_a_sample()
    {
        var names = EmailDiagnosticSamples.Placeholders("Hi {{RecipientName}}", "{{$Amount}} due {{DueDate}} ref {{Reference}} {{Whatever}}", null);
        Assert.Equal(["RecipientName", "Amount", "DueDate", "Reference", "Whatever"], names);

        var values = EmailDiagnosticSamples.For(names, "ada@bank.test", "Ada", null);
        Assert.Equal("Ada", values["RecipientName"]);
        Assert.Equal("1,000,000.00", values["Amount"]);
        Assert.Equal("TEST-0001", values["Reference"]);
        Assert.Equal("[Whatever]", values["Whatever"]);
        Assert.All(values.Values, v => Assert.False(string.IsNullOrWhiteSpace(v)));
    }
}

using System.Net.Mail;
using System.Text.RegularExpressions;
using CRMS.Application.Common;
using CRMS.Application.Notification.Interfaces;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Notification.Commands;

// Admin > Diagnostics > Email: send any of the system's email templates to an address of the
// administrator's choosing, through the real sender, and hear back at once whether the provider
// took it. The email goes through the ordinary queue, so it also appears under Outbound Email.

/// <summary>How the email channel is wired on this host. Read from configuration by the host.</summary>
public interface IEmailSenderInfo
{
    EmailSenderInfoDto Get();
}

public record EmailSenderInfoDto(
    string Provider,        // "AWS SES", "SMTP" or "Mock"
    bool IsMock,            // nothing leaves the server
    string FromAddress,
    string? FromName,
    string? Region,
    string? SiteUrl);       // base for links placed in test messages

public record EmailTemplateChoiceDto(string Code, string Name, string Subject, IReadOnlyList<string> Placeholders);

public record EmailDiagnosticsDto(EmailSenderInfoDto Sender, IReadOnlyList<EmailTemplateChoiceDto> Templates);

public record GetEmailDiagnosticsQuery : IRequest<ApplicationResult<EmailDiagnosticsDto>>;

public class GetEmailDiagnosticsHandler : IRequestHandler<GetEmailDiagnosticsQuery, ApplicationResult<EmailDiagnosticsDto>>
{
    private readonly INotificationTemplateRepository _templates;
    private readonly IEmailSenderInfo _sender;

    public GetEmailDiagnosticsHandler(INotificationTemplateRepository templates, IEmailSenderInfo sender)
    {
        _templates = templates;
        _sender = sender;
    }

    public async Task<ApplicationResult<EmailDiagnosticsDto>> Handle(GetEmailDiagnosticsQuery request, CancellationToken ct = default)
    {
        var all = await _templates.GetAllActiveAsync(ct);
        var choices = all
            .Where(t => t.Channel == NotificationChannel.Email)
            .OrderBy(t => t.Code, StringComparer.Ordinal)
            .Select(t => new EmailTemplateChoiceDto(t.Code, t.Name, t.Subject ?? string.Empty,
                EmailDiagnosticSamples.Placeholders(t.Subject, t.BodyTemplate, t.BodyHtmlTemplate)))
            .ToList();
        return ApplicationResult<EmailDiagnosticsDto>.Success(new EmailDiagnosticsDto(_sender.Get(), choices));
    }
}

public record DiagnosticEmailResultDto(
    Guid NotificationId,
    bool Accepted,
    string Status,
    string Subject,
    string ToAddress,
    string? Provider,
    string? MessageId,
    string? FailureReason,
    DateTime? NextRetryAt,
    long ElapsedMs);

public record SendDiagnosticEmailCommand(string ToAddress, string TemplateCode, Guid UserId, string UserName)
    : IRequest<ApplicationResult<DiagnosticEmailResultDto>>;

public class SendDiagnosticEmailHandler : IRequestHandler<SendDiagnosticEmailCommand, ApplicationResult<DiagnosticEmailResultDto>>
{
    public const string SubjectPrefix = "[Test] ";

    private readonly INotificationTemplateRepository _templates;
    private readonly INotificationService _notifications;
    private readonly INotificationRepository _queue;
    private readonly IEmailSenderInfo _sender;
    private readonly IOutboundEmailAudit _audit;

    public SendDiagnosticEmailHandler(
        INotificationTemplateRepository templates, INotificationService notifications,
        INotificationRepository queue, IEmailSenderInfo sender, IOutboundEmailAudit audit)
    {
        _templates = templates;
        _notifications = notifications;
        _queue = queue;
        _sender = sender;
        _audit = audit;
    }

    public async Task<ApplicationResult<DiagnosticEmailResultDto>> Handle(SendDiagnosticEmailCommand request, CancellationToken ct = default)
    {
        var to = (request.ToAddress ?? string.Empty).Trim();
        if (!LooksLikeAnAddress(to))
            return ApplicationResult<DiagnosticEmailResultDto>.Failure("Enter one valid email address, such as name@example.com.");

        var template = await _templates.GetByCodeAsync(request.TemplateCode ?? string.Empty, NotificationChannel.Email, "en", ct);
        if (template is null)
            return ApplicationResult<DiagnosticEmailResultDto>.Failure("That template was not found or is not active.");

        var variables = EmailDiagnosticSamples.For(
            EmailDiagnosticSamples.Placeholders(template.Subject, template.BodyTemplate, template.BodyHtmlTemplate),
            to, request.UserName, _sender.Get().SiteUrl);

        var clock = System.Diagnostics.Stopwatch.StartNew();
        var id = await _notifications.SendEmailNowAsync(to, request.UserName, template.Code, variables, SubjectPrefix, request.UserId, ct);
        clock.Stop();

        var row = await _queue.GetByIdAsync(id, ct)
            ?? throw new InvalidOperationException("The test email was sent but its record could not be read back.");
        var accepted = row.Status is NotificationStatus.Sent or NotificationStatus.Delivered;

        await _audit.RecordAsync(new OutboundEmailAuditEntry(
            OutboundEmailAuditEntry.TestSent, row.Id, to,
            $"Diagnostic email \"{row.Subject}\" ({template.Code}) to {to} by {request.UserName}: {(accepted ? "accepted by " + row.ProviderName : "not sent: " + row.FailureReason)}.",
            request.UserId, request.UserName), ct);

        return ApplicationResult<DiagnosticEmailResultDto>.Success(new DiagnosticEmailResultDto(
            row.Id, accepted, row.Status.ToString(), row.Subject, to,
            row.ProviderName, row.ExternalMessageId, row.FailureReason, row.NextRetryAt, clock.ElapsedMilliseconds));
    }

    private static bool LooksLikeAnAddress(string value)
    {
        if (value.Length is 0 or > 256 || value.Any(char.IsWhiteSpace) || value.Count(c => c == '@') != 1) return false;
        try
        {
            var parsed = new MailAddress(value);
            return parsed.Address == value && parsed.Host.Contains('.');
        }
        catch (FormatException) { return false; }
    }
}

/// <summary>Sample values for a template's placeholders, so a test message reads like a real one.</summary>
public static class EmailDiagnosticSamples
{
    private static readonly Regex Placeholder = new(@"\{\{\$?([A-Za-z0-9_]+)\}\}", RegexOptions.Compiled);

    public static IReadOnlyList<string> Placeholders(params string?[] texts) =>
        texts.Where(t => t is not null)
            .SelectMany(t => Placeholder.Matches(t!).Select(m => m.Groups[1].Value))
            .Distinct(StringComparer.Ordinal)
            .ToList();

    public static Dictionary<string, string> For(IEnumerable<string> placeholders, string toAddress, string userName, string? siteUrl)
    {
        var site = string.IsNullOrWhiteSpace(siteUrl) ? "https://crms.example.com" : siteUrl.TrimEnd('/');
        var now = DateTime.UtcNow;
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in placeholders)
            result[name] = Sample(name, toAddress, userName, site, now);
        return result;
    }

    private static string Sample(string name, string toAddress, string userName, string site, DateTime now)
    {
        var n = name.ToLowerInvariant();
        if (n is "recipientname" or "name" or "fullname" or "username") return string.IsNullOrWhiteSpace(userName) ? "Test Recipient" : userName;
        if (n is "loginemail" or "email" or "recipientemail") return toAddress;
        if (n == "temppassword") return "Test-Only-Password1";
        if (n == "resetlink") return $"{site}/reset-password?token=diagnostic-test";
        if (n == "setuplink") return $"{site}/partner/setup/diagnostic-test";
        if (n.EndsWith("link") || n.EndsWith("url")) return $"{site}/admin/diagnostics";
        if (n == "issues") return "API key and callback signing secret";
        if (n == "expiryminutes") return "60";
        if (n == "expiryhours") return "24";
        if (n is "expiresat" or "deadline" or "expirydate" or "duedate") return now.AddDays(1).ToString("dd MMM yyyy HH:mm");
        if (n.EndsWith("at") || n.EndsWith("date") || n.EndsWith("time") || n == "timestamp") return now.ToString("dd MMM yyyy HH:mm");
        if (n.Contains("amount") || n.Contains("value") || n.Contains("total") || n.Contains("balance")) return "1,000,000.00";
        if (n.Contains("reference") || n.Contains("number") || n.EndsWith("id") || n.EndsWith("no")) return "TEST-0001";
        if (n.Contains("days")) return "7";
        if (n.Contains("count")) return "3";
        return $"[{name}]";
    }
}

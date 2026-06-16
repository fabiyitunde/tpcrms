using System.Net;
using System.Net.Mail;
using CRMS.Application.Notification.Interfaces;
using CRMS.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CRMS.Infrastructure.ExternalServices.Notifications;

/// <summary>
/// SMTP implementation of the email notification channel. Works with the SES SMTP interface
/// (host email-smtp.&lt;region&gt;.amazonaws.com using SES SMTP credentials) or any generic SMTP relay.
/// Selected via Email:Provider = "Smtp". From identity is shared with the SES sender (Email:FromAddress/FromName);
/// connection settings live under Email:Smtp.
/// </summary>
public class SmtpEmailSender : INotificationSender
{
    private readonly string _fromAddress;
    private readonly string? _fromName;
    private readonly string _host;
    private readonly int _port;
    private readonly string? _username;
    private readonly string? _password;
    private readonly bool _enableSsl;
    private readonly ILogger<SmtpEmailSender> _logger;

    public NotificationChannel Channel => NotificationChannel.Email;

    public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
    {
        _logger = logger;

        var email = configuration.GetSection("Email");
        _fromAddress = email["FromAddress"]
            ?? throw new InvalidOperationException("Email:FromAddress is required for the SMTP email sender.");
        _fromName = email["FromName"];

        var smtp = email.GetSection("Smtp");
        _host = smtp["Host"]
            ?? throw new InvalidOperationException("Email:Smtp:Host is required for the SMTP email sender.");
        _port = smtp.GetValue<int?>("Port") ?? 587;
        _username = smtp["Username"];
        _password = smtp["Password"];
        _enableSsl = smtp.GetValue<bool?>("EnableSsl") ?? true; // STARTTLS on 587

        _logger.LogInformation(
            "SMTP email sender initialized. From: {From}, Host: {Host}:{Port}, SSL: {Ssl}",
            string.IsNullOrWhiteSpace(_fromName) ? _fromAddress : $"{_fromName} <{_fromAddress}>",
            _host, _port, _enableSsl);
    }

    public async Task<NotificationSendResult> SendAsync(NotificationMessage message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(message.RecipientAddress))
            return new NotificationSendResult(
                IsSuccess: false, ProviderName: "Smtp",
                ErrorMessage: "Recipient email address is empty.", CanRetry: false);

        using var mail = new MailMessage
        {
            From = string.IsNullOrWhiteSpace(_fromName)
                ? new MailAddress(_fromAddress)
                : new MailAddress(_fromAddress, _fromName),
            Subject = message.Subject ?? string.Empty
        };
        mail.To.Add(message.RecipientAddress);

        if (!string.IsNullOrWhiteSpace(message.BodyHtml))
        {
            // Provide both plain-text and HTML parts for best deliverability/rendering.
            mail.Body = message.Body ?? string.Empty;
            mail.IsBodyHtml = false;
            mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
                message.BodyHtml, System.Text.Encoding.UTF8, "text/html"));
        }
        else
        {
            mail.Body = message.Body ?? string.Empty;
            mail.IsBodyHtml = false;
        }

        using var client = new SmtpClient(_host, _port)
        {
            EnableSsl = _enableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };
        if (!string.IsNullOrEmpty(_username) && !string.IsNullOrEmpty(_password))
            client.Credentials = new NetworkCredential(_username, _password);

        try
        {
            await client.SendMailAsync(mail, ct);
            return new NotificationSendResult(
                IsSuccess: true,
                ProviderName: "Smtp",
                ProviderResponse: $"Sent via {_host}:{_port}");
        }
        catch (SmtpException ex)
        {
            // 4xx (temporary) SMTP replies are retryable; 5xx (permanent) are not.
            var retry = ex.StatusCode is SmtpStatusCode.ServiceNotAvailable
                or SmtpStatusCode.MailboxBusy
                or SmtpStatusCode.MailboxUnavailable
                or SmtpStatusCode.InsufficientStorage
                or SmtpStatusCode.TransactionFailed
                or SmtpStatusCode.GeneralFailure;
            _logger.LogError(ex, "SMTP send failed ({Status}, canRetry={Retry})", ex.StatusCode, retry);
            return new NotificationSendResult(
                IsSuccess: false, ProviderName: "Smtp",
                ProviderResponse: ex.Message,
                ErrorMessage: $"SMTP error ({ex.StatusCode}).", CanRetry: retry);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending email via SMTP");
            return new NotificationSendResult(
                IsSuccess: false, ProviderName: "Smtp",
                ProviderResponse: ex.Message,
                ErrorMessage: "Unexpected error sending email via SMTP.", CanRetry: true);
        }
    }
}

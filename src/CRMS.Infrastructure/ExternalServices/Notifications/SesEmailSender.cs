using Amazon;
using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using CRMS.Application.Notification.Interfaces;
using CRMS.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CRMS.Infrastructure.ExternalServices.Notifications;

/// <summary>
/// AWS SES (Simple Email Service v2) implementation of the email notification channel.
/// Auth: explicit AccessKey/SecretKey when provided in config (local/dev), otherwise the default
/// credential chain (the EB instance role in production) — same pattern as S3FileStorageService.
/// Bounce/complaint protection is handled by SES's account-level suppression list (Phase 1).
/// </summary>
/// 

public class SesEmailSender : INotificationSender, IDisposable
{
    private readonly IAmazonSimpleEmailServiceV2 _client;
    private readonly string _fromEmailAddress;   // "Display Name <addr>" if a name is configured
    private readonly string? _configurationSetName;
    private readonly ILogger<SesEmailSender> _logger;
    private bool _disposed;

    public NotificationChannel Channel => NotificationChannel.Email;

    public SesEmailSender(IConfiguration configuration, ILogger<SesEmailSender> logger)
    {
        _logger = logger;

        var settings = configuration.GetSection("Email");
        var fromAddress = settings["FromAddress"]
            ?? throw new InvalidOperationException("Email:FromAddress is required for the SES email sender.");
        var fromName = settings["FromName"];
        _configurationSetName = string.IsNullOrWhiteSpace(settings["ConfigurationSetName"])
            ? null : settings["ConfigurationSetName"];

        // SES accepts an RFC 5322 "Display Name <email>" string for the From header.
        _fromEmailAddress = string.IsNullOrWhiteSpace(fromName)
            ? fromAddress
            : $"{fromName} <{fromAddress}>";

        var region = settings["Region"] ?? "eu-west-1";
        var accessKey = settings["AccessKey"];
        var secretKey = settings["SecretKey"];

        var config = new AmazonSimpleEmailServiceV2Config
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(region)
        };

        _client = (!string.IsNullOrEmpty(accessKey) && !string.IsNullOrEmpty(secretKey))
            ? new AmazonSimpleEmailServiceV2Client(accessKey, secretKey, config)
            : new AmazonSimpleEmailServiceV2Client(config); // default chain (IAM instance role)

        _logger.LogInformation(
            "SES email sender initialized. From: {From}, Region: {Region}, ConfigurationSet: {ConfigSet}",
            _fromEmailAddress, region, _configurationSetName ?? "(none)");
    }

    public async Task<NotificationSendResult> SendAsync(NotificationMessage message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(message.RecipientAddress))
            return new NotificationSendResult(
                IsSuccess: false, ProviderName: "AmazonSES",
                ErrorMessage: "Recipient email address is empty.", CanRetry: false);

        var body = new Body
        {
            Text = new Content { Data = message.Body ?? string.Empty, Charset = "UTF-8" }
        };
        if (!string.IsNullOrWhiteSpace(message.BodyHtml))
            body.Html = new Content { Data = message.BodyHtml, Charset = "UTF-8" };

        var request = new SendEmailRequest
        {
            FromEmailAddress = _fromEmailAddress,
            Destination = new Destination { ToAddresses = [message.RecipientAddress] },
            Content = new EmailContent
            {
                Simple = new Message
                {
                    Subject = new Content { Data = message.Subject ?? string.Empty, Charset = "UTF-8" },
                    Body = body
                }
            }
        };
        if (_configurationSetName is not null)
            request.ConfigurationSetName = _configurationSetName;

        try
        {
            var response = await _client.SendEmailAsync(request, ct);
            return new NotificationSendResult(
                IsSuccess: true,
                ExternalMessageId: response.MessageId,
                ProviderName: "AmazonSES",
                ProviderResponse: $"MessageId={response.MessageId}");
        }
        // ── Permanent failures — do not retry ──────────────────────────────
        catch (MessageRejectedException ex)
        {
            return Fail(ex, "Message rejected by SES.", canRetry: false);
        }
        catch (MailFromDomainNotVerifiedException ex)
        {
            return Fail(ex, "MAIL FROM domain not verified in SES.", canRetry: false);
        }
        catch (AccountSuspendedException ex)
        {
            return Fail(ex, "SES account is suspended.", canRetry: false);
        }
        catch (SendingPausedException ex)
        {
            return Fail(ex, "SES sending is paused for this account/config set.", canRetry: false);
        }
        catch (BadRequestException ex)
        {
            return Fail(ex, "Malformed SES request.", canRetry: false);
        }
        // ── Transient failures — allow retry ───────────────────────────────
        catch (TooManyRequestsException ex)
        {
            return Fail(ex, "SES throttled the request.", canRetry: true);
        }
        catch (LimitExceededException ex)
        {
            return Fail(ex, "SES sending limit exceeded.", canRetry: true);
        }
        catch (AmazonSimpleEmailServiceV2Exception ex)
        {
            // 5xx and throttling are retryable; treat other 4xx as permanent.
            var retry = (int)ex.StatusCode >= 500 || (int)ex.StatusCode == 429;
            return Fail(ex, $"SES error ({ex.StatusCode}).", canRetry: retry);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return Fail(ex, "Unexpected error sending email via SES.", canRetry: true);
        }
    }

    private NotificationSendResult Fail(Exception ex, string summary, bool canRetry)
    {
        _logger.LogError(ex, "SES email send failed (canRetry={CanRetry}): {Summary}", canRetry, summary);
        return new NotificationSendResult(
            IsSuccess: false,
            ProviderName: "AmazonSES",
            ProviderResponse: ex.Message,
            ErrorMessage: summary,
            CanRetry: canRetry);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _client.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

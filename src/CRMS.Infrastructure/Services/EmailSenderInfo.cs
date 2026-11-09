using CRMS.Application.Notification.Commands;
using Microsoft.Extensions.Configuration;

namespace CRMS.Infrastructure.Services;

/// <summary>
/// Describes the email channel this host was wired with, by the same rules as
/// <c>DependencyInjection</c>: no Email section or Email:UseMock → Mock; Email:Provider "Smtp" →
/// SMTP; otherwise AWS SES. Shown on Admin > Diagnostics so a "sent" that went to the mock is not
/// mistaken for a delivery.
/// </summary>
public class EmailSenderInfo : IEmailSenderInfo
{
    private readonly IConfiguration _configuration;

    public EmailSenderInfo(IConfiguration configuration) => _configuration = configuration;

    public EmailSenderInfoDto Get()
    {
        var email = _configuration.GetSection("Email");
        var isMock = !email.Exists() || email.GetValue<bool>("UseMock");
        var provider = isMock ? "Mock"
            : (email["Provider"] ?? "Ses").Equals("Smtp", StringComparison.OrdinalIgnoreCase) ? "SMTP" : "AWS SES";
        var site = _configuration["WebsiteUrl"];
        return new EmailSenderInfoDto(
            provider, isMock,
            email["FromAddress"] ?? string.Empty,
            email["FromName"],
            provider == "SMTP" ? email["Smtp:Host"] : email["Region"] ?? "eu-west-1",
            string.IsNullOrWhiteSpace(site) ? null : site);
    }
}

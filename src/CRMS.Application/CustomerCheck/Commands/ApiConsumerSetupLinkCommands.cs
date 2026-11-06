using CRMS.Application.Common;
using CRMS.Application.CustomerCheck.DTOs;
using CRMS.Domain.Aggregates.CustomerCheck;
using CRMS.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace CRMS.Application.CustomerCheck.Commands;

// A consumer's key and callback signing secret are handed over by a one-time link, emailed to the
// consumer's contact address. The email carries the link and nothing else; the credentials are
// made at the moment the link is opened and shown once, on that page. Until then the consumer's
// current credentials keep working.

/// <summary>Sends the email that carries a setup link.</summary>
public interface IApiConsumerSetupMailer
{
    Task SendAsync(ApiConsumerSetupMail mail, CancellationToken ct = default);
}

/// <param name="Issues">What opening the link issues, in words, e.g. "API key and callback signing secret".</param>
public record ApiConsumerSetupMail(string To, string ConsumerName, string Link, string Issues, int ValidForHours, DateTime ExpiresAt);

/// <summary>The audit trail for credentials: who sent a link, when it was opened and from where,
/// who looked at a signing secret. Never the credentials themselves.</summary>
public interface IApiConsumerAudit
{
    Task RecordAsync(ApiConsumerAuditEntry entry, CancellationToken ct = default);
}

public record ApiConsumerAuditEntry(
    string Event, Guid ConsumerId, string ConsumerName, string Description,
    Guid? UserId = null, string? UserName = null, string? IpAddress = null)
{
    public const string LinkSent = "SETUP_LINK_SENT";
    public const string LinkOpened = "SETUP_LINK_OPENED";
    public const string SecretViewed = "CALLBACK_SECRET_VIEWED";
}

public static class ApiConsumerSetupLinks
{
    /// <summary>The page the link opens, relative to the intranet's public address. The token is a
    /// path segment, not a query value, so an email service's click tracking cannot mangle it.</summary>
    public const string Path = "/partner/setup/";

    public static (string Token, string Hash) NewToken()
    {
        var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return (token, Hash(token));
    }

    public static string Hash(string token) => ApiKeys.Hash(token);

    public static string Describe(bool key, bool secret) =>
        key && secret ? "API key and callback signing secret" : key ? "API key" : "callback signing secret";

    public static ApiConsumerSetupLinkDto ToDto(ApiConsumerSetupLink l, DateTime now) => new(
        l.Id, l.SentTo, l.IssuesKey, l.IssuesSecret, l.SentByName, l.CreatedAt, l.ExpiresAt, l.OpenedAt, l.OpenedFromIp, l.State(now));
}

/// <param name="SiteBaseUrl">The intranet's public address, e.g. https://crms.example.com.</param>
public record SendApiConsumerSetupLinkCommand(
    Guid ConsumerId, bool IssueKey, bool IssueSecret, string SiteBaseUrl, int ValidForHours, Guid UserId, string UserName)
    : IRequest<ApplicationResult<ApiConsumerSetupLinkDto>>;

public class SendApiConsumerSetupLinkHandler : IRequestHandler<SendApiConsumerSetupLinkCommand, ApplicationResult<ApiConsumerSetupLinkDto>>
{
    private readonly IApiConsumerRepository _consumers;
    private readonly IApiConsumerSetupLinkRepository _links;
    private readonly IApiConsumerSetupMailer _mailer;
    private readonly IApiConsumerAudit _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SendApiConsumerSetupLinkHandler> _logger;

    public SendApiConsumerSetupLinkHandler(
        IApiConsumerRepository consumers, IApiConsumerSetupLinkRepository links, IApiConsumerSetupMailer mailer,
        IApiConsumerAudit audit, IUnitOfWork unitOfWork, ILogger<SendApiConsumerSetupLinkHandler> logger)
    {
        _consumers = consumers;
        _links = links;
        _mailer = mailer;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApplicationResult<ApiConsumerSetupLinkDto>> Handle(SendApiConsumerSetupLinkCommand request, CancellationToken ct = default)
    {
        var consumer = await _consumers.GetByIdAsync(request.ConsumerId, ct);
        if (consumer is null)
            return ApplicationResult<ApiConsumerSetupLinkDto>.Failure("Consumer not found.");
        if (!consumer.IsActive)
            return ApplicationResult<ApiConsumerSetupLinkDto>.Failure("Activate the consumer before sending it a setup link.");
        if (string.IsNullOrWhiteSpace(consumer.ContactEmail))
            return ApplicationResult<ApiConsumerSetupLinkDto>.Failure("Add the consumer's contact email first. Setup links are sent there.");
        if (!Uri.TryCreate(request.SiteBaseUrl?.Trim(), UriKind.Absolute, out var site)
            || (site.Scheme != Uri.UriSchemeHttps && site.Scheme != Uri.UriSchemeHttp))
            return ApplicationResult<ApiConsumerSetupLinkDto>.Failure("The site address for the link is not configured.");

        var now = DateTime.UtcNow;
        var hours = Math.Clamp(request.ValidForHours, 1, 168);
        var (token, hash) = ApiConsumerSetupLinks.NewToken();
        var issued = ApiConsumerSetupLink.Issue(
            consumer.Id, hash, consumer.ContactEmail, request.IssueKey, request.IssueSecret,
            request.UserId, request.UserName, now, TimeSpan.FromHours(hours));
        if (issued.IsFailure)
            return ApplicationResult<ApiConsumerSetupLinkDto>.Failure(issued.Error);
        var link = issued.Value;

        // One live link per consumer: the newest. An earlier email that is still unread stops working.
        await _links.RevokeOpenAsync(consumer.Id, now, ct);
        await _links.AddAsync(link, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        var url = site.GetLeftPart(UriPartial.Authority) + site.AbsolutePath.TrimEnd('/') + ApiConsumerSetupLinks.Path + token;
        var issues = ApiConsumerSetupLinks.Describe(link.IssuesKey, link.IssuesSecret);
        try
        {
            await _mailer.SendAsync(new ApiConsumerSetupMail(link.SentTo, consumer.Name, url, issues, hours, link.ExpiresAt), ct);
        }
        catch (Exception ex)
        {
            // A link nobody received must not stay open.
            _logger.LogError(ex, "Setup link for API consumer {Consumer} could not be emailed.", consumer.Name);
            await _links.RevokeOpenAsync(consumer.Id, DateTime.UtcNow, ct);
            return ApplicationResult<ApiConsumerSetupLinkDto>.Failure("The email could not be sent, so the link was cancelled. Try again.");
        }

        await _audit.RecordAsync(new ApiConsumerAuditEntry(
            ApiConsumerAuditEntry.LinkSent, consumer.Id, consumer.Name,
            $"Sent a setup link for API consumer '{consumer.Name}' to {link.SentTo}. Opening it issues a new {issues}. Valid until {link.ExpiresAt:yyyy-MM-dd HH:mm} UTC.",
            request.UserId, request.UserName), ct);

        return ApplicationResult<ApiConsumerSetupLinkDto>.Success(ApiConsumerSetupLinks.ToDto(link, now));
    }
}

/// <summary>What the setup page shows before anything is issued. Reading it changes nothing, so
/// a mail scanner that follows the link does not use it up.</summary>
public record PeekApiConsumerSetupLinkQuery(string Token) : IRequest<ApplicationResult<ApiConsumerSetupLinkView>>;

public class PeekApiConsumerSetupLinkHandler : IRequestHandler<PeekApiConsumerSetupLinkQuery, ApplicationResult<ApiConsumerSetupLinkView>>
{
    private readonly IApiConsumerRepository _consumers;
    private readonly IApiConsumerSetupLinkRepository _links;

    public PeekApiConsumerSetupLinkHandler(IApiConsumerRepository consumers, IApiConsumerSetupLinkRepository links)
    {
        _consumers = consumers;
        _links = links;
    }

    public async Task<ApplicationResult<ApiConsumerSetupLinkView>> Handle(PeekApiConsumerSetupLinkQuery request, CancellationToken ct = default)
    {
        var found = await ApiConsumerSetupLinkLookup.FindAsync(_links, _consumers, request.Token, ct);
        if (found is not { } f)
            return ApplicationResult<ApiConsumerSetupLinkView>.Success(ApiConsumerSetupLinkView.Invalid);

        var (link, consumer) = f;
        return ApplicationResult<ApiConsumerSetupLinkView>.Success(new ApiConsumerSetupLinkView(
            link.State(DateTime.UtcNow), consumer.Name, link.IssuesKey, link.IssuesSecret, link.ExpiresAt, link.OpenedAt));
    }
}

internal static class ApiConsumerSetupLinkLookup
{
    /// <summary>The link for this token and its consumer, or null when the token is not one of
    /// ours or its consumer is gone or switched off.</summary>
    public static async Task<(ApiConsumerSetupLink Link, ApiConsumer Consumer)?> FindAsync(
        IApiConsumerSetupLinkRepository links, IApiConsumerRepository consumers, string? token, CancellationToken ct)
    {
        token = token?.Trim();
        if (string.IsNullOrEmpty(token) || token.Length > 200)
            return null;
        var link = await links.GetByTokenHashAsync(ApiConsumerSetupLinks.Hash(token), ct);
        if (link is null)
            return null;
        var consumer = await consumers.GetByIdAsync(link.ConsumerId, ct);
        return consumer is { IsActive: true } ? (link, consumer) : null;
    }
}

/// <summary>Opens the link: issues what it promised and returns it, once.</summary>
public record RedeemApiConsumerSetupLinkCommand(string Token, string? IpAddress) : IRequest<ApplicationResult<ApiConsumerSetupRedeemedDto>>;

public class RedeemApiConsumerSetupLinkHandler : IRequestHandler<RedeemApiConsumerSetupLinkCommand, ApplicationResult<ApiConsumerSetupRedeemedDto>>
{
    private readonly IApiConsumerRepository _consumers;
    private readonly IApiConsumerSetupLinkRepository _links;
    private readonly ICustomerCheckCipher _cipher;
    private readonly IApiConsumerAudit _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RedeemApiConsumerSetupLinkHandler> _logger;

    public RedeemApiConsumerSetupLinkHandler(
        IApiConsumerRepository consumers, IApiConsumerSetupLinkRepository links, ICustomerCheckCipher cipher,
        IApiConsumerAudit audit, IUnitOfWork unitOfWork, ILogger<RedeemApiConsumerSetupLinkHandler> logger)
    {
        _consumers = consumers;
        _links = links;
        _cipher = cipher;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApplicationResult<ApiConsumerSetupRedeemedDto>> Handle(RedeemApiConsumerSetupLinkCommand request, CancellationToken ct = default)
    {
        const string gone = "This link can no longer be used.";

        var found = await ApiConsumerSetupLinkLookup.FindAsync(_links, _consumers, request.Token, ct);
        if (found is not { } f)
            return ApplicationResult<ApiConsumerSetupRedeemedDto>.Failure(gone);
        var (link, consumer) = f;

        var now = DateTime.UtcNow;
        if (!link.CanBeOpened(now))
            return ApplicationResult<ApiConsumerSetupRedeemedDto>.Failure(gone);

        // The claim is what makes the link one-time: it lands for exactly one request.
        if (!await _links.TryClaimAsync(link.Id, now, request.IpAddress, ct))
            return ApplicationResult<ApiConsumerSetupRedeemedDto>.Failure(gone);

        string? key = null, secret = null;
        try
        {
            // Recorded against whoever sent the link; nobody is signed in on this page.
            var sender = Guid.TryParse(link.CreatedBy, out var id) ? id : Guid.Empty;
            if (link.IssuesKey)
            {
                var (newKey, hash, prefix) = ApiKeys.Generate();
                consumer.RotateKey(hash, prefix, sender);
                key = newKey;
            }
            if (link.IssuesSecret)
            {
                var (newSecret, prefix) = ApiKeys.GenerateSecret();
                consumer.SetCallbackSecret(_cipher.Protect(newSecret), prefix, sender);
                secret = newSecret;
            }
            _consumers.Update(consumer);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Setup link for API consumer {Consumer} was opened but its credentials could not be issued.", consumer.Name);
            await _links.ReleaseAsync(link.Id, CancellationToken.None);
            return ApplicationResult<ApiConsumerSetupRedeemedDto>.Failure("Something went wrong and nothing was changed. Please try again.");
        }

        try
        {
            await _audit.RecordAsync(new ApiConsumerAuditEntry(
                ApiConsumerAuditEntry.LinkOpened, consumer.Id, consumer.Name,
                $"The setup link sent to {link.SentTo} for API consumer '{consumer.Name}' was opened. Issued: a new {ApiConsumerSetupLinks.Describe(link.IssuesKey, link.IssuesSecret)}.",
                IpAddress: request.IpAddress), ct);
        }
        catch (Exception ex)
        {
            // The credentials are already live and the link row itself records the opening.
            _logger.LogError(ex, "Could not write the audit entry for the opened setup link of API consumer {Consumer}.", consumer.Name);
        }

        return ApplicationResult<ApiConsumerSetupRedeemedDto>.Success(new ApiConsumerSetupRedeemedDto(consumer.Name, key, secret));
    }
}

public record GetApiConsumerSetupLinksQuery(Guid ConsumerId) : IRequest<ApplicationResult<List<ApiConsumerSetupLinkDto>>>;

public class GetApiConsumerSetupLinksHandler : IRequestHandler<GetApiConsumerSetupLinksQuery, ApplicationResult<List<ApiConsumerSetupLinkDto>>>
{
    private readonly IApiConsumerSetupLinkRepository _links;

    public GetApiConsumerSetupLinksHandler(IApiConsumerSetupLinkRepository links) => _links = links;

    public async Task<ApplicationResult<List<ApiConsumerSetupLinkDto>>> Handle(GetApiConsumerSetupLinksQuery request, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var links = await _links.GetByConsumerAsync(request.ConsumerId, 10, ct);
        return ApplicationResult<List<ApiConsumerSetupLinkDto>>.Success(links.Select(l => ApiConsumerSetupLinks.ToDto(l, now)).ToList());
    }
}

/// <summary>Shows an administrator the consumer's current callback signing secret, for settling a
/// dispute about which secret is in force. Every look is written to the audit trail first; if
/// that fails, nothing is shown. The API key cannot be shown this way: only its hash is kept.</summary>
public record RevealApiConsumerCallbackSecretCommand(Guid ConsumerId, Guid UserId, string UserName) : IRequest<ApplicationResult<string>>;

public class RevealApiConsumerCallbackSecretHandler : IRequestHandler<RevealApiConsumerCallbackSecretCommand, ApplicationResult<string>>
{
    private readonly IApiConsumerRepository _consumers;
    private readonly ICustomerCheckCipher _cipher;
    private readonly IApiConsumerAudit _audit;

    public RevealApiConsumerCallbackSecretHandler(IApiConsumerRepository consumers, ICustomerCheckCipher cipher, IApiConsumerAudit audit)
    {
        _consumers = consumers;
        _cipher = cipher;
        _audit = audit;
    }

    public async Task<ApplicationResult<string>> Handle(RevealApiConsumerCallbackSecretCommand request, CancellationToken ct = default)
    {
        if (request.UserId == Guid.Empty)
            return ApplicationResult<string>.Failure("Sign in again to view the secret.");
        var consumer = await _consumers.GetByIdAsync(request.ConsumerId, ct);
        if (consumer is null)
            return ApplicationResult<string>.Failure("Consumer not found.");
        if (string.IsNullOrEmpty(consumer.CallbackSecretCiphertext))
            return ApplicationResult<string>.Failure("This consumer has no callback signing secret. Rotate the secret to issue one.");

        await _audit.RecordAsync(new ApiConsumerAuditEntry(
            ApiConsumerAuditEntry.SecretViewed, consumer.Id, consumer.Name,
            $"Viewed the callback signing secret of API consumer '{consumer.Name}'.",
            request.UserId, request.UserName), ct);

        try
        {
            return ApplicationResult<string>.Success(_cipher.Unprotect(consumer.CallbackSecretCiphertext));
        }
        catch (Exception)
        {
            return ApplicationResult<string>.Failure("The stored secret cannot be read with this server's data key. Rotate the secret to issue a new one.");
        }
    }
}

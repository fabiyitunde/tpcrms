using CRMS.Application.Notification.Interfaces;
using CRMS.Application.Rhshf.Services;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using CRMS.Infrastructure.Events.Handlers;
using CRMS.Infrastructure.ExternalServices.Rhshf;
using CRMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CRMS.Infrastructure.BackgroundServices;

/// <summary>
/// Reminds the FAC before an RH-SHF offer lapses (CRMSQA#2). The letter states it is valid for
/// seven days; nothing enforces that yet, but a FAC who has not answered by day five is reminded once,
/// per offer version, through the same email queue as every other FAC email.
/// <para>
/// "Once" is decided from the case's own email history — a reminder already queued for this offer
/// is never queued again — rather than a flag on the offer, so it needs no schema change and holds
/// across restarts. Registered by the intranet host only: both hosts load the infrastructure, and
/// two copies of this loop would race to remind the same FAC.
/// </para>
/// </summary>
public class RhshfOfferReminderBackgroundService : BackgroundService
{
    public const string TemplateCode = "RHSHF_OFFER_REMINDER";

    /// <summary>How long before the lapse date the reminder goes.</summary>
    public static readonly TimeSpan Lead = TimeSpan.FromDays(2);

    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<RhshfOfferReminderBackgroundService> _logger;

    public RhshfOfferReminderBackgroundService(IServiceScopeFactory scopes, ILogger<RhshfOfferReminderBackgroundService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let startup (migrations, seeding) finish before the first pass.
        try { await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var sent = await SendDueRemindersAsync(scope.ServiceProvider, DateTime.UtcNow, stoppingToken);
                if (sent > 0)
                    _logger.LogInformation("Queued {Count} RH-SHF offer expiry reminder(s).", sent);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "RH-SHF offer reminder pass failed");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }

    /// <summary>One pass. Public so it can be driven directly in tests.</summary>
    public static async Task<int> SendDueRemindersAsync(IServiceProvider services, DateTime now, CancellationToken ct = default)
    {
        var db = services.GetRequiredService<CRMSDbContext>();
        var profiles = services.GetRequiredService<IRhshfCreditProfileRepository>();
        var notifications = services.GetRequiredService<INotificationService>();
        var history = services.GetRequiredService<INotificationRepository>();
        var settings = services.GetRequiredService<IOptions<RhshfSettings>>().Value;

        // Unanswered offers whose lapse date falls inside the lead window and has not passed.
        var windowStart = now.AddDays(-RhshfOfferDocumentDataBuilder.OfferValidityDays);
        var windowEnd = now.Add(Lead).AddDays(-RhshfOfferDocumentDataBuilder.OfferValidityDays);
        var due = await db.RhshfOffers
            .Where(o => (o.Status == RhshfOfferStatus.Generated || o.Status == RhshfOfferStatus.AwaitingFacResponse)
                        && o.GeneratedAt > windowStart && o.GeneratedAt <= windowEnd)
            .ToListAsync(ct);

        var sent = 0;
        foreach (var offer in due)
        {
            var profile = await profiles.GetByIdAsync(offer.RhshfCreditProfileId, ct);
            if (profile is null || profile.InternalStage != RhshfInternalStage.AwaitingOfferAcceptance
                || string.IsNullOrWhiteSpace(profile.ContactEmail))
                continue;

            var marker = offer.Id.ToString();
            var previous = await history.GetByLoanApplicationNumberAsync(profile.Reference, ct);
            if (previous.Any(n => n.TemplateCode == TemplateCode && (n.ContextData ?? "").Contains(marker)))
                continue;

            var variables = RhshfFacEmailVariables.Build(profile, offer, settings.PortalUrl);
            variables["OfferId"] = marker; // kept in the notification's ContextData: what makes this once-only
            await notifications.SendEmailAsync(
                NotificationType.Reminder, profile.ContactEmail, profile.CompanyName, TemplateCode, variables,
                loanApplicationNumber: profile.Reference, ct: ct);
            sent++;
        }

        return sent;
    }
}

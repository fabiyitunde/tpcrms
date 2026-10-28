using System.Net;
using CRMS.Application.Notification.Interfaces;
using CRMS.Application.Rhshf.Services;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using CRMS.Infrastructure.ExternalServices.Rhshf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CRMS.Infrastructure.Events.Handlers;

/// <summary>
/// Emails the FAC when its RH-SHF case reaches a point it needs to know about: an offer issued or
/// corrected, a return for correction, its acceptance recorded, the final outcome (CRMSQA#2).
/// <para>
/// CRMS sends these because the decisions are taken here. Each one goes through the notification
/// queue, so it is stored with its delivery status — generated, sent, failed and retried — and
/// filed under the case reference, where the case page lists them. That record, not a successful
/// status change, is the evidence a FAC was told.
/// </para>
/// <para>
/// Runs after the change is committed, alongside the portal callback. A failure here is logged and
/// swallowed: an email that could not be queued must never undo the credit decision behind it.
/// </para>
/// </summary>
public class RhshfFacEmailHandler :
    IDomainEventHandler<RhshfOfferReadyEvent>,
    IDomainEventHandler<RhshfPortalUpdateEvent>,
    IDomainEventHandler<RhshfCaseDecidedEvent>
{
    private readonly IRhshfCreditProfileRepository _profiles;
    private readonly IRhshfOfferRepository _offers;
    private readonly INotificationService _notifications;
    private readonly RhshfSettings _settings;
    private readonly ILogger<RhshfFacEmailHandler> _logger;

    public RhshfFacEmailHandler(
        IRhshfCreditProfileRepository profiles, IRhshfOfferRepository offers, INotificationService notifications,
        IOptions<RhshfSettings> settings, ILogger<RhshfFacEmailHandler> logger)
    {
        _profiles = profiles;
        _offers = offers;
        _notifications = notifications;
        _settings = settings.Value;
        _logger = logger;
    }

    public Task HandleAsync(RhshfOfferReadyEvent domainEvent, CancellationToken ct = default) =>
        SendAsync(domainEvent.RhshfCreditProfileId, "RHSHF_OFFER_READY", NotificationType.Custom, ct);

    public Task HandleAsync(RhshfPortalUpdateEvent domainEvent, CancellationToken ct = default) => domainEvent.EventType switch
    {
        RhshfCallbackEventType.CorrectionRequired =>
            SendAsync(domainEvent.RhshfCreditProfileId, "RHSHF_CORRECTION_REQUIRED", NotificationType.ApplicationReturned, ct),
        RhshfCallbackEventType.OfferAccepted =>
            SendAsync(domainEvent.RhshfCreditProfileId, "RHSHF_OFFER_ACCEPTED", NotificationType.Custom, ct),
        // Submission and resubmission are the FAC's own actions on the CRMS form; telling them by
        // email what they have just done is noise.
        _ => Task.CompletedTask,
    };

    public Task HandleAsync(RhshfCaseDecidedEvent domainEvent, CancellationToken ct = default) =>
        SendAsync(domainEvent.RhshfCreditProfileId, "RHSHF_OUTCOME", NotificationType.Custom, ct);

    private async Task SendAsync(Guid profileId, string templateCode, NotificationType type, CancellationToken ct)
    {
        try
        {
            var profile = await _profiles.GetByIdAsync(profileId, ct);
            if (profile is null)
                return;

            if (string.IsNullOrWhiteSpace(profile.ContactEmail))
            {
                _logger.LogWarning("RH-SHF {Reference}: no FAC contact email on the case, so {Template} was not sent.",
                    profile.Reference, templateCode);
                return;
            }

            var offer = await _offers.GetByProfileAndCycleAsync(profile.Id, profile.CurrentCycleNumber, ct);
            var variables = RhshfFacEmailVariables.Build(profile, offer, _settings.PortalUrl);

            await _notifications.SendEmailAsync(
                type, profile.ContactEmail, profile.CompanyName, templateCode, variables,
                loanApplicationNumber: profile.Reference, ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RH-SHF FAC email {Template} could not be queued for profile {ProfileId}", templateCode, profileId);
        }
    }
}

/// <summary>The values every FAC email draws on. Shared with the expiry reminder so both state the
/// deadline the same way.</summary>
public static class RhshfFacEmailVariables
{
    public static Dictionary<string, string> Build(RhshfCreditProfile profile, RhshfOffer? offer, string? portalUrl)
    {
        var currency = profile.Currency;
        var version = Math.Max(profile.OfferVersion, 1);
        var correction = profile.GetOutstandingReturn();
        var reason = correction?.Note ?? profile.DecisionNotes ?? "";

        var (outcome, outcomeText) = profile.Status switch
        {
            RhshfCaseStatus.Approved => ("Approved and disbursed",
                "Your RH-SHF credit facility has been approved and the loan has been booked."),
            RhshfCaseStatus.Declined => ("Declined",
                "The Bank has decided not to proceed with your RH-SHF credit facility."),
            RhshfCaseStatus.Cancelled => ("Cancelled",
                "Your RH-SHF credit facility has been cancelled because the offer was not accepted."),
            _ => (profile.Status.ToString(), "There is an update on your RH-SHF credit facility."),
        };
        // An approval note is an internal booking reference, not something to put in front of the FAC.
        var showReason = profile.Status is RhshfCaseStatus.Declined or RhshfCaseStatus.Cancelled && !string.IsNullOrWhiteSpace(reason);

        return new Dictionary<string, string>
        {
            ["RecipientName"] = profile.CompanyName,
            ["RecipientNameHtml"] = WebUtility.HtmlEncode(profile.CompanyName),
            ["Reference"] = profile.Reference,
            ["OfferVersion"] = version.ToString(),
            ["ApprovedAmount"] = $"{currency} {(profile.ApprovedAmount ?? profile.TotalEopValue):N2}",
            ["Deadline"] = offer is null ? "the date stated in the offer"
                : RhshfOfferDocumentDataBuilder.ExpiresOn(offer.GeneratedAt).ToString("d MMMM yyyy"),
            ["ReplacesNote"] = version > 1
                ? $"This offer replaces the earlier version, which can no longer be accepted. "
                : "",
            ["Stage"] = correction?.ProfilingStage is { } stage
                ? System.Text.RegularExpressions.Regex.Replace(stage.ToString(), "(?<!^)([A-Z])", " $1")
                : "See the note below",
            ["Reason"] = string.IsNullOrWhiteSpace(reason) ? "Please review your profile and resubmit it." : reason,
            ["ReasonHtml"] = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(reason) ? "Please review your profile and resubmit it." : reason),
            ["Outcome"] = outcome,
            ["OutcomeText"] = outcomeText,
            ["ReasonText"] = showReason ? $"Reason: {reason}\n\n" : "",
            ["ReasonBlock"] = showReason
                ? "<p style=\"margin:16px 0 8px;\"><strong>Reason</strong></p><p style=\"margin:0 0 16px;\">" + WebUtility.HtmlEncode(reason) + "</p>"
                : "",
            ["PortalInstruction"] = string.IsNullOrWhiteSpace(portalUrl)
                ? "Sign in to the RH-SHF portal and open Credit Profiling, then choose Continue on CRMS."
                : $"Sign in to the RH-SHF portal at {portalUrl.Trim()} and open Credit Profiling, then choose Continue on CRMS.",
        };
    }
}

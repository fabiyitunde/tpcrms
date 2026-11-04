using System.Net;
using CRMS.Application.Namp.Interfaces;
using CRMS.Application.Notification.Interfaces;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Common;
using CRMS.Domain.Constants;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CRMS.Infrastructure.Events.Handlers;

/// <summary>
/// When the portal amends a case's EOPs (CRMSQA#3), emails the staff who act next at the case's
/// branch: the Legal Officers if the new review starts at legal clearance, otherwise the Credit
/// Officers. Runs after the amendment is saved; a failure here never undoes it.
/// </summary>
public class RhshfEopAmendedStaffNotificationHandler : IDomainEventHandler<RhshfEopsAmendedEvent>
{
    public const string TemplateCode = "RHSHF_EOP_AMENDED";

    private readonly IRhshfCreditProfileRepository _profiles;
    private readonly INampNotificationRecipientResolver _recipientResolver;
    private readonly INotificationService _notificationService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RhshfEopAmendedStaffNotificationHandler> _logger;

    public RhshfEopAmendedStaffNotificationHandler(
        IRhshfCreditProfileRepository profiles,
        INampNotificationRecipientResolver recipientResolver,
        INotificationService notificationService,
        IConfiguration configuration,
        ILogger<RhshfEopAmendedStaffNotificationHandler> logger)
    {
        _profiles = profiles;
        _recipientResolver = recipientResolver;
        _notificationService = notificationService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task HandleAsync(RhshfEopsAmendedEvent domainEvent, CancellationToken ct = default)
    {
        try
        {
            var profile = await _profiles.GetByIdAsync(domainEvent.RhshfCreditProfileId, ct);
            var amendment = profile?.Amendments.FirstOrDefault(a => a.Id == domainEvent.AmendmentId);
            if (profile is null || amendment is null)
            {
                _logger.LogWarning("RH-SHF amendment notification skipped: case {Id} or amendment {AmendmentId} not found",
                    domainEvent.RhshfCreditProfileId, domainEvent.AmendmentId);
                return;
            }

            var inReview = profile.Status == RhshfCaseStatus.UnderReview;
            var role = inReview && profile.InternalStage == RhshfInternalStage.LegalClearance ? Roles.LegalOfficer : Roles.CreditOfficer;
            var stageText = !inReview
                ? "The case is with the FAC, so its next review will be of the amended EOP."
                : role == Roles.LegalOfficer
                    ? "A new review of the case has started at legal clearance."
                    : "A new review of the case has started at appraisal, and every stage will look at the amended EOP again.";

            var locationIds = new List<Guid>();
            if (profile.ResolvedBranchId is Guid branchId) locationIds.Add(branchId);
            if (profile.ResolvedOfficeId is Guid officeId) locationIds.Add(officeId);

            var recipients = await _recipientResolver.ResolveAsync(role, locationIds, ct);
            if (recipients.Count == 0)
            {
                _logger.LogWarning("RH-SHF amendment notification: no active {Role} found for {Reference}", role, profile.Reference);
                return;
            }

            var changes = string.Join("; ", amendment.Changes.Select(c => c.Describe()))
                          + (amendment.PreviousTotalEopValue != amendment.NewTotalEopValue
                              ? $". Total EOP value ₦{amendment.PreviousTotalEopValue:N2} to ₦{amendment.NewTotalEopValue:N2}."
                              : ".");
            var actionUrl = BuildCaseUrl(profile.Reference);

            foreach (var user in recipients)
            {
                // The template engine substitutes verbatim, so the HTML body takes encoded copies.
                var variables = new Dictionary<string, string>
                {
                    { "RecipientName", user.FullName },
                    { "RecipientNameHtml", WebUtility.HtmlEncode(user.FullName) },
                    { "Reference", profile.Reference },
                    { "CompanyName", profile.CompanyName },
                    { "CompanyNameHtml", WebUtility.HtmlEncode(profile.CompanyName) },
                    { "Changes", changes },
                    { "ChangesHtml", WebUtility.HtmlEncode(changes) },
                    { "StageText", stageText },
                    { "ActionUrl", actionUrl },
                };

                await _notificationService.SendEmailAsync(
                    NotificationType.WorkflowAssigned,
                    user.Email,
                    user.FullName,
                    TemplateCode,
                    variables,
                    user.Id,
                    null,
                    profile.Reference,
                    NotificationPriority.Normal,
                    ct);
            }

            _logger.LogInformation("RH-SHF amendment notifications queued for {Count} {Role}(s): {Reference}",
                recipients.Count, role, profile.Reference);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send RH-SHF amendment notifications for case {Id}", domainEvent.RhshfCreditProfileId);
        }
    }

    private string BuildCaseUrl(string reference)
    {
        var baseUrl = (_configuration["WebsiteUrl"] ?? string.Empty).TrimEnd('/');
        return $"{baseUrl}/rhshf/detail/{Uri.EscapeDataString(reference)}";
    }
}

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
/// Tells the role that acts at an RH-SHF stage that a case has reached it, by email and on the bell,
/// the way NAMP has done since June 2026. The stage-to-role map mirrors the case page's own gates
/// (CanAppraise, CanClearLegally, CanReviewRisk, CanVoteOnCommittee, CanRatify, CanBookDisbursement).
/// Recipients are scoped to the case's resolved branch and office. Stages that wait on the FAC, not
/// on staff, are not announced here: the FAC is emailed by RhshfFacEmailHandler.
/// </summary>
public class RhshfStageChangedNotificationHandler : IDomainEventHandler<RhshfInternalStageChangedEvent>
{
    private static readonly Dictionary<RhshfInternalStage, (string Role, string Name)> ActingRole = new()
    {
        [RhshfInternalStage.Appraisal] = (Roles.CreditOfficer, "Appraisal"),
        [RhshfInternalStage.LegalClearance] = (Roles.LegalOfficer, "Legal Clearance"),
        [RhshfInternalStage.RiskReview] = (Roles.RiskManager, "Risk Review"),
        [RhshfInternalStage.CommitteeVoting] = (Roles.CommitteeMember, "Committee Voting"),
        [RhshfInternalStage.Ratification] = (Roles.FinalApprover, "Ratification"),
        [RhshfInternalStage.PreDeploymentVerification] = (Roles.DisbursementOfficer, "Pre-Deployment Verification"),
        [RhshfInternalStage.Disbursement] = (Roles.DisbursementOfficer, "Disbursement"),
    };

    private readonly IRhshfCreditProfileRepository _profiles;
    private readonly IStaffRecipientResolver _recipients;
    private readonly INotificationService _notifications;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RhshfStageChangedNotificationHandler> _logger;

    public RhshfStageChangedNotificationHandler(
        IRhshfCreditProfileRepository profiles,
        IStaffRecipientResolver recipients,
        INotificationService notifications,
        IConfiguration configuration,
        ILogger<RhshfStageChangedNotificationHandler> logger)
    {
        _profiles = profiles;
        _recipients = recipients;
        _notifications = notifications;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task HandleAsync(RhshfInternalStageChangedEvent domainEvent, CancellationToken ct = default)
    {
        if (!ActingRole.TryGetValue(domainEvent.Stage, out var acting))
            return;

        try
        {
            var profile = await _profiles.GetByIdAsync(domainEvent.RhshfCreditProfileId, ct);
            if (profile is null)
            {
                _logger.LogWarning("RH-SHF notification skipped — case {Id} not found", domainEvent.RhshfCreditProfileId);
                return;
            }

            var users = await _recipients.ResolveAsync(acting.Role, RhshfStaffNotifications.Locations(profile), ct);
            var actionUrl = RhshfStaffNotifications.CaseUrl(_configuration, profile);
            var sent = 0;

            foreach (var user in users)
            {
                if (user.Id == domainEvent.ActorUserId) continue;

                var variables = new Dictionary<string, string>
                {
                    { "RecipientName", user.FullName },
                    { "StageName", acting.Name },
                    { "Reference", profile.Reference },
                    { "CompanyName", profile.CompanyName },
                    { "Amount", $"NGN {profile.TotalEopValue:N2}" },
                    { "ActionUrl", actionUrl },
                    { "Summary", $"{profile.CompanyName} is at {acting.Name} and waiting for you." },
                };

                await _notifications.SendEmailAsync(
                    domainEvent.Stage == RhshfInternalStage.CommitteeVoting ? NotificationType.CommitteeVoteRequired : NotificationType.WorkflowAssigned,
                    user.Email, user.FullName, "RHSHF_ACTION_REQUIRED",
                    variables, user.Id, null, profile.Reference, NotificationPriority.Normal, ct);
                sent++;
            }

            _logger.LogInformation("RH-SHF action notifications queued for {Count} recipient(s) — {Reference} at {Stage}",
                sent, profile.Reference, acting.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send RH-SHF notifications for case {Id} at stage {Stage}",
                domainEvent.RhshfCreditProfileId, domainEvent.Stage);
        }
    }
}

/// <summary>
/// Tells the branch's Credit Officers how an RH-SHF case ended: approved and disbursed, declined, or
/// withdrawn. RH-SHF has no originating officer the way the other tracks do (the FAC applies from the
/// portal), so the Credit Officers of the case's branch are the staff who followed it.
/// </summary>
public class RhshfCaseDecidedStaffNotificationHandler : IDomainEventHandler<RhshfCaseDecidedEvent>
{
    private readonly IRhshfCreditProfileRepository _profiles;
    private readonly IStaffRecipientResolver _recipients;
    private readonly INotificationService _notifications;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RhshfCaseDecidedStaffNotificationHandler> _logger;

    public RhshfCaseDecidedStaffNotificationHandler(
        IRhshfCreditProfileRepository profiles,
        IStaffRecipientResolver recipients,
        INotificationService notifications,
        IConfiguration configuration,
        ILogger<RhshfCaseDecidedStaffNotificationHandler> logger)
    {
        _profiles = profiles;
        _recipients = recipients;
        _notifications = notifications;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task HandleAsync(RhshfCaseDecidedEvent domainEvent, CancellationToken ct = default)
    {
        try
        {
            var profile = await _profiles.GetByIdAsync(domainEvent.RhshfCreditProfileId, ct);
            if (profile is null) return;

            var outcome = profile.Status switch
            {
                RhshfCaseStatus.Approved => "approved and disbursed",
                RhshfCaseStatus.Declined => "declined",
                RhshfCaseStatus.Cancelled => "withdrawn by the FAC",
                _ => null,
            };
            if (outcome is null) return;

            var users = await _recipients.ResolveAsync(Roles.CreditOfficer, RhshfStaffNotifications.Locations(profile), ct);
            var actionUrl = RhshfStaffNotifications.CaseUrl(_configuration, profile);
            var reason = string.IsNullOrWhiteSpace(profile.DecisionNotes) ? "Not specified" : profile.DecisionNotes;
            var type = profile.Status == RhshfCaseStatus.Approved ? NotificationType.ApplicationDisbursed : NotificationType.ApplicationRejected;

            foreach (var user in users)
            {
                var variables = new Dictionary<string, string>
                {
                    { "RecipientName", user.FullName },
                    { "Outcome", outcome },
                    { "Reference", profile.Reference },
                    { "CompanyName", profile.CompanyName },
                    { "Reason", reason },
                    { "ActionUrl", actionUrl },
                    { "Summary", $"{profile.CompanyName} has been {outcome}." },
                };

                await _notifications.SendEmailAsync(
                    type, user.Email, user.FullName, "RHSHF_CASE_UPDATE",
                    variables, user.Id, null, profile.Reference, NotificationPriority.Normal, ct);
            }

            _logger.LogInformation("RH-SHF case update queued for {Count} recipient(s) — {Reference}: {Outcome}",
                users.Count, profile.Reference, outcome);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send RH-SHF case update for {Id}", domainEvent.RhshfCreditProfileId);
        }
    }
}

internal static class RhshfStaffNotifications
{
    public static IEnumerable<Guid> Locations(RhshfCreditProfile profile)
    {
        if (profile.ResolvedBranchId is Guid b) yield return b;
        if (profile.ResolvedOfficeId is Guid o) yield return o;
    }

    public static string CaseUrl(IConfiguration configuration, RhshfCreditProfile profile)
    {
        var baseUrl = (configuration["WebsiteUrl"] ?? string.Empty).TrimEnd('/');
        var path = $"/rhshf/detail/{profile.Reference}";
        return string.IsNullOrEmpty(baseUrl) ? path : baseUrl + path;
    }
}

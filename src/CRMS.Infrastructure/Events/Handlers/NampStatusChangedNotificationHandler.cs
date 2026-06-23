using CRMS.Application.Namp.Interfaces;
using CRMS.Application.Notification.Interfaces;
using CRMS.Domain.Aggregates.Namp;
using CRMS.Domain.Common;
using CRMS.Domain.Constants;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CRMS.Infrastructure.Events.Handlers;

/// <summary>
/// Emails the responsible actor(s) whenever a NAMP application changes status.
/// Config-driven backbone: for an actionable stage it resolves the stage's AssignedRole
/// (scoped to the application's branch) and sends "action required"; committee-circulation
/// statuses fan out a vote request to each committee member; terminal declines notify the
/// originating loan officer with the reason. Non-notifiable statuses are ignored.
/// Runs in the post-save event scope, so its own DbContext is separate from the transition's
/// and any failure here can never roll back the committed status change.
/// </summary>
public class NampStatusChangedNotificationHandler : IDomainEventHandler<NampStatusChangedEvent>
{
    private static readonly HashSet<NampApplicationStatus> CommitteeStatuses = new()
    {
        NampApplicationStatus.BranchCommitteeCirculation,
        NampApplicationStatus.ZonalCommitteeCirculation,
        NampApplicationStatus.RegionalCommitteeCirculation,
        NampApplicationStatus.HOCommitteeCirculation,
    };

    private static readonly HashSet<NampApplicationStatus> DeclinedStatuses = new()
    {
        NampApplicationStatus.FinancialDeclined,
        NampApplicationStatus.BranchCommitteeDeclined,
        NampApplicationStatus.ZonalCommitteeDeclined,
        NampApplicationStatus.RegionalCommitteeDeclined,
        NampApplicationStatus.HOCommitteeDeclined,
        NampApplicationStatus.RatificationDeclined,
        NampApplicationStatus.OfferLapsed,
        NampApplicationStatus.LegalDeclined,
    };

    // Stage-entry points where the next responsible role needs to act.
    private static readonly HashSet<NampApplicationStatus> ActionStatuses = new()
    {
        NampApplicationStatus.Submitted,
        NampApplicationStatus.Ratification,
        NampApplicationStatus.OfferGenerated,
        NampApplicationStatus.LegalClearance,
        NampApplicationStatus.LegalReturned,
        NampApplicationStatus.PreDeploymentVerification,
        NampApplicationStatus.Deployment,
        NampApplicationStatus.Active,
    };

    private readonly INampApplicationRepository _nampRepo;
    private readonly INampWorkflowConfigRepository _configRepo;
    private readonly ICommitteeReviewRepository _committeeRepo;
    private readonly IUserRepository _userRepo;
    private readonly INampNotificationRecipientResolver _recipientResolver;
    private readonly INotificationService _notificationService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NampStatusChangedNotificationHandler> _logger;

    public NampStatusChangedNotificationHandler(
        INampApplicationRepository nampRepo,
        INampWorkflowConfigRepository configRepo,
        ICommitteeReviewRepository committeeRepo,
        IUserRepository userRepo,
        INampNotificationRecipientResolver recipientResolver,
        INotificationService notificationService,
        IConfiguration configuration,
        ILogger<NampStatusChangedNotificationHandler> logger)
    {
        _nampRepo = nampRepo;
        _configRepo = configRepo;
        _committeeRepo = committeeRepo;
        _userRepo = userRepo;
        _recipientResolver = recipientResolver;
        _notificationService = notificationService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task HandleAsync(NampStatusChangedEvent domainEvent, CancellationToken ct = default)
    {
        var status = domainEvent.NewStatus;

        var isCommittee = CommitteeStatuses.Contains(status);
        var isDeclined = DeclinedStatuses.Contains(status);
        var isAction = ActionStatuses.Contains(status);

        if (!isCommittee && !isDeclined && !isAction)
            return; // not a notifiable transition (e.g. Draft, FinancialAppraisal in-progress)

        try
        {
            var app = await _nampRepo.GetByIdAsync(domainEvent.NampApplicationId, ct);
            if (app is null)
            {
                _logger.LogWarning("NAMP notification skipped — application {Id} not found", domainEvent.NampApplicationId);
                return;
            }

            var config = await _configRepo.GetByStatusAsync(status, ct);
            var stageName = config?.DisplayName ?? status.ToString();

            if (isCommittee)
                await NotifyCommitteeAsync(app, ct);
            else if (isDeclined)
                await NotifyDeclineAsync(app, stageName, domainEvent.Note, ct);
            else
                await NotifyActionRequiredAsync(app, config, stageName, status, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send NAMP notifications for application {Id} at status {Status}",
                domainEvent.NampApplicationId, status);
        }
    }

    private async Task NotifyActionRequiredAsync(NampApplication app, NampWorkflowConfig? config, string stageName, NampApplicationStatus status, CancellationToken ct)
    {
        if (config is null)
        {
            _logger.LogWarning("NAMP action notification skipped — no workflow config for stage {Stage}", stageName);
            return;
        }

        // Ratification is tier-specific: route to the manager for the application's committee tier
        // (Branch→BranchManager, Zonal→ZonalManager, …) rather than the generic configured role.
        var role = status == NampApplicationStatus.Ratification
            ? Roles.RatifierRoleForTier(app.CommitteeTier)
            : config.AssignedRole;

        if (string.IsNullOrWhiteSpace(role))
        {
            _logger.LogWarning("NAMP action notification skipped — no assigned role for stage {Stage}", stageName);
            return;
        }

        var recipients = await _recipientResolver.ResolveAsync(role, app, ct);
        if (recipients.Count == 0)
        {
            _logger.LogWarning("NAMP action notification — no active recipients in role {Role} for application {Number}",
                role, app.ApplicationNumber);
            return;
        }

        var actionUrl = BuildActionUrl(app.Id);
        var sla = FormatSla(config.SlaHours);

        foreach (var user in recipients)
        {
            var variables = new Dictionary<string, string>
            {
                { "RecipientName", user.FullName },
                { "StageName", stageName },
                { "ApplicationNumber", app.ApplicationNumber },
                { "ApplicantName", app.ApplicantName },
                { "Sla", sla },
                { "ActionUrl", actionUrl },
            };

            await _notificationService.SendEmailAsync(
                NotificationType.WorkflowAssigned,
                user.Email,
                user.FullName,
                "NAMP_ACTION_REQUIRED",
                variables,
                user.Id,
                app.Id,
                app.ApplicationNumber,
                NotificationPriority.Normal,
                ct);
        }

        _logger.LogInformation("NAMP action notifications queued for {Count} recipient(s) — {Number} at {Stage}",
            recipients.Count, app.ApplicationNumber, stageName);
    }

    private async Task NotifyCommitteeAsync(NampApplication app, CancellationToken ct)
    {
        if (app.CurrentCommitteeReviewId is not Guid reviewId)
        {
            _logger.LogWarning("NAMP committee notification skipped — application {Number} has no current committee review",
                app.ApplicationNumber);
            return;
        }

        var review = await _committeeRepo.GetByIdAsync(reviewId, ct);
        if (review is null)
        {
            _logger.LogWarning("NAMP committee notification skipped — committee review {ReviewId} not found", reviewId);
            return;
        }

        var actionUrl = BuildActionUrl(app.Id);
        var deadline = review.DeadlineAt?.ToString("dd-MMM-yyyy HH:mm") ?? "No deadline";
        var committeeType = review.CommitteeType.ToString();
        var sent = 0;

        foreach (var member in review.Members)
        {
            var user = await _userRepo.GetByIdAsync(member.UserId, ct);
            if (user is null || string.IsNullOrWhiteSpace(user.Email))
                continue;

            var variables = new Dictionary<string, string>
            {
                { "RecipientName", user.FullName },
                { "CommitteeType", committeeType },
                { "ApplicationNumber", app.ApplicationNumber },
                { "ApplicantName", app.ApplicantName },
                { "Deadline", deadline },
                { "ActionUrl", actionUrl },
            };

            await _notificationService.SendEmailAsync(
                NotificationType.CommitteeVoteRequired,
                user.Email,
                user.FullName,
                "NAMP_COMMITTEE_VOTE",
                variables,
                user.Id,
                app.Id,
                app.ApplicationNumber,
                NotificationPriority.Normal,
                ct);
            sent++;
        }

        _logger.LogInformation("NAMP committee vote notifications queued for {Count} member(s) — {Number}",
            sent, app.ApplicationNumber);
    }

    private async Task NotifyDeclineAsync(NampApplication app, string stageName, string? note, CancellationToken ct)
    {
        var user = await _userRepo.GetByIdAsync(app.RecalledByUserId, ct);
        if (user is null || string.IsNullOrWhiteSpace(user.Email))
        {
            _logger.LogWarning("NAMP decline notification skipped — originating loan officer not resolvable for {Number}",
                app.ApplicationNumber);
            return;
        }

        var variables = new Dictionary<string, string>
        {
            { "RecipientName", user.FullName },
            { "StageName", stageName },
            { "ApplicationNumber", app.ApplicationNumber },
            { "ApplicantName", app.ApplicantName },
            { "Reason", string.IsNullOrWhiteSpace(note) ? "Not specified" : note },
        };

        await _notificationService.SendEmailAsync(
            NotificationType.ApplicationRejected,
            user.Email,
            user.FullName,
            "NAMP_DECLINED",
            variables,
            user.Id,
            app.Id,
            app.ApplicationNumber,
            NotificationPriority.Normal,
            ct);

        _logger.LogInformation("NAMP decline notification queued for {Number} at {Stage}", app.ApplicationNumber, stageName);
    }

    private string BuildActionUrl(Guid applicationId)
    {
        var baseUrl = (_configuration["WebsiteUrl"] ?? string.Empty).TrimEnd('/');
        return string.IsNullOrEmpty(baseUrl)
            ? $"/namp/{applicationId}"
            : $"{baseUrl}/namp/{applicationId}";
    }

    private static string FormatSla(int slaHours)
    {
        if (slaHours <= 0) return "the agreed SLA";
        if (slaHours % 24 == 0)
        {
            var days = slaHours / 24;
            return days == 1 ? "1 day" : $"{days} days";
        }
        return slaHours == 1 ? "1 hour" : $"{slaHours} hours";
    }
}

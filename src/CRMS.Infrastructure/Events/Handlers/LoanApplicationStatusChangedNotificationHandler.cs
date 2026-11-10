using CRMS.Application.Notification.Interfaces;
using CRMS.Domain.Aggregates.LoanApplication;
using CRMS.Domain.Common;
using CRMS.Domain.Constants;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CRMS.Infrastructure.Events.Handlers;

/// <summary>
/// Tells staff when a corporate application changes hands, the way NAMP has done since June 2026.
/// <para>
/// Who acts at a status comes from the active workflow definition: the roles that hold a transition
/// out of the new status, which is exactly the rule the case page uses to show its buttons. Each of
/// those users, scoped to the application's branch, gets CORPORATE_ACTION_REQUIRED by email and on the
/// bell. A status that sends the case back, declines it or completes it is instead reported to the
/// Loan Officer who created it with CORPORATE_CASE_UPDATE. Committee Circulation is the one status
/// whose transitions belong to the system; there the Credit Officer is told to set up the committee,
/// and the members themselves hear when voting opens (CommitteeVotingStartedNotificationHandler).
/// </para>
/// </summary>
public class LoanApplicationStatusChangedNotificationHandler : IDomainEventHandler<LoanApplicationStatusChangedEvent>
{
    private static readonly Dictionary<LoanApplicationStatus, string> ReportedToInitiator = new()
    {
        [LoanApplicationStatus.Draft] = "returned to you for rework",
        [LoanApplicationStatus.BranchReturned] = "returned to you for rework",
        [LoanApplicationStatus.BranchRejected] = "declined",
        [LoanApplicationStatus.CommitteeRejected] = "declined by the committee",
        [LoanApplicationStatus.Rejected] = "declined",
        [LoanApplicationStatus.RatificationDeclined] = "declined at ratification",
        [LoanApplicationStatus.Disbursed] = "disbursed",
    };

    // Statuses the system leaves on its own, or that end a case without anyone else needing to act.
    private static readonly HashSet<LoanApplicationStatus> Silent =
    [
        LoanApplicationStatus.CommitteeApproved, LoanApplicationStatus.Closed, LoanApplicationStatus.Cancelled,
        LoanApplicationStatus.DataGathering,
    ];

    private readonly ILoanApplicationRepository _applications;
    private readonly IWorkflowDefinitionRepository _workflows;
    private readonly IUserRepository _users;
    private readonly IStaffRecipientResolver _recipients;
    private readonly INotificationService _notifications;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LoanApplicationStatusChangedNotificationHandler> _logger;

    public LoanApplicationStatusChangedNotificationHandler(
        ILoanApplicationRepository applications,
        IWorkflowDefinitionRepository workflows,
        IUserRepository users,
        IStaffRecipientResolver recipients,
        INotificationService notifications,
        IConfiguration configuration,
        ILogger<LoanApplicationStatusChangedNotificationHandler> logger)
    {
        _applications = applications;
        _workflows = workflows;
        _users = users;
        _recipients = recipients;
        _notifications = notifications;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task HandleAsync(LoanApplicationStatusChangedEvent domainEvent, CancellationToken ct = default)
    {
        var status = domainEvent.NewStatus;
        if (Silent.Contains(status))
            return;

        try
        {
            var app = await _applications.GetByIdAsync(domainEvent.ApplicationId, ct);
            if (app is null)
            {
                _logger.LogWarning("Corporate notification skipped — application {Id} not found", domainEvent.ApplicationId);
                return;
            }

            var workflow = await _workflows.GetActiveByTypeAsync(app.Type, ct);
            var stage = workflow?.Stages.FirstOrDefault(s => s.Status == status);
            var stageName = stage?.DisplayName ?? Humanise(status);

            if (ReportedToInitiator.TryGetValue(status, out var outcome))
            {
                await NotifyInitiatorAsync(app, stageName, outcome, domainEvent.Comment, ct);
                return;
            }

            var roles = status == LoanApplicationStatus.CommitteeCirculation
                ? [Roles.CreditOfficer]
                : RolesActingAt(workflow, stage, status);

            if (roles.Count == 0)
            {
                _logger.LogInformation("Corporate notification — nobody acts at {Status}; {Number} not announced", status, app.ApplicationNumber);
                return;
            }

            await NotifyActionRequiredAsync(app, stageName, stage?.SLAHours ?? 0, roles, domainEvent.ChangedByUserId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send corporate notifications for application {Id} at status {Status}",
                domainEvent.ApplicationId, status);
        }
    }

    /// <summary>The roles with a way out of the status, else the stage's assigned role; never the administrator.</summary>
    private static List<string> RolesActingAt(Domain.Aggregates.Workflow.WorkflowDefinition? workflow, Domain.Aggregates.Workflow.WorkflowStage? stage, LoanApplicationStatus status)
    {
        var roles = workflow?.GetAvailableTransitions(status)
            .Select(t => t.RequiredRole)
            .Where(r => !string.IsNullOrWhiteSpace(r) && r != Roles.SystemAdmin)
            .Distinct()
            .ToList() ?? [];

        if (roles.Count == 0 && !string.IsNullOrWhiteSpace(stage?.AssignedRole) && stage.AssignedRole != Roles.SystemAdmin)
            roles.Add(stage.AssignedRole);

        return roles;
    }

    private async Task NotifyActionRequiredAsync(LoanApplication app, string stageName, int slaHours, List<string> roles, Guid actorId, CancellationToken ct)
    {
        var locations = app.BranchId is Guid branchId ? new[] { branchId } : Array.Empty<Guid>();
        var seen = new HashSet<Guid> { actorId }; // the person who just moved it does not need telling
        var actionUrl = BuildUrl($"/applications/{app.Id}");
        var sla = FormatSla(slaHours);
        var sent = 0;

        foreach (var role in roles)
        {
            var users = await _recipients.ResolveAsync(role, locations, ct);
            foreach (var user in users)
            {
                if (!seen.Add(user.Id)) continue;

                var variables = new Dictionary<string, string>
                {
                    { "RecipientName", user.FullName },
                    { "StageName", stageName },
                    { "ApplicationNumber", app.ApplicationNumber },
                    { "CustomerName", app.CustomerName },
                    { "Amount", app.RequestedAmount.ToString() },
                    { "Sla", sla },
                    { "ActionUrl", actionUrl },
                    { "Summary", $"{app.CustomerName} is at {stageName} and waiting for you." },
                };

                await _notifications.SendEmailAsync(
                    NotificationType.WorkflowAssigned, user.Email, user.FullName, "CORPORATE_ACTION_REQUIRED",
                    variables, user.Id, app.Id, app.ApplicationNumber, NotificationPriority.Normal, ct);
                sent++;
            }
        }

        _logger.LogInformation("Corporate action notifications queued for {Count} recipient(s) — {Number} at {Stage}",
            sent, app.ApplicationNumber, stageName);
    }

    private async Task NotifyInitiatorAsync(LoanApplication app, string stageName, string outcome, string? reason, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(app.InitiatedByUserId, ct);
        if (user is null || string.IsNullOrWhiteSpace(user.Email))
        {
            _logger.LogWarning("Corporate case update skipped — initiating officer not resolvable for {Number}", app.ApplicationNumber);
            return;
        }

        var variables = new Dictionary<string, string>
        {
            { "RecipientName", user.FullName },
            { "Outcome", outcome },
            { "StageName", stageName },
            { "ApplicationNumber", app.ApplicationNumber },
            { "CustomerName", app.CustomerName },
            { "Reason", string.IsNullOrWhiteSpace(reason) ? "Not specified" : reason },
            { "ActionUrl", BuildUrl($"/applications/{app.Id}") },
            { "Summary", $"{app.CustomerName} has been {outcome}." },
        };

        var type = outcome.StartsWith("declined", StringComparison.Ordinal) ? NotificationType.ApplicationRejected
            : outcome == "disbursed" ? NotificationType.ApplicationDisbursed
            : NotificationType.ApplicationReturned;

        await _notifications.SendEmailAsync(
            type, user.Email, user.FullName, "CORPORATE_CASE_UPDATE",
            variables, user.Id, app.Id, app.ApplicationNumber, NotificationPriority.Normal, ct);

        _logger.LogInformation("Corporate case update queued for {Number}: {Outcome}", app.ApplicationNumber, outcome);
    }

    private string BuildUrl(string path)
    {
        var baseUrl = (_configuration["WebsiteUrl"] ?? string.Empty).TrimEnd('/');
        return string.IsNullOrEmpty(baseUrl) ? path : baseUrl + path;
    }

    private static string Humanise(LoanApplicationStatus status)
        => System.Text.RegularExpressions.Regex.Replace(status.ToString(), "(?<=[a-z])(?=[A-Z])", " ");

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

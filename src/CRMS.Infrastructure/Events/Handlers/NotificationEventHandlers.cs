using CRMS.Application.Notification.Interfaces;
using CRMS.Domain.Aggregates.Committee;
using CRMS.Domain.Aggregates.LoanApplication;
using CRMS.Domain.Aggregates.Workflow;
using CRMS.Domain.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace CRMS.Infrastructure.Events.Handlers;

/// <summary>
/// Handles workflow events that trigger notifications.
/// </summary>
public class WorkflowSLABreachedNotificationHandler : IDomainEventHandler<WorkflowSLABreachedEvent>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<WorkflowSLABreachedNotificationHandler> _logger;

    public WorkflowSLABreachedNotificationHandler(
        INotificationService notificationService,
        ILogger<WorkflowSLABreachedNotificationHandler> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task HandleAsync(WorkflowSLABreachedEvent domainEvent, CancellationToken ct = default)
    {
        _logger.LogInformation("Sending SLA breach notification for workflow {WorkflowId}", domainEvent.WorkflowInstanceId);

        // TODO: Get actual user email from workflow assignment
        // For now, log that we would send a notification
        _logger.LogWarning("SLA breached for loan {LoanId} at status {Status}. Notification would be sent.",
            domainEvent.LoanApplicationId, domainEvent.Status);
    }
}

public class WorkflowEscalatedNotificationHandler : IDomainEventHandler<WorkflowEscalatedEvent>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<WorkflowEscalatedNotificationHandler> _logger;

    public WorkflowEscalatedNotificationHandler(
        INotificationService notificationService,
        ILogger<WorkflowEscalatedNotificationHandler> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task HandleAsync(WorkflowEscalatedEvent domainEvent, CancellationToken ct = default)
    {
        _logger.LogInformation("Sending escalation notification for workflow {WorkflowId}", domainEvent.WorkflowInstanceId);

        // TODO: Get escalation target user email
        _logger.LogWarning("Workflow escalated for loan {LoanId} to level {Level}. Notification would be sent.",
            domainEvent.LoanApplicationId, domainEvent.EscalationLevel);
    }
}

public class WorkflowAssignedNotificationHandler : IDomainEventHandler<WorkflowAssignedEvent>
{
    private readonly INotificationService _notificationService;
    private readonly IUserRepository _userRepository;
    private readonly ILoanApplicationRepository _loanApplicationRepository;
    private readonly ILogger<WorkflowAssignedNotificationHandler> _logger;

    public WorkflowAssignedNotificationHandler(
        INotificationService notificationService,
        IUserRepository userRepository,
        ILoanApplicationRepository loanApplicationRepository,
        ILogger<WorkflowAssignedNotificationHandler> logger)
    {
        _notificationService = notificationService;
        _userRepository = userRepository;
        _loanApplicationRepository = loanApplicationRepository;
        _logger = logger;
    }

    public async Task HandleAsync(WorkflowAssignedEvent domainEvent, CancellationToken ct = default)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(domainEvent.AssignedToUserId, ct);
            var loanApp = await _loanApplicationRepository.GetByIdAsync(domainEvent.LoanApplicationId, ct);

            if (user == null || loanApp == null || string.IsNullOrEmpty(user.Email))
            {
                _logger.LogWarning("Cannot send workflow assignment notification - user or loan app not found");
                return;
            }

            var variables = new Dictionary<string, string>
            {
                { "UserName", user.FullName },
                { "ApplicationNumber", loanApp.ApplicationNumber },
                { "CustomerName", loanApp.CustomerName },
                { "Amount", loanApp.RequestedAmount.ToString() }
            };

            await _notificationService.SendEmailAsync(
                NotificationType.WorkflowAssigned,
                user.Email,
                user.FullName,
                "WORKFLOW_ASSIGNED",
                variables,
                user.Id,
                loanApp.Id,
                loanApp.ApplicationNumber,
                NotificationPriority.Normal,
                ct);

            _logger.LogInformation("Workflow assignment notification sent to {Email}", user.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send workflow assignment notification");
        }
    }
}

/// <summary>
/// Handles committee events that trigger notifications.
/// </summary>
public class CommitteeVotingStartedNotificationHandler : IDomainEventHandler<CommitteeVotingStartedEvent>
{
    private readonly INotificationService _notificationService;
    private readonly ICommitteeReviewRepository _committeeRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILoanApplicationRepository _loanApplicationRepository;
    private readonly ILogger<CommitteeVotingStartedNotificationHandler> _logger;

    public CommitteeVotingStartedNotificationHandler(
        INotificationService notificationService,
        ICommitteeReviewRepository committeeRepository,
        IUserRepository userRepository,
        ILoanApplicationRepository loanApplicationRepository,
        ILogger<CommitteeVotingStartedNotificationHandler> logger)
    {
        _notificationService = notificationService;
        _committeeRepository = committeeRepository;
        _userRepository = userRepository;
        _loanApplicationRepository = loanApplicationRepository;
        _logger = logger;
    }

    public async Task HandleAsync(CommitteeVotingStartedEvent domainEvent, CancellationToken ct = default)
    {
        try
        {
            var review = await _committeeRepository.GetByIdAsync(domainEvent.ReviewId, ct);
            var loanApp = await _loanApplicationRepository.GetByIdAsync(domainEvent.LoanApplicationId, ct);

            if (review == null || loanApp == null)
            {
                _logger.LogWarning("Cannot send committee voting notification - review or loan app not found");
                return;
            }

            // Notify all committee members
            foreach (var member in review.Members)
            {
                var user = await _userRepository.GetByIdAsync(member.UserId, ct);
                if (user == null || string.IsNullOrEmpty(user.Email))
                    continue;

                var variables = new Dictionary<string, string>
                {
                    { "UserName", user.FullName },
                    { "ApplicationNumber", loanApp.ApplicationNumber },
                    { "CustomerName", loanApp.CustomerName },
                    { "Amount", loanApp.RequestedAmount.ToString() },
                    { "CommitteeType", review.CommitteeType.ToString() },
                    { "Deadline", domainEvent.Deadline?.ToString("dd-MMM-yyyy HH:mm") ?? "No deadline" }
                };

                await _notificationService.SendEmailAsync(
                    NotificationType.CommitteeVoteRequired,
                    user.Email,
                    user.FullName,
                    "COMMITTEE_VOTE_REQUIRED",
                    variables,
                    user.Id,
                    loanApp.Id,
                    loanApp.ApplicationNumber,
                    NotificationPriority.High,
                    ct);
            }

            _logger.LogInformation("Committee voting notifications sent for review {ReviewId}", domainEvent.ReviewId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send committee voting notifications");
        }
    }
}

/// <summary>
/// Handles loan application events that trigger notifications.
/// </summary>
public class LoanApplicationApprovedNotificationHandler : IDomainEventHandler<LoanApplicationApprovedEvent>
{
    private readonly INotificationService _notificationService;
    private readonly ILoanApplicationRepository _loanApplicationRepository;
    private readonly ILogger<LoanApplicationApprovedNotificationHandler> _logger;

    public LoanApplicationApprovedNotificationHandler(
        INotificationService notificationService,
        ILoanApplicationRepository loanApplicationRepository,
        ILogger<LoanApplicationApprovedNotificationHandler> logger)
    {
        _notificationService = notificationService;
        _loanApplicationRepository = loanApplicationRepository;
        _logger = logger;
    }

    public async Task HandleAsync(LoanApplicationApprovedEvent domainEvent, CancellationToken ct = default)
    {
        try
        {
            var loanApp = await _loanApplicationRepository.GetByIdAsync(domainEvent.ApplicationId, ct);
            if (loanApp == null)
            {
                _logger.LogWarning("Cannot send approval notification - loan app not found");
                return;
            }

            // For corporate loans, notify the initiating officer
            // For retail loans, notify the customer (would need customer email)
            _logger.LogInformation("Loan {ApplicationNumber} approved for {Amount}. Notification would be sent.",
                domainEvent.ApplicationNumber, domainEvent.ApprovedAmount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send loan approval notification");
        }
    }
}

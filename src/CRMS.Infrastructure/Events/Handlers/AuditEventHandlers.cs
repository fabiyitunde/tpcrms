using CRMS.Domain.Aggregates.Committee;
using CRMS.Domain.Aggregates.Configuration;
using CRMS.Domain.Aggregates.CreditBureau;
using CRMS.Domain.Aggregates.LoanApplication;
using CRMS.Domain.Aggregates.Workflow;
using CRMS.Domain.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Services;
using Microsoft.Extensions.Logging;

namespace CRMS.Infrastructure.Events.Handlers;

/// <summary>
/// Handles domain events that need to be recorded in the audit log.
/// </summary>
public class WorkflowTransitionAuditHandler : IDomainEventHandler<WorkflowTransitionedEvent>
{
    private readonly AuditService _auditService;
    private readonly ILogger<WorkflowTransitionAuditHandler> _logger;

    public WorkflowTransitionAuditHandler(AuditService auditService, ILogger<WorkflowTransitionAuditHandler> logger)
    {
        _auditService = auditService;
        _logger = logger;
    }

    public async Task HandleAsync(WorkflowTransitionedEvent domainEvent, CancellationToken ct = default)
    {
        _logger.LogInformation("Audit: Workflow transitioned from {From} to {To} for loan {LoanId}",
            domainEvent.FromStatus, domainEvent.ToStatus, domainEvent.LoanApplicationId);

        await _auditService.LogAsync(
            AuditAction.StatusChange,
            AuditCategory.Workflow,
            $"Workflow transitioned from {domainEvent.FromStatus} to {domainEvent.ToStatus}",
            "WorkflowInstance",
            domainEvent.WorkflowInstanceId,
            null,
            domainEvent.PerformedByUserId,
            null,
            null,
            null,
            domainEvent.LoanApplicationId,
            null,
            new { Status = domainEvent.FromStatus.ToString() },
            new { Status = domainEvent.ToStatus.ToString(), Action = domainEvent.Action.ToString() },
            ct: ct);
    }
}

public class CommitteeVoteAuditHandler : IDomainEventHandler<CommitteeVoteCastEvent>
{
    private readonly AuditService _auditService;
    private readonly ILogger<CommitteeVoteAuditHandler> _logger;

    public CommitteeVoteAuditHandler(AuditService auditService, ILogger<CommitteeVoteAuditHandler> logger)
    {
        _auditService = auditService;
        _logger = logger;
    }

    public async Task HandleAsync(CommitteeVoteCastEvent domainEvent, CancellationToken ct = default)
    {
        _logger.LogInformation("Audit: Committee vote {Vote} cast by {UserId} for loan {LoanId}",
            domainEvent.Vote, domainEvent.UserId, domainEvent.LoanApplicationId);

        await _auditService.LogAsync(
            AuditAction.Vote,
            AuditCategory.Committee,
            $"Committee vote cast: {domainEvent.Vote}",
            "CommitteeReview",
            domainEvent.ReviewId,
            null,
            domainEvent.UserId,
            null,
            null,
            null,
            domainEvent.LoanApplicationId,
            null,
            newValues: new { Vote = domainEvent.Vote.ToString() },
            ct: ct);
    }
}

public class CommitteeDecisionAuditHandler : IDomainEventHandler<CommitteeDecisionRecordedEvent>
{
    private readonly AuditService _auditService;
    private readonly ILogger<CommitteeDecisionAuditHandler> _logger;

    public CommitteeDecisionAuditHandler(AuditService auditService, ILogger<CommitteeDecisionAuditHandler> logger)
    {
        _auditService = auditService;
        _logger = logger;
    }

    public async Task HandleAsync(CommitteeDecisionRecordedEvent domainEvent, CancellationToken ct = default)
    {
        _logger.LogInformation("Audit: Committee decision {Decision} for loan {LoanId}",
            domainEvent.Decision, domainEvent.LoanApplicationId);

        await _auditService.LogAsync(
            AuditAction.Decision,
            AuditCategory.Committee,
            $"Committee decision recorded: {domainEvent.Decision}",
            "CommitteeReview",
            domainEvent.ReviewId,
            null,
            null,
            null,
            null,
            null,
            domainEvent.LoanApplicationId,
            null,
            newValues: new 
            { 
                Decision = domainEvent.Decision.ToString(),
                ApprovedAmount = domainEvent.ApprovedAmount,
                ApprovedTenor = domainEvent.ApprovedTenor,
                ApprovedRate = domainEvent.ApprovedRate
            },
            ct: ct);
    }
}

public class ScoringParameterChangeAuditHandler : IDomainEventHandler<ScoringParameterChangeApprovedEvent>
{
    private readonly AuditService _auditService;
    private readonly ILogger<ScoringParameterChangeAuditHandler> _logger;

    public ScoringParameterChangeAuditHandler(AuditService auditService, ILogger<ScoringParameterChangeAuditHandler> logger)
    {
        _auditService = auditService;
        _logger = logger;
    }

    public async Task HandleAsync(ScoringParameterChangeApprovedEvent domainEvent, CancellationToken ct = default)
    {
        _logger.LogInformation("Audit: Scoring parameter {Category}.{Key} changed from {Old} to {New}",
            domainEvent.Category, domainEvent.ParameterKey, domainEvent.PreviousValue, domainEvent.NewValue);

        await _auditService.LogAsync(
            AuditAction.ConfigApprove,
            AuditCategory.Configuration,
            $"Scoring parameter approved: {domainEvent.Category}.{domainEvent.ParameterKey}",
            "ScoringParameter",
            domainEvent.ParameterId,
            $"{domainEvent.Category}.{domainEvent.ParameterKey}",
            domainEvent.ApprovedByUserId,
            null,
            null,
            null,
            null,
            null,
            new { Value = domainEvent.PreviousValue },
            new { Value = domainEvent.NewValue },
            ct: ct);
    }
}

public class LoanApplicationCreatedAuditHandler : IDomainEventHandler<LoanApplicationCreatedEvent>
{
    private readonly AuditService _auditService;
    private readonly ILogger<LoanApplicationCreatedAuditHandler> _logger;

    public LoanApplicationCreatedAuditHandler(AuditService auditService, ILogger<LoanApplicationCreatedAuditHandler> logger)
    {
        _auditService = auditService;
        _logger = logger;
    }

    public async Task HandleAsync(LoanApplicationCreatedEvent domainEvent, CancellationToken ct = default)
    {
        _logger.LogInformation("Audit: Loan application {AppNumber} created", domainEvent.ApplicationNumber);

        await _auditService.LogAsync(
            AuditAction.Create,
            AuditCategory.LoanApplication,
            $"Loan application created: {domainEvent.ApplicationNumber}",
            "LoanApplication",
            domainEvent.ApplicationId,
            domainEvent.ApplicationNumber,
            null,
            null,
            null,
            null,
            domainEvent.ApplicationId,
            domainEvent.ApplicationNumber,
            newValues: new { Type = domainEvent.Type.ToString() },
            ct: ct);
    }
}

public class LoanApplicationApprovedAuditHandler : IDomainEventHandler<LoanApplicationApprovedEvent>
{
    private readonly AuditService _auditService;
    private readonly ILogger<LoanApplicationApprovedAuditHandler> _logger;

    public LoanApplicationApprovedAuditHandler(AuditService auditService, ILogger<LoanApplicationApprovedAuditHandler> logger)
    {
        _auditService = auditService;
        _logger = logger;
    }

    public async Task HandleAsync(LoanApplicationApprovedEvent domainEvent, CancellationToken ct = default)
    {
        _logger.LogInformation("Audit: Loan application {AppNumber} approved for {Amount}",
            domainEvent.ApplicationNumber, domainEvent.ApprovedAmount);

        await _auditService.LogAsync(
            AuditAction.Approve,
            AuditCategory.LoanApplication,
            $"Loan application approved: {domainEvent.ApplicationNumber}",
            "LoanApplication",
            domainEvent.ApplicationId,
            domainEvent.ApplicationNumber,
            null,
            null,
            null,
            null,
            domainEvent.ApplicationId,
            domainEvent.ApplicationNumber,
            newValues: new { ApprovedAmount = domainEvent.ApprovedAmount },
            ct: ct);
    }
}

/// <summary>
/// Audit handler for credit analysis initiation (NDPA/CBN compliance).
/// Logs when credit bureau checks are started for a loan application.
/// </summary>
public class CreditAnalysisStartedAuditHandler : IDomainEventHandler<CreditAnalysisStartedEvent>
{
    private readonly AuditService _auditService;
    private readonly ILogger<CreditAnalysisStartedAuditHandler> _logger;

    public CreditAnalysisStartedAuditHandler(AuditService auditService, ILogger<CreditAnalysisStartedAuditHandler> logger)
    {
        _auditService = auditService;
        _logger = logger;
    }

    public async Task HandleAsync(CreditAnalysisStartedEvent domainEvent, CancellationToken ct = default)
    {
        _logger.LogInformation("Audit: Credit analysis started for {AppNumber} with {TotalChecks} checks",
            domainEvent.ApplicationNumber, domainEvent.TotalChecks);

        await _auditService.LogAsync(
            AuditAction.StatusChange,
            AuditCategory.CreditBureau,
            $"Credit analysis started for {domainEvent.TotalChecks} parties",
            "LoanApplication",
            domainEvent.ApplicationId,
            domainEvent.ApplicationNumber,
            null,
            null,
            null,
            null,
            domainEvent.ApplicationId,
            domainEvent.ApplicationNumber,
            newValues: new { TotalChecks = domainEvent.TotalChecks },
            ct: ct);
    }
}

/// <summary>
/// Audit handler for credit bureau report requests (NDPA/CBN compliance).
/// Logs when a credit report is requested from the bureau - sensitive operation.
/// </summary>
public class BureauReportRequestedAuditHandler : IDomainEventHandler<BureauReportRequestedEvent>
{
    private readonly AuditService _auditService;
    private readonly ILogger<BureauReportRequestedAuditHandler> _logger;

    public BureauReportRequestedAuditHandler(AuditService auditService, ILogger<BureauReportRequestedAuditHandler> logger)
    {
        _auditService = auditService;
        _logger = logger;
    }

    public async Task HandleAsync(BureauReportRequestedEvent domainEvent, CancellationToken ct = default)
    {
        // Mask BVN for logging (show only last 4 digits)
        var maskedBvn = domainEvent.BVN != null && domainEvent.BVN.Length >= 4 
            ? $"****{domainEvent.BVN[^4..]}" 
            : "N/A";
        
        _logger.LogInformation("Audit: Credit bureau report requested for {SubjectName} (BVN: {MaskedBvn}) via {Provider}",
            domainEvent.SubjectName, maskedBvn, domainEvent.Provider);

        await _auditService.LogAsync(
            AuditAction.Read, // Accessing external credit data
            AuditCategory.CreditBureau,
            $"Credit bureau report requested for {domainEvent.SubjectName}",
            "BureauReport",
            domainEvent.ReportId,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            newValues: new 
            { 
                Provider = domainEvent.Provider.ToString(),
                SubjectName = domainEvent.SubjectName,
                BVN = maskedBvn, // Store masked BVN only
                ConsentRecordId = domainEvent.ConsentRecordId
            },
            ct: ct);
    }
}

/// <summary>
/// Audit handler for credit bureau report completion (NDPA/CBN compliance).
/// Logs when a credit report is received from the bureau.
/// </summary>
public class BureauReportCompletedAuditHandler : IDomainEventHandler<BureauReportCompletedEvent>
{
    private readonly AuditService _auditService;
    private readonly ILogger<BureauReportCompletedAuditHandler> _logger;

    public BureauReportCompletedAuditHandler(AuditService auditService, ILogger<BureauReportCompletedAuditHandler> logger)
    {
        _auditService = auditService;
        _logger = logger;
    }

    public async Task HandleAsync(BureauReportCompletedEvent domainEvent, CancellationToken ct = default)
    {
        _logger.LogInformation("Audit: Credit bureau report completed for {SubjectName} with score {CreditScore}",
            domainEvent.SubjectName, domainEvent.CreditScore ?? 0);

        await _auditService.LogAsync(
            AuditAction.Update,
            AuditCategory.CreditBureau,
            $"Credit bureau report completed for {domainEvent.SubjectName}",
            "BureauReport",
            domainEvent.ReportId,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            newValues: new 
            { 
                Provider = domainEvent.Provider.ToString(),
                SubjectName = domainEvent.SubjectName,
                CreditScore = domainEvent.CreditScore
            },
            ct: ct);
    }
}

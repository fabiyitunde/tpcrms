using CRMS.Domain.Aggregates.Committee;
using CRMS.Domain.Aggregates.LoanApplication;
using CRMS.Domain.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using CRMS.Domain.Services;
using CRMS.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace CRMS.Infrastructure.Events.Handlers;

/// <summary>
/// Handles committee decision events and updates loan application status and workflow accordingly.
/// </summary>
public class CommitteeDecisionWorkflowHandler : IDomainEventHandler<CommitteeDecisionRecordedEvent>
{
    private readonly ILoanApplicationRepository _loanApplicationRepository;
    private readonly IWorkflowInstanceRepository _workflowInstanceRepository;
    private readonly WorkflowService _workflowService;
    private readonly ILogger<CommitteeDecisionWorkflowHandler> _logger;

    public CommitteeDecisionWorkflowHandler(
        ILoanApplicationRepository loanApplicationRepository,
        IWorkflowInstanceRepository workflowInstanceRepository,
        WorkflowService workflowService,
        ILogger<CommitteeDecisionWorkflowHandler> logger)
    {
        _loanApplicationRepository = loanApplicationRepository;
        _workflowInstanceRepository = workflowInstanceRepository;
        _workflowService = workflowService;
        _logger = logger;
    }

    public async Task HandleAsync(CommitteeDecisionRecordedEvent domainEvent, CancellationToken ct = default)
    {
        _logger.LogInformation("Processing committee decision {Decision} for loan {LoanId}",
            domainEvent.Decision, domainEvent.LoanApplicationId);

        try
        {
            var loanApplication = await _loanApplicationRepository.GetByIdAsync(domainEvent.LoanApplicationId, ct);
            if (loanApplication == null)
            {
                _logger.LogError("Loan application {LoanId} not found for committee decision", domainEvent.LoanApplicationId);
                return;
            }

            var workflowInstance = await _workflowInstanceRepository.GetByLoanApplicationIdAsync(domainEvent.LoanApplicationId, ct);
            if (workflowInstance == null)
            {
                _logger.LogError("Workflow instance not found for loan {LoanId}", domainEvent.LoanApplicationId);
                return;
            }

            LoanApplicationStatus targetStatus;
            WorkflowAction action;

            switch (domainEvent.Decision)
            {
                case CommitteeDecision.Approved:
                    targetStatus = LoanApplicationStatus.CommitteeApproved;
                    action = WorkflowAction.Approve;
                    
                    // Update loan application with approved terms using existing method
                    if (domainEvent.ApprovedAmount.HasValue)
                    {
                        var approvedMoney = Money.Create(domainEvent.ApprovedAmount.Value, "NGN");
                        loanApplication.ApproveCommittee(
                            Guid.Empty, // System action
                            approvedMoney,
                            domainEvent.ApprovedTenor ?? loanApplication.RequestedTenorMonths,
                            domainEvent.ApprovedRate ?? 0,
                            "Committee decision: Approved");
                    }
                    break;

                case CommitteeDecision.Rejected:
                    targetStatus = LoanApplicationStatus.CommitteeRejected;
                    action = WorkflowAction.Reject;
                    loanApplication.RejectCommittee(Guid.Empty, "Committee decision: Rejected");
                    break;

                case CommitteeDecision.Deferred:
                    // Deferred means return to HO Review for more information
                    targetStatus = LoanApplicationStatus.HOReview;
                    action = WorkflowAction.Return;
                    break;

                default:
                    _logger.LogWarning("Unknown committee decision: {Decision}", domainEvent.Decision);
                    return;
            }

            // Transition workflow
            var transitionResult = await _workflowService.TransitionAsync(
                workflowInstance.Id,
                targetStatus,
                action,
                Guid.Empty, // System action
                "System",
                $"Auto-transition based on committee decision: {domainEvent.Decision}",
                ct);

            if (transitionResult.IsFailure)
            {
                _logger.LogError("Failed to transition workflow for loan {LoanId}: {Error}",
                    domainEvent.LoanApplicationId, transitionResult.Error);
                return;
            }

            _loanApplicationRepository.Update(loanApplication);

            _logger.LogInformation("Successfully processed committee decision for loan {LoanId}. New status: {Status}",
                domainEvent.LoanApplicationId, targetStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing committee decision for loan {LoanId}", domainEvent.LoanApplicationId);
            throw;
        }
    }
}

/// <summary>
/// Handles credit checks completed event and transitions workflow to HO Review.
/// </summary>
public class AllCreditChecksCompletedWorkflowHandler : IDomainEventHandler<AllCreditChecksCompletedEvent>
{
    private readonly IWorkflowInstanceRepository _workflowInstanceRepository;
    private readonly WorkflowService _workflowService;
    private readonly ILogger<AllCreditChecksCompletedWorkflowHandler> _logger;

    public AllCreditChecksCompletedWorkflowHandler(
        IWorkflowInstanceRepository workflowInstanceRepository,
        WorkflowService workflowService,
        ILogger<AllCreditChecksCompletedWorkflowHandler> logger)
    {
        _workflowInstanceRepository = workflowInstanceRepository;
        _workflowService = workflowService;
        _logger = logger;
    }

    public async Task HandleAsync(AllCreditChecksCompletedEvent domainEvent, CancellationToken ct = default)
    {
        _logger.LogInformation("All credit checks completed for loan {LoanId}. Transitioning to HO Review.",
            domainEvent.ApplicationId);

        try
        {
            var workflowInstance = await _workflowInstanceRepository.GetByLoanApplicationIdAsync(domainEvent.ApplicationId, ct);
            if (workflowInstance == null)
            {
                _logger.LogError("Workflow instance not found for loan {LoanId}", domainEvent.ApplicationId);
                return;
            }

            // Only transition if currently in CreditAnalysis stage
            if (workflowInstance.CurrentStatus != LoanApplicationStatus.CreditAnalysis)
            {
                _logger.LogWarning("Loan {LoanId} is not in CreditAnalysis stage (current: {Status}). Skipping auto-transition.",
                    domainEvent.ApplicationId, workflowInstance.CurrentStatus);
                return;
            }

            var transitionResult = await _workflowService.TransitionAsync(
                workflowInstance.Id,
                LoanApplicationStatus.HOReview,
                WorkflowAction.MoveToNextStage,
                Guid.Empty, // System action
                "System",
                "Auto-transition: All credit checks completed",
                ct);

            if (transitionResult.IsFailure)
            {
                _logger.LogError("Failed to transition workflow for loan {LoanId}: {Error}",
                    domainEvent.ApplicationId, transitionResult.Error);
                return;
            }

            _logger.LogInformation("Successfully transitioned loan {LoanId} to HO Review", domainEvent.ApplicationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error transitioning loan {LoanId} after credit checks completed", domainEvent.ApplicationId);
            throw;
        }
    }
}

using CRMS.Domain.Aggregates.Committee;
using CRMS.Domain.Aggregates.LoanApplication;
using CRMS.Domain.Common;
using CRMS.Domain.Constants;
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CommitteeDecisionWorkflowHandler> _logger;

    public CommitteeDecisionWorkflowHandler(
        ILoanApplicationRepository loanApplicationRepository,
        IWorkflowInstanceRepository workflowInstanceRepository,
        WorkflowService workflowService,
        IUnitOfWork unitOfWork,
        ILogger<CommitteeDecisionWorkflowHandler> logger)
    {
        _loanApplicationRepository = loanApplicationRepository;
        _workflowInstanceRepository = workflowInstanceRepository;
        _workflowService = workflowService;
        _unitOfWork = unitOfWork;
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

            switch (domainEvent.Decision)
            {
                case CommitteeDecision.Approved:
                {
                    // Record committee approval on the loan application
                    if (domainEvent.ApprovedAmount.HasValue)
                    {
                        var approvedMoney = Money.Create(domainEvent.ApprovedAmount.Value, "NGN");
                        loanApplication.ApproveCommittee(
                            domainEvent.DecisionByUserId,
                            approvedMoney,
                            domainEvent.ApprovedTenor ?? loanApplication.RequestedTenorMonths,
                            domainEvent.ApprovedRate ?? 0,
                            "Committee decision: Approved");
                    }

                    // Step 1: Record the committee milestone in the workflow history
                    var committeeApprovedResult = await _workflowService.TransitionAsync(
                        workflowInstance.Id,
                        LoanApplicationStatus.CommitteeApproved,
                        WorkflowAction.MoveToNextStage,
                        domainEvent.DecisionByUserId,
                        "SystemAdmin", // SystemAdmin bypasses role check for system-driven transitions
                        "Committee voted: Approved",
                        ct);

                    if (committeeApprovedResult.IsFailure)
                    {
                        _logger.LogError("Failed to transition workflow to CommitteeApproved for loan {LoanId}: {Error}",
                            domainEvent.LoanApplicationId, committeeApprovedResult.Error);
                        return;
                    }

                    // Step 2: Auto-transition to FinalApproval — MD/CEO sign-off queue
                    loanApplication.MoveToFinalApproval(SystemConstants.SystemUserId);

                    var finalApprovalResult = await _workflowService.TransitionAsync(
                        workflowInstance.Id,
                        LoanApplicationStatus.FinalApproval,
                        WorkflowAction.MoveToNextStage,
                        SystemConstants.SystemUserId,
                        "SystemAdmin",
                        "Auto-transition: Awaiting MD/CEO executive sign-off",
                        ct);

                    if (finalApprovalResult.IsFailure)
                    {
                        _logger.LogError("Failed to transition workflow to FinalApproval for loan {LoanId}: {Error}",
                            domainEvent.LoanApplicationId, finalApprovalResult.Error);
                        return;
                    }

                    _loanApplicationRepository.Update(loanApplication);
                    await _unitOfWork.SaveChangesAsync(ct);

                    _logger.LogInformation("Successfully processed committee approval for loan {LoanId}. Moved to FinalApproval.",
                        domainEvent.LoanApplicationId);
                    return;
                }

                case CommitteeDecision.Rejected:
                    loanApplication.RejectCommittee(domainEvent.DecisionByUserId, "Committee decision: Rejected");
                    break;

                case CommitteeDecision.Deferred:
                {
                    var deferResult = loanApplication.DeferFromCommittee(
                        domainEvent.DecisionByUserId,
                        domainEvent.Rationale ?? "Additional information required");
                    if (deferResult.IsFailure)
                    {
                        _logger.LogError("DeferFromCommittee failed for loan {LoanId}: {Error}",
                            domainEvent.LoanApplicationId, deferResult.Error);
                        return;
                    }
                    break;
                }

                default:
                    _logger.LogWarning("Unknown committee decision: {Decision}", domainEvent.Decision);
                    return;
            }

            // Rejected / Deferred: single transition
            LoanApplicationStatus targetStatus = domainEvent.Decision == CommitteeDecision.Rejected
                ? LoanApplicationStatus.CommitteeRejected
                : LoanApplicationStatus.HOReview;
            WorkflowAction action = domainEvent.Decision == CommitteeDecision.Rejected
                ? WorkflowAction.Reject
                : WorkflowAction.Return;

            var transitionResult = await _workflowService.TransitionAsync(
                workflowInstance.Id,
                targetStatus,
                action,
                domainEvent.DecisionByUserId,
                "SystemAdmin",
                $"Auto-transition based on committee decision: {domainEvent.Decision}",
                ct);

            if (transitionResult.IsFailure)
            {
                _logger.LogError("Failed to transition workflow for loan {LoanId}: {Error}",
                    domainEvent.LoanApplicationId, transitionResult.Error);
                return;
            }

            _loanApplicationRepository.Update(loanApplication);
            await _unitOfWork.SaveChangesAsync(ct);

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
    private readonly ILoanApplicationRepository _loanApplicationRepository;
    private readonly IWorkflowInstanceRepository _workflowInstanceRepository;
    private readonly WorkflowService _workflowService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AllCreditChecksCompletedWorkflowHandler> _logger;

    public AllCreditChecksCompletedWorkflowHandler(
        ILoanApplicationRepository loanApplicationRepository,
        IWorkflowInstanceRepository workflowInstanceRepository,
        WorkflowService workflowService,
        IUnitOfWork unitOfWork,
        ILogger<AllCreditChecksCompletedWorkflowHandler> logger)
    {
        _loanApplicationRepository = loanApplicationRepository;
        _workflowInstanceRepository = workflowInstanceRepository;
        _workflowService = workflowService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task HandleAsync(AllCreditChecksCompletedEvent domainEvent, CancellationToken ct = default)
    {
        // All bureau checks are now complete. The Credit Officer reviews the results on the
        // application detail page and manually advances the application to HO Review via the
        // Approve button. No automatic transition is performed here.
        _logger.LogInformation(
            "All credit checks completed for loan {LoanId}. Application is ready for Credit Officer review.",
            domainEvent.ApplicationId);
    }
}


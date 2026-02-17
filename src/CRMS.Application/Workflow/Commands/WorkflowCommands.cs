using CRMS.Application.Common;
using CRMS.Application.Workflow.DTOs;
using CRMS.Domain.Aggregates.Workflow;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using CRMS.Domain.Services;

namespace CRMS.Application.Workflow.Commands;

// Initialize workflow for a loan application
public record InitializeWorkflowCommand(
    Guid LoanApplicationId,
    LoanApplicationType ApplicationType,
    LoanApplicationStatus InitialStatus,
    Guid InitiatedByUserId
) : IRequest<ApplicationResult<WorkflowInstanceDto>>;

public class InitializeWorkflowHandler : IRequestHandler<InitializeWorkflowCommand, ApplicationResult<WorkflowInstanceDto>>
{
    private readonly WorkflowService _workflowService;
    private readonly IUnitOfWork _unitOfWork;

    public InitializeWorkflowHandler(WorkflowService workflowService, IUnitOfWork unitOfWork)
    {
        _workflowService = workflowService;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<WorkflowInstanceDto>> Handle(InitializeWorkflowCommand request, CancellationToken ct = default)
    {
        var result = await _workflowService.InitializeWorkflowAsync(
            request.LoanApplicationId,
            request.ApplicationType,
            request.InitialStatus,
            request.InitiatedByUserId,
            ct);

        if (result.IsFailure)
            return ApplicationResult<WorkflowInstanceDto>.Failure(result.Error);

        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<WorkflowInstanceDto>.Success(MapToDto(result.Value));
    }

    private static WorkflowInstanceDto MapToDto(WorkflowInstance instance) => WorkflowMapper.ToDto(instance);
}

// Transition workflow to next state
public record TransitionWorkflowCommand(
    Guid WorkflowInstanceId,
    LoanApplicationStatus ToStatus,
    WorkflowAction Action,
    Guid PerformedByUserId,
    string UserRole,
    string? Comment
) : IRequest<ApplicationResult<WorkflowInstanceDto>>;

public class TransitionWorkflowHandler : IRequestHandler<TransitionWorkflowCommand, ApplicationResult<WorkflowInstanceDto>>
{
    private readonly WorkflowService _workflowService;
    private readonly IWorkflowInstanceRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public TransitionWorkflowHandler(
        WorkflowService workflowService,
        IWorkflowInstanceRepository repository,
        IUnitOfWork unitOfWork)
    {
        _workflowService = workflowService;
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<WorkflowInstanceDto>> Handle(TransitionWorkflowCommand request, CancellationToken ct = default)
    {
        var result = await _workflowService.TransitionAsync(
            request.WorkflowInstanceId,
            request.ToStatus,
            request.Action,
            request.PerformedByUserId,
            request.UserRole,
            request.Comment,
            ct);

        if (result.IsFailure)
            return ApplicationResult<WorkflowInstanceDto>.Failure(result.Error);

        await _unitOfWork.SaveChangesAsync(ct);

        var instance = await _repository.GetByIdAsync(request.WorkflowInstanceId, ct);
        return ApplicationResult<WorkflowInstanceDto>.Success(WorkflowMapper.ToDto(instance!));
    }
}

// Assign workflow to user
public record AssignWorkflowCommand(
    Guid WorkflowInstanceId,
    Guid AssignToUserId,
    Guid AssignedByUserId
) : IRequest<ApplicationResult<WorkflowInstanceDto>>;

public class AssignWorkflowHandler : IRequestHandler<AssignWorkflowCommand, ApplicationResult<WorkflowInstanceDto>>
{
    private readonly WorkflowService _workflowService;
    private readonly IWorkflowInstanceRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public AssignWorkflowHandler(
        WorkflowService workflowService,
        IWorkflowInstanceRepository repository,
        IUnitOfWork unitOfWork)
    {
        _workflowService = workflowService;
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<WorkflowInstanceDto>> Handle(AssignWorkflowCommand request, CancellationToken ct = default)
    {
        var result = await _workflowService.AssignAsync(
            request.WorkflowInstanceId,
            request.AssignToUserId,
            request.AssignedByUserId,
            ct);

        if (result.IsFailure)
            return ApplicationResult<WorkflowInstanceDto>.Failure(result.Error);

        await _unitOfWork.SaveChangesAsync(ct);

        var instance = await _repository.GetByIdAsync(request.WorkflowInstanceId, ct);
        return ApplicationResult<WorkflowInstanceDto>.Success(WorkflowMapper.ToDto(instance!));
    }
}

// Unassign workflow
public record UnassignWorkflowCommand(
    Guid WorkflowInstanceId,
    Guid UnassignedByUserId
) : IRequest<ApplicationResult<WorkflowInstanceDto>>;

public class UnassignWorkflowHandler : IRequestHandler<UnassignWorkflowCommand, ApplicationResult<WorkflowInstanceDto>>
{
    private readonly IWorkflowInstanceRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UnassignWorkflowHandler(IWorkflowInstanceRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<WorkflowInstanceDto>> Handle(UnassignWorkflowCommand request, CancellationToken ct = default)
    {
        var instance = await _repository.GetByIdAsync(request.WorkflowInstanceId, ct);
        if (instance == null)
            return ApplicationResult<WorkflowInstanceDto>.Failure("Workflow instance not found");

        var result = instance.Unassign(request.UnassignedByUserId);
        if (result.IsFailure)
            return ApplicationResult<WorkflowInstanceDto>.Failure(result.Error);

        _repository.Update(instance);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<WorkflowInstanceDto>.Success(WorkflowMapper.ToDto(instance));
    }
}

// Escalate workflow
public record EscalateWorkflowCommand(
    Guid WorkflowInstanceId,
    Guid EscalatedByUserId,
    string Reason
) : IRequest<ApplicationResult<WorkflowInstanceDto>>;

public class EscalateWorkflowHandler : IRequestHandler<EscalateWorkflowCommand, ApplicationResult<WorkflowInstanceDto>>
{
    private readonly IWorkflowInstanceRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public EscalateWorkflowHandler(IWorkflowInstanceRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<WorkflowInstanceDto>> Handle(EscalateWorkflowCommand request, CancellationToken ct = default)
    {
        var instance = await _repository.GetByIdAsync(request.WorkflowInstanceId, ct);
        if (instance == null)
            return ApplicationResult<WorkflowInstanceDto>.Failure("Workflow instance not found");

        var result = instance.Escalate(request.EscalatedByUserId, request.Reason);
        if (result.IsFailure)
            return ApplicationResult<WorkflowInstanceDto>.Failure(result.Error);

        _repository.Update(instance);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<WorkflowInstanceDto>.Success(WorkflowMapper.ToDto(instance));
    }
}

// Seed corporate loan workflow definition
public record SeedCorporateLoanWorkflowCommand(Guid CreatedByUserId) : IRequest<ApplicationResult<Guid>>;

public class SeedCorporateLoanWorkflowHandler : IRequestHandler<SeedCorporateLoanWorkflowCommand, ApplicationResult<Guid>>
{
    private readonly IWorkflowDefinitionRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public SeedCorporateLoanWorkflowHandler(IWorkflowDefinitionRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<Guid>> Handle(SeedCorporateLoanWorkflowCommand request, CancellationToken ct = default)
    {
        // Check if already exists
        var existing = await _repository.GetActiveByTypeAsync(LoanApplicationType.Corporate, ct);
        if (existing != null)
            return ApplicationResult<Guid>.Success(existing.Id);

        var definitionResult = WorkflowDefinition.Create(
            "Corporate Loan Workflow",
            "Standard workflow for corporate loan applications with branch, HO, and committee approval stages",
            LoanApplicationType.Corporate);

        if (definitionResult.IsFailure)
            return ApplicationResult<Guid>.Failure(definitionResult.Error);

        var definition = definitionResult.Value;

        // Add stages
        definition.AddStage(LoanApplicationStatus.Draft, "Draft", "Application being prepared", "LoanOfficer", 0, 1);
        definition.AddStage(LoanApplicationStatus.Submitted, "Submitted", "Submitted for processing", "LoanOfficer", 4, 2);
        definition.AddStage(LoanApplicationStatus.DataGathering, "Data Gathering", "Collecting required documents", "LoanOfficer", 24, 3);
        definition.AddStage(LoanApplicationStatus.BranchReview, "Branch Review", "Awaiting branch approval", "BranchApprover", 8, 4, requiresComment: true);
        definition.AddStage(LoanApplicationStatus.BranchApproved, "Branch Approved", "Approved by branch, awaiting credit checks", "System", 1, 5);
        definition.AddStage(LoanApplicationStatus.BranchReturned, "Returned", "Returned for corrections", "LoanOfficer", 24, 6);
        definition.AddStage(LoanApplicationStatus.BranchRejected, "Branch Rejected", "Rejected at branch level", "None", 0, 7, isTerminal: true);
        definition.AddStage(LoanApplicationStatus.CreditAnalysis, "Credit Analysis", "Credit checks in progress", "System", 48, 8);
        definition.AddStage(LoanApplicationStatus.HOReview, "HO Review", "Head Office review", "CreditOfficer", 24, 9, requiresComment: true);
        definition.AddStage(LoanApplicationStatus.CommitteeCirculation, "Committee Circulation", "Under committee review", "CommitteeMember", 72, 10);
        definition.AddStage(LoanApplicationStatus.CommitteeApproved, "Committee Approved", "Approved by committee", "FinalApprover", 8, 11);
        definition.AddStage(LoanApplicationStatus.CommitteeRejected, "Committee Rejected", "Rejected by committee", "None", 0, 12, isTerminal: true);
        definition.AddStage(LoanApplicationStatus.Approved, "Final Approved", "Final approval granted", "Operations", 4, 13);
        definition.AddStage(LoanApplicationStatus.Rejected, "Rejected", "Application rejected", "None", 0, 14, isTerminal: true);
        definition.AddStage(LoanApplicationStatus.OfferGenerated, "Offer Generated", "Offer letter ready", "Operations", 24, 15);
        definition.AddStage(LoanApplicationStatus.OfferAccepted, "Offer Accepted", "Customer accepted offer", "Operations", 48, 16);
        definition.AddStage(LoanApplicationStatus.Disbursed, "Disbursed", "Loan disbursed", "None", 0, 17, isTerminal: true);

        // Add transitions
        // Draft -> Submitted
        definition.AddTransition(LoanApplicationStatus.Draft, LoanApplicationStatus.Submitted, WorkflowAction.Submit, "LoanOfficer");
        
        // Submitted -> DataGathering
        definition.AddTransition(LoanApplicationStatus.Submitted, LoanApplicationStatus.DataGathering, WorkflowAction.MoveToNextStage, "LoanOfficer");
        
        // DataGathering -> BranchReview
        definition.AddTransition(LoanApplicationStatus.DataGathering, LoanApplicationStatus.BranchReview, WorkflowAction.Submit, "LoanOfficer");
        
        // BranchReview actions
        definition.AddTransition(LoanApplicationStatus.BranchReview, LoanApplicationStatus.BranchApproved, WorkflowAction.Approve, "BranchApprover", requiresComment: true);
        definition.AddTransition(LoanApplicationStatus.BranchReview, LoanApplicationStatus.BranchReturned, WorkflowAction.Return, "BranchApprover", requiresComment: true);
        definition.AddTransition(LoanApplicationStatus.BranchReview, LoanApplicationStatus.BranchRejected, WorkflowAction.Reject, "BranchApprover", requiresComment: true);
        
        // BranchReturned -> BranchReview (resubmit)
        definition.AddTransition(LoanApplicationStatus.BranchReturned, LoanApplicationStatus.BranchReview, WorkflowAction.Submit, "LoanOfficer");
        
        // BranchApproved -> CreditAnalysis (system auto-transition)
        definition.AddTransition(LoanApplicationStatus.BranchApproved, LoanApplicationStatus.CreditAnalysis, WorkflowAction.MoveToNextStage, "System");
        
        // CreditAnalysis -> HOReview
        definition.AddTransition(LoanApplicationStatus.CreditAnalysis, LoanApplicationStatus.HOReview, WorkflowAction.MoveToNextStage, "System");
        
        // HOReview actions
        definition.AddTransition(LoanApplicationStatus.HOReview, LoanApplicationStatus.CommitteeCirculation, WorkflowAction.Approve, "CreditOfficer", requiresComment: true);
        definition.AddTransition(LoanApplicationStatus.HOReview, LoanApplicationStatus.Rejected, WorkflowAction.Reject, "CreditOfficer", requiresComment: true);
        definition.AddTransition(LoanApplicationStatus.HOReview, LoanApplicationStatus.BranchReview, WorkflowAction.Return, "CreditOfficer", requiresComment: true);
        
        // Committee actions
        definition.AddTransition(LoanApplicationStatus.CommitteeCirculation, LoanApplicationStatus.CommitteeApproved, WorkflowAction.Approve, "CommitteeMember", requiresComment: true);
        definition.AddTransition(LoanApplicationStatus.CommitteeCirculation, LoanApplicationStatus.CommitteeRejected, WorkflowAction.Reject, "CommitteeMember", requiresComment: true);
        
        // CommitteeApproved -> Approved (final)
        definition.AddTransition(LoanApplicationStatus.CommitteeApproved, LoanApplicationStatus.Approved, WorkflowAction.Approve, "FinalApprover", requiresComment: true);
        definition.AddTransition(LoanApplicationStatus.CommitteeApproved, LoanApplicationStatus.Rejected, WorkflowAction.Reject, "FinalApprover", requiresComment: true);
        
        // Approved -> OfferGenerated
        definition.AddTransition(LoanApplicationStatus.Approved, LoanApplicationStatus.OfferGenerated, WorkflowAction.MoveToNextStage, "Operations");
        
        // OfferGenerated -> OfferAccepted
        definition.AddTransition(LoanApplicationStatus.OfferGenerated, LoanApplicationStatus.OfferAccepted, WorkflowAction.Approve, "Operations");
        
        // OfferAccepted -> Disbursed
        definition.AddTransition(LoanApplicationStatus.OfferAccepted, LoanApplicationStatus.Disbursed, WorkflowAction.Complete, "Operations");

        await _repository.AddAsync(definition, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<Guid>.Success(definition.Id);
    }
}

// Mapper
internal static class WorkflowMapper
{
    public static WorkflowInstanceDto ToDto(WorkflowInstance instance) => new(
        instance.Id,
        instance.LoanApplicationId,
        instance.WorkflowDefinitionId,
        instance.CurrentStatus.ToString(),
        instance.CurrentStageDisplayName,
        instance.AssignedRole,
        instance.AssignedToUserId,
        instance.AssignedAt,
        instance.EnteredCurrentStageAt,
        instance.SLADueAt,
        instance.IsSLABreached,
        instance.EscalationLevel,
        instance.IsCompleted,
        instance.CompletedAt,
        instance.FinalStatus?.ToString(),
        instance.GetTimeInCurrentStage(),
        instance.GetRemainingTime(),
        instance.TransitionHistory.Take(10).Select(t => new WorkflowTransitionLogDto(
            t.Id,
            t.FromStatus?.ToString(),
            t.ToStatus.ToString(),
            t.Action.ToString(),
            t.PerformedByUserId,
            t.PerformedAt,
            t.Comment
        )).ToList()
    );

    public static WorkflowDefinitionDto ToDto(WorkflowDefinition definition) => new(
        definition.Id,
        definition.Name,
        definition.Description,
        definition.ApplicationType.ToString(),
        definition.IsActive,
        definition.Version,
        definition.Stages.OrderBy(s => s.SortOrder).Select(s => new WorkflowStageDto(
            s.Id,
            s.Status.ToString(),
            s.DisplayName,
            s.Description,
            s.AssignedRole,
            s.SLAHours,
            s.SortOrder,
            s.RequiresComment,
            s.IsTerminal
        )).ToList(),
        definition.Transitions.Select(t => new WorkflowTransitionDto(
            t.Id,
            t.FromStatus.ToString(),
            t.ToStatus.ToString(),
            t.Action.ToString(),
            t.RequiredRole,
            t.RequiresComment
        )).ToList()
    );
}

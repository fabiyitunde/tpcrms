using CRMS.Application.Common;
using CRMS.Application.Workflow.Commands;
using CRMS.Application.Workflow.DTOs;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using CRMS.Domain.Services;

namespace CRMS.Application.Workflow.Queries;

// Get workflow instance by ID
public record GetWorkflowInstanceByIdQuery(Guid Id) : IRequest<ApplicationResult<WorkflowInstanceDto>>;

public class GetWorkflowInstanceByIdHandler : IRequestHandler<GetWorkflowInstanceByIdQuery, ApplicationResult<WorkflowInstanceDto>>
{
    private readonly IWorkflowInstanceRepository _repository;

    public GetWorkflowInstanceByIdHandler(IWorkflowInstanceRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<WorkflowInstanceDto>> Handle(GetWorkflowInstanceByIdQuery request, CancellationToken ct = default)
    {
        var instance = await _repository.GetByIdAsync(request.Id, ct);
        if (instance == null)
            return ApplicationResult<WorkflowInstanceDto>.Failure("Workflow instance not found");

        return ApplicationResult<WorkflowInstanceDto>.Success(WorkflowMapper.ToDto(instance));
    }
}

// Get workflow by loan application
public record GetWorkflowByLoanApplicationQuery(Guid LoanApplicationId) : IRequest<ApplicationResult<WorkflowInstanceDto>>;

public class GetWorkflowByLoanApplicationHandler : IRequestHandler<GetWorkflowByLoanApplicationQuery, ApplicationResult<WorkflowInstanceDto>>
{
    private readonly IWorkflowInstanceRepository _repository;

    public GetWorkflowByLoanApplicationHandler(IWorkflowInstanceRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<WorkflowInstanceDto>> Handle(GetWorkflowByLoanApplicationQuery request, CancellationToken ct = default)
    {
        var instance = await _repository.GetByLoanApplicationIdAsync(request.LoanApplicationId, ct);
        if (instance == null)
            return ApplicationResult<WorkflowInstanceDto>.Failure("No workflow found for this loan application");

        return ApplicationResult<WorkflowInstanceDto>.Success(WorkflowMapper.ToDto(instance));
    }
}

// Get available actions for current user
public record GetAvailableActionsQuery(
    Guid WorkflowInstanceId,
    string UserRole
) : IRequest<ApplicationResult<List<AvailableActionDto>>>;

public class GetAvailableActionsHandler : IRequestHandler<GetAvailableActionsQuery, ApplicationResult<List<AvailableActionDto>>>
{
    private readonly WorkflowService _workflowService;

    public GetAvailableActionsHandler(WorkflowService workflowService)
    {
        _workflowService = workflowService;
    }

    public async Task<ApplicationResult<List<AvailableActionDto>>> Handle(GetAvailableActionsQuery request, CancellationToken ct = default)
    {
        var result = await _workflowService.GetAvailableActionsAsync(
            request.WorkflowInstanceId,
            request.UserRole,
            ct);

        if (result.IsFailure)
            return ApplicationResult<List<AvailableActionDto>>.Failure(result.Error);

        var dtos = result.Value.Select(a => new AvailableActionDto(
            a.Action.ToString(),
            a.ToStatus.ToString(),
            a.RequiresComment,
            a.DisplayName
        )).ToList();

        return ApplicationResult<List<AvailableActionDto>>.Success(dtos);
    }
}

// Get queue for role
public record GetWorkflowQueueByRoleQuery(
    string Role,
    int PageNumber = 1,
    int PageSize = 20
) : IRequest<ApplicationResult<List<WorkflowInstanceSummaryDto>>>;

public class GetWorkflowQueueByRoleHandler : IRequestHandler<GetWorkflowQueueByRoleQuery, ApplicationResult<List<WorkflowInstanceSummaryDto>>>
{
    private readonly IWorkflowInstanceRepository _workflowRepository;
    private readonly ILoanApplicationRepository _loanRepository;

    public GetWorkflowQueueByRoleHandler(
        IWorkflowInstanceRepository workflowRepository,
        ILoanApplicationRepository loanRepository)
    {
        _workflowRepository = workflowRepository;
        _loanRepository = loanRepository;
    }

    public async Task<ApplicationResult<List<WorkflowInstanceSummaryDto>>> Handle(GetWorkflowQueueByRoleQuery request, CancellationToken ct = default)
    {
        var instances = await _workflowRepository.GetByAssignedRoleAsync(request.Role, ct);
        
        var summaries = new List<WorkflowInstanceSummaryDto>();
        foreach (var instance in instances)
        {
            var loan = await _loanRepository.GetByIdAsync(instance.LoanApplicationId, ct);
            if (loan != null)
            {
                summaries.Add(new WorkflowInstanceSummaryDto(
                    instance.Id,
                    instance.LoanApplicationId,
                    loan.ApplicationNumber,
                    loan.CustomerName,
                    instance.CurrentStatus.ToString(),
                    instance.CurrentStageDisplayName,
                    instance.AssignedRole,
                    instance.AssignedToUserId,
                    instance.SLADueAt,
                    instance.IsSLABreached,
                    instance.IsSLADue()
                ));
            }
        }

        return ApplicationResult<List<WorkflowInstanceSummaryDto>>.Success(summaries);
    }
}

// Get user's assigned work
public record GetMyWorkflowQueueQuery(
    Guid UserId
) : IRequest<ApplicationResult<List<WorkflowInstanceSummaryDto>>>;

public class GetMyWorkflowQueueHandler : IRequestHandler<GetMyWorkflowQueueQuery, ApplicationResult<List<WorkflowInstanceSummaryDto>>>
{
    private readonly IWorkflowInstanceRepository _workflowRepository;
    private readonly ILoanApplicationRepository _loanRepository;

    public GetMyWorkflowQueueHandler(
        IWorkflowInstanceRepository workflowRepository,
        ILoanApplicationRepository loanRepository)
    {
        _workflowRepository = workflowRepository;
        _loanRepository = loanRepository;
    }

    public async Task<ApplicationResult<List<WorkflowInstanceSummaryDto>>> Handle(GetMyWorkflowQueueQuery request, CancellationToken ct = default)
    {
        var instances = await _workflowRepository.GetByAssignedUserAsync(request.UserId, ct);
        
        var summaries = new List<WorkflowInstanceSummaryDto>();
        foreach (var instance in instances)
        {
            var loan = await _loanRepository.GetByIdAsync(instance.LoanApplicationId, ct);
            if (loan != null)
            {
                summaries.Add(new WorkflowInstanceSummaryDto(
                    instance.Id,
                    instance.LoanApplicationId,
                    loan.ApplicationNumber,
                    loan.CustomerName,
                    instance.CurrentStatus.ToString(),
                    instance.CurrentStageDisplayName,
                    instance.AssignedRole,
                    instance.AssignedToUserId,
                    instance.SLADueAt,
                    instance.IsSLABreached,
                    instance.IsSLADue()
                ));
            }
        }

        return ApplicationResult<List<WorkflowInstanceSummaryDto>>.Success(summaries);
    }
}

// Get overdue workflows
public record GetOverdueWorkflowsQuery : IRequest<ApplicationResult<List<WorkflowInstanceSummaryDto>>>;

public class GetOverdueWorkflowsHandler : IRequestHandler<GetOverdueWorkflowsQuery, ApplicationResult<List<WorkflowInstanceSummaryDto>>>
{
    private readonly IWorkflowInstanceRepository _workflowRepository;
    private readonly ILoanApplicationRepository _loanRepository;

    public GetOverdueWorkflowsHandler(
        IWorkflowInstanceRepository workflowRepository,
        ILoanApplicationRepository loanRepository)
    {
        _workflowRepository = workflowRepository;
        _loanRepository = loanRepository;
    }

    public async Task<ApplicationResult<List<WorkflowInstanceSummaryDto>>> Handle(GetOverdueWorkflowsQuery request, CancellationToken ct = default)
    {
        var instances = await _workflowRepository.GetOverdueSLAAsync(ct);
        
        var summaries = new List<WorkflowInstanceSummaryDto>();
        foreach (var instance in instances)
        {
            var loan = await _loanRepository.GetByIdAsync(instance.LoanApplicationId, ct);
            if (loan != null)
            {
                summaries.Add(new WorkflowInstanceSummaryDto(
                    instance.Id,
                    instance.LoanApplicationId,
                    loan.ApplicationNumber,
                    loan.CustomerName,
                    instance.CurrentStatus.ToString(),
                    instance.CurrentStageDisplayName,
                    instance.AssignedRole,
                    instance.AssignedToUserId,
                    instance.SLADueAt,
                    instance.IsSLABreached,
                    true
                ));
            }
        }

        return ApplicationResult<List<WorkflowInstanceSummaryDto>>.Success(summaries);
    }
}

// Get workflow definition
public record GetWorkflowDefinitionQuery(LoanApplicationType ApplicationType) : IRequest<ApplicationResult<WorkflowDefinitionDto>>;

public class GetWorkflowDefinitionHandler : IRequestHandler<GetWorkflowDefinitionQuery, ApplicationResult<WorkflowDefinitionDto>>
{
    private readonly IWorkflowDefinitionRepository _repository;

    public GetWorkflowDefinitionHandler(IWorkflowDefinitionRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<WorkflowDefinitionDto>> Handle(GetWorkflowDefinitionQuery request, CancellationToken ct = default)
    {
        var definition = await _repository.GetActiveByTypeAsync(request.ApplicationType, ct);
        if (definition == null)
            return ApplicationResult<WorkflowDefinitionDto>.Failure($"No active workflow definition for {request.ApplicationType}");

        return ApplicationResult<WorkflowDefinitionDto>.Success(WorkflowMapper.ToDto(definition));
    }
}

// Get queue summary by role
public record GetQueueSummaryQuery : IRequest<ApplicationResult<List<WorkflowQueueSummaryDto>>>;

public class GetQueueSummaryHandler : IRequestHandler<GetQueueSummaryQuery, ApplicationResult<List<WorkflowQueueSummaryDto>>>
{
    private readonly IWorkflowInstanceRepository _repository;

    public GetQueueSummaryHandler(IWorkflowInstanceRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<List<WorkflowQueueSummaryDto>>> Handle(GetQueueSummaryQuery request, CancellationToken ct = default)
    {
        var roles = new[] { "LoanOfficer", "BranchApprover", "CreditOfficer", "CommitteeMember", "FinalApprover", "Operations" };
        var summaries = new List<WorkflowQueueSummaryDto>();

        foreach (var role in roles)
        {
            var instances = await _repository.GetByAssignedRoleAsync(role, ct);
            var total = instances.Count;
            var overdue = instances.Count(i => i.IsSLADue());
            var assigned = instances.Count(i => i.AssignedToUserId.HasValue);

            summaries.Add(new WorkflowQueueSummaryDto(
                role,
                total,
                overdue,
                assigned,
                total - assigned
            ));
        }

        return ApplicationResult<List<WorkflowQueueSummaryDto>>.Success(summaries);
    }
}

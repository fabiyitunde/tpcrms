using CRMS.Domain.Aggregates.Workflow;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Interfaces;

public interface IWorkflowDefinitionRepository
{
    Task<WorkflowDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<WorkflowDefinition?> GetActiveByTypeAsync(LoanApplicationType type, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowDefinition>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(WorkflowDefinition definition, CancellationToken ct = default);
    void Update(WorkflowDefinition definition);
}

public interface IWorkflowInstanceRepository
{
    Task<WorkflowInstance?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<WorkflowInstance?> GetByLoanApplicationIdAsync(Guid loanApplicationId, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowInstance>> GetByStatusAsync(LoanApplicationStatus status, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowInstance>> GetByAssignedRoleAsync(string role, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowInstance>> GetByAssignedUserAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowInstance>> GetOverdueSLAAsync(CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowInstance>> GetPendingAsync(int pageNumber, int pageSize, CancellationToken ct = default);
    Task<int> GetCountByStatusAsync(LoanApplicationStatus status, CancellationToken ct = default);
    Task<int> GetCountByRoleAsync(string role, CancellationToken ct = default);
    Task AddAsync(WorkflowInstance instance, CancellationToken ct = default);
    void Update(WorkflowInstance instance);
}

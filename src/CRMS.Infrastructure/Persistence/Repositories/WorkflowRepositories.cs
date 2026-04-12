using CRMS.Domain.Aggregates.Workflow;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories;

public class WorkflowDefinitionRepository : IWorkflowDefinitionRepository
{
    private readonly CRMSDbContext _context;

    public WorkflowDefinitionRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<WorkflowDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.WorkflowDefinitions
            .Include(w => w.Stages)
            .Include(w => w.Transitions)
            .FirstOrDefaultAsync(w => w.Id == id, ct);
    }

    public async Task<WorkflowDefinition?> GetActiveByTypeAsync(LoanApplicationType type, CancellationToken ct = default)
    {
        return await _context.WorkflowDefinitions
            .Include(w => w.Stages)
            .Include(w => w.Transitions)
            .FirstOrDefaultAsync(w => w.ApplicationType == type && w.IsActive, ct);
    }

    public async Task<IReadOnlyList<WorkflowDefinition>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.WorkflowDefinitions
            .Include(w => w.Stages)
            .OrderBy(w => w.ApplicationType)
            .ThenByDescending(w => w.Version)
            .ToListAsync(ct);
    }

    public async Task AddAsync(WorkflowDefinition definition, CancellationToken ct = default)
    {
        await _context.WorkflowDefinitions.AddAsync(definition, ct);
    }

    public void Update(WorkflowDefinition definition)
    {
        _context.WorkflowDefinitions.Update(definition);
    }
}

public class WorkflowInstanceRepository : IWorkflowInstanceRepository
{
    private readonly CRMSDbContext _context;

    public WorkflowInstanceRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<WorkflowInstance?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.WorkflowInstances
            .Include(w => w.TransitionHistory.OrderByDescending(t => t.PerformedAt))
            .FirstOrDefaultAsync(w => w.Id == id, ct);
    }

    public async Task<WorkflowInstance?> GetByLoanApplicationIdAsync(Guid loanApplicationId, CancellationToken ct = default)
    {
        return await _context.WorkflowInstances
            .Include(w => w.TransitionHistory.OrderByDescending(t => t.PerformedAt))
            .FirstOrDefaultAsync(w => w.LoanApplicationId == loanApplicationId, ct);
    }

    public async Task<IReadOnlyList<WorkflowInstance>> GetByStatusAsync(LoanApplicationStatus status, CancellationToken ct = default)
    {
        return await _context.WorkflowInstances
            .Where(w => w.CurrentStatus == status && !w.IsCompleted)
            .OrderBy(w => w.EnteredCurrentStageAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<WorkflowInstance>> GetByAssignedRoleAsync(string role, CancellationToken ct = default)
    {
        return await _context.WorkflowInstances
            .Where(w => w.AssignedRole == role && !w.IsCompleted)
            .OrderBy(w => w.SLADueAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<WorkflowInstance>> GetByAssignedUserAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.WorkflowInstances
            .Where(w => w.AssignedToUserId == userId && !w.IsCompleted)
            .OrderBy(w => w.SLADueAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<WorkflowInstance>> GetOverdueSLAAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return await _context.WorkflowInstances
            .Where(w => !w.IsCompleted && w.SLADueAt.HasValue && w.SLADueAt < now)
            .OrderBy(w => w.SLADueAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<WorkflowInstance>> GetPendingAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        return await _context.WorkflowInstances
            .Where(w => !w.IsCompleted)
            .OrderBy(w => w.SLADueAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<int> GetCountByStatusAsync(LoanApplicationStatus status, CancellationToken ct = default)
    {
        return await _context.WorkflowInstances
            .CountAsync(w => w.CurrentStatus == status && !w.IsCompleted, ct);
    }

    public async Task<int> GetCountByRoleAsync(string role, CancellationToken ct = default)
    {
        return await _context.WorkflowInstances
            .CountAsync(w => w.AssignedRole == role && !w.IsCompleted, ct);
    }

    public async Task AddAsync(WorkflowInstance instance, CancellationToken ct = default)
    {
        await _context.WorkflowInstances.AddAsync(instance, ct);
    }

    public void Update(WorkflowInstance instance)
    {
        // Disable auto-detect changes to prevent EF Core from discovering new child entities
        // (WorkflowTransitionLog entries) via snapshot diff and incorrectly marking them as
        // Modified (UPDATE) instead of Added (INSERT). We rely on the explicit state assignments
        // below, and DetectChanges will run correctly inside SaveChangesAsync.
        _context.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            var entry = _context.Entry(instance);

            if (entry.State == EntityState.Detached)
            {
                // Entity not yet tracked — start tracking via Update(), then fix any new
                // WorkflowTransitionLog entries that EF Core would incorrectly mark as Modified
                // instead of Added (non-empty Guid keys look like existing rows to EF Core).
                var newLogs = instance.TransitionHistory
                    .Where(t => _context.Entry(t).State == EntityState.Detached).ToList();

                _context.WorkflowInstances.Update(instance);

                foreach (var log in newLogs)
                    _context.Entry(log).State = EntityState.Added;
            }
            else
            {
                // Entity is already tracked (loaded via GetByIdAsync in the same DbContext scope).
                // EF Core's change tracker detects all property changes on the aggregate root.
                // Do NOT call DbSet.Update() — it marks every related entity (existing
                // WorkflowTransitionLog rows) as Modified, generating unnecessary UPDATEs that
                // can fail with constraint errors and prevent SaveChanges from completing.
                //
                // Just explicitly attach any new transition log entries that aren't tracked yet.
                foreach (var log in instance.TransitionHistory)
                    if (_context.Entry(log).State == EntityState.Detached)
                        _context.Entry(log).State = EntityState.Added;
            }
        }
        finally
        {
            _context.ChangeTracker.AutoDetectChangesEnabled = true;
        }
    }
}

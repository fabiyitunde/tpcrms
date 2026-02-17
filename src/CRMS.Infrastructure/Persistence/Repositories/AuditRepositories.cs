using CRMS.Domain.Aggregates.Audit;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly CRMSDbContext _context;

    public AuditLogRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(AuditLog log, CancellationToken ct = default)
    {
        await _context.AuditLogs.AddAsync(log, ct);
    }

    public async Task AddRangeAsync(IEnumerable<AuditLog> logs, CancellationToken ct = default)
    {
        await _context.AuditLogs.AddRangeAsync(logs, ct);
    }

    public async Task<AuditLog?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.AuditLogs.FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<IReadOnlyList<AuditLog>> GetByEntityAsync(string entityType, Guid entityId, CancellationToken ct = default)
    {
        return await _context.AuditLogs
            .Where(x => x.EntityType == entityType && x.EntityId == entityId)
            .OrderByDescending(x => x.Timestamp)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AuditLog>> GetByLoanApplicationAsync(Guid loanApplicationId, CancellationToken ct = default)
    {
        return await _context.AuditLogs
            .Where(x => x.LoanApplicationId == loanApplicationId)
            .OrderByDescending(x => x.Timestamp)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AuditLog>> GetByUserAsync(Guid userId, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var query = _context.AuditLogs.Where(x => x.UserId == userId);

        if (from.HasValue)
            query = query.Where(x => x.Timestamp >= from.Value);

        if (to.HasValue)
            query = query.Where(x => x.Timestamp <= to.Value);

        return await query
            .OrderByDescending(x => x.Timestamp)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AuditLog>> GetByCategoryAsync(AuditCategory category, DateTime? from, DateTime? to, int limit = 100, CancellationToken ct = default)
    {
        var query = _context.AuditLogs.Where(x => x.Category == category);

        if (from.HasValue)
            query = query.Where(x => x.Timestamp >= from.Value);

        if (to.HasValue)
            query = query.Where(x => x.Timestamp <= to.Value);

        return await query
            .OrderByDescending(x => x.Timestamp)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AuditLog>> GetByActionAsync(AuditAction action, DateTime? from, DateTime? to, int limit = 100, CancellationToken ct = default)
    {
        var query = _context.AuditLogs.Where(x => x.Action == action);

        if (from.HasValue)
            query = query.Where(x => x.Timestamp >= from.Value);

        if (to.HasValue)
            query = query.Where(x => x.Timestamp <= to.Value);

        return await query
            .OrderByDescending(x => x.Timestamp)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AuditLog>> GetRecentAsync(int count = 100, CancellationToken ct = default)
    {
        return await _context.AuditLogs
            .OrderByDescending(x => x.Timestamp)
            .Take(count)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AuditLog>> GetFailedActionsAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var query = _context.AuditLogs.Where(x => !x.IsSuccess);

        if (from.HasValue)
            query = query.Where(x => x.Timestamp >= from.Value);

        if (to.HasValue)
            query = query.Where(x => x.Timestamp <= to.Value);

        return await query
            .OrderByDescending(x => x.Timestamp)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AuditLog>> SearchAsync(
        AuditCategory? category = null,
        AuditAction? action = null,
        Guid? userId = null,
        Guid? loanApplicationId = null,
        string? entityType = null,
        DateTime? from = null,
        DateTime? to = null,
        bool? isSuccess = null,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        var query = _context.AuditLogs.AsQueryable();

        if (category.HasValue)
            query = query.Where(x => x.Category == category.Value);

        if (action.HasValue)
            query = query.Where(x => x.Action == action.Value);

        if (userId.HasValue)
            query = query.Where(x => x.UserId == userId.Value);

        if (loanApplicationId.HasValue)
            query = query.Where(x => x.LoanApplicationId == loanApplicationId.Value);

        if (!string.IsNullOrEmpty(entityType))
            query = query.Where(x => x.EntityType == entityType);

        if (from.HasValue)
            query = query.Where(x => x.Timestamp >= from.Value);

        if (to.HasValue)
            query = query.Where(x => x.Timestamp <= to.Value);

        if (isSuccess.HasValue)
            query = query.Where(x => x.IsSuccess == isSuccess.Value);

        return await query
            .OrderByDescending(x => x.Timestamp)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<int> GetCountAsync(
        AuditCategory? category = null,
        AuditAction? action = null,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken ct = default)
    {
        var query = _context.AuditLogs.AsQueryable();

        if (category.HasValue)
            query = query.Where(x => x.Category == category.Value);

        if (action.HasValue)
            query = query.Where(x => x.Action == action.Value);

        if (from.HasValue)
            query = query.Where(x => x.Timestamp >= from.Value);

        if (to.HasValue)
            query = query.Where(x => x.Timestamp <= to.Value);

        return await query.CountAsync(ct);
    }
}

public class DataAccessLogRepository : IDataAccessLogRepository
{
    private readonly CRMSDbContext _context;

    public DataAccessLogRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(DataAccessLog log, CancellationToken ct = default)
    {
        await _context.DataAccessLogs.AddAsync(log, ct);
    }

    public async Task<IReadOnlyList<DataAccessLog>> GetByUserAsync(Guid userId, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var query = _context.DataAccessLogs.Where(x => x.UserId == userId);

        if (from.HasValue)
            query = query.Where(x => x.AccessedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(x => x.AccessedAt <= to.Value);

        return await query
            .OrderByDescending(x => x.AccessedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DataAccessLog>> GetByEntityAsync(string entityType, Guid entityId, CancellationToken ct = default)
    {
        return await _context.DataAccessLogs
            .Where(x => x.EntityType == entityType && x.EntityId == entityId)
            .OrderByDescending(x => x.AccessedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DataAccessLog>> GetByDataTypeAsync(SensitiveDataType dataType, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var query = _context.DataAccessLogs.Where(x => x.DataType == dataType);

        if (from.HasValue)
            query = query.Where(x => x.AccessedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(x => x.AccessedAt <= to.Value);

        return await query
            .OrderByDescending(x => x.AccessedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DataAccessLog>> GetByLoanApplicationAsync(Guid loanApplicationId, CancellationToken ct = default)
    {
        return await _context.DataAccessLogs
            .Where(x => x.LoanApplicationId == loanApplicationId)
            .OrderByDescending(x => x.AccessedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DataAccessLog>> GetRecentAsync(int count = 100, CancellationToken ct = default)
    {
        return await _context.DataAccessLogs
            .OrderByDescending(x => x.AccessedAt)
            .Take(count)
            .ToListAsync(ct);
    }
}

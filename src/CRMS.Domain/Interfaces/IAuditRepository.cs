using CRMS.Domain.Aggregates.Audit;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Interfaces;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog log, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<AuditLog> logs, CancellationToken ct = default);
    
    Task<AuditLog?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<AuditLog>> GetByEntityAsync(string entityType, Guid entityId, CancellationToken ct = default);
    Task<IReadOnlyList<AuditLog>> GetByLoanApplicationAsync(Guid loanApplicationId, CancellationToken ct = default);
    Task<IReadOnlyList<AuditLog>> GetByUserAsync(Guid userId, DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<IReadOnlyList<AuditLog>> GetByCategoryAsync(AuditCategory category, DateTime? from, DateTime? to, int limit = 100, CancellationToken ct = default);
    Task<IReadOnlyList<AuditLog>> GetByActionAsync(AuditAction action, DateTime? from, DateTime? to, int limit = 100, CancellationToken ct = default);
    Task<IReadOnlyList<AuditLog>> GetRecentAsync(int count = 100, CancellationToken ct = default);
    Task<IReadOnlyList<AuditLog>> GetFailedActionsAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
    
    Task<IReadOnlyList<AuditLog>> SearchAsync(
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
        CancellationToken ct = default);
    
    Task<int> GetCountAsync(
        AuditCategory? category = null,
        AuditAction? action = null,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken ct = default);
}

public interface IDataAccessLogRepository
{
    Task AddAsync(DataAccessLog log, CancellationToken ct = default);
    
    Task<IReadOnlyList<DataAccessLog>> GetByUserAsync(Guid userId, DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<IReadOnlyList<DataAccessLog>> GetByEntityAsync(string entityType, Guid entityId, CancellationToken ct = default);
    Task<IReadOnlyList<DataAccessLog>> GetByDataTypeAsync(SensitiveDataType dataType, DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<IReadOnlyList<DataAccessLog>> GetByLoanApplicationAsync(Guid loanApplicationId, CancellationToken ct = default);
    Task<IReadOnlyList<DataAccessLog>> GetRecentAsync(int count = 100, CancellationToken ct = default);
}

using CRMS.Domain.Aggregates.Configuration;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories;

public class ScoringParameterRepository : IScoringParameterRepository
{
    private readonly CRMSDbContext _context;

    public ScoringParameterRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<ScoringParameter?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.ScoringParameters.FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<ScoringParameter?> GetByKeyAsync(string category, string parameterKey, CancellationToken ct = default)
    {
        return await _context.ScoringParameters
            .FirstOrDefaultAsync(x => x.Category == category && x.ParameterKey == parameterKey, ct);
    }

    public async Task<IReadOnlyList<ScoringParameter>> GetAllActiveAsync(CancellationToken ct = default)
    {
        return await _context.ScoringParameters
            .Where(x => x.IsActive)
            .OrderBy(x => x.Category)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.ParameterKey)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ScoringParameter>> GetByCategoryAsync(string category, CancellationToken ct = default)
    {
        return await _context.ScoringParameters
            .Where(x => x.Category == category && x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.ParameterKey)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ScoringParameter>> GetPendingChangesAsync(CancellationToken ct = default)
    {
        return await _context.ScoringParameters
            .Where(x => x.ChangeStatus == ParameterChangeStatus.Pending)
            .OrderBy(x => x.PendingChangeAt)
            .ToListAsync(ct);
    }

    public async Task<Dictionary<string, decimal>> GetAllActiveAsLookupAsync(CancellationToken ct = default)
    {
        var parameters = await _context.ScoringParameters
            .Where(x => x.IsActive)
            .Select(x => new { Key = $"{x.Category}.{x.ParameterKey}", x.CurrentValue })
            .ToListAsync(ct);

        return parameters.ToDictionary(x => x.Key, x => x.CurrentValue);
    }

    public async Task AddAsync(ScoringParameter parameter, CancellationToken ct = default)
    {
        await _context.ScoringParameters.AddAsync(parameter, ct);
    }

    public void Update(ScoringParameter parameter)
    {
        _context.ScoringParameters.Update(parameter);
    }

    public async Task AddHistoryAsync(ScoringParameterHistory history, CancellationToken ct = default)
    {
        await _context.ScoringParameterHistory.AddAsync(history, ct);
    }

    public async Task<IReadOnlyList<ScoringParameterHistory>> GetHistoryByParameterIdAsync(Guid parameterId, CancellationToken ct = default)
    {
        return await _context.ScoringParameterHistory
            .Where(x => x.ScoringParameterId == parameterId)
            .OrderByDescending(x => x.ApprovedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ScoringParameterHistory>> GetRecentHistoryAsync(int count = 50, CancellationToken ct = default)
    {
        return await _context.ScoringParameterHistory
            .OrderByDescending(x => x.ApprovedAt)
            .Take(count)
            .ToListAsync(ct);
    }
}

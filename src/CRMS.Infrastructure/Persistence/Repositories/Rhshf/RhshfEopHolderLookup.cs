using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories.Rhshf;

public class RhshfEopHolderLookup : IRhshfEopHolderLookup
{
    private readonly CRMSDbContext _context;

    public RhshfEopHolderLookup(CRMSDbContext context)
    {
        _context = context;
    }

    // Both the per-EOP summaries and the lines are searched: a case submitted before the portal sent
    // the summary carries the EOP id on its lines only.
    public async Task<IReadOnlyList<RhshfEopHolder>> GetLiveHoldersAsync(IReadOnlyCollection<Guid> eopIds, CancellationToken ct = default)
    {
        if (eopIds.Count == 0)
            return [];
        var ids = eopIds.Select(id => (Guid?)id).ToList();

        var live = _context.RhshfCreditProfiles.Where(p =>
            p.Status != RhshfCaseStatus.Declined && p.Status != RhshfCaseStatus.Cancelled && p.Status != RhshfCaseStatus.Expired);

        var fromSummaries = await (
                from e in _context.RhshfFinancingEops
                join p in live on e.RhshfCreditProfileId equals p.Id
                where ids.Contains(e.EopId)
                select new { e.EopId, e.EopReference, p.Reference })
            .ToListAsync(ct);

        var fromLines = await (
                from l in _context.Set<RhshfFinancingLine>()
                join p in live on l.RhshfCreditProfileId equals p.Id
                where ids.Contains(l.EopId)
                select new { l.EopId, p.Reference })
            .Distinct()
            .ToListAsync(ct);

        return fromSummaries.Select(s => new RhshfEopHolder(s.EopId!.Value, s.EopReference, s.Reference))
            .Concat(fromLines.Select(l => new RhshfEopHolder(l.EopId!.Value, null, l.Reference)))
            .GroupBy(h => (h.EopId, h.CaseReference))
            .Select(g => g.FirstOrDefault(h => h.EopReference is not null) ?? g.First())
            .OrderBy(h => h.CaseReference).ThenBy(h => h.EopId)
            .ToList();
    }
}

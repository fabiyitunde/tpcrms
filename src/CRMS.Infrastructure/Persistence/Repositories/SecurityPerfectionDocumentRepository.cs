using CRMS.Domain.Aggregates.LoanApplication;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories;

public class SecurityPerfectionDocumentRepository : ISecurityPerfectionDocumentRepository
{
    private readonly CRMSDbContext _context;

    public SecurityPerfectionDocumentRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(SecurityPerfectionDocument document, CancellationToken ct = default)
    {
        await _context.SecurityPerfectionDocuments.AddAsync(document, ct);
    }

    public async Task<SecurityPerfectionDocument?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.SecurityPerfectionDocuments.FindAsync([id], ct);
    }

    public async Task<IReadOnlyList<SecurityPerfectionDocument>> GetByApplicationIdAsync(
        Guid applicationId, CancellationToken ct = default)
    {
        return await _context.SecurityPerfectionDocuments
            .Where(x => x.ApplicationId == applicationId)
            .OrderBy(x => x.Category)
            .ThenBy(x => x.UploadedAt)
            .ToListAsync(ct);
    }

    public void Delete(SecurityPerfectionDocument document)
    {
        _context.SecurityPerfectionDocuments.Remove(document);
    }
}

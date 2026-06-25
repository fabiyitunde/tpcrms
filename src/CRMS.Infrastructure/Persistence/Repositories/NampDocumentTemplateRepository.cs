using CRMS.Domain.Aggregates.Namp;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories;

public class NampDocumentTemplateRepository : INampDocumentTemplateRepository
{
    private readonly CRMSDbContext _context;

    public NampDocumentTemplateRepository(CRMSDbContext context) => _context = context;

    public async Task<NampDocumentTemplate?> GetByTypeAsync(NampDocumentType documentType, CancellationToken ct = default) =>
        await _context.NampDocumentTemplates
            .FirstOrDefaultAsync(t => t.DocumentType == documentType, ct);

    public async Task<IReadOnlyList<NampDocumentTemplate>> GetAllAsync(CancellationToken ct = default) =>
        await _context.NampDocumentTemplates
            .OrderBy(t => t.DocumentType)
            .ToListAsync(ct);

    public async Task AddAsync(NampDocumentTemplate template, CancellationToken ct = default) =>
        await _context.NampDocumentTemplates.AddAsync(template, ct);
}

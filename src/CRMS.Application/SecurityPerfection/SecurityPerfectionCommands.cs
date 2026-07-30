using CRMS.Application.Common;
using CRMS.Domain.Aggregates.LoanApplication;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.SecurityPerfection;

// ─── DTOs ────────────────────────────────────────────────────────────────────

public record SecurityPerfectionDocumentDto(
    Guid Id,
    string Category,
    Guid? CollateralId,
    string CollateralDescription,
    string DocumentType,
    string Description,
    string FileName,
    string StoragePath,
    long FileSizeBytes,
    string ContentType,
    Guid UploadedByUserId,
    DateTime UploadedAt
);

// ─── Upload (loan-level) ──────────────────────────────────────────────────────

public record UploadLoanLevelSecurityDocumentCommand(
    Guid ApplicationId,
    string DocumentType,
    string Description,
    string FileName,
    string StoragePath,
    long FileSizeBytes,
    string ContentType,
    Guid UploadedByUserId
) : IRequest<ApplicationResult<SecurityPerfectionDocumentDto>>;

public class UploadLoanLevelSecurityDocumentHandler
    : IRequestHandler<UploadLoanLevelSecurityDocumentCommand, ApplicationResult<SecurityPerfectionDocumentDto>>
{
    private readonly ISecurityPerfectionDocumentRepository _repo;
    private readonly IUnitOfWork _uow;

    public UploadLoanLevelSecurityDocumentHandler(ISecurityPerfectionDocumentRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult<SecurityPerfectionDocumentDto>> Handle(
        UploadLoanLevelSecurityDocumentCommand request, CancellationToken ct)
    {
        var result = SecurityPerfectionDocument.CreateLoanLevel(
            request.ApplicationId,
            request.DocumentType,
            request.Description,
            request.FileName,
            request.StoragePath,
            request.FileSizeBytes,
            request.ContentType,
            request.UploadedByUserId);

        if (result.IsFailure)
            return ApplicationResult<SecurityPerfectionDocumentDto>.Failure(result.Error);

        await _repo.AddAsync(result.Value, ct);
        await _uow.SaveChangesAsync(ct);

        return ApplicationResult<SecurityPerfectionDocumentDto>.Success(SpDocMapper.ToDto(result.Value));
    }
}

// ─── Upload (collateral-specific) ────────────────────────────────────────────

public record UploadCollateralSecurityDocumentCommand(
    Guid ApplicationId,
    Guid CollateralId,
    string CollateralDescription,
    string DocumentType,
    string Description,
    string FileName,
    string StoragePath,
    long FileSizeBytes,
    string ContentType,
    Guid UploadedByUserId
) : IRequest<ApplicationResult<SecurityPerfectionDocumentDto>>;

public class UploadCollateralSecurityDocumentHandler
    : IRequestHandler<UploadCollateralSecurityDocumentCommand, ApplicationResult<SecurityPerfectionDocumentDto>>
{
    private readonly ISecurityPerfectionDocumentRepository _repo;
    private readonly IUnitOfWork _uow;

    public UploadCollateralSecurityDocumentHandler(ISecurityPerfectionDocumentRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult<SecurityPerfectionDocumentDto>> Handle(
        UploadCollateralSecurityDocumentCommand request, CancellationToken ct)
    {
        var result = SecurityPerfectionDocument.CreateCollateralSpecific(
            request.ApplicationId,
            request.CollateralId,
            request.CollateralDescription,
            request.DocumentType,
            request.Description,
            request.FileName,
            request.StoragePath,
            request.FileSizeBytes,
            request.ContentType,
            request.UploadedByUserId);

        if (result.IsFailure)
            return ApplicationResult<SecurityPerfectionDocumentDto>.Failure(result.Error);

        await _repo.AddAsync(result.Value, ct);
        await _uow.SaveChangesAsync(ct);

        return ApplicationResult<SecurityPerfectionDocumentDto>.Success(SpDocMapper.ToDto(result.Value));
    }
}

// ─── Delete ───────────────────────────────────────────────────────────────────

public record DeleteSecurityPerfectionDocumentCommand(Guid DocumentId) : IRequest<ApplicationResult>;

public class DeleteSecurityPerfectionDocumentHandler
    : IRequestHandler<DeleteSecurityPerfectionDocumentCommand, ApplicationResult>
{
    private readonly ISecurityPerfectionDocumentRepository _repo;
    private readonly IUnitOfWork _uow;

    public DeleteSecurityPerfectionDocumentHandler(ISecurityPerfectionDocumentRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(DeleteSecurityPerfectionDocumentCommand request, CancellationToken ct)
    {
        var doc = await _repo.GetByIdAsync(request.DocumentId, ct);
        if (doc == null)
            return ApplicationResult.Failure("Document not found");

        _repo.Delete(doc);
        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}

// ─── Get all for application ─────────────────────────────────────────────────

public record GetSecurityPerfectionDocumentsQuery(Guid ApplicationId)
    : IRequest<List<SecurityPerfectionDocumentDto>>;

public class GetSecurityPerfectionDocumentsHandler
    : IRequestHandler<GetSecurityPerfectionDocumentsQuery, List<SecurityPerfectionDocumentDto>>
{
    private readonly ISecurityPerfectionDocumentRepository _repo;

    public GetSecurityPerfectionDocumentsHandler(ISecurityPerfectionDocumentRepository repo)
    {
        _repo = repo;
    }

    public async Task<List<SecurityPerfectionDocumentDto>> Handle(
        GetSecurityPerfectionDocumentsQuery request, CancellationToken ct)
    {
        var docs = await _repo.GetByApplicationIdAsync(request.ApplicationId, ct);
        return docs.Select(SpDocMapper.ToDto).ToList();
    }
}

// ─── Shared mapper ───────────────────────────────────────────────────────────

file static class SpDocMapper
{
    public static SecurityPerfectionDocumentDto ToDto(SecurityPerfectionDocument d) => new(
        d.Id,
        d.Category,
        d.CollateralId,
        d.CollateralDescription,
        d.DocumentType,
        d.Description,
        d.FileName,
        d.StoragePath,
        d.FileSizeBytes,
        d.ContentType,
        d.UploadedByUserId,
        d.UploadedAt);
}

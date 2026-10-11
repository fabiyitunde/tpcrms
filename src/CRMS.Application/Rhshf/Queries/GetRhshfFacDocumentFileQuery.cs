using CRMS.Application.Common;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Queries;

/// <summary>Which of the FAC's own uploads is being read back.</summary>
public enum RhshfFacDocumentKind
{
    Supporting,
    Collateral,
}

/// <summary>
/// Reads back a document the FAC uploaded, for preview or download on the profiling form. Unlike the
/// staff DownloadRhshf*DocumentQuery handlers, this one is scoped to a case **reference**: the
/// profiling page is public (cookie-session, no user account), so a document id alone must never be
/// enough to fetch a file — otherwise one FAC could read another case's uploads by guessing an id.
/// The caller supplies the reference from the verified session, and this handler refuses any document
/// that does not belong to it.
/// </summary>
public record GetRhshfFacDocumentFileQuery(string Reference, Guid DocumentId, RhshfFacDocumentKind Kind)
    : IRequest<ApplicationResult<RhshfDownloadedFileDto>>;

public class GetRhshfFacDocumentFileHandler : IRequestHandler<GetRhshfFacDocumentFileQuery, ApplicationResult<RhshfDownloadedFileDto>>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfCollateralRepository _collateralRepo;
    private readonly IFileStorageService _fileStorage;

    public GetRhshfFacDocumentFileHandler(
        IRhshfCreditProfileRepository repo, IRhshfCollateralRepository collateralRepo, IFileStorageService fileStorage)
    {
        _repo = repo;
        _collateralRepo = collateralRepo;
        _fileStorage = fileStorage;
    }

    public async Task<ApplicationResult<RhshfDownloadedFileDto>> Handle(
        GetRhshfFacDocumentFileQuery request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult<RhshfDownloadedFileDto>.Failure("Case not found.");

        var (storagePath, fileName, contentType) = request.Kind switch
        {
            RhshfFacDocumentKind.Supporting => await ResolveSupportingAsync(profile.Id, request.DocumentId, ct),
            RhshfFacDocumentKind.Collateral => await ResolveCollateralAsync(profile.Id, request.DocumentId, ct),
            _ => (null, null, null),
        };

        // Deliberately the same message whether the document is missing or belongs to another case —
        // a public page should not confirm that someone else's document id exists.
        if (storagePath is null)
            return ApplicationResult<RhshfDownloadedFileDto>.Failure("Document not found on this case.");

        var content = await _fileStorage.DownloadAsync(storagePath, ct);
        return ApplicationResult<RhshfDownloadedFileDto>.Success(
            new RhshfDownloadedFileDto(content, fileName ?? "document", contentType ?? "application/octet-stream"));
    }

    private async Task<(string? StoragePath, string? FileName, string? ContentType)> ResolveSupportingAsync(
        Guid profileId, Guid documentId, CancellationToken ct)
    {
        var document = await _repo.GetSupportingDocumentByIdAsync(documentId, ct);
        return document is null || document.RhshfCreditProfileId != profileId
            ? (null, null, null)
            : (document.StoragePath, document.FileName, document.ContentType);
    }

    private async Task<(string? StoragePath, string? FileName, string? ContentType)> ResolveCollateralAsync(
        Guid profileId, Guid documentId, CancellationToken ct)
    {
        var document = await _collateralRepo.GetDocumentByIdAsync(documentId, ct);
        if (document is null)
            return (null, null, null);

        // A collateral document hangs off the instrument, so ownership runs through it.
        var collateral = await _collateralRepo.GetByIdAsync(document.RhshfCollateralId, ct);
        return collateral is null || collateral.RhshfCreditProfileId != profileId
            ? (null, null, null)
            : (document.StoragePath, document.FileName, document.ContentType);
    }
}

using CRMS.Application.Common;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>
/// §4 SupportingDocuments stage — no fixed required-document checklist in v1 (design doc §6);
/// the FAC can attach any number of files. Uses IFileStorageService directly, the same generic
/// storage abstraction every other document upload in CRMS already uses.
/// </summary>
public record UploadRhshfSupportingDocumentCommand(
    string Reference, string FileName, string ContentType, byte[] Content) : IRequest<ApplicationResult>;

public class UploadRhshfSupportingDocumentHandler : IRequestHandler<UploadRhshfSupportingDocumentCommand, ApplicationResult>
{
    private const string ContainerName = "rhshf-documents";
    private const long MaxSizeBytes = 10 * 1024 * 1024; // 10 MB per file

    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IFileStorageService _fileStorage;
    private readonly IUnitOfWork _uow;

    public UploadRhshfSupportingDocumentHandler(IRhshfCreditProfileRepository repo, IFileStorageService fileStorage, IUnitOfWork uow)
    {
        _repo = repo;
        _fileStorage = fileStorage;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(UploadRhshfSupportingDocumentCommand request, CancellationToken ct = default)
    {
        if (request.Content.Length == 0)
            return ApplicationResult.Failure("File is empty.");
        if (request.Content.Length > MaxSizeBytes)
            return ApplicationResult.Failure("File exceeds the 10 MB size limit.");

        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        // Upload first — the domain call below only fails on a stage mismatch, not on storage
        // I/O, so there's no path where a stored blob is orphaned by a subsequent domain failure
        // under normal operation.
        var storagePath = await _fileStorage.UploadAsync(
            ContainerName, $"{profile.Reference}/{Guid.NewGuid()}-{request.FileName}", request.Content, request.ContentType, ct);

        var result = profile.AddSupportingDocument(request.FileName, request.ContentType, storagePath, request.Content.Length);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}

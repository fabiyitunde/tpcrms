using CRMS.Application.Common;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>
/// Carries a document the same FAC supplied on one of its earlier cases over to this one, instead of
/// making it upload the file again. Added for the Credit Appraisal Memorandum, which a FAC typically
/// has already prepared, but written against any category.
/// <para>
/// The bytes are copied into this case's own storage path rather than the row pointing at the source
/// blob: two cases sharing one blob would mean removing a document from either case breaks the
/// other, and the source case is an audited record that must not be reachable for deletion through
/// this one.
/// </para>
/// </summary>
public record ReuseRhshfSupportingDocumentCommand(string Reference, Guid SourceDocumentId)
    : IRequest<ApplicationResult>;

public class ReuseRhshfSupportingDocumentHandler : IRequestHandler<ReuseRhshfSupportingDocumentCommand, ApplicationResult>
{
    private const string ContainerName = "rhshf-documents";

    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IFileStorageService _fileStorage;
    private readonly IUnitOfWork _uow;

    public ReuseRhshfSupportingDocumentHandler(
        IRhshfCreditProfileRepository repo, IFileStorageService fileStorage, IUnitOfWork uow)
    {
        _repo = repo;
        _fileStorage = fileStorage;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(ReuseRhshfSupportingDocumentCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        var source = await _repo.GetSupportingDocumentByIdAsync(request.SourceDocumentId, ct);
        if (source is null)
            return ApplicationResult.Failure("The document being carried over no longer exists.");

        // Authorisation, not a convenience lookup: the candidate list is already scoped to this
        // FAC's own cases, so re-deriving it and requiring the posted id to appear in it is what
        // stops a crafted request from pulling another aggregator's document into this case.
        var candidates = await _repo.GetReusableDocumentsForFacAsync(
            profile.FacId, profile.Id, source.Category, ct);
        var candidate = candidates.FirstOrDefault(c => c.DocumentId == request.SourceDocumentId);
        if (candidate is null)
            return ApplicationResult.Failure("That document does not belong to this applicant's earlier cases.");

        byte[] content;
        try
        {
            content = await _fileStorage.DownloadAsync(candidate.StoragePath, ct);
        }
        catch (Exception ex)
        {
            // The source row survives its blob going missing (a storage migration, a manual delete),
            // and the FAC can still upload a fresh copy — so this is a clear message, not a throw.
            return ApplicationResult.Failure($"The stored file could not be read, so it can't be carried over. Upload it again instead. ({ex.Message})");
        }

        if (content.Length == 0)
            return ApplicationResult.Failure("The stored file is empty, so it can't be carried over. Upload it again instead.");

        var storagePath = await _fileStorage.UploadAsync(
            ContainerName, $"{profile.Reference}/{Guid.NewGuid()}-{candidate.FileName}", content, candidate.ContentType, ct);

        var result = profile.AddReusedSupportingDocument(
            candidate.Category, candidate.FileName, candidate.ContentType, storagePath, content.Length,
            candidate.DocumentId, candidate.SourceReference, candidate.UploadedAt);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}

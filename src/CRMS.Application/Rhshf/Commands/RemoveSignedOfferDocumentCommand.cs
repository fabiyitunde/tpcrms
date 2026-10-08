using CRMS.Application.Common;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>
/// Lets the FAC pull a signed upload they got wrong (wrong file, wrong slot) and re-upload the right
/// one — allowed only while the offer is still undecided (enforced in RhshfOffer.RemoveDocument).
/// Removes both the tracked record and the stored blob so a mistaken upload leaves nothing behind.
/// </summary>
public record RemoveSignedOfferDocumentCommand(string Reference, Guid DocumentId) : IRequest<ApplicationResult>;

public class RemoveSignedOfferDocumentHandler : IRequestHandler<RemoveSignedOfferDocumentCommand, ApplicationResult>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfOfferRepository _offerRepo;
    private readonly IFileStorageService _fileStorage;
    private readonly IUnitOfWork _uow;

    public RemoveSignedOfferDocumentHandler(
        IRhshfCreditProfileRepository repo, IRhshfOfferRepository offerRepo, IFileStorageService fileStorage, IUnitOfWork uow)
    {
        _repo = repo;
        _offerRepo = offerRepo;
        _fileStorage = fileStorage;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(RemoveSignedOfferDocumentCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        var offer = await _offerRepo.GetByProfileAndCycleAsync(profile.Id, profile.CurrentCycleNumber, ct);
        if (offer is null)
            return ApplicationResult.Failure("No offer has been generated for this case's current cycle.");

        // Grab the storage path before removing — the entity leaves the collection on RemoveDocument.
        var storagePath = offer.Documents.FirstOrDefault(d => d.Id == request.DocumentId)?.StoragePath;

        var result = offer.RemoveDocument(request.DocumentId);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        await _uow.SaveChangesAsync(ct);

        // Best-effort blob cleanup — the authoritative record is already gone, so a storage hiccup here
        // shouldn't fail the FAC's action (the file is now unreferenced regardless).
        if (!string.IsNullOrEmpty(storagePath))
        {
            try { await _fileStorage.DeleteAsync(storagePath, ct); }
            catch { /* orphaned blob is harmless; the DB record is the source of truth */ }
        }

        return ApplicationResult.Success();
    }
}

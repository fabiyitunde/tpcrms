using CRMS.Application.Common;
using CRMS.Application.Rhshf.Interfaces;
using CRMS.Application.Rhshf.Services;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>
/// Re-generates the offer document for the current cycle and re-points the offer at it — recovery
/// for a case whose generated document is missing or unreadable (e.g. an offer created outside the
/// normal ratify path, or a lost storage file). The content is rebuilt from the ratified amount and
/// case data through the same builder ratification uses, so a reissue is the same document the FAC
/// was originally sent. Only permitted while the FAC has not yet accepted/rejected — an accepted
/// offer is contractual and is never silently replaced (changing terms is a fresh offer via
/// Return-to-FAC).
/// </summary>
public record RegenerateRhshfOfferLetterCommand(string Reference, Guid ActorUserId) : IRequest<ApplicationResult>;

public class RegenerateRhshfOfferLetterHandler : IRequestHandler<RegenerateRhshfOfferLetterCommand, ApplicationResult>
{
    private const string OfferContainerName = "rhshf-offers";

    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfOfferRepository _offerRepo;
    private readonly IRhshfOfferDocumentGenerator _offerDocumentGenerator;
    private readonly RhshfOfferDocumentDataBuilder _documentDataBuilder;
    private readonly IFileStorageService _fileStorage;
    private readonly IUnitOfWork _uow;

    public RegenerateRhshfOfferLetterHandler(
        IRhshfCreditProfileRepository repo, IRhshfOfferRepository offerRepo,
        IRhshfOfferDocumentGenerator offerDocumentGenerator,
        RhshfOfferDocumentDataBuilder documentDataBuilder,
        IFileStorageService fileStorage, IUnitOfWork uow)
    {
        _repo = repo;
        _offerRepo = offerRepo;
        _offerDocumentGenerator = offerDocumentGenerator;
        _documentDataBuilder = documentDataBuilder;
        _fileStorage = fileStorage;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(RegenerateRhshfOfferLetterCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        var offer = await _offerRepo.GetByProfileAndCycleAsync(profile.Id, profile.CurrentCycleNumber, ct);
        if (offer is null)
            return ApplicationResult.Failure("No offer exists for this case — it must be ratified before a document can be generated.");

        if (profile.ApprovedAmount is null)
            return ApplicationResult.Failure("No ratified amount is on file for this case.");

        // The merged document, built exactly as ratification builds it. This path used to produce the
        // old letter-plus-Key-Facts pair instead, so reissuing an offer handed the borrower a
        // different set of documents from the one they were first sent — and, worse, attached a Key
        // Facts Statement to an offer issued without one, which then demanded a second signed copy
        // the borrower had never been given.
        var build = await _documentDataBuilder.BuildAsync(profile, profile.ApprovedAmount.Value, ct);
        var pdfBytes = await _offerDocumentGenerator.GenerateAsync(build.Data, ct);

        var storagePath = await _fileStorage.UploadAsync(
            OfferContainerName, $"{profile.Reference}/offer-cycle{offer.CycleNumber}.pdf", pdfBytes, "application/pdf", ct);

        var result = offer.RegenerateDocument(storagePath);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        // Re-checked on every reissue, so fixing the product clears the warning and a product that
        // has since drifted raises one.
        offer.RecordSuppressedProductFees(build.SuppressedProductFees);

        // The offer was loaded tracked, so the document path change persists on save.
        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}

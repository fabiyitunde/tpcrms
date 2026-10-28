using CRMS.Application.Common;
using CRMS.Application.Rhshf.Interfaces;
using CRMS.Application.Rhshf.Services;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>
/// Replaces an issued offer that was wrong — accepted or not yet answered — with a corrected one the
/// FAC must accept.
/// <para>
/// RegenerateRhshfOfferLetterCommand cannot do this: it overwrites the document in place under the
/// same version, which is recovery for a lost file, not a correction the FAC may already have read.
/// Here nothing is overwritten. The current offer is marked superseded and keeps its document and
/// any signed copies; the corrected offer is a new row with its own file, built from the case as it now stands
/// through the same builder ratification uses. The amount and terms are the ratified ones — this
/// corrects the document, it does not re-decide the credit.
/// </para>
/// </summary>
public record IssueCorrectedRhshfOfferCommand(string Reference, Guid ActorUserId, string Reason) : IRequest<ApplicationResult>;

public class IssueCorrectedRhshfOfferHandler : IRequestHandler<IssueCorrectedRhshfOfferCommand, ApplicationResult>
{
    private const string OfferContainerName = "rhshf-offers";

    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfOfferRepository _offerRepo;
    private readonly IRhshfOfferDocumentGenerator _offerDocumentGenerator;
    private readonly RhshfOfferDocumentDataBuilder _documentDataBuilder;
    private readonly IFileStorageService _fileStorage;
    private readonly IUnitOfWork _uow;

    public IssueCorrectedRhshfOfferHandler(
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

    public async Task<ApplicationResult> Handle(IssueCorrectedRhshfOfferCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        if (profile.ApprovedAmount is null)
            return ApplicationResult.Failure("No ratified amount is on file for this case.");

        var current = await _offerRepo.GetByProfileAndCycleAsync(profile.Id, profile.CurrentCycleNumber, ct);
        if (current is null)
            return ApplicationResult.Failure("No offer exists for this case's current cycle.");

        if (current.Status is not (RhshfOfferStatus.Accepted or RhshfOfferStatus.Generated or RhshfOfferStatus.AwaitingFacResponse))
            return ApplicationResult.Failure("Only an issued or accepted offer can be replaced by a corrected one.");

        // The document is generated and stored before anything on the case changes. The circuit
        // shares one DbContext, so a half-applied change left tracked here — offer superseded, no
        // replacement — would be saved by the next unrelated action on the page. A failure after
        // this point leaves at worst an unreferenced file.
        var version = profile.OfferVersion + 1;
        var build = await _documentDataBuilder.BuildAsync(profile, profile.ApprovedAmount.Value, ct, offerVersion: version);
        var pdfBytes = await _offerDocumentGenerator.GenerateAsync(build.Data, ct);

        // A new file, never the current offer's: that document may be signed, or already in the FAC's hands, and it stays.
        var storagePath = await _fileStorage.UploadAsync(
            OfferContainerName, $"{profile.Reference}/offer-cycle{profile.CurrentCycleNumber}-v{version}.pdf",
            pdfBytes, "application/pdf", ct);

        var reopened = profile.ReopenOfferForCorrection(request.ActorUserId, request.Reason);
        if (reopened.IsFailure)
            return ApplicationResult.Failure(reopened.Error);

        current.Supersede();

        var corrected = RhshfOffer.Create(profile.Id, profile.CurrentCycleNumber, storagePath);
        if (corrected.IsFailure)
            return ApplicationResult.Failure(corrected.Error);

        corrected.Value.RecordSuppressedProductFees(build.SuppressedProductFees);
        await _offerRepo.AddAsync(corrected.Value, ct);

        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}

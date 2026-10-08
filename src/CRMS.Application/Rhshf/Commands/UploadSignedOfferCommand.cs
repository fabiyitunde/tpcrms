using CRMS.Application.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>
/// The FAC's signed copy of the offer (design doc §3.6/§6 #10) — a hard precondition for Accept,
/// enforced in RhshfOffer itself, not just here. No stage-order guard beyond "the offer exists and
/// hasn't been decided yet" (checked inside RhshfOffer.AddDocument).
/// </summary>
public record UploadSignedOfferCommand(
    string Reference, string FileName, string ContentType, byte[] Content,
    RhshfOfferDocumentKind Kind = RhshfOfferDocumentKind.Other) : IRequest<ApplicationResult>;

public class UploadSignedOfferHandler : IRequestHandler<UploadSignedOfferCommand, ApplicationResult>
{
    private const string ContainerName = "rhshf-offer-signed";
    private const long MaxSizeBytes = 10 * 1024 * 1024; // 10 MB, same limit as profiling's supporting documents

    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfOfferRepository _offerRepo;
    private readonly IFileStorageService _fileStorage;
    private readonly IUnitOfWork _uow;

    public UploadSignedOfferHandler(
        IRhshfCreditProfileRepository repo, IRhshfOfferRepository offerRepo, IFileStorageService fileStorage, IUnitOfWork uow)
    {
        _repo = repo;
        _offerRepo = offerRepo;
        _fileStorage = fileStorage;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(UploadSignedOfferCommand request, CancellationToken ct = default)
    {
        if (request.Content.Length == 0)
            return ApplicationResult.Failure("File is empty.");
        if (request.Content.Length > MaxSizeBytes)
            return ApplicationResult.Failure("File exceeds the 10 MB size limit.");

        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        var offer = await _offerRepo.GetByProfileAndCycleAsync(profile.Id, profile.CurrentCycleNumber, ct);
        if (offer is null)
            return ApplicationResult.Failure("No offer has been generated for this case's current cycle.");

        var storagePath = await _fileStorage.UploadAsync(
            ContainerName, $"{profile.Reference}/{Guid.NewGuid()}-{request.FileName}", request.Content, request.ContentType, ct);

        var result = offer.AddDocument(request.FileName, request.ContentType, storagePath, request.Content.Length, request.Kind);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}

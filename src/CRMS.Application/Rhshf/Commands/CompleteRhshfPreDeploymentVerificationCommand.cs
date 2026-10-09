using CRMS.Application.Common;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>Disbursement Officer clearing the Pre-Deployment gate — advances to Disbursement.</summary>
public record CompleteRhshfPreDeploymentVerificationCommand(string Reference, Guid UserId, string? Note)
    : IRequest<ApplicationResult>;

public class CompleteRhshfPreDeploymentVerificationHandler : IRequestHandler<CompleteRhshfPreDeploymentVerificationCommand, ApplicationResult>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfOfferRepository _offerRepo;
    private readonly IUnitOfWork _uow;

    public CompleteRhshfPreDeploymentVerificationHandler(
        IRhshfCreditProfileRepository repo, IRhshfOfferRepository offerRepo, IUnitOfWork uow)
    {
        _repo = repo;
        _offerRepo = offerRepo;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(CompleteRhshfPreDeploymentVerificationCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        // Re-derive the auto (offer-documents) items from the live offer before gating, so they reflect
        // reality and can't be stale-ticked. The offer aggregate is loaded here since it's outside the profile.
        var offer = await _offerRepo.GetByProfileAndCycleAsync(profile.Id, profile.CurrentCycleNumber, ct);
        profile.SyncAutoOfferDocumentItems(offer?.HasRequiredSignedDocuments ?? false);

        var result = profile.CompletePreDeploymentVerification(request.UserId, request.Note);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}

using CRMS.Application.Common;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>
/// Manually re-enqueues the OfferReady portal callback for a case whose automatic delivery failed or
/// was exhausted (e.g. the initial callback was interrupted and the portal never got the offer link).
/// It creates a fresh callback attempt (NextRetryAt = now), which the background dispatcher picks up
/// on its next poll and sends — rebuilding the payload from the case's current state. Only meaningful
/// while the case is awaiting the FAC's offer response.
/// </summary>
public record ResendRhshfOfferReadyCallbackCommand(string Reference) : IRequest<ApplicationResult>;

public class ResendRhshfOfferReadyCallbackHandler : IRequestHandler<ResendRhshfOfferReadyCallbackCommand, ApplicationResult>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfCallbackAttemptRepository _callbackRepo;
    private readonly IUnitOfWork _uow;

    public ResendRhshfOfferReadyCallbackHandler(
        IRhshfCreditProfileRepository repo, IRhshfCallbackAttemptRepository callbackRepo, IUnitOfWork uow)
    {
        _repo = repo;
        _callbackRepo = callbackRepo;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(ResendRhshfOfferReadyCallbackCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        if (profile.InternalStage != RhshfInternalStage.AwaitingOfferAcceptance)
            return ApplicationResult.Failure("The offer-ready notification only applies while the case is awaiting the FAC's offer response.");

        var attempt = RhshfCallbackAttempt.CreateFirst(profile.Id, RhshfCallbackEventType.OfferReady, DateTime.UtcNow);
        await _callbackRepo.AddAsync(attempt, ct);
        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}

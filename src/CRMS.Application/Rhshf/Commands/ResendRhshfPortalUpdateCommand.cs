using CRMS.Application.Common;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>
/// Re-queues one portal update by its EventId — for an update whose automatic retries ran out, or
/// one the portal says it never received. The payload is rebuilt from the case's current state when
/// it is sent, so a re-send can never push the portal back to an older position.
/// </summary>
public record ResendRhshfPortalUpdateCommand(string Reference, string EventId) : IRequest<ApplicationResult>;

public class ResendRhshfPortalUpdateHandler : IRequestHandler<ResendRhshfPortalUpdateCommand, ApplicationResult>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfCallbackAttemptRepository _attempts;
    private readonly IUnitOfWork _uow;

    public ResendRhshfPortalUpdateHandler(
        IRhshfCreditProfileRepository repo, IRhshfCallbackAttemptRepository attempts, IUnitOfWork uow)
    {
        _repo = repo;
        _attempts = attempts;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(ResendRhshfPortalUpdateCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        var history = await _attempts.GetByProfileAsync(profile.Id, ct);
        var matching = history.Where(a => a.EventId == request.EventId).ToList();
        if (matching.Count == 0)
            return ApplicationResult.Failure("That update was not found on this case.");

        // A second click while the first re-send is still queued would only send the same thing twice.
        if (matching.Any(a => a.NextRetryAt is not null))
            return ApplicationResult.Failure("This update is already queued to send.");

        await _attempts.AddAsync(matching[0].CreateManualResend(), ct);
        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}

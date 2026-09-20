using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace CRMS.Infrastructure.Events.Handlers;

/// <summary>
/// Enqueues an outbound webhook attempt when a case reaches a terminal (or terminal-like) outcome
/// (design doc §4.4, Phase 10). Deliberately does not send anything itself — actual delivery,
/// signing, and retry is RhshfCallbackBackgroundService's job, driven by the RhshfCallbackAttempt
/// row this writes. Runs post-commit (see DomainEventPublishingInterceptor), so this handler's own
/// SaveChangesAsync is a separate transaction from whatever produced the event.
/// </summary>
public class RhshfCaseDecidedCallbackHandler : IDomainEventHandler<RhshfCaseDecidedEvent>
{
    private readonly IRhshfCallbackAttemptRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<RhshfCaseDecidedCallbackHandler> _logger;

    public RhshfCaseDecidedCallbackHandler(IRhshfCallbackAttemptRepository repo, IUnitOfWork uow, ILogger<RhshfCaseDecidedCallbackHandler> logger)
    {
        _repo = repo;
        _uow = uow;
        _logger = logger;
    }

    public async Task HandleAsync(RhshfCaseDecidedEvent domainEvent, CancellationToken ct = default)
    {
        var attempt = RhshfCallbackAttempt.CreateFirst(domainEvent.RhshfCreditProfileId, RhshfCallbackEventType.Decided, domainEvent.OccurredAt);
        await _repo.AddAsync(attempt, ct);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Enqueued RH-SHF outcome webhook {EventId} for profile {ProfileId}", attempt.EventId, domainEvent.RhshfCreditProfileId);
    }
}

/// <summary>Same enqueue mechanics as RhshfCaseDecidedCallbackHandler, for the non-terminal
/// actionRequired: "REVIEW_OFFER" signal (§6 #10) fired from Ratify()'s Ratified branch.</summary>
public class RhshfOfferReadyCallbackHandler : IDomainEventHandler<RhshfOfferReadyEvent>
{
    private readonly IRhshfCallbackAttemptRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<RhshfOfferReadyCallbackHandler> _logger;

    public RhshfOfferReadyCallbackHandler(IRhshfCallbackAttemptRepository repo, IUnitOfWork uow, ILogger<RhshfOfferReadyCallbackHandler> logger)
    {
        _repo = repo;
        _uow = uow;
        _logger = logger;
    }

    public async Task HandleAsync(RhshfOfferReadyEvent domainEvent, CancellationToken ct = default)
    {
        var attempt = RhshfCallbackAttempt.CreateFirst(domainEvent.RhshfCreditProfileId, RhshfCallbackEventType.OfferReady, domainEvent.OccurredAt);
        await _repo.AddAsync(attempt, ct);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Enqueued RH-SHF offer-ready webhook {EventId} for profile {ProfileId}", attempt.EventId, domainEvent.RhshfCreditProfileId);
    }
}

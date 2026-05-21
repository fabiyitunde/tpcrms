using CRMS.Domain.Aggregates.Namp;
using CRMS.Domain.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using CRMS.Infrastructure.ExternalServices.Namp;
using Microsoft.Extensions.Logging;

namespace CRMS.Infrastructure.Events.Handlers;

/// <summary>
/// Sends outbound PAYS callbacks when a NampApplication reaches a notable status.
/// - OfferGenerated → Approved (ratification complete, offer letter ready)
/// - Active         → Active   (GPS confirmed, equipment deployed)
/// - Any decline/lapse path → Declined (with reason note)
/// Runs as a second handler for NampStatusChangedEvent alongside NampStatusChangedWorkflowHandler.
/// Non-fatal: callback failures are logged but do not roll back the transaction.
/// </summary>
public class NampCallbackEventHandler : IDomainEventHandler<NampStatusChangedEvent>
{
    private static readonly HashSet<NampApplicationStatus> CallbackStatuses =
    [
        // Approved path — offer letter generated on ratification
        NampApplicationStatus.OfferGenerated,

        // Active — equipment deployed + GPS live
        NampApplicationStatus.Active,

        // Decline paths
        NampApplicationStatus.TechnicalDeclined,
        NampApplicationStatus.FinancialDeclined,
        NampApplicationStatus.BranchCommitteeDeclined,
        NampApplicationStatus.ZonalCommitteeDeclined,
        NampApplicationStatus.RegionalCommitteeDeclined,
        NampApplicationStatus.HOCommitteeDeclined,
        NampApplicationStatus.RatificationDeclined,
        NampApplicationStatus.OfferLapsed,
        NampApplicationStatus.Declined,
    ];

    private readonly INampApplicationRepository _appRepo;
    private readonly INampCallbackService _callbackService;
    private readonly ILogger<NampCallbackEventHandler> _logger;

    public NampCallbackEventHandler(
        INampApplicationRepository appRepo,
        INampCallbackService callbackService,
        ILogger<NampCallbackEventHandler> logger)
    {
        _appRepo = appRepo;
        _callbackService = callbackService;
        _logger = logger;
    }

    public async Task HandleAsync(NampStatusChangedEvent domainEvent, CancellationToken ct = default)
    {
        if (!CallbackStatuses.Contains(domainEvent.NewStatus))
            return;

        var callbackStatus = domainEvent.NewStatus switch
        {
            NampApplicationStatus.OfferGenerated => NampCallbackStatus.Approved,
            NampApplicationStatus.Active         => NampCallbackStatus.Active,
            _                                    => NampCallbackStatus.Declined,
        };

        // Retrieve the application reference (PAYS external key)
        var app = await _appRepo.GetByIdAsync(domainEvent.NampApplicationId, ct);
        if (app is null)
        {
            _logger.LogWarning(
                "NampCallbackEventHandler: application {Id} not found — callback not sent for status {Status}",
                domainEvent.NampApplicationId,
                domainEvent.NewStatus);
            return;
        }

        _logger.LogInformation(
            "Sending PAYS callback for NAMP application {Reference}: {Status} → {CallbackStatus}",
            app.ApplicationReference,
            domainEvent.NewStatus,
            callbackStatus);

        await _callbackService.SendCallbackAsync(
            app.ApplicationReference,
            callbackStatus,
            note: domainEvent.Note,
            ct);
    }
}

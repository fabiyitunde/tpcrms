using CRMS.Domain.Aggregates.Namp;
using CRMS.Domain.Common;
using CRMS.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace CRMS.Infrastructure.Events.Handlers;

/// <summary>
/// Updates the NampWorkflowInstance whenever a NampApplication status changes.
/// Creates the instance on the first event (Draft); transitions it on subsequent events.
/// </summary>
public class NampStatusChangedWorkflowHandler : IDomainEventHandler<NampStatusChangedEvent>
{
    private readonly INampWorkflowConfigRepository _configRepo;
    private readonly INampWorkflowInstanceRepository _instanceRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<NampStatusChangedWorkflowHandler> _logger;

    public NampStatusChangedWorkflowHandler(
        INampWorkflowConfigRepository configRepo,
        INampWorkflowInstanceRepository instanceRepo,
        IUnitOfWork unitOfWork,
        ILogger<NampStatusChangedWorkflowHandler> logger)
    {
        _configRepo = configRepo;
        _instanceRepo = instanceRepo;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task HandleAsync(NampStatusChangedEvent domainEvent, CancellationToken ct = default)
    {
        var config = await _configRepo.GetByStatusAsync(domainEvent.NewStatus, ct);
        if (config is null)
        {
            _logger.LogWarning(
                "NampWorkflowConfig not found for status {Status} — NampWorkflowInstance not updated for application {Id}",
                domainEvent.NewStatus,
                domainEvent.NampApplicationId);
            return;
        }

        var instance = await _instanceRepo.GetByNampApplicationIdAsync(domainEvent.NampApplicationId, ct);

        if (instance is null)
        {
            // First event — create the instance
            instance = NampWorkflowInstance.Create(
                domainEvent.NampApplicationId,
                domainEvent.NewStatus,
                config.DisplayName,
                config.AssignedRole,
                config.SlaHours);

            instance.SetAuditInfo(domainEvent.ChangedByUserId.ToString(), isNew: true);
            await _instanceRepo.AddAsync(instance, ct);
        }
        else
        {
            instance.Transition(
                domainEvent.NewStatus,
                config.DisplayName,
                config.AssignedRole,
                config.SlaHours,
                config.IsTerminal);

            instance.SetAuditInfo(domainEvent.ChangedByUserId.ToString());
            _instanceRepo.Update(instance);
        }

        await _unitOfWork.SaveChangesAsync(ct);
    }
}

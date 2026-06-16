using CRMS.Application.Namp.Interfaces;
using CRMS.Application.Notification.Interfaces;
using CRMS.Domain.Aggregates.Namp;
using CRMS.Domain.Common;
using CRMS.Domain.Constants;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CRMS.Infrastructure.Events.Handlers;

/// <summary>
/// "Front door doorbell": when a new NAMP application is staged from the inbound webhook,
/// emails the Loan Officers at the resolved branch so they know to recall it from the queue.
/// Fires before a NampApplication exists, so it works off the staging record directly.
/// Runs in the post-save event scope; any failure here can never roll back the staged record.
/// </summary>
public class NampStagingReceivedNotificationHandler : IDomainEventHandler<NampStagingRecordReceivedEvent>
{
    private readonly INampStagingRepository _stagingRepo;
    private readonly INampNotificationRecipientResolver _recipientResolver;
    private readonly INotificationService _notificationService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NampStagingReceivedNotificationHandler> _logger;

    public NampStagingReceivedNotificationHandler(
        INampStagingRepository stagingRepo,
        INampNotificationRecipientResolver recipientResolver,
        INotificationService notificationService,
        IConfiguration configuration,
        ILogger<NampStagingReceivedNotificationHandler> logger)
    {
        _stagingRepo = stagingRepo;
        _recipientResolver = recipientResolver;
        _notificationService = notificationService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task HandleAsync(NampStagingRecordReceivedEvent domainEvent, CancellationToken ct = default)
    {
        try
        {
            var record = await _stagingRepo.GetByIdAsync(domainEvent.StagingRecordId, ct);
            if (record is null)
            {
                _logger.LogWarning("NAMP staging notification skipped — record {Id} not found", domainEvent.StagingRecordId);
                return;
            }

            // Already recalled by the time this runs — the loan officer has it; nothing to announce.
            if (record.IsRecalled)
                return;

            var locationIds = new List<Guid>();
            if (record.ResolvedBranchId is Guid branchId) locationIds.Add(branchId);
            if (record.ResolvedOfficeId is Guid officeId) locationIds.Add(officeId);
            if (record.ResolvedLocationId is Guid locationId) locationIds.Add(locationId);

            var recipients = await _recipientResolver.ResolveAsync(Roles.LoanOfficer, locationIds, ct);
            if (recipients.Count == 0)
            {
                _logger.LogWarning("NAMP staging notification — no active loan officers found for {Number}",
                    record.CrmsApplicationNumber);
                return;
            }

            var actionUrl = BuildQueueUrl();

            foreach (var user in recipients)
            {
                var variables = new Dictionary<string, string>
                {
                    { "RecipientName", user.FullName },
                    { "ApplicationNumber", record.CrmsApplicationNumber },
                    { "ApplicantName", record.ApplicantName },
                    { "ActionUrl", actionUrl },
                };

                await _notificationService.SendEmailAsync(
                    NotificationType.WorkflowAssigned,
                    user.Email,
                    user.FullName,
                    "NAMP_NEW_IN_QUEUE",
                    variables,
                    user.Id,
                    null,
                    record.CrmsApplicationNumber,
                    NotificationPriority.Normal,
                    ct);
            }

            _logger.LogInformation("NAMP new-in-queue notifications queued for {Count} loan officer(s) — {Number}",
                recipients.Count, record.CrmsApplicationNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send NAMP staging notifications for record {Id}", domainEvent.StagingRecordId);
        }
    }

    private string BuildQueueUrl()
    {
        var baseUrl = (_configuration["WebsiteUrl"] ?? string.Empty).TrimEnd('/');
        return string.IsNullOrEmpty(baseUrl) ? "/namp" : $"{baseUrl}/namp";
    }
}

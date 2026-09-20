using CRMS.Application.Rhshf.Webhooks;
using CRMS.Domain.Interfaces;
using CRMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CRMS.Infrastructure.ExternalServices.Rhshf;

/// <summary>
/// Owns the actual "send one due attempt, schedule a retry or give up" logic (design doc §4.4,
/// Phase 10) — extracted out of RhshfCallbackBackgroundService so it's directly unit-testable
/// against an in-memory DbContext + a fake IRhshfCallbackService, without needing to drive a
/// BackgroundService's infinite ExecuteAsync loop. The background service is a thin poll-loop
/// wrapper around this class.
/// </summary>
public class RhshfCallbackDispatcher
{
    public const int MaxAttempts = 5;

    // Attempt 1->2, 2->3, 3->4, 4->5 — sums to ~26 minutes, matching the brief's "~5 attempts over
    // ~30 minutes" without needing a formula that could overshoot on a slow attempt.
    public static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(3),
        TimeSpan.FromMinutes(7),
        TimeSpan.FromMinutes(15),
    ];

    private readonly CRMSDbContext _db;
    private readonly IRhshfCreditProfileRepository _profileRepo;
    private readonly IRhshfCallbackAttemptRepository _attemptRepo;
    private readonly IRhshfCallbackService _callbackService;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<RhshfCallbackDispatcher> _logger;

    public RhshfCallbackDispatcher(
        CRMSDbContext db,
        IRhshfCreditProfileRepository profileRepo,
        IRhshfCallbackAttemptRepository attemptRepo,
        IRhshfCallbackService callbackService,
        IUnitOfWork uow,
        ILogger<RhshfCallbackDispatcher> logger)
    {
        _db = db;
        _profileRepo = profileRepo;
        _attemptRepo = attemptRepo;
        _callbackService = callbackService;
        _uow = uow;
        _logger = logger;
    }

    public async Task<int> ProcessDueAttemptsAsync(CancellationToken ct = default)
    {
        var due = await _attemptRepo.GetDueAsync(DateTime.UtcNow, ct);
        foreach (var attempt in due)
            await ProcessAttemptAsync(attempt.Id, ct);

        return due.Count;
    }

    public async Task ProcessAttemptAsync(Guid attemptId, CancellationToken ct = default)
    {
        var attempt = await _db.RhshfCallbackAttempts.FindAsync([attemptId], ct);
        if (attempt is null)
            return;

        var profile = await _profileRepo.GetByIdAsync(attempt.RhshfCreditProfileId, ct);
        if (profile is null)
        {
            _logger.LogWarning(
                "RH-SHF callback attempt {AttemptId}: profile {ProfileId} not found — cannot build payload",
                attemptId, attempt.RhshfCreditProfileId);
            attempt.RecordResult(succeeded: false, responseStatusCode: null);
            await _uow.SaveChangesAsync(ct);
            return;
        }

        var payload = RhshfWebhookPayloadBuilder.Build(profile, attempt.EventType, attempt.EventId, attempt.EventOccurredAt);
        var result = await _callbackService.SendAsync(profile.CallbackUrl, payload, ct);

        attempt.RecordResult(result.Succeeded, result.StatusCode);

        if (!result.Succeeded)
        {
            if (attempt.AttemptNumber < MaxAttempts)
            {
                var delay = RetryDelays[Math.Min(attempt.AttemptNumber - 1, RetryDelays.Length - 1)];
                var retry = attempt.CreateRetry(DateTime.UtcNow.Add(delay));
                await _attemptRepo.AddAsync(retry, ct);

                _logger.LogWarning(
                    "RH-SHF callback {EventId} attempt {AttemptNumber} failed for {Reference} — retrying in {Delay}",
                    attempt.EventId, attempt.AttemptNumber, profile.Reference, delay);
            }
            else
            {
                _logger.LogError(
                    "RH-SHF callback {EventId} for {Reference} exhausted all {MaxAttempts} attempts — manual reconciliation required. Last error: {Error}",
                    attempt.EventId, profile.Reference, MaxAttempts, result.ErrorMessage ?? $"HTTP {result.StatusCode}");
            }
        }

        await _uow.SaveChangesAsync(ct);
    }
}

using CRMS.Application.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Queries;

/// <summary>Every update the CRMS has sent (or tried to send) to the portal for one case, newest
/// first. Before this, an update that used up its retries left only a log line nobody on the bank
/// side could see, and staff fell back to phoning the FAC.</summary>
public record GetRhshfPortalUpdatesQuery(string Reference) : IRequest<ApplicationResult<List<RhshfPortalUpdateDto>>>;

public enum RhshfPortalUpdateState
{
    Delivered,
    Retrying,
    Failed,
}

public record RhshfPortalUpdateDto(
    string EventId,
    RhshfCallbackEventType EventType,
    DateTime OccurredAt,
    RhshfPortalUpdateState State,
    int Attempts,
    DateTime? LastAttemptAt,
    int? LastResponseStatusCode,
    DateTime? NextRetryAt);

public class GetRhshfPortalUpdatesHandler : IRequestHandler<GetRhshfPortalUpdatesQuery, ApplicationResult<List<RhshfPortalUpdateDto>>>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfCallbackAttemptRepository _attempts;

    public GetRhshfPortalUpdatesHandler(IRhshfCreditProfileRepository repo, IRhshfCallbackAttemptRepository attempts)
    {
        _repo = repo;
        _attempts = attempts;
    }

    public async Task<ApplicationResult<List<RhshfPortalUpdateDto>>> Handle(GetRhshfPortalUpdatesQuery request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult<List<RhshfPortalUpdateDto>>.Failure("Case not found.");

        var attempts = await _attempts.GetByProfileAsync(profile.Id, ct);

        // One logical update is several rows: one per attempt, all sharing the EventId.
        var updates = attempts
            .GroupBy(a => a.EventId)
            .Select(g =>
            {
                var sent = g.Where(a => a.SentAt is not null).OrderBy(a => a.SentAt).ToList();
                var last = sent.LastOrDefault();
                var pending = g.FirstOrDefault(a => a.NextRetryAt is not null);
                var state = g.Any(a => a.Succeeded) ? RhshfPortalUpdateState.Delivered
                    : pending is not null ? RhshfPortalUpdateState.Retrying
                    : RhshfPortalUpdateState.Failed;

                return new RhshfPortalUpdateDto(
                    g.Key,
                    g.First().EventType,
                    g.First().EventOccurredAt,
                    state,
                    sent.Count,
                    last?.SentAt,
                    last?.ResponseStatusCode,
                    pending?.NextRetryAt);
            })
            .OrderByDescending(u => u.OccurredAt)
            .ToList();

        return ApplicationResult<List<RhshfPortalUpdateDto>>.Success(updates);
    }
}

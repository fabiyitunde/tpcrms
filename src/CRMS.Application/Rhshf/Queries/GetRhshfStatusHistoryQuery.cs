using CRMS.Application.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Queries;

public record GetRhshfStatusHistoryQuery(string Reference)
    : IRequest<ApplicationResult<List<RhshfStatusHistoryDto>>>;

/// <summary>
/// One entry in the case's chronological trail. <see cref="Actor"/> is always populated — a
/// resolved user name where a CRMS user acted, the recorded label ("FAC", "Committee", "Portal")
/// otherwise — so the view never has to decide between an id, a label and a blank.
/// </summary>
public record RhshfStatusHistoryDto(
    int CycleNumber,
    RhshfCaseStatus Status,
    RhshfInternalStage? InternalStage,
    RhshfProfilingStage? ProfilingStage,
    string Action,
    Guid? ActorUserId,
    string Actor,
    bool IsSystemActor,
    string? Note,
    DateTime ChangedAt);

public class GetRhshfStatusHistoryHandler
    : IRequestHandler<GetRhshfStatusHistoryQuery, ApplicationResult<List<RhshfStatusHistoryDto>>>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IUserNameResolver _names;

    public GetRhshfStatusHistoryHandler(IRhshfCreditProfileRepository repo, IUserNameResolver names)
    {
        _repo = repo;
        _names = names;
    }

    public async Task<ApplicationResult<List<RhshfStatusHistoryDto>>> Handle(
        GetRhshfStatusHistoryQuery request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult<List<RhshfStatusHistoryDto>>.Failure("Case not found.");

        var entries = profile.StatusHistory.OrderBy(h => h.ChangedAt).ToList();
        var names = await _names.ResolveManyAsync(
            entries.Where(h => h.ActorUserId.HasValue).Select(h => h.ActorUserId!.Value), ct);

        var dtos = entries.Select(h => new RhshfStatusHistoryDto(
            h.CycleNumber,
            h.Status,
            h.InternalStage,
            h.ProfilingStage,
            h.Action,
            h.ActorUserId,
            h.ActorUserId is Guid id && names.TryGetValue(id, out var name) ? name : h.ActorLabel ?? "System",
            IsSystemActor: h.ActorUserId is null,
            h.Note,
            h.ChangedAt)).ToList();

        return ApplicationResult<List<RhshfStatusHistoryDto>>.Success(dtos);
    }
}

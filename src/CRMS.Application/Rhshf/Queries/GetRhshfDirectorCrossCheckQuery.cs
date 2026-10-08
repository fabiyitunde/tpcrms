using System.Text.Json;
using CRMS.Application.Common;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Queries;

/// <summary>
/// Reads the SAVED directors cross-check snapshot (FAC-declared vs CAC registry vs core banking).
/// Read-only and free — it never calls CAC/CBS, so revisiting the tab shows the last result without
/// re-billing the CAC lookup. Returns null data when no cross-check has been run yet (the UI then
/// prompts to run one). Running/refreshing a cross-check is RunRhshfDirectorCrossCheckCommand.
/// </summary>
public record GetRhshfDirectorCrossCheckQuery(string Reference) : IRequest<ApplicationResult<RhshfDirectorCrossCheckDto?>>;

public record RhshfDirectorCrossCheckDto(
    IReadOnlyList<RhshfCrossCheckFacDto> Fac,
    bool CacOk, string? CacStatus, string? CacError, IReadOnlyList<RhshfCrossCheckRefDto> Cac,
    bool CbsOk, string? CbsError, IReadOnlyList<RhshfCrossCheckRefDto> CbsDirectors, IReadOnlyList<RhshfCrossCheckRefDto> CbsSignatories,
    DateTime FetchedAt);

/// <summary>A director the FAC declared, with whether the two reference sources corroborate them.
/// Null flags mean that source could not be reached when the cross-check was run.</summary>
public record RhshfCrossCheckFacDto(
    string FullName, bool HasBvn, decimal? ShareholdingPercent, bool IsChairman, bool? InCac, bool? InCbs);

/// <summary>A party from a reference source (CAC or CBS), with whether the FAC declared them.</summary>
public record RhshfCrossCheckRefDto(string FullName, string? Detail, bool HasBvn, bool DeclaredByFac);

public class GetRhshfDirectorCrossCheckHandler
    : IRequestHandler<GetRhshfDirectorCrossCheckQuery, ApplicationResult<RhshfDirectorCrossCheckDto?>>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfDirectorCrossCheckRepository _snapshotRepo;

    public GetRhshfDirectorCrossCheckHandler(
        IRhshfCreditProfileRepository repo, IRhshfDirectorCrossCheckRepository snapshotRepo)
    {
        _repo = repo;
        _snapshotRepo = snapshotRepo;
    }

    public async Task<ApplicationResult<RhshfDirectorCrossCheckDto?>> Handle(
        GetRhshfDirectorCrossCheckQuery request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult<RhshfDirectorCrossCheckDto?>.Failure("Case not found.");

        var snapshot = await _snapshotRepo.GetByProfileIdAsync(profile.Id, ct);
        if (snapshot is null)
            return ApplicationResult<RhshfDirectorCrossCheckDto?>.Success(null); // never run — UI prompts to run

        var dto = JsonSerializer.Deserialize<RhshfDirectorCrossCheckDto>(snapshot.SnapshotJson);
        return ApplicationResult<RhshfDirectorCrossCheckDto?>.Success(dto);
    }
}

using CRMS.Application.Common;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Queries;

public record GetRhshfDirectorsQuery(string Reference) : IRequest<ApplicationResult<RhshfDirectorsDto>>;

/// <summary>Directors plus the CAC company profile they were fetched alongside — one round trip,
/// since the Overview tab renders both together.</summary>
public record RhshfDirectorsDto(
    string? CacStatus,
    string? CacEntityType,
    string? CacRegistrationDate,
    string? CacNatureOfBusiness,
    decimal? CacShareCapital,
    string? CacAddress,
    DateTime? CacFetchedAt,
    List<RhshfDirectorDto> Directors);

public record RhshfDirectorDto(
    Guid Id,
    string FullName,
    string? Occupation,
    bool IsChairman,
    string? AffiliateType,
    long? NumSharesAllotted,
    string? TypeOfShares,
    decimal? ShareholdingPercent,
    /// <summary>Presence only — the number itself is never sent to the browser. Mirrors NAMP's
    /// Provided/Missing badge rather than rendering a BVN in a table anyone can screenshot.</summary>
    bool HasBvn,
    bool SourcedFromCac,
    /// <summary>Declared by the FAC (during profiling) — staff cannot delete these, only verify.</summary>
    bool DeclaredByFac,
    string? Email,
    string? PhoneNumber);

public class GetRhshfDirectorsHandler : IRequestHandler<GetRhshfDirectorsQuery, ApplicationResult<RhshfDirectorsDto>>
{
    private readonly IRhshfCreditProfileRepository _repo;

    public GetRhshfDirectorsHandler(IRhshfCreditProfileRepository repo) => _repo = repo;

    public async Task<ApplicationResult<RhshfDirectorsDto>> Handle(
        GetRhshfDirectorsQuery request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult<RhshfDirectorsDto>.Failure("Case not found.");

        var directors = profile.Directors
            .OrderByDescending(d => d.IsChairman)
            .ThenBy(d => d.FullName)
            .Select(d => new RhshfDirectorDto(
                d.Id, d.FullName, d.Occupation, d.IsChairman, d.AffiliateType,
                d.NumSharesAllotted, d.TypeOfShares, d.ShareholdingPercent,
                !string.IsNullOrWhiteSpace(d.Bvn), d.SourcedFromCac, d.DeclaredByFac, d.Email, d.PhoneNumber))
            .ToList();

        return ApplicationResult<RhshfDirectorsDto>.Success(new RhshfDirectorsDto(
            profile.CacStatus, profile.CacEntityType, profile.CacRegistrationDate,
            profile.CacNatureOfBusiness, profile.CacShareCapital, profile.CacAddress,
            profile.CacFetchedAt, directors));
    }
}

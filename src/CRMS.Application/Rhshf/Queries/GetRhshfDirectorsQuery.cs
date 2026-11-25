using CRMS.Application.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using CRMS.Domain.Services;

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
    List<RhshfDirectorDto> Directors,
    /// <summary>Management staff the FAC declared — no shares, no bureau check.</summary>
    List<RhshfManagementStaffDto>? ManagementStaff = null);

public record RhshfManagementStaffDto(
    Guid Id, string FullName, string Position, string? PhoneNumber, string? Email, bool IsChiefPromoter);

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
    string? PhoneNumber,
    /// <summary>Masked (e.g. 222****90). The full value only through the audited reveal.</summary>
    string? BvnMasked = null,
    bool HasNin = false,
    string? NinMasked = null,
    RhshfMeansOfIdType? MeansOfIdType = null,
    string? MeansOfIdNumber = null,
    DateTime? MeansOfIdExpiry = null,
    bool IsMeansOfIdExpired = false,
    bool IsChiefPromoter = false);

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
                !string.IsNullOrWhiteSpace(d.Bvn), d.SourcedFromCac, d.DeclaredByFac, d.Email, d.PhoneNumber,
                BvnMasked: string.IsNullOrWhiteSpace(d.Bvn) ? null : SensitiveDataMasker.MaskBvn(d.Bvn),
                HasNin: !string.IsNullOrWhiteSpace(d.Nin),
                NinMasked: string.IsNullOrWhiteSpace(d.Nin) ? null : SensitiveDataMasker.MaskBvn(d.Nin),
                MeansOfIdType: d.MeansOfIdType,
                // Masked too: on a NIN slip the ID number is the NIN.
                MeansOfIdNumber: string.IsNullOrWhiteSpace(d.MeansOfIdNumber) ? null : SensitiveDataMasker.MaskAccountNumber(d.MeansOfIdNumber),
                MeansOfIdExpiry: d.MeansOfIdExpiry,
                IsMeansOfIdExpired: d.IsMeansOfIdExpired(DateTime.UtcNow),
                IsChiefPromoter: d.IsChiefPromoter))
            .ToList();

        return ApplicationResult<RhshfDirectorsDto>.Success(new RhshfDirectorsDto(
            profile.CacStatus, profile.CacEntityType, profile.CacRegistrationDate,
            profile.CacNatureOfBusiness, profile.CacShareCapital, profile.CacAddress,
            profile.CacFetchedAt, directors,
            profile.ManagementStaff
                .OrderBy(m => m.FullName)
                .Select(m => new RhshfManagementStaffDto(m.Id, m.FullName, m.Position, m.PhoneNumber, m.Email, m.IsChiefPromoter))
                .ToList()));
    }
}

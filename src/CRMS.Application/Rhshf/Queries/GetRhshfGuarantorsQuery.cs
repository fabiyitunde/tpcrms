using CRMS.Application.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Queries;

public record GetRhshfGuarantorsQuery(string Reference) : IRequest<ApplicationResult<List<RhshfGuarantorDto>>>;

/// <summary>A guarantor on a case. BVN is exposed as presence only (never the number itself, mirroring
/// directors); the RC number is a public registry identifier, so it is shown.</summary>
public record RhshfGuarantorDto(
    Guid Id,
    RhshfGuarantorType GuarantorType,
    string FullName,
    bool HasBvn,
    string? RcNumber,
    string? Relationship,
    string? PhoneNumber,
    string? Email,
    string? Address,
    decimal? GuaranteeAmount,
    string? Notes);

public class GetRhshfGuarantorsHandler : IRequestHandler<GetRhshfGuarantorsQuery, ApplicationResult<List<RhshfGuarantorDto>>>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfGuarantorRepository _guarantors;

    public GetRhshfGuarantorsHandler(IRhshfCreditProfileRepository repo, IRhshfGuarantorRepository guarantors)
    {
        _repo = repo;
        _guarantors = guarantors;
    }

    public async Task<ApplicationResult<List<RhshfGuarantorDto>>> Handle(GetRhshfGuarantorsQuery request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult<List<RhshfGuarantorDto>>.Failure("Case not found.");

        var guarantors = await _guarantors.GetByProfileIdAsync(profile.Id, ct);

        var dtos = guarantors
            .OrderBy(g => g.FullName)
            .Select(g => new RhshfGuarantorDto(
                g.Id, g.GuarantorType, g.FullName, !string.IsNullOrWhiteSpace(g.Bvn), g.RcNumber,
                g.Relationship, g.PhoneNumber, g.Email, g.Address, g.GuaranteeAmount, g.Notes))
            .ToList();

        return ApplicationResult<List<RhshfGuarantorDto>>.Success(dtos);
    }
}

using CRMS.Application.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Queries;

public record GetRhshfCollateralQuery(string Reference) : IRequest<ApplicationResult<List<RhshfCollateralDto>>>;

public record RhshfCollateralDocumentDto(Guid Id, string FileName, long SizeBytes, DateTime UploadedAt);

public record RhshfCollateralDto(
    Guid Id, RhshfCollateralType Type, string? ReferenceNumber, DateTime? IssuedDate, DateTime? ExpiryDate,
    Guid RecordedBy, DateTime RecordedAt, string? Notes,
    string? GuarantorBankName, decimal? GuaranteeAmount, bool? IsUnconditional,
    decimal? CrgCoveragePercentage,
    string? PropertyDescription, decimal? PropertyValue, string? TitleReferenceNumber,
    string? RegistrationAuthority, RhshfCollateralPerfectionStatus? PerfectionStatus,
    List<RhshfCollateralDocumentDto> Documents);

public class GetRhshfCollateralHandler : IRequestHandler<GetRhshfCollateralQuery, ApplicationResult<List<RhshfCollateralDto>>>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfCollateralRepository _collateralRepo;

    public GetRhshfCollateralHandler(IRhshfCreditProfileRepository repo, IRhshfCollateralRepository collateralRepo)
    {
        _repo = repo;
        _collateralRepo = collateralRepo;
    }

    public async Task<ApplicationResult<List<RhshfCollateralDto>>> Handle(GetRhshfCollateralQuery request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult<List<RhshfCollateralDto>>.Failure("Case not found.");

        var records = await _collateralRepo.GetByProfileAndCycleAsync(profile.Id, profile.CurrentCycleNumber, ct);
        var dto = records.Select(c => new RhshfCollateralDto(
            c.Id, c.Type, c.ReferenceNumber, c.IssuedDate, c.ExpiryDate, c.RecordedBy, c.RecordedAt, c.Notes,
            c.GuarantorBankName, c.GuaranteeAmount, c.IsUnconditional,
            c.CrgCoveragePercentage,
            c.PropertyDescription, c.PropertyValue, c.TitleReferenceNumber, c.RegistrationAuthority, c.PerfectionStatus,
            c.Documents.Select(d => new RhshfCollateralDocumentDto(d.Id, d.FileName, d.SizeBytes, d.UploadedAt)).ToList()))
            .ToList();

        return ApplicationResult<List<RhshfCollateralDto>>.Success(dto);
    }
}

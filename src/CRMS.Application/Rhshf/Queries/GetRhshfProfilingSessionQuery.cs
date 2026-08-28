using CRMS.Application.Common;
using CRMS.Application.Rhshf.DTOs;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Queries;

public record GetRhshfProfilingSessionQuery(string Reference) : IRequest<ApplicationResult<RhshfProfilingSessionDto>>;

public class GetRhshfProfilingSessionHandler : IRequestHandler<GetRhshfProfilingSessionQuery, ApplicationResult<RhshfProfilingSessionDto>>
{
    private readonly IRhshfCreditProfileRepository _repo;

    public GetRhshfProfilingSessionHandler(IRhshfCreditProfileRepository repo)
    {
        _repo = repo;
    }

    public async Task<ApplicationResult<RhshfProfilingSessionDto>> Handle(
        GetRhshfProfilingSessionQuery request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult<RhshfProfilingSessionDto>.Failure("Case not found.");

        var dto = new RhshfProfilingSessionDto(
            Reference: profile.Reference,
            Status: profile.Status,
            CurrentStage: profile.CurrentStage,
            CompanyName: profile.CompanyName,
            RcNumber: profile.RcNumber,
            Tin: profile.Tin,
            BoaAccountNumber: profile.BoaAccountNumber,
            State: profile.State,
            Lga: profile.Lga,
            TotalEopValue: profile.TotalEopValue,
            Currency: profile.Currency,
            FarmerCount: profile.FarmerCount,
            EopLines: profile.EopLines.Select(l => new RhshfEopLineDto(l.Commodity, l.QuantityKg, l.UnitPricePerKg, l.LineValue)).ToList(),
            BureauCheckOutcome: profile.BureauCheckOutcome,
            BureauTotalLoans: profile.BureauTotalLoans,
            BureauActiveLoans: profile.BureauActiveLoans,
            BureauDelinquentFacilities: profile.BureauDelinquentFacilities,
            BureauTotalOutstanding: profile.BureauTotalOutstanding,
            BureauTotalOverdue: profile.BureauTotalOverdue,
            SupportingDocuments: profile.SupportingDocuments
                .Select(d => new RhshfSupportingDocumentDto(d.Id, d.FileName, d.SizeBytes, d.UploadedAt))
                .ToList());

        return ApplicationResult<RhshfProfilingSessionDto>.Success(dto);
    }
}

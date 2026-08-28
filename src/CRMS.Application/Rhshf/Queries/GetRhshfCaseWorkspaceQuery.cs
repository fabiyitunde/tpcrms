using CRMS.Application.Common;
using CRMS.Application.Rhshf.DTOs;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Queries;

public record GetRhshfCaseWorkspaceQuery(string Reference) : IRequest<ApplicationResult<RhshfCaseWorkspaceDto>>;

public class GetRhshfCaseWorkspaceHandler : IRequestHandler<GetRhshfCaseWorkspaceQuery, ApplicationResult<RhshfCaseWorkspaceDto>>
{
    private readonly IRhshfCreditProfileRepository _repo;

    public GetRhshfCaseWorkspaceHandler(IRhshfCreditProfileRepository repo) => _repo = repo;

    public async Task<ApplicationResult<RhshfCaseWorkspaceDto>> Handle(GetRhshfCaseWorkspaceQuery request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult<RhshfCaseWorkspaceDto>.Failure("Case not found.");

        var dto = new RhshfCaseWorkspaceDto(
            Reference: profile.Reference,
            Status: profile.Status,
            InternalStage: profile.InternalStage,
            CurrentCycleNumber: profile.CurrentCycleNumber,
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
                .Select(d => new RhshfSupportingDocumentDto(d.Id, d.FileName, d.SizeBytes, d.UploadedAt)).ToList(),
            Appraisals: profile.Appraisals
                .Select(a => new RhshfAppraisalDto(a.CycleNumber, a.CreditOfficerId, a.AppraisedAt, a.Outcome, a.Notes)).ToList(),
            RiskReviews: profile.RiskReviews
                .Select(r => new RhshfRiskReviewDto(r.CycleNumber, r.RiskOfficerId, r.ReviewedAt, r.Outcome, r.Notes)).ToList());

        return ApplicationResult<RhshfCaseWorkspaceDto>.Success(dto);
    }
}

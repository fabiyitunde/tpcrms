using CRMS.Application.Common;
using CRMS.Application.Rhshf.DTOs;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Queries;

public record GetRhshfProfilingSessionQuery(string Reference) : IRequest<ApplicationResult<RhshfProfilingSessionDto>>;

public class GetRhshfProfilingSessionHandler : IRequestHandler<GetRhshfProfilingSessionQuery, ApplicationResult<RhshfProfilingSessionDto>>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfDocumentRequirementRepository _requirementRepo;

    public GetRhshfProfilingSessionHandler(
        IRhshfCreditProfileRepository repo, IRhshfDocumentRequirementRepository requirementRepo)
    {
        _repo = repo;
        _requirementRepo = requirementRepo;
    }

    public async Task<ApplicationResult<RhshfProfilingSessionDto>> Handle(
        GetRhshfProfilingSessionQuery request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult<RhshfProfilingSessionDto>.Failure("Case not found.");

        var requirements = await _requirementRepo.GetActiveAsync(ct);
        var attachedByCategory = profile.SupportingDocuments
            .GroupBy(d => d.Category)
            .ToDictionary(g => g.Key, g => g.Count());

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
                .Select(d => new RhshfSupportingDocumentDto(d.Id, d.FileName, d.SizeBytes, d.UploadedAt) { Category = d.Category })
                .ToList(),
            DocumentRequirements: requirements
                .Select(r => new RhshfDocumentRequirementStatusDto(
                    r.Category, r.Title, r.Description, r.IsMandatory, r.SortOrder,
                    IsSatisfied: attachedByCategory.ContainsKey(r.Category),
                    AttachedCount: attachedByCategory.GetValueOrDefault(r.Category)))
                .ToList(),
            FarmPlans: profile.GetTargetCycleFarmPlans()
                .Select(p => new RhshfProfilingFarmPlanDto(
                    p.Id, p.Crop, p.Hectares, p.ExpectedYieldKgPerHectare, p.ExpectedPricePerKg,
                    p.ExpectedOutputKg, p.ExpectedRevenue))
                .ToList());

        return ApplicationResult<RhshfProfilingSessionDto>.Success(dto);
    }
}

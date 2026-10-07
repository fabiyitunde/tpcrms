using CRMS.Application.Common;
using CRMS.Application.Rhshf.DTOs;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Queries;

public record GetRhshfProfilingSessionQuery(string Reference) : IRequest<ApplicationResult<RhshfProfilingSessionDto>>;

public class GetRhshfProfilingSessionHandler : IRequestHandler<GetRhshfProfilingSessionQuery, ApplicationResult<RhshfProfilingSessionDto>>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfDocumentRequirementRepository _requirementRepo;
    private readonly IRhshfOfferRepository _offerRepo;
    private readonly IRhshfGuarantorRepository _guarantorRepo;
    private readonly IRhshfCollateralRepository _collateralRepo;

    public GetRhshfProfilingSessionHandler(
        IRhshfCreditProfileRepository repo, IRhshfDocumentRequirementRepository requirementRepo,
        IRhshfOfferRepository offerRepo, IRhshfGuarantorRepository guarantorRepo,
        IRhshfCollateralRepository collateralRepo)
    {
        _repo = repo;
        _requirementRepo = requirementRepo;
        _offerRepo = offerRepo;
        _guarantorRepo = guarantorRepo;
        _collateralRepo = collateralRepo;
    }

    public async Task<ApplicationResult<RhshfProfilingSessionDto>> Handle(
        GetRhshfProfilingSessionQuery request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult<RhshfProfilingSessionDto>.Failure("Case not found.");

        var requirements = await _requirementRepo.GetActiveAsync(ct);
        var guarantors = await _guarantorRepo.GetByProfileIdAsync(profile.Id, ct);
        var collateral = await _collateralRepo.GetByProfileAndCycleAsync(profile.Id, profile.ProfilingTargetCycleNumber, ct);
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
                .ToList(),
            Directors: profile.Directors
                .OrderByDescending(d => d.IsChairman).ThenBy(d => d.FullName)
                .Select(d => new RhshfProfilingDirectorDto(
                    d.Id, d.FullName, !string.IsNullOrWhiteSpace(d.Bvn), d.ShareholdingPercent, d.IsChairman))
                .ToList(),
            Guarantors: guarantors
                .OrderBy(g => g.FullName)
                .Select(g => new RhshfProfilingGuarantorDto(
                    g.Id, g.FullName, g.GuarantorType, !string.IsNullOrWhiteSpace(g.Bvn), g.RcNumber,
                    g.Relationship, g.GuaranteeAmount))
                .ToList(),
            Collateral: collateral
                .Select(c => new RhshfProfilingCollateralDto(
                    c.Id, c.Type, c.ReferenceNumber, c.GuarantorBankName, c.GuaranteeAmount,
                    c.CrgCoveragePercentage, c.PropertyDescription, c.PropertyValue, c.Notes))
                .ToList(),
            IsAwaitingOfferAcceptance: await IsAwaitingOfferAcceptanceAsync(profile, ct));

        return ApplicationResult<RhshfProfilingSessionDto>.Success(dto);
    }

    /// <summary>Same condition the status endpoint reports as REVIEW_OFFER: an offer exists for this
    /// cycle and is still Generated (neither accepted nor rejected).</summary>
    private async Task<bool> IsAwaitingOfferAcceptanceAsync(RhshfCreditProfile profile, CancellationToken ct)
    {
        if (profile.InternalStage != RhshfInternalStage.AwaitingOfferAcceptance)
            return false;

        var offer = await _offerRepo.GetByProfileAndCycleAsync(profile.Id, profile.CurrentCycleNumber, ct);
        return offer is not null && offer.Status == RhshfOfferStatus.Generated;
    }
}

using CRMS.Application.Common;
using CRMS.Application.Rhshf.DTOs;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using CRMS.Domain.Services;

namespace CRMS.Application.Rhshf.Queries;

public record GetRhshfProfilingSessionQuery(string Reference) : IRequest<ApplicationResult<RhshfProfilingSessionDto>>;

public class GetRhshfProfilingSessionHandler : IRequestHandler<GetRhshfProfilingSessionQuery, ApplicationResult<RhshfProfilingSessionDto>>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfDocumentRequirementRepository _requirementRepo;
    private readonly IRhshfOfferRepository _offerRepo;
    private readonly IRhshfGuarantorRepository _guarantorRepo;
    private readonly IRhshfCollateralRepository _collateralRepo;
    private readonly IRhshfProfilingFieldRequirementRepository? _fieldRequirementRepo;

    public GetRhshfProfilingSessionHandler(
        IRhshfCreditProfileRepository repo, IRhshfDocumentRequirementRepository requirementRepo,
        IRhshfOfferRepository offerRepo, IRhshfGuarantorRepository guarantorRepo,
        IRhshfCollateralRepository collateralRepo,
        IRhshfProfilingFieldRequirementRepository? fieldRequirementRepo = null)
    {
        _fieldRequirementRepo = fieldRequirementRepo;
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
        IReadOnlyList<RhshfProfilingFieldRequirement> fieldRequirements =
            _fieldRequirementRepo is null ? [] : await _fieldRequirementRepo.GetAllAsync(ct);

        // One evaluation serves both the per-slot "satisfied" flags and the list of what still
        // blocks submission, so the page can never disagree with the gate.
        var readiness = RhshfProfilingReadiness.Evaluate(profile, requirements, fieldRequirements, DateTime.UtcNow);
        var personNames = profile.Directors.Select(d => (d.Id, d.FullName))
            .Concat(profile.ManagementStaff.Select(m => (m.Id, m.FullName)))
            .ToDictionary(p => p.Id, p => p.FullName);

        var outstandingReturn = profile.GetOutstandingReturn();
        var dto = new RhshfProfilingSessionDto(
            Reference: profile.Reference,
            Status: profile.Status,
            CurrentStage: profile.CurrentStage,
            FurthestStage: profile.FurthestProfilingStageReached(),
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
                .Select(d => new RhshfSupportingDocumentDto(d.Id, d.FileName, d.SizeBytes, d.UploadedAt)
                {
                    Category = d.Category, ContentType = d.ContentType, ReusedFromReference = d.ReusedFromReference,
                    OriginalUploadedAt = d.OriginalUploadedAt,
                    DirectorId = d.DirectorId, ManagementStaffId = d.ManagementStaffId,
                    PersonName = (d.DirectorId ?? d.ManagementStaffId) is { } pid ? personNames.GetValueOrDefault(pid) : null,
                    VerificationStatus = d.VerificationStatus, RejectionReason = d.RejectionReason, VerifiedAt = d.VerifiedAt,
                })
                .ToList(),
            DocumentRequirements: requirements
                .Select(r => new RhshfDocumentRequirementStatusDto(
                    r.Category, r.Title, r.Description, r.IsMandatory, r.SortOrder,
                    // Per the readiness list: a per-person slot is satisfied only when every person's
                    // file is in, and a slot holding a rejected file is not satisfied at all.
                    IsSatisfied: r.Category == RhshfDocumentCategory.Other
                        ? attachedByCategory.ContainsKey(r.Category)
                        : readiness.Where(i => i.IsDocument && i.Category == r.Category)
                            .All(i => i.Status is not (RhshfReadinessStatus.Missing or RhshfReadinessStatus.Rejected)),
                    AttachedCount: attachedByCategory.GetValueOrDefault(r.Category),
                    AllowedFormats: r.AllowedFormatList,
                    MustMatchFinancialTemplate: r.MustMatchFinancialTemplate,
                    AppliesTo: r.AppliesTo))
                .ToList(),
            FarmPlans: profile.GetTargetCycleFarmPlans()
                .Select(p => new RhshfProfilingFarmPlanDto(
                    p.Id, p.Crop, p.Hectares, p.ExpectedYieldKgPerHectare, p.ExpectedPricePerKg,
                    p.ExpectedOutputKg, p.ExpectedRevenue))
                .ToList(),
            Directors: profile.Directors
                .OrderByDescending(d => d.IsChairman).ThenBy(d => d.FullName)
                .Select(d => new RhshfProfilingDirectorDto(
                    d.Id, d.FullName, !string.IsNullOrWhiteSpace(d.Bvn), d.ShareholdingPercent, d.IsChairman,
                    HasNin: !string.IsNullOrWhiteSpace(d.Nin), PhoneNumber: d.PhoneNumber, Email: d.Email,
                    MeansOfIdType: d.MeansOfIdType,
                    MeansOfIdNumber: string.IsNullOrWhiteSpace(d.MeansOfIdNumber) ? null : SensitiveDataMasker.MaskAccountNumber(d.MeansOfIdNumber),
                    MeansOfIdExpiry: d.MeansOfIdExpiry,
                    IsChiefPromoter: d.IsChiefPromoter))
                .ToList(),
            Guarantors: guarantors
                .OrderBy(g => g.FullName)
                .Select(g => new RhshfProfilingGuarantorDto(
                    g.Id, g.FullName, g.GuarantorType, !string.IsNullOrWhiteSpace(g.Bvn), g.RcNumber,
                    g.RelationshipDisplay, g.GuaranteeAmount, g.RelationshipCategory, g.RelationshipOther))
                .ToList(),
            Collateral: collateral
                .Select(c => new RhshfProfilingCollateralDto(
                    c.Id, c.Type, c.ReferenceNumber, c.GuarantorBankName, c.GuaranteeAmount,
                    c.CrgCoveragePercentage, c.PropertyDescription, c.PropertyValue, c.Notes,
                    c.Documents.Select(d => new RhshfProfilingCollateralDocumentDto(d.Id, d.FileName, d.SizeBytes, d.ContentType)).ToList()))
                .ToList(),
            IsAwaitingOfferAcceptance: await IsAwaitingOfferAcceptanceAsync(profile, ct),
            DeclaredShareholdingTotal: profile.DeclaredShareholdingTotal,
            RemainingShareholding: profile.RemainingShareholding(),
            ReturnReason: outstandingReturn?.Note,
            ReturnedAt: outstandingReturn?.ChangedAt,
            IsReturned: outstandingReturn is not null,
            ManagementStaff: profile.ManagementStaff
                .OrderBy(m => m.FullName)
                .Select(m => new RhshfProfilingManagementStaffDto(m.Id, m.FullName, m.Position, m.PhoneNumber, m.Email, m.IsChiefPromoter))
                .ToList(),
            FieldMandatory: fieldRequirements.ToDictionary(f => f.Key.ToString(), f => f.IsMandatory),
            SubmissionGaps: readiness.Where(i => i.BlocksSubmission)
                .Select(i => i.Status == RhshfReadinessStatus.Rejected ? $"{i.Label} (rejected: replace it)"
                    : i.Note is not null && i.Status == RhshfReadinessStatus.Invalid ? $"{i.Label} ({i.Note})"
                    : i.Label)
                .ToList());

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

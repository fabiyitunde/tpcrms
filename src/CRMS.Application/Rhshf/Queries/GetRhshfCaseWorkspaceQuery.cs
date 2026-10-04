using CRMS.Application.Common;
using CRMS.Application.Rhshf.DTOs;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Queries;

public record GetRhshfCaseWorkspaceQuery(string Reference) : IRequest<ApplicationResult<RhshfCaseWorkspaceDto>>;

public class GetRhshfCaseWorkspaceHandler : IRequestHandler<GetRhshfCaseWorkspaceQuery, ApplicationResult<RhshfCaseWorkspaceDto>>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IUserNameResolver _names;
    private readonly IRhshfRoutingConfigRepository _routing;

    public GetRhshfCaseWorkspaceHandler(
        IRhshfCreditProfileRepository repo, IUserNameResolver names, IRhshfRoutingConfigRepository routing)
    {
        _repo = repo;
        _names = names;
        _routing = routing;
    }

    /// <summary>Friendly label for a committee tier — the same mapping the Committee tab will use so
    /// the two never disagree.</summary>
    private static string TierLabel(CommitteeType tier) => tier switch
    {
        CommitteeType.BranchCredit => "Branch",
        CommitteeType.ZonalCredit => "Zonal",
        CommitteeType.RegionalCredit => "Regional",
        CommitteeType.HeadOfficeCredit => "Head Office",
        CommitteeType.ManagementCredit => "Management",
        CommitteeType.BoardCredit => "Board",
        _ => tier.ToString(),
    };

    public async Task<ApplicationResult<RhshfCaseWorkspaceDto>> Handle(GetRhshfCaseWorkspaceQuery request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult<RhshfCaseWorkspaceDto>.Failure("Case not found.");

        // One resolve pass across every actor on the case — appraisers, risk officers, ratifiers.
        var actorNames = await _names.ResolveManyAsync(
            profile.Appraisals.Select(a => a.CreditOfficerId)
                .Concat(profile.RiskReviews.Select(r => r.RiskOfficerId))
                .Concat(profile.Ratifications.Select(r => r.FinalApproverId)), ct);
        string Name(Guid id) => actorNames.TryGetValue(id, out var n) ? n : "—";

        // Resolve the committee tier from the EOP value up front — this is the same band table the
        // committee review is sized against later, so showing it now matches where the case will land.
        var routing = await _routing.ResolveAsync(profile.TotalEopValue, ct);
        string? tierLabel = routing is null ? null : TierLabel(routing.Tier);
        string? tierBand = routing is null ? null
            : $"{profile.Currency} {routing.MinEopValue:N0} – {(routing.MaxEopValue >= 999_999_999_999m ? "above" : $"{profile.Currency} {routing.MaxEopValue:N0}")}";

        var dto = new RhshfCaseWorkspaceDto(
            Reference: profile.Reference,
            SubmissionId: profile.SubmissionId,
            Status: profile.Status,
            InternalStage: profile.InternalStage,
            CurrentProfilingStage: profile.CurrentStage,
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
            BureauRawJson: profile.BureauRawJson,
            SupportingDocuments: profile.SupportingDocuments
                .Select(d => new RhshfSupportingDocumentDto(d.Id, d.FileName, d.SizeBytes, d.UploadedAt) { Category = d.Category }).ToList(),
            Appraisals: profile.Appraisals
                .Select(a => new RhshfAppraisalDto(a.CycleNumber, a.CreditOfficerId, Name(a.CreditOfficerId), a.AppraisedAt, a.Outcome, a.Notes)).ToList(),
            RiskReviews: profile.RiskReviews
                .Select(r => new RhshfRiskReviewDto(r.CycleNumber, r.RiskOfficerId, Name(r.RiskOfficerId), r.ReviewedAt, r.Outcome, r.Notes)).ToList(),
            Ratifications: profile.Ratifications
                .Select(r => new RhshfRatificationDto(r.CycleNumber, r.FinalApproverId, Name(r.FinalApproverId), r.RatifiedAt, r.Outcome, r.ApprovedAmount, r.Notes)).ToList(),
            DecisionOutcome: profile.DecisionOutcome,
            ApprovedAmount: profile.ApprovedAmount,
            DecidedAt: profile.DecidedAt,
            DecidedBy: profile.DecidedBy,
            DecisionNotes: profile.DecisionNotes,
            BranchResolutionNote: profile.BranchResolutionNote,
            ReceivedAt: profile.ReceivedAt,
            UpdatedAt: profile.UpdatedAt,
            CommitteeTier: tierLabel,
            CommitteeTierBand: tierBand);

        return ApplicationResult<RhshfCaseWorkspaceDto>.Success(dto);
    }
}

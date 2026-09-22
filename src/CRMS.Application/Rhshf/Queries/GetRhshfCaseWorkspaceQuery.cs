using CRMS.Application.Common;
using CRMS.Application.Rhshf.DTOs;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Queries;

public record GetRhshfCaseWorkspaceQuery(string Reference) : IRequest<ApplicationResult<RhshfCaseWorkspaceDto>>;

public class GetRhshfCaseWorkspaceHandler : IRequestHandler<GetRhshfCaseWorkspaceQuery, ApplicationResult<RhshfCaseWorkspaceDto>>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IUserNameResolver _names;

    public GetRhshfCaseWorkspaceHandler(IRhshfCreditProfileRepository repo, IUserNameResolver names)
    {
        _repo = repo;
        _names = names;
    }

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
                .Select(d => new RhshfSupportingDocumentDto(d.Id, d.FileName, d.SizeBytes, d.UploadedAt)).ToList(),
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
            UpdatedAt: profile.UpdatedAt);

        return ApplicationResult<RhshfCaseWorkspaceDto>.Success(dto);
    }
}

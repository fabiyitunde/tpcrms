using CRMS.Application.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Queries;

/// <summary>
/// Previews which committee a case will go to if the Risk Officer clears it — the tier resolved from
/// the EOP value and that tier's standing committee roster/quorum — WITHOUT creating anything. Shown
/// in the risk-clearance modal so the officer sees where they are sending the case at the moment of
/// clearing. The actual review is created on clearance by ReviewRhshfRiskCommand using the same
/// resolution, so this preview and the resulting Committee tab match.
/// </summary>
public record GetRhshfCommitteeRoutingPreviewQuery(string Reference)
    : IRequest<ApplicationResult<RhshfCommitteeRoutingPreviewDto>>;

public record RhshfCommitteeRoutingPreviewDto(
    decimal EopValue, string Currency,
    CommitteeType? Tier, int? RequiredVotes, int? MinimumApprovalVotes,
    List<RhshfCommitteePreviewMemberDto> Members,
    /// <summary>Set when routing or the standing committee is not configured — the clearance will then
    /// fail with the same reason, so surface it up front.</summary>
    string? Warning);

public record RhshfCommitteePreviewMemberDto(string UserName, string Role, bool IsChairperson);

public class GetRhshfCommitteeRoutingPreviewHandler
    : IRequestHandler<GetRhshfCommitteeRoutingPreviewQuery, ApplicationResult<RhshfCommitteeRoutingPreviewDto>>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfRoutingConfigRepository _routingRepo;
    private readonly IStandingCommitteeRepository _standingRepo;

    public GetRhshfCommitteeRoutingPreviewHandler(
        IRhshfCreditProfileRepository repo, IRhshfRoutingConfigRepository routingRepo,
        IStandingCommitteeRepository standingRepo)
    {
        _repo = repo;
        _routingRepo = routingRepo;
        _standingRepo = standingRepo;
    }

    public async Task<ApplicationResult<RhshfCommitteeRoutingPreviewDto>> Handle(
        GetRhshfCommitteeRoutingPreviewQuery request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult<RhshfCommitteeRoutingPreviewDto>.Failure("Case not found.");

        var eop = profile.TotalEopValue;

        var routing = await _routingRepo.ResolveAsync(eop, ct);
        if (routing is null)
            return ApplicationResult<RhshfCommitteeRoutingPreviewDto>.Success(new RhshfCommitteeRoutingPreviewDto(
                eop, profile.Currency, null, null, null, [],
                "No routing rule matches this EOP value, so clearance will fail. Configure it under Admin > RH-SHF Routing Config."));

        var standing = await _standingRepo.GetByCommitteeTypeAndLocationAsync(routing.Tier, profile.ResolvedBranchId, ct);
        if (standing is null)
            return ApplicationResult<RhshfCommitteeRoutingPreviewDto>.Success(new RhshfCommitteeRoutingPreviewDto(
                eop, profile.Currency, routing.Tier, null, null, [],
                $"No standing committee is configured for the {routing.Tier} tier, so clearance will fail. Configure it under Admin > Committees."));

        var members = standing.Members
            .OrderByDescending(m => m.IsChairperson).ThenBy(m => m.UserName)
            .Select(m => new RhshfCommitteePreviewMemberDto(m.UserName, m.Role, m.IsChairperson))
            .ToList();

        return ApplicationResult<RhshfCommitteeRoutingPreviewDto>.Success(new RhshfCommitteeRoutingPreviewDto(
            eop, profile.Currency, routing.Tier, standing.RequiredVotes, standing.MinimumApprovalVotes, members, null));
    }
}

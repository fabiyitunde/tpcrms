using CRMS.Application.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Queries;

public record GetRhshfCommitteeReviewQuery(string Reference) : IRequest<ApplicationResult<RhshfCommitteeReviewDto>>;

public record RhshfCommitteeVoteDto(Guid UserId, string UserName, RhshfCommitteeVoteChoice Vote, DateTime VotedAt, string? Comment);

public record RhshfCommitteeReviewDto(
    int CycleNumber,
    int RequiredVotes,
    int MinimumApprovalVotes,
    CommitteeType Tier,
    Guid? BranchId,
    RhshfCommitteeDecision? FinalDecision,
    List<RhshfCommitteeVoteDto> Votes);

public class GetRhshfCommitteeReviewHandler : IRequestHandler<GetRhshfCommitteeReviewQuery, ApplicationResult<RhshfCommitteeReviewDto>>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfCommitteeReviewRepository _committeeRepo;
    private readonly IUserNameResolver _names;

    public GetRhshfCommitteeReviewHandler(
        IRhshfCreditProfileRepository repo, IRhshfCommitteeReviewRepository committeeRepo, IUserNameResolver names)
    {
        _repo = repo;
        _committeeRepo = committeeRepo;
        _names = names;
    }

    public async Task<ApplicationResult<RhshfCommitteeReviewDto>> Handle(GetRhshfCommitteeReviewQuery request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult<RhshfCommitteeReviewDto>.Failure("Case not found.");

        var review = await _committeeRepo.GetByProfileAndCycleAsync(profile.Id, profile.CurrentCycleNumber, ct);
        if (review is null)
            return ApplicationResult<RhshfCommitteeReviewDto>.Failure("No committee review found for this case's current cycle.");

        var names = await _names.ResolveManyAsync(review.Votes.Select(v => v.UserId), ct);
        var dto = new RhshfCommitteeReviewDto(
            review.CycleNumber, review.RequiredVotes, review.MinimumApprovalVotes, review.Tier, review.BranchId, review.FinalDecision,
            review.Votes.Select(v => new RhshfCommitteeVoteDto(
                v.UserId, names.TryGetValue(v.UserId, out var n) ? n : "—", v.Vote, v.VotedAt, v.Comment)).ToList());

        return ApplicationResult<RhshfCommitteeReviewDto>.Success(dto);
    }
}

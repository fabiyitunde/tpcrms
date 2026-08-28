using CRMS.Application.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Queries;

public record GetRhshfCommitteeReviewQuery(string Reference) : IRequest<ApplicationResult<RhshfCommitteeReviewDto>>;

public record RhshfCommitteeVoteDto(Guid UserId, RhshfCommitteeVoteChoice Vote, DateTime VotedAt, string? Comment);

public record RhshfCommitteeReviewDto(
    int CycleNumber,
    int RequiredVotes,
    int MinimumApprovalVotes,
    RhshfCommitteeDecision? FinalDecision,
    List<RhshfCommitteeVoteDto> Votes);

public class GetRhshfCommitteeReviewHandler : IRequestHandler<GetRhshfCommitteeReviewQuery, ApplicationResult<RhshfCommitteeReviewDto>>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfCommitteeReviewRepository _committeeRepo;

    public GetRhshfCommitteeReviewHandler(IRhshfCreditProfileRepository repo, IRhshfCommitteeReviewRepository committeeRepo)
    {
        _repo = repo;
        _committeeRepo = committeeRepo;
    }

    public async Task<ApplicationResult<RhshfCommitteeReviewDto>> Handle(GetRhshfCommitteeReviewQuery request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult<RhshfCommitteeReviewDto>.Failure("Case not found.");

        var review = await _committeeRepo.GetByProfileAndCycleAsync(profile.Id, profile.CurrentCycleNumber, ct);
        if (review is null)
            return ApplicationResult<RhshfCommitteeReviewDto>.Failure("No committee review found for this case's current cycle.");

        var dto = new RhshfCommitteeReviewDto(
            review.CycleNumber, review.RequiredVotes, review.MinimumApprovalVotes, review.FinalDecision,
            review.Votes.Select(v => new RhshfCommitteeVoteDto(v.UserId, v.Vote, v.VotedAt, v.Comment)).ToList());

        return ApplicationResult<RhshfCommitteeReviewDto>.Success(dto);
    }
}

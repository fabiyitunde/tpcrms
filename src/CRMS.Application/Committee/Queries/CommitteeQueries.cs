using CRMS.Application.Common;
using CRMS.Application.Committee.Commands;
using CRMS.Application.Committee.DTOs;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Committee.Queries;

// Get review by ID
public record GetCommitteeReviewByIdQuery(Guid Id) : IRequest<ApplicationResult<CommitteeReviewDto>>;

public class GetCommitteeReviewByIdHandler : IRequestHandler<GetCommitteeReviewByIdQuery, ApplicationResult<CommitteeReviewDto>>
{
    private readonly ICommitteeReviewRepository _repository;

    public GetCommitteeReviewByIdHandler(ICommitteeReviewRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<CommitteeReviewDto>> Handle(GetCommitteeReviewByIdQuery request, CancellationToken ct = default)
    {
        var review = await _repository.GetByIdAsync(request.Id, ct);
        if (review == null)
            return ApplicationResult<CommitteeReviewDto>.Failure("Committee review not found");

        return ApplicationResult<CommitteeReviewDto>.Success(CommitteeMapper.ToDto(review));
    }
}

// Get review by loan application
public record GetCommitteeReviewByLoanApplicationQuery(Guid LoanApplicationId) : IRequest<ApplicationResult<CommitteeReviewDto>>;

public class GetCommitteeReviewByLoanApplicationHandler : IRequestHandler<GetCommitteeReviewByLoanApplicationQuery, ApplicationResult<CommitteeReviewDto>>
{
    private readonly ICommitteeReviewRepository _repository;

    public GetCommitteeReviewByLoanApplicationHandler(ICommitteeReviewRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<CommitteeReviewDto>> Handle(GetCommitteeReviewByLoanApplicationQuery request, CancellationToken ct = default)
    {
        var review = await _repository.GetByLoanApplicationIdAsync(request.LoanApplicationId, ct);
        if (review == null)
            return ApplicationResult<CommitteeReviewDto>.Failure("No committee review found for this loan application");

        return ApplicationResult<CommitteeReviewDto>.Success(CommitteeMapper.ToDto(review));
    }
}

// Get user's pending votes
public record GetMyPendingVotesQuery(Guid UserId) : IRequest<ApplicationResult<List<CommitteeReviewSummaryDto>>>;

public class GetMyPendingVotesHandler : IRequestHandler<GetMyPendingVotesQuery, ApplicationResult<List<CommitteeReviewSummaryDto>>>
{
    private readonly ICommitteeReviewRepository _repository;

    public GetMyPendingVotesHandler(ICommitteeReviewRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<List<CommitteeReviewSummaryDto>>> Handle(GetMyPendingVotesQuery request, CancellationToken ct = default)
    {
        var reviews = await _repository.GetPendingVotesByUserIdAsync(request.UserId, ct);
        
        var summaries = reviews.Select(r => new CommitteeReviewSummaryDto(
            r.Id,
            r.LoanApplicationId,
            r.ApplicationNumber,
            r.CommitteeType.ToString(),
            r.Status.ToString(),
            r.DeadlineAt,
            r.ApprovalVotes,
            r.RejectionVotes,
            r.PendingVotes,
            r.IsOverdue,
            false // User hasn't voted
        )).ToList();

        return ApplicationResult<List<CommitteeReviewSummaryDto>>.Success(summaries);
    }
}

// Get user's committee reviews
public record GetMyCommitteeReviewsQuery(Guid UserId) : IRequest<ApplicationResult<List<CommitteeReviewSummaryDto>>>;

public class GetMyCommitteeReviewsHandler : IRequestHandler<GetMyCommitteeReviewsQuery, ApplicationResult<List<CommitteeReviewSummaryDto>>>
{
    private readonly ICommitteeReviewRepository _repository;

    public GetMyCommitteeReviewsHandler(ICommitteeReviewRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<List<CommitteeReviewSummaryDto>>> Handle(GetMyCommitteeReviewsQuery request, CancellationToken ct = default)
    {
        var reviews = await _repository.GetByMemberUserIdAsync(request.UserId, ct);
        
        var summaries = reviews.Select(r => {
            var member = r.Members.FirstOrDefault(m => m.UserId == request.UserId);
            return new CommitteeReviewSummaryDto(
                r.Id,
                r.LoanApplicationId,
                r.ApplicationNumber,
                r.CommitteeType.ToString(),
                r.Status.ToString(),
                r.DeadlineAt,
                r.ApprovalVotes,
                r.RejectionVotes,
                r.PendingVotes,
                r.IsOverdue,
                member?.HasVoted ?? false
            );
        }).ToList();

        return ApplicationResult<List<CommitteeReviewSummaryDto>>.Success(summaries);
    }
}

// Get reviews by status
public record GetCommitteeReviewsByStatusQuery(CommitteeReviewStatus Status) : IRequest<ApplicationResult<List<CommitteeReviewSummaryDto>>>;

public class GetCommitteeReviewsByStatusHandler : IRequestHandler<GetCommitteeReviewsByStatusQuery, ApplicationResult<List<CommitteeReviewSummaryDto>>>
{
    private readonly ICommitteeReviewRepository _repository;

    public GetCommitteeReviewsByStatusHandler(ICommitteeReviewRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<List<CommitteeReviewSummaryDto>>> Handle(GetCommitteeReviewsByStatusQuery request, CancellationToken ct = default)
    {
        var reviews = await _repository.GetByStatusAsync(request.Status, ct);
        
        var summaries = reviews.Select(r => new CommitteeReviewSummaryDto(
            r.Id,
            r.LoanApplicationId,
            r.ApplicationNumber,
            r.CommitteeType.ToString(),
            r.Status.ToString(),
            r.DeadlineAt,
            r.ApprovalVotes,
            r.RejectionVotes,
            r.PendingVotes,
            r.IsOverdue,
            false
        )).ToList();

        return ApplicationResult<List<CommitteeReviewSummaryDto>>.Success(summaries);
    }
}

// Get overdue reviews
public record GetOverdueCommitteeReviewsQuery : IRequest<ApplicationResult<List<CommitteeReviewSummaryDto>>>;

public class GetOverdueCommitteeReviewsHandler : IRequestHandler<GetOverdueCommitteeReviewsQuery, ApplicationResult<List<CommitteeReviewSummaryDto>>>
{
    private readonly ICommitteeReviewRepository _repository;

    public GetOverdueCommitteeReviewsHandler(ICommitteeReviewRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<List<CommitteeReviewSummaryDto>>> Handle(GetOverdueCommitteeReviewsQuery request, CancellationToken ct = default)
    {
        var reviews = await _repository.GetOverdueAsync(ct);
        
        var summaries = reviews.Select(r => new CommitteeReviewSummaryDto(
            r.Id,
            r.LoanApplicationId,
            r.ApplicationNumber,
            r.CommitteeType.ToString(),
            r.Status.ToString(),
            r.DeadlineAt,
            r.ApprovalVotes,
            r.RejectionVotes,
            r.PendingVotes,
            true,
            false
        )).ToList();

        return ApplicationResult<List<CommitteeReviewSummaryDto>>.Success(summaries);
    }
}

// Get voting summary
public record GetVotingSummaryQuery(Guid CommitteeReviewId) : IRequest<ApplicationResult<VotingSummaryDto>>;

public class GetVotingSummaryHandler : IRequestHandler<GetVotingSummaryQuery, ApplicationResult<VotingSummaryDto>>
{
    private readonly ICommitteeReviewRepository _repository;

    public GetVotingSummaryHandler(ICommitteeReviewRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<VotingSummaryDto>> Handle(GetVotingSummaryQuery request, CancellationToken ct = default)
    {
        var review = await _repository.GetByIdAsync(request.CommitteeReviewId, ct);
        if (review == null)
            return ApplicationResult<VotingSummaryDto>.Failure("Committee review not found");

        var summary = new VotingSummaryDto(
            review.Members.Count,
            review.Members.Count(m => m.HasVoted),
            review.ApprovalVotes,
            review.RejectionVotes,
            review.AbstainVotes,
            review.PendingVotes,
            review.HasQuorum,
            review.HasMajorityApproval,
            review.RequiredVotes,
            review.MinimumApprovalVotes
        );

        return ApplicationResult<VotingSummaryDto>.Success(summary);
    }
}

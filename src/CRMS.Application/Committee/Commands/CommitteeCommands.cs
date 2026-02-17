using CRMS.Application.Common;
using CRMS.Application.Committee.DTOs;
using CRMS.Domain.Aggregates.Committee;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Committee.Commands;

// Create committee review
public record CreateCommitteeReviewCommand(
    Guid LoanApplicationId,
    string ApplicationNumber,
    CommitteeType CommitteeType,
    Guid CirculatedByUserId,
    int RequiredVotes,
    int MinimumApprovalVotes,
    int DeadlineHours = 72
) : IRequest<ApplicationResult<CommitteeReviewDto>>;

public class CreateCommitteeReviewHandler : IRequestHandler<CreateCommitteeReviewCommand, ApplicationResult<CommitteeReviewDto>>
{
    private readonly ICommitteeReviewRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCommitteeReviewHandler(ICommitteeReviewRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<CommitteeReviewDto>> Handle(CreateCommitteeReviewCommand request, CancellationToken ct = default)
    {
        // Check for existing active review
        var existing = await _repository.GetActiveByLoanApplicationIdAsync(request.LoanApplicationId, ct);
        if (existing != null)
            return ApplicationResult<CommitteeReviewDto>.Failure("An active committee review already exists for this loan application");

        var result = CommitteeReview.Create(
            request.LoanApplicationId,
            request.ApplicationNumber,
            request.CommitteeType,
            request.CirculatedByUserId,
            request.RequiredVotes,
            request.MinimumApprovalVotes,
            request.DeadlineHours);

        if (result.IsFailure)
            return ApplicationResult<CommitteeReviewDto>.Failure(result.Error);

        await _repository.AddAsync(result.Value, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<CommitteeReviewDto>.Success(CommitteeMapper.ToDto(result.Value));
    }
}

// Add committee member
public record AddCommitteeMemberCommand(
    Guid CommitteeReviewId,
    Guid UserId,
    string UserName,
    string Role,
    bool IsChairperson
) : IRequest<ApplicationResult<CommitteeReviewDto>>;

public class AddCommitteeMemberHandler : IRequestHandler<AddCommitteeMemberCommand, ApplicationResult<CommitteeReviewDto>>
{
    private readonly ICommitteeReviewRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public AddCommitteeMemberHandler(ICommitteeReviewRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<CommitteeReviewDto>> Handle(AddCommitteeMemberCommand request, CancellationToken ct = default)
    {
        var review = await _repository.GetByIdAsync(request.CommitteeReviewId, ct);
        if (review == null)
            return ApplicationResult<CommitteeReviewDto>.Failure("Committee review not found");

        var result = review.AddMember(request.UserId, request.UserName, request.Role, request.IsChairperson);
        if (result.IsFailure)
            return ApplicationResult<CommitteeReviewDto>.Failure(result.Error);

        _repository.Update(review);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<CommitteeReviewDto>.Success(CommitteeMapper.ToDto(review));
    }
}

// Start voting
public record StartVotingCommand(Guid CommitteeReviewId) : IRequest<ApplicationResult<CommitteeReviewDto>>;

public class StartVotingHandler : IRequestHandler<StartVotingCommand, ApplicationResult<CommitteeReviewDto>>
{
    private readonly ICommitteeReviewRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public StartVotingHandler(ICommitteeReviewRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<CommitteeReviewDto>> Handle(StartVotingCommand request, CancellationToken ct = default)
    {
        var review = await _repository.GetByIdAsync(request.CommitteeReviewId, ct);
        if (review == null)
            return ApplicationResult<CommitteeReviewDto>.Failure("Committee review not found");

        var result = review.StartVoting();
        if (result.IsFailure)
            return ApplicationResult<CommitteeReviewDto>.Failure(result.Error);

        _repository.Update(review);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<CommitteeReviewDto>.Success(CommitteeMapper.ToDto(review));
    }
}

// Cast vote
public record CastVoteCommand(
    Guid CommitteeReviewId,
    Guid UserId,
    CommitteeVote Vote,
    string? Comment
) : IRequest<ApplicationResult<CommitteeReviewDto>>;

public class CastVoteHandler : IRequestHandler<CastVoteCommand, ApplicationResult<CommitteeReviewDto>>
{
    private readonly ICommitteeReviewRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CastVoteHandler(ICommitteeReviewRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<CommitteeReviewDto>> Handle(CastVoteCommand request, CancellationToken ct = default)
    {
        var review = await _repository.GetByIdAsync(request.CommitteeReviewId, ct);
        if (review == null)
            return ApplicationResult<CommitteeReviewDto>.Failure("Committee review not found");

        var result = review.CastVote(request.UserId, request.Vote, request.Comment);
        if (result.IsFailure)
            return ApplicationResult<CommitteeReviewDto>.Failure(result.Error);

        _repository.Update(review);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<CommitteeReviewDto>.Success(CommitteeMapper.ToDto(review));
    }
}

// Add comment
public record AddCommitteeCommentCommand(
    Guid CommitteeReviewId,
    Guid UserId,
    string Content,
    CommentVisibility Visibility
) : IRequest<ApplicationResult<CommitteeReviewDto>>;

public class AddCommitteeCommentHandler : IRequestHandler<AddCommitteeCommentCommand, ApplicationResult<CommitteeReviewDto>>
{
    private readonly ICommitteeReviewRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public AddCommitteeCommentHandler(ICommitteeReviewRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<CommitteeReviewDto>> Handle(AddCommitteeCommentCommand request, CancellationToken ct = default)
    {
        var review = await _repository.GetByIdAsync(request.CommitteeReviewId, ct);
        if (review == null)
            return ApplicationResult<CommitteeReviewDto>.Failure("Committee review not found");

        var result = review.AddComment(request.UserId, request.Content, request.Visibility);
        if (result.IsFailure)
            return ApplicationResult<CommitteeReviewDto>.Failure(result.Error);

        _repository.Update(review);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<CommitteeReviewDto>.Success(CommitteeMapper.ToDto(review));
    }
}

// Record decision
public record RecordCommitteeDecisionCommand(
    Guid CommitteeReviewId,
    Guid DecidedByUserId,
    CommitteeDecision Decision,
    string Rationale,
    decimal? ApprovedAmount,
    int? ApprovedTenorMonths,
    decimal? ApprovedInterestRate,
    string? Conditions
) : IRequest<ApplicationResult<CommitteeReviewDto>>;

public class RecordCommitteeDecisionHandler : IRequestHandler<RecordCommitteeDecisionCommand, ApplicationResult<CommitteeReviewDto>>
{
    private readonly ICommitteeReviewRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public RecordCommitteeDecisionHandler(ICommitteeReviewRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<CommitteeReviewDto>> Handle(RecordCommitteeDecisionCommand request, CancellationToken ct = default)
    {
        var review = await _repository.GetByIdAsync(request.CommitteeReviewId, ct);
        if (review == null)
            return ApplicationResult<CommitteeReviewDto>.Failure("Committee review not found");

        var result = review.RecordDecision(
            request.DecidedByUserId,
            request.Decision,
            request.Rationale,
            request.ApprovedAmount,
            request.ApprovedTenorMonths,
            request.ApprovedInterestRate,
            request.Conditions);

        if (result.IsFailure)
            return ApplicationResult<CommitteeReviewDto>.Failure(result.Error);

        _repository.Update(review);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<CommitteeReviewDto>.Success(CommitteeMapper.ToDto(review));
    }
}

// Close review
public record CloseCommitteeReviewCommand(
    Guid CommitteeReviewId,
    Guid ClosedByUserId
) : IRequest<ApplicationResult<CommitteeReviewDto>>;

public class CloseCommitteeReviewHandler : IRequestHandler<CloseCommitteeReviewCommand, ApplicationResult<CommitteeReviewDto>>
{
    private readonly ICommitteeReviewRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CloseCommitteeReviewHandler(ICommitteeReviewRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<CommitteeReviewDto>> Handle(CloseCommitteeReviewCommand request, CancellationToken ct = default)
    {
        var review = await _repository.GetByIdAsync(request.CommitteeReviewId, ct);
        if (review == null)
            return ApplicationResult<CommitteeReviewDto>.Failure("Committee review not found");

        var result = review.Close(request.ClosedByUserId);
        if (result.IsFailure)
            return ApplicationResult<CommitteeReviewDto>.Failure(result.Error);

        _repository.Update(review);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<CommitteeReviewDto>.Success(CommitteeMapper.ToDto(review));
    }
}

// Mapper
internal static class CommitteeMapper
{
    public static CommitteeReviewDto ToDto(CommitteeReview review) => new(
        review.Id,
        review.LoanApplicationId,
        review.ApplicationNumber,
        review.CommitteeType.ToString(),
        review.Status.ToString(),
        review.CirculatedAt,
        review.CirculatedByUserId,
        review.DeadlineAt,
        review.RequiredVotes,
        review.MinimumApprovalVotes,
        review.FinalDecision?.ToString(),
        review.DecisionAt,
        review.DecisionByUserId,
        review.DecisionRationale,
        review.ApprovedAmount,
        review.ApprovedTenorMonths,
        review.ApprovedInterestRate,
        review.ApprovalConditions,
        review.ApprovalVotes,
        review.RejectionVotes,
        review.AbstainVotes,
        review.PendingVotes,
        review.HasQuorum,
        review.HasMajorityApproval,
        review.IsOverdue,
        review.Members.Select(ToMemberDto).ToList(),
        review.Comments.Take(10).Select(c => ToCommentDto(c, "")).ToList()
    );

    public static CommitteeMemberDto ToMemberDto(CommitteeMember member) => new(
        member.Id,
        member.UserId,
        member.UserName,
        member.Role,
        member.IsChairperson,
        member.Vote?.ToString(),
        member.VotedAt,
        member.VoteComment,
        member.AssignedAt,
        member.HasVoted
    );

    public static CommitteeCommentDto ToCommentDto(CommitteeComment comment, string userName) => new(
        comment.Id,
        comment.UserId,
        userName,
        comment.Content,
        comment.Visibility.ToString(),
        comment.CreatedAt,
        comment.IsEdited
    );
}

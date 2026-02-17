using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Committee;

/// <summary>
/// Represents a committee review session for a loan application.
/// Tracks committee members, their votes, comments, and final decision.
/// </summary>
public class CommitteeReview : AggregateRoot
{
    public Guid LoanApplicationId { get; private set; }
    public string ApplicationNumber { get; private set; } = string.Empty;
    public CommitteeType CommitteeType { get; private set; }
    public CommitteeReviewStatus Status { get; private set; }
    
    // Review Details
    public DateTime CirculatedAt { get; private set; }
    public Guid CirculatedByUserId { get; private set; }
    public DateTime? DeadlineAt { get; private set; }
    public int RequiredVotes { get; private set; }
    public int MinimumApprovalVotes { get; private set; }
    
    // Decision
    public CommitteeDecision? FinalDecision { get; private set; }
    public DateTime? DecisionAt { get; private set; }
    public Guid? DecisionByUserId { get; private set; }
    public string? DecisionRationale { get; private set; }
    
    // Approved Terms (if approved)
    public decimal? ApprovedAmount { get; private set; }
    public int? ApprovedTenorMonths { get; private set; }
    public decimal? ApprovedInterestRate { get; private set; }
    public string? ApprovalConditions { get; private set; }

    private readonly List<CommitteeMember> _members = [];
    private readonly List<CommitteeComment> _comments = [];
    private readonly List<CommitteeDocument> _documents = [];

    public IReadOnlyCollection<CommitteeMember> Members => _members.AsReadOnly();
    public IReadOnlyCollection<CommitteeComment> Comments => _comments.AsReadOnly();
    public IReadOnlyCollection<CommitteeDocument> Documents => _documents.AsReadOnly();

    private CommitteeReview() { }

    public static Result<CommitteeReview> Create(
        Guid loanApplicationId,
        string applicationNumber,
        CommitteeType committeeType,
        Guid circulatedByUserId,
        int requiredVotes,
        int minimumApprovalVotes,
        int deadlineHours = 72)
    {
        if (requiredVotes <= 0)
            return Result.Failure<CommitteeReview>("Required votes must be greater than zero");

        if (minimumApprovalVotes <= 0 || minimumApprovalVotes > requiredVotes)
            return Result.Failure<CommitteeReview>("Minimum approval votes must be between 1 and required votes");

        var review = new CommitteeReview
        {
            LoanApplicationId = loanApplicationId,
            ApplicationNumber = applicationNumber,
            CommitteeType = committeeType,
            Status = CommitteeReviewStatus.Pending,
            CirculatedAt = DateTime.UtcNow,
            CirculatedByUserId = circulatedByUserId,
            DeadlineAt = DateTime.UtcNow.AddHours(deadlineHours),
            RequiredVotes = requiredVotes,
            MinimumApprovalVotes = minimumApprovalVotes
        };

        review.AddDomainEvent(new CommitteeReviewCreatedEvent(
            review.Id, loanApplicationId, applicationNumber, committeeType));

        return Result.Success(review);
    }

    public Result AddMember(Guid userId, string userName, string role, bool isChairperson = false)
    {
        if (Status != CommitteeReviewStatus.Pending)
            return Result.Failure("Cannot add members after voting has started");

        if (_members.Any(m => m.UserId == userId))
            return Result.Failure("Member already added to committee");

        var member = CommitteeMember.Create(Id, userId, userName, role, isChairperson);
        if (member.IsFailure)
            return Result.Failure(member.Error);

        _members.Add(member.Value);
        
        AddDomainEvent(new CommitteeMemberAddedEvent(Id, LoanApplicationId, userId, userName));
        
        return Result.Success();
    }

    public Result RemoveMember(Guid userId)
    {
        if (Status != CommitteeReviewStatus.Pending)
            return Result.Failure("Cannot remove members after voting has started");

        var member = _members.FirstOrDefault(m => m.UserId == userId);
        if (member == null)
            return Result.Failure("Member not found");

        _members.Remove(member);
        return Result.Success();
    }

    public Result StartVoting()
    {
        if (Status != CommitteeReviewStatus.Pending)
            return Result.Failure("Review is not in pending status");

        if (_members.Count < RequiredVotes)
            return Result.Failure($"Insufficient committee members. Required: {RequiredVotes}, Current: {_members.Count}");

        Status = CommitteeReviewStatus.InProgress;
        
        AddDomainEvent(new CommitteeVotingStartedEvent(Id, LoanApplicationId, DeadlineAt));
        
        return Result.Success();
    }

    public Result CastVote(Guid userId, CommitteeVote vote, string? comment = null)
    {
        if (Status != CommitteeReviewStatus.InProgress)
            return Result.Failure("Voting is not in progress");

        var member = _members.FirstOrDefault(m => m.UserId == userId);
        if (member == null)
            return Result.Failure("User is not a committee member");

        if (member.HasVoted)
            return Result.Failure("Member has already voted");

        var result = member.CastVote(vote, comment);
        if (result.IsFailure)
            return result;

        AddDomainEvent(new CommitteeVoteCastEvent(Id, LoanApplicationId, userId, vote));

        // Check if all votes are in
        if (_members.All(m => m.HasVoted))
        {
            Status = CommitteeReviewStatus.VotingComplete;
            AddDomainEvent(new CommitteeVotingCompletedEvent(Id, LoanApplicationId));
        }

        return Result.Success();
    }

    public Result AddComment(Guid userId, string content, CommentVisibility visibility = CommentVisibility.Committee)
    {
        if (Status == CommitteeReviewStatus.Closed)
            return Result.Failure("Cannot add comments to closed review");

        var member = _members.FirstOrDefault(m => m.UserId == userId);
        if (member == null && visibility != CommentVisibility.Internal)
            return Result.Failure("Only committee members can add committee-visible comments");

        var comment = CommitteeComment.Create(Id, userId, content, visibility);
        if (comment.IsFailure)
            return Result.Failure(comment.Error);

        _comments.Add(comment.Value);
        
        AddDomainEvent(new CommitteeCommentAddedEvent(Id, LoanApplicationId, userId));
        
        return Result.Success();
    }

    public Result AttachDocument(
        Guid userId,
        string fileName,
        string filePath,
        string description,
        DocumentVisibility visibility = DocumentVisibility.Committee)
    {
        if (Status == CommitteeReviewStatus.Closed)
            return Result.Failure("Cannot attach documents to closed review");

        var document = CommitteeDocument.Create(Id, userId, fileName, filePath, description, visibility);
        if (document.IsFailure)
            return Result.Failure(document.Error);

        _documents.Add(document.Value);
        return Result.Success();
    }

    public Result RecordDecision(
        Guid decidedByUserId,
        CommitteeDecision decision,
        string rationale,
        decimal? approvedAmount = null,
        int? approvedTenorMonths = null,
        decimal? approvedInterestRate = null,
        string? conditions = null)
    {
        if (Status != CommitteeReviewStatus.VotingComplete && Status != CommitteeReviewStatus.InProgress)
            return Result.Failure("Review must be in voting complete or in-progress status");

        // Validate chairperson or authorized user
        var member = _members.FirstOrDefault(m => m.UserId == decidedByUserId);
        if (member == null || !member.IsChairperson)
        {
            // Allow if all votes are in
            if (Status != CommitteeReviewStatus.VotingComplete)
                return Result.Failure("Only chairperson can record decision before all votes are cast");
        }

        if (string.IsNullOrWhiteSpace(rationale))
            return Result.Failure("Decision rationale is required");

        if (decision == CommitteeDecision.Approved)
        {
            if (!approvedAmount.HasValue || approvedAmount <= 0)
                return Result.Failure("Approved amount is required for approval");
            if (!approvedTenorMonths.HasValue || approvedTenorMonths <= 0)
                return Result.Failure("Approved tenor is required for approval");
            if (!approvedInterestRate.HasValue || approvedInterestRate <= 0)
                return Result.Failure("Approved interest rate is required for approval");
        }

        FinalDecision = decision;
        DecisionAt = DateTime.UtcNow;
        DecisionByUserId = decidedByUserId;
        DecisionRationale = rationale;
        ApprovedAmount = approvedAmount;
        ApprovedTenorMonths = approvedTenorMonths;
        ApprovedInterestRate = approvedInterestRate;
        ApprovalConditions = conditions;
        Status = CommitteeReviewStatus.Decided;

        AddDomainEvent(new CommitteeDecisionRecordedEvent(
            Id, LoanApplicationId, decision, approvedAmount, approvedTenorMonths, approvedInterestRate));

        return Result.Success();
    }

    public Result Close(Guid closedByUserId)
    {
        if (Status != CommitteeReviewStatus.Decided)
            return Result.Failure("Can only close decided reviews");

        Status = CommitteeReviewStatus.Closed;
        
        AddDomainEvent(new CommitteeReviewClosedEvent(Id, LoanApplicationId, FinalDecision ?? CommitteeDecision.Rejected));
        
        return Result.Success();
    }

    // Voting Summary
    public int ApprovalVotes => _members.Count(m => m.Vote == CommitteeVote.Approve);
    public int RejectionVotes => _members.Count(m => m.Vote == CommitteeVote.Reject);
    public int AbstainVotes => _members.Count(m => m.Vote == CommitteeVote.Abstain);
    public int PendingVotes => _members.Count(m => !m.HasVoted);
    public bool HasQuorum => _members.Count(m => m.HasVoted) >= RequiredVotes;
    public bool HasMajorityApproval => ApprovalVotes >= MinimumApprovalVotes;
    public bool IsOverdue => DeadlineAt.HasValue && DateTime.UtcNow > DeadlineAt.Value;
}

// Domain Events
public record CommitteeReviewCreatedEvent(
    Guid ReviewId, Guid LoanApplicationId, string ApplicationNumber, CommitteeType CommitteeType) : DomainEvent;

public record CommitteeMemberAddedEvent(
    Guid ReviewId, Guid LoanApplicationId, Guid UserId, string UserName) : DomainEvent;

public record CommitteeVotingStartedEvent(
    Guid ReviewId, Guid LoanApplicationId, DateTime? Deadline) : DomainEvent;

public record CommitteeVoteCastEvent(
    Guid ReviewId, Guid LoanApplicationId, Guid UserId, CommitteeVote Vote) : DomainEvent;

public record CommitteeVotingCompletedEvent(
    Guid ReviewId, Guid LoanApplicationId) : DomainEvent;

public record CommitteeCommentAddedEvent(
    Guid ReviewId, Guid LoanApplicationId, Guid UserId) : DomainEvent;

public record CommitteeDecisionRecordedEvent(
    Guid ReviewId, Guid LoanApplicationId, CommitteeDecision Decision,
    decimal? ApprovedAmount, int? ApprovedTenor, decimal? ApprovedRate) : DomainEvent;

public record CommitteeReviewClosedEvent(
    Guid ReviewId, Guid LoanApplicationId, CommitteeDecision FinalDecision) : DomainEvent;

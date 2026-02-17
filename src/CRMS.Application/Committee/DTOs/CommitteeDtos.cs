namespace CRMS.Application.Committee.DTOs;

public record CommitteeReviewDto(
    Guid Id,
    Guid LoanApplicationId,
    string ApplicationNumber,
    string CommitteeType,
    string Status,
    DateTime CirculatedAt,
    Guid CirculatedByUserId,
    DateTime? DeadlineAt,
    int RequiredVotes,
    int MinimumApprovalVotes,
    // Decision
    string? FinalDecision,
    DateTime? DecisionAt,
    Guid? DecisionByUserId,
    string? DecisionRationale,
    decimal? ApprovedAmount,
    int? ApprovedTenorMonths,
    decimal? ApprovedInterestRate,
    string? ApprovalConditions,
    // Summary
    int ApprovalVotes,
    int RejectionVotes,
    int AbstainVotes,
    int PendingVotes,
    bool HasQuorum,
    bool HasMajorityApproval,
    bool IsOverdue,
    // Related
    List<CommitteeMemberDto> Members,
    List<CommitteeCommentDto> RecentComments
);

public record CommitteeReviewSummaryDto(
    Guid Id,
    Guid LoanApplicationId,
    string ApplicationNumber,
    string CommitteeType,
    string Status,
    DateTime? DeadlineAt,
    int ApprovalVotes,
    int RejectionVotes,
    int PendingVotes,
    bool IsOverdue,
    bool HasUserVoted
);

public record CommitteeMemberDto(
    Guid Id,
    Guid UserId,
    string UserName,
    string Role,
    bool IsChairperson,
    string? Vote,
    DateTime? VotedAt,
    string? VoteComment,
    DateTime AssignedAt,
    bool HasVoted
);

public record CommitteeCommentDto(
    Guid Id,
    Guid UserId,
    string UserName,
    string Content,
    string Visibility,
    DateTime CreatedAt,
    bool IsEdited
);

public record CommitteeDocumentDto(
    Guid Id,
    Guid UploadedByUserId,
    string UploadedByUserName,
    string FileName,
    string FilePath,
    string Description,
    string Visibility,
    DateTime UploadedAt
);

public record VotingSummaryDto(
    int TotalMembers,
    int VotesCast,
    int ApprovalVotes,
    int RejectionVotes,
    int AbstainVotes,
    int PendingVotes,
    bool HasQuorum,
    bool HasMajorityApproval,
    int RequiredVotes,
    int MinimumApprovalVotes
);

// Input DTOs
public record CreateCommitteeReviewRequest(
    Guid LoanApplicationId,
    string CommitteeType,
    int RequiredVotes,
    int MinimumApprovalVotes,
    int DeadlineHours = 72
);

public record AddCommitteeMemberRequest(
    Guid UserId,
    string UserName,
    string Role,
    bool IsChairperson = false
);

public record CastVoteRequest(
    string Vote,
    string? Comment
);

public record AddCommentRequest(
    string Content,
    string Visibility = "Committee"
);

public record RecordDecisionRequest(
    string Decision,
    string Rationale,
    decimal? ApprovedAmount,
    int? ApprovedTenorMonths,
    decimal? ApprovedInterestRate,
    string? Conditions
);

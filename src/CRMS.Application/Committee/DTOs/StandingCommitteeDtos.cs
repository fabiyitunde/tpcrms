namespace CRMS.Application.Committee.DTOs;

public record StandingCommitteeDto(
    Guid Id,
    string Name,
    string CommitteeType,
    int RequiredVotes,
    int MinimumApprovalVotes,
    int DefaultDeadlineHours,
    decimal MinAmountThreshold,
    decimal? MaxAmountThreshold,
    bool IsActive,
    List<StandingCommitteeMemberDto> Members
);

public record StandingCommitteeMemberDto(
    Guid Id,
    Guid UserId,
    string UserName,
    string Role,
    bool IsChairperson
);

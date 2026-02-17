namespace CRMS.Domain.Enums;

public enum CommitteeType
{
    BranchCredit,       // Branch-level credit committee
    RegionalCredit,     // Regional credit committee
    HeadOfficeCredit,   // HO credit committee
    ManagementCredit,   // Management credit committee (larger facilities)
    BoardCredit         // Board credit committee (very large facilities)
}

public enum CommitteeReviewStatus
{
    Pending,        // Review created, members being assigned
    InProgress,     // Voting has started
    VotingComplete, // All votes received
    Decided,        // Decision recorded
    Closed          // Review finalized
}

public enum CommitteeVote
{
    Approve,
    Reject,
    Abstain
}

public enum CommitteeDecision
{
    Approved,
    ApprovedWithConditions,
    Rejected,
    Deferred,   // Requires more information
    Escalated   // Escalate to higher committee
}

public enum CommentVisibility
{
    Committee,      // Visible to committee members only
    Internal,       // Visible to all bank staff
    Applicant       // Visible to applicant (rare)
}

public enum DocumentVisibility
{
    Committee,      // Committee members only
    Internal,       // All bank staff
    Public          // Include in loan pack
}

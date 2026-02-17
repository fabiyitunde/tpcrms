namespace CRMS.Domain.Enums;

public enum WorkflowAction
{
    Create,
    Submit,
    Approve,
    Reject,
    Return,
    Assign,
    Unassign,
    Escalate,
    Complete,
    Cancel,
    Reopen,
    MoveToNextStage,
    RequestInfo,
    ProvidInfo,
    Override
}

public enum EscalationLevel
{
    None = 0,
    Level1 = 1,  // Supervisor
    Level2 = 2,  // Manager
    Level3 = 3   // Executive
}

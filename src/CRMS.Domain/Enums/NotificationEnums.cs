namespace CRMS.Domain.Enums;

public enum NotificationChannel
{
    Email,
    SMS,
    WhatsApp,
    InApp,
    Push
}

public enum NotificationType
{
    // Loan Application
    ApplicationSubmitted,
    ApplicationApproved,
    ApplicationRejected,
    ApplicationReturned,
    ApplicationDisbursed,
    
    // Workflow
    WorkflowAssigned,
    WorkflowEscalated,
    WorkflowSLAWarning,
    WorkflowSLABreached,
    
    // Committee
    CommitteeReviewCreated,
    CommitteeVotingStarted,
    CommitteeVoteRequired,
    CommitteeDecisionMade,
    
    // Credit Bureau
    CreditCheckCompleted,
    CreditCheckFailed,
    
    // Documents
    DocumentRequired,
    DocumentUploaded,
    DocumentVerified,
    DocumentRejected,
    
    // Security
    LoginAlert,
    PasswordChanged,
    PasswordReset,
    
    // System
    SystemAlert,
    Reminder,
    Custom
}

public enum NotificationStatus
{
    Pending,
    Scheduled,
    Sending,
    Sent,
    Delivered,
    Read,
    Failed,
    Retry,
    Cancelled
}

public enum NotificationPriority
{
    Low,
    Normal,
    High,
    Urgent
}

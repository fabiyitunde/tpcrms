using CRMS.Domain.Common;

namespace CRMS.Domain.Aggregates.Configuration;

/// <summary>
/// Audit trail for all scoring parameter changes.
/// Immutable record of every change for compliance and rollback capability.
/// </summary>
public class ScoringParameterHistory : Entity
{
    public Guid ScoringParameterId { get; private set; }
    public string Category { get; private set; } = string.Empty;
    public string ParameterKey { get; private set; } = string.Empty;
    
    public decimal PreviousValue { get; private set; }
    public decimal NewValue { get; private set; }
    
    public string ChangeType { get; private set; } = string.Empty; // "Created", "Approved", "Rejected"
    public string? ChangeReason { get; private set; }
    
    public Guid RequestedByUserId { get; private set; }
    public DateTime RequestedAt { get; private set; }
    
    public Guid? ApprovedByUserId { get; private set; }
    public DateTime? ApprovedAt { get; private set; }
    public string? ApprovalNotes { get; private set; }
    
    public int VersionNumber { get; private set; }

    private ScoringParameterHistory() { }

    public static ScoringParameterHistory RecordCreation(
        Guid parameterId,
        string category,
        string parameterKey,
        decimal initialValue,
        Guid createdByUserId)
    {
        return new ScoringParameterHistory
        {
            ScoringParameterId = parameterId,
            Category = category,
            ParameterKey = parameterKey,
            PreviousValue = 0,
            NewValue = initialValue,
            ChangeType = "Created",
            ChangeReason = "Initial parameter creation",
            RequestedByUserId = createdByUserId,
            RequestedAt = DateTime.UtcNow,
            ApprovedByUserId = createdByUserId,
            ApprovedAt = DateTime.UtcNow,
            VersionNumber = 1
        };
    }

    public static ScoringParameterHistory RecordApprovedChange(
        Guid parameterId,
        string category,
        string parameterKey,
        decimal previousValue,
        decimal newValue,
        string changeReason,
        Guid requestedByUserId,
        DateTime requestedAt,
        Guid approvedByUserId,
        string? approvalNotes,
        int versionNumber)
    {
        return new ScoringParameterHistory
        {
            ScoringParameterId = parameterId,
            Category = category,
            ParameterKey = parameterKey,
            PreviousValue = previousValue,
            NewValue = newValue,
            ChangeType = "Approved",
            ChangeReason = changeReason,
            RequestedByUserId = requestedByUserId,
            RequestedAt = requestedAt,
            ApprovedByUserId = approvedByUserId,
            ApprovedAt = DateTime.UtcNow,
            ApprovalNotes = approvalNotes,
            VersionNumber = versionNumber
        };
    }

    public static ScoringParameterHistory RecordRejectedChange(
        Guid parameterId,
        string category,
        string parameterKey,
        decimal currentValue,
        decimal requestedValue,
        string changeReason,
        Guid requestedByUserId,
        DateTime requestedAt,
        Guid rejectedByUserId,
        string rejectionReason,
        int versionNumber)
    {
        return new ScoringParameterHistory
        {
            ScoringParameterId = parameterId,
            Category = category,
            ParameterKey = parameterKey,
            PreviousValue = currentValue,
            NewValue = requestedValue,
            ChangeType = "Rejected",
            ChangeReason = changeReason,
            RequestedByUserId = requestedByUserId,
            RequestedAt = requestedAt,
            ApprovedByUserId = rejectedByUserId,
            ApprovedAt = DateTime.UtcNow,
            ApprovalNotes = $"REJECTED: {rejectionReason}",
            VersionNumber = versionNumber
        };
    }
}

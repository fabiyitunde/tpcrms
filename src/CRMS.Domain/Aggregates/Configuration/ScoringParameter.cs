using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Configuration;

/// <summary>
/// A single scoring parameter that can be configured by authorized users.
/// Supports maker-checker workflow for changes.
/// </summary>
public class ScoringParameter : AggregateRoot
{
    public string Category { get; private set; } = string.Empty;  // e.g., "CreditHistory", "Cashflow", "Recommendations"
    public string ParameterKey { get; private set; } = string.Empty;  // e.g., "BaseScore", "DefaultPenalty"
    public string DisplayName { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public ParameterDataType DataType { get; private set; }
    
    // Current active value
    public decimal CurrentValue { get; private set; }
    public decimal? MinValue { get; private set; }
    public decimal? MaxValue { get; private set; }
    
    // Pending change (maker-checker)
    public decimal? PendingValue { get; private set; }
    public Guid? PendingChangeByUserId { get; private set; }
    public DateTime? PendingChangeAt { get; private set; }
    public string? PendingChangeReason { get; private set; }
    public ParameterChangeStatus ChangeStatus { get; private set; }
    
    // Approval
    public Guid? ApprovedByUserId { get; private set; }
    public DateTime? ApprovedAt { get; private set; }
    public string? ApprovalNotes { get; private set; }
    public string? RejectionReason { get; private set; }
    
    // Audit
    public DateTime LastModifiedAt { get; private set; }
    public Guid LastModifiedByUserId { get; private set; }
    public int Version { get; private set; }
    
    // Metadata
    public bool IsActive { get; private set; }
    public int SortOrder { get; private set; }

    private ScoringParameter() { }

    public static Result<ScoringParameter> Create(
        string category,
        string parameterKey,
        string displayName,
        string description,
        ParameterDataType dataType,
        decimal initialValue,
        Guid createdByUserId,
        decimal? minValue = null,
        decimal? maxValue = null,
        int sortOrder = 0)
    {
        if (string.IsNullOrWhiteSpace(category))
            return Result.Failure<ScoringParameter>("Category is required");

        if (string.IsNullOrWhiteSpace(parameterKey))
            return Result.Failure<ScoringParameter>("Parameter key is required");

        if (minValue.HasValue && maxValue.HasValue && minValue > maxValue)
            return Result.Failure<ScoringParameter>("Min value cannot be greater than max value");

        if (minValue.HasValue && initialValue < minValue)
            return Result.Failure<ScoringParameter>($"Initial value cannot be less than minimum ({minValue})");

        if (maxValue.HasValue && initialValue > maxValue)
            return Result.Failure<ScoringParameter>($"Initial value cannot be greater than maximum ({maxValue})");

        var param = new ScoringParameter
        {
            Category = category,
            ParameterKey = parameterKey,
            DisplayName = displayName,
            Description = description,
            DataType = dataType,
            CurrentValue = initialValue,
            MinValue = minValue,
            MaxValue = maxValue,
            ChangeStatus = ParameterChangeStatus.None,
            IsActive = true,
            SortOrder = sortOrder,
            LastModifiedAt = DateTime.UtcNow,
            LastModifiedByUserId = createdByUserId,
            Version = 1
        };

        return Result.Success(param);
    }

    /// <summary>
    /// Request a change to this parameter (maker step).
    /// Change will be pending until approved by another user.
    /// </summary>
    public Result RequestChange(decimal newValue, Guid requestedByUserId, string reason)
    {
        if (ChangeStatus == ParameterChangeStatus.Pending)
            return Result.Failure("A change is already pending approval. Cancel or approve it first.");

        if (MinValue.HasValue && newValue < MinValue)
            return Result.Failure($"Value cannot be less than minimum ({MinValue})");

        if (MaxValue.HasValue && newValue > MaxValue)
            return Result.Failure($"Value cannot be greater than maximum ({MaxValue})");

        if (newValue == CurrentValue)
            return Result.Failure("New value is the same as current value");

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure("Change reason is required for audit purposes");

        PendingValue = newValue;
        PendingChangeByUserId = requestedByUserId;
        PendingChangeAt = DateTime.UtcNow;
        PendingChangeReason = reason;
        ChangeStatus = ParameterChangeStatus.Pending;
        RejectionReason = null;

        AddDomainEvent(new ScoringParameterChangeRequestedEvent(
            Id, Category, ParameterKey, CurrentValue, newValue, requestedByUserId, reason));

        return Result.Success();
    }

    /// <summary>
    /// Approve a pending change (checker step).
    /// </summary>
    public Result ApproveChange(Guid approvedByUserId, string? notes = null)
    {
        if (ChangeStatus != ParameterChangeStatus.Pending)
            return Result.Failure("No pending change to approve");

        if (approvedByUserId == PendingChangeByUserId)
            return Result.Failure("The same user cannot make and approve a change (maker-checker violation)");

        if (!PendingValue.HasValue)
            return Result.Failure("Pending value is missing");

        var previousValue = CurrentValue;
        CurrentValue = PendingValue.Value;
        ApprovedByUserId = approvedByUserId;
        ApprovedAt = DateTime.UtcNow;
        ApprovalNotes = notes;
        ChangeStatus = ParameterChangeStatus.Approved;
        LastModifiedAt = DateTime.UtcNow;
        LastModifiedByUserId = approvedByUserId;
        Version++;

        // Clear pending state
        PendingValue = null;
        PendingChangeByUserId = null;
        PendingChangeAt = null;
        PendingChangeReason = null;

        AddDomainEvent(new ScoringParameterChangeApprovedEvent(
            Id, Category, ParameterKey, previousValue, CurrentValue, approvedByUserId));

        return Result.Success();
    }

    /// <summary>
    /// Reject a pending change.
    /// </summary>
    public Result RejectChange(Guid rejectedByUserId, string reason)
    {
        if (ChangeStatus != ParameterChangeStatus.Pending)
            return Result.Failure("No pending change to reject");

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure("Rejection reason is required");

        RejectionReason = reason;
        ChangeStatus = ParameterChangeStatus.Rejected;
        
        AddDomainEvent(new ScoringParameterChangeRejectedEvent(
            Id, Category, ParameterKey, PendingValue ?? 0, rejectedByUserId, reason));

        // Clear pending state
        PendingValue = null;
        PendingChangeByUserId = null;
        PendingChangeAt = null;
        PendingChangeReason = null;

        return Result.Success();
    }

    /// <summary>
    /// Cancel a pending change (by the original requester).
    /// </summary>
    public Result CancelChange(Guid cancelledByUserId)
    {
        if (ChangeStatus != ParameterChangeStatus.Pending)
            return Result.Failure("No pending change to cancel");

        if (cancelledByUserId != PendingChangeByUserId)
            return Result.Failure("Only the original requester can cancel a pending change");

        ChangeStatus = ParameterChangeStatus.None;
        PendingValue = null;
        PendingChangeByUserId = null;
        PendingChangeAt = null;
        PendingChangeReason = null;

        return Result.Success();
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}

// Domain Events
public record ScoringParameterChangeRequestedEvent(
    Guid ParameterId, string Category, string ParameterKey, 
    decimal CurrentValue, decimal RequestedValue, 
    Guid RequestedByUserId, string Reason) : DomainEvent;

public record ScoringParameterChangeApprovedEvent(
    Guid ParameterId, string Category, string ParameterKey,
    decimal PreviousValue, decimal NewValue,
    Guid ApprovedByUserId) : DomainEvent;

public record ScoringParameterChangeRejectedEvent(
    Guid ParameterId, string Category, string ParameterKey,
    decimal RequestedValue, Guid RejectedByUserId, string Reason) : DomainEvent;

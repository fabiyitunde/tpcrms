using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Audit;

/// <summary>
/// Immutable audit log entry for compliance and traceability.
/// Records all significant actions in the system.
/// </summary>
public class AuditLog : Entity
{
    // What happened
    public AuditAction Action { get; private set; }
    public AuditCategory Category { get; private set; }
    public string Description { get; private set; } = string.Empty;
    
    // Who did it
    public Guid? UserId { get; private set; }
    public string? UserName { get; private set; }
    public string? UserRole { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    
    // What was affected
    public string EntityType { get; private set; } = string.Empty;
    public Guid? EntityId { get; private set; }
    public string? EntityReference { get; private set; }  // e.g., ApplicationNumber
    
    // Related entities
    public Guid? LoanApplicationId { get; private set; }
    public string? LoanApplicationNumber { get; private set; }
    
    // Change details
    public string? OldValues { get; private set; }  // JSON
    public string? NewValues { get; private set; }  // JSON
    public string? AdditionalData { get; private set; }  // JSON for extra context
    
    // Result
    public bool IsSuccess { get; private set; }
    public string? ErrorMessage { get; private set; }
    
    // Timestamps (CreatedAt from Entity is the audit timestamp)
    public DateTime Timestamp { get; private set; }

    private AuditLog() { }

    public static AuditLog Create(
        AuditAction action,
        AuditCategory category,
        string description,
        string entityType,
        Guid? entityId = null,
        string? entityReference = null,
        Guid? userId = null,
        string? userName = null,
        string? userRole = null,
        string? ipAddress = null,
        string? userAgent = null,
        Guid? loanApplicationId = null,
        string? loanApplicationNumber = null,
        string? oldValues = null,
        string? newValues = null,
        string? additionalData = null,
        bool isSuccess = true,
        string? errorMessage = null)
    {
        return new AuditLog
        {
            Action = action,
            Category = category,
            Description = description,
            EntityType = entityType,
            EntityId = entityId,
            EntityReference = entityReference,
            UserId = userId,
            UserName = userName,
            UserRole = userRole,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            LoanApplicationId = loanApplicationId,
            LoanApplicationNumber = loanApplicationNumber,
            OldValues = oldValues,
            NewValues = newValues,
            AdditionalData = additionalData,
            IsSuccess = isSuccess,
            ErrorMessage = errorMessage,
            Timestamp = DateTime.UtcNow
        };
    }
}

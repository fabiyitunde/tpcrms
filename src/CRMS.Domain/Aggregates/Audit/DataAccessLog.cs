using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Audit;

/// <summary>
/// Records access to sensitive data for compliance.
/// Tracks who viewed what sensitive information and when.
/// </summary>
public class DataAccessLog : Entity
{
    public Guid UserId { get; private set; }
    public string UserName { get; private set; } = string.Empty;
    public string UserRole { get; private set; } = string.Empty;
    
    public SensitiveDataType DataType { get; private set; }
    public string EntityType { get; private set; } = string.Empty;
    public Guid EntityId { get; private set; }
    public string? EntityReference { get; private set; }
    
    public Guid? LoanApplicationId { get; private set; }
    public string? LoanApplicationNumber { get; private set; }
    
    public DataAccessType AccessType { get; private set; }
    public string? AccessReason { get; private set; }
    
    public string? IpAddress { get; private set; }
    public DateTime AccessedAt { get; private set; }

    private DataAccessLog() { }

    public static DataAccessLog Create(
        Guid userId,
        string userName,
        string userRole,
        SensitiveDataType dataType,
        string entityType,
        Guid entityId,
        DataAccessType accessType,
        string? entityReference = null,
        Guid? loanApplicationId = null,
        string? loanApplicationNumber = null,
        string? accessReason = null,
        string? ipAddress = null)
    {
        return new DataAccessLog
        {
            UserId = userId,
            UserName = userName,
            UserRole = userRole,
            DataType = dataType,
            EntityType = entityType,
            EntityId = entityId,
            EntityReference = entityReference,
            LoanApplicationId = loanApplicationId,
            LoanApplicationNumber = loanApplicationNumber,
            AccessType = accessType,
            AccessReason = accessReason,
            IpAddress = ipAddress,
            AccessedAt = DateTime.UtcNow
        };
    }
}

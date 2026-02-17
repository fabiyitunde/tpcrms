using CRMS.Domain.Common;

namespace CRMS.Domain.Aggregates.Reporting;

/// <summary>
/// Defines a report configuration that can be saved and reused.
/// </summary>
public class ReportDefinition : AggregateRoot
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public ReportType Type { get; private set; }
    public ReportCategory Category { get; private set; }
    
    // Report parameters as JSON
    public string? Parameters { get; private set; }
    
    // Access control
    public string AllowedRoles { get; private set; } = string.Empty; // Comma-separated roles
    public bool IsSystemReport { get; private set; }
    public bool IsActive { get; private set; }
    
    // Scheduling (for automated reports)
    public string? CronSchedule { get; private set; }
    public bool IsScheduled { get; private set; }
    
    // Audit
    public Guid CreatedByUserId { get; private set; }
    public Guid? LastModifiedByUserId { get; private set; }
    public DateTime? LastModifiedAt { get; private set; }
    public DateTime? LastExecutedAt { get; private set; }

    private ReportDefinition() { }

    public static Result<ReportDefinition> Create(
        string code,
        string name,
        string description,
        ReportType type,
        ReportCategory category,
        Guid createdByUserId,
        string allowedRoles,
        string? parameters = null,
        bool isSystemReport = false)
    {
        if (string.IsNullOrWhiteSpace(code))
            return Result.Failure<ReportDefinition>("Report code is required");

        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<ReportDefinition>("Report name is required");

        return Result.Success(new ReportDefinition
        {
            Code = code,
            Name = name,
            Description = description,
            Type = type,
            Category = category,
            Parameters = parameters,
            AllowedRoles = allowedRoles,
            IsSystemReport = isSystemReport,
            IsActive = true,
            CreatedByUserId = createdByUserId
        });
    }

    public void SetSchedule(string cronSchedule)
    {
        CronSchedule = cronSchedule;
        IsScheduled = !string.IsNullOrEmpty(cronSchedule);
    }

    public void RecordExecution() => LastExecutedAt = DateTime.UtcNow;
    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}

public enum ReportType
{
    Table,
    Chart,
    Summary,
    Dashboard,
    Export
}

public enum ReportCategory
{
    LoanFunnel,
    Portfolio,
    Performance,
    Risk,
    Compliance,
    Operations,
    Executive
}

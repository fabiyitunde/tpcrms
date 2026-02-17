using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Notification;

/// <summary>
/// Notification template for generating notification content.
/// Supports multiple channels and variable substitution.
/// </summary>
public class NotificationTemplate : AggregateRoot
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public NotificationType Type { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public string Language { get; private set; } = "en";
    
    // Email specific
    public string? Subject { get; private set; }
    public string BodyTemplate { get; private set; } = string.Empty;
    public string? BodyHtmlTemplate { get; private set; }
    
    // Variables available in template (JSON schema)
    public string? AvailableVariables { get; private set; }
    
    // Audit
    public bool IsActive { get; private set; }
    public int Version { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public Guid? LastModifiedByUserId { get; private set; }
    public DateTime? LastModifiedAt { get; private set; }

    private NotificationTemplate() { }

    public static Result<NotificationTemplate> Create(
        string code,
        string name,
        string description,
        NotificationType type,
        NotificationChannel channel,
        string bodyTemplate,
        Guid createdByUserId,
        string? subject = null,
        string? bodyHtmlTemplate = null,
        string? availableVariables = null,
        string language = "en")
    {
        if (string.IsNullOrWhiteSpace(code))
            return Result.Failure<NotificationTemplate>("Template code is required");

        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<NotificationTemplate>("Template name is required");

        if (string.IsNullOrWhiteSpace(bodyTemplate))
            return Result.Failure<NotificationTemplate>("Body template is required");

        if (channel == NotificationChannel.Email && string.IsNullOrWhiteSpace(subject))
            return Result.Failure<NotificationTemplate>("Subject is required for email templates");

        return Result.Success(new NotificationTemplate
        {
            Code = code,
            Name = name,
            Description = description,
            Type = type,
            Channel = channel,
            Language = language,
            Subject = subject,
            BodyTemplate = bodyTemplate,
            BodyHtmlTemplate = bodyHtmlTemplate,
            AvailableVariables = availableVariables,
            IsActive = true,
            Version = 1,
            CreatedByUserId = createdByUserId
        });
    }

    public Result Update(
        string name,
        string description,
        string bodyTemplate,
        Guid modifiedByUserId,
        string? subject = null,
        string? bodyHtmlTemplate = null,
        string? availableVariables = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure("Template name is required");

        if (string.IsNullOrWhiteSpace(bodyTemplate))
            return Result.Failure("Body template is required");

        Name = name;
        Description = description;
        Subject = subject;
        BodyTemplate = bodyTemplate;
        BodyHtmlTemplate = bodyHtmlTemplate;
        AvailableVariables = availableVariables;
        LastModifiedByUserId = modifiedByUserId;
        LastModifiedAt = DateTime.UtcNow;
        Version++;

        return Result.Success();
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;

    /// <summary>
    /// Renders the template with the provided variables.
    /// </summary>
    public (string Subject, string Body, string? BodyHtml) Render(Dictionary<string, string> variables)
    {
        var subject = ReplaceVariables(Subject ?? "", variables);
        var body = ReplaceVariables(BodyTemplate, variables);
        var bodyHtml = BodyHtmlTemplate != null ? ReplaceVariables(BodyHtmlTemplate, variables) : null;

        return (subject, body, bodyHtml);
    }

    private static string ReplaceVariables(string template, Dictionary<string, string> variables)
    {
        var result = template;
        foreach (var kvp in variables)
        {
            result = result.Replace($"{{{{{kvp.Key}}}}}", kvp.Value);
            result = result.Replace($"{{{{${kvp.Key}}}}}", kvp.Value);
        }
        return result;
    }
}

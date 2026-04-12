namespace CRMS.Domain.Constants;

/// <summary>
/// Well-known constants for system-initiated actions.
/// Used wherever an automated process performs an action that requires a user ID
/// (audit logs, status history, workflow transitions) but no human actor is involved.
/// </summary>
public static class SystemConstants
{
    /// <summary>
    /// Fixed identifier used as the actor for all system-automated actions.
    /// Audit displays should resolve this ID as "System Process" rather than a person's name.
    /// </summary>
    public static readonly Guid SystemUserId = new("00000000-0000-0000-0000-000000000001");
}

namespace CRMS.Domain.Enums;

/// <summary>
/// Defines the visibility scope for a user based on their role.
/// Determines which loan applications they can view/access.
/// </summary>
public enum VisibilityScope
{
    /// <summary>
    /// User can only see applications they created.
    /// </summary>
    Own = 0,

    /// <summary>
    /// User can see all applications in their assigned branch.
    /// </summary>
    Branch = 1,

    /// <summary>
    /// User can see all applications in branches under their zone.
    /// </summary>
    Zone = 2,

    /// <summary>
    /// User can see all applications in zones under their region.
    /// </summary>
    Region = 3,

    /// <summary>
    /// User can see all applications across the organization (HO roles).
    /// </summary>
    Global = 4
}

namespace CRMS.Web.Intranet.Models;

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool RememberMe { get; set; }
}

public class LoginResponse
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public string? RefreshToken { get; set; }
    public UserInfo? User { get; set; }
    public string? Error { get; set; }
}

public class UserInfo
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string FullName => $"{FirstName} {LastName}";
    public string Initials => $"{FirstName?.FirstOrDefault()}{LastName?.FirstOrDefault()}";
    public List<string> Roles { get; set; } = [];
    public List<string> Permissions { get; set; } = [];
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    public string? LocationType { get; set; }
    /// <summary>
    /// True when the user is domiciled at the Head Office — grants cross-branch (global) visibility.
    /// <para>
    /// No location at all also counts as Head Office. That is the bank's rule: a user is scoped to a
    /// branch or a zone only once one is explicitly assigned, and until then they are head-office
    /// staff. Reading an unset location as "not head office" is what previously left a Head Office
    /// Risk Officer seeing nothing — scoped to their own tier while being treated as branch staff.
    /// </para>
    /// </summary>
    public bool IsHeadOffice =>
        string.Equals(LocationType, "HeadOffice", StringComparison.OrdinalIgnoreCase)
        || LocationId is null;
    // Backward compatibility
    public string? BranchId { get => LocationId?.ToString(); set => LocationId = Guid.TryParse(value, out var id) ? id : null; }
    public string? BranchName { get => LocationName; set => LocationName = value; }

    public string PrimaryRole => Roles.FirstOrDefault() ?? string.Empty;

    public bool HasRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    public bool HasAnyRole(params string[] roles) => roles.Any(r => HasRole(r));
    public bool HasPermission(string permission) => Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
}

public class AuthState
{
    public bool IsAuthenticated { get; set; }
    public UserInfo? User { get; set; }
    public string? Token { get; set; }
}

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
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}";
    public string Initials => $"{FirstName?.FirstOrDefault()}{LastName?.FirstOrDefault()}";
    public List<string> Roles { get; set; } = [];
    public List<string> Permissions { get; set; } = [];
    public string? BranchId { get; set; }
    public string? BranchName { get; set; }

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

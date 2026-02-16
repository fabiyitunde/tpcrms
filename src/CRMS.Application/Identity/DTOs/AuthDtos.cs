namespace CRMS.Application.Identity.DTOs;

public record LoginRequest(string Email, string Password);

public record LoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiry,
    UserDto User
);

public record RefreshTokenRequest(string RefreshToken);

public record RegisterUserRequest(
    string Email,
    string UserName,
    string Password,
    string FirstName,
    string LastName,
    string UserType,
    string? PhoneNumber,
    Guid? BranchId,
    List<string>? Roles
);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record UserDto(
    Guid Id,
    string Email,
    string UserName,
    string FirstName,
    string LastName,
    string FullName,
    string Type,
    string Status,
    string? PhoneNumber,
    Guid? BranchId,
    DateTime? LastLoginAt,
    List<string> Roles,
    List<string> Permissions
);

public record UserSummaryDto(
    Guid Id,
    string Email,
    string FullName,
    string Type,
    string Status,
    List<string> Roles
);

public record RoleDto(
    Guid Id,
    string Name,
    string? Description,
    string Type,
    List<string> Permissions
);

public record PermissionDto(
    Guid Id,
    string Code,
    string Name,
    string Module,
    string? Description
);

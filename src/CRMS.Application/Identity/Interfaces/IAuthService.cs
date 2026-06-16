using CRMS.Application.Common;
using CRMS.Application.Identity.DTOs;

namespace CRMS.Application.Identity.Interfaces;

public interface IAuthService
{
    Task<ApplicationResult<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<ApplicationResult<LoginResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct = default);
    Task<ApplicationResult> LogoutAsync(Guid userId, CancellationToken ct = default);
    Task<ApplicationResult> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default);

    /// <summary>
    /// Generates a single-use, time-limited reset token, emails a reset link, and ALWAYS reports success
    /// (whether or not the email exists) to avoid account enumeration. <paramref name="resetUrlBase"/> is the
    /// front-end reset page URL, e.g. "https://crms.example.com/reset-password".
    /// </summary>
    Task<ApplicationResult> RequestPasswordResetAsync(string email, string resetUrlBase, CancellationToken ct = default);

    /// <summary>Validates the reset token and sets the new password (single-use; revokes sessions).</summary>
    Task<ApplicationResult> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default);
}

public interface ITokenService
{
    string GenerateAccessToken(Guid userId, string email, IEnumerable<string> roles, IEnumerable<string> permissions);
    string GenerateRefreshToken();
    bool ValidateRefreshToken(string token);
}

public interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string passwordHash);
}

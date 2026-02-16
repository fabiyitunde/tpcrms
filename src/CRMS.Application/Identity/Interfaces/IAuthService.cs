using CRMS.Application.Common;
using CRMS.Application.Identity.DTOs;

namespace CRMS.Application.Identity.Interfaces;

public interface IAuthService
{
    Task<ApplicationResult<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<ApplicationResult<LoginResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct = default);
    Task<ApplicationResult> LogoutAsync(Guid userId, CancellationToken ct = default);
    Task<ApplicationResult> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default);
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

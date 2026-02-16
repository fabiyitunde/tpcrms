using CRMS.Application.Common;
using CRMS.Application.Identity.DTOs;
using CRMS.Application.Identity.Interfaces;
using CRMS.Domain.Interfaces;
using Microsoft.Extensions.Options;

namespace CRMS.Infrastructure.Identity;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IPermissionRepository _permissionRepository;
    private readonly ITokenService _tokenService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly JwtSettings _jwtSettings;

    public AuthService(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IPermissionRepository permissionRepository,
        ITokenService tokenService,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        IOptions<JwtSettings> jwtSettings)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _permissionRepository = permissionRepository;
        _tokenService = tokenService;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
        _jwtSettings = jwtSettings.Value;
    }

    public async Task<ApplicationResult<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email, ct);
        if (user == null)
            return ApplicationResult<LoginResponse>.Failure("Invalid email or password");

        if (user.IsLockedOut)
            return ApplicationResult<LoginResponse>.Failure("Account is locked. Please try again later.");

        if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            user.RecordFailedLogin(5, TimeSpan.FromMinutes(30));
            await _unitOfWork.SaveChangesAsync(ct);
            return ApplicationResult<LoginResponse>.Failure("Invalid email or password");
        }

        var roles = await _roleRepository.GetUserRolesAsync(user.Id, ct);
        var permissions = await _permissionRepository.GetUserPermissionsAsync(user.Id, ct);

        var roleNames = roles.Select(r => r.Name).ToList();
        var permissionCodes = permissions.Select(p => p.Code).ToList();

        var accessToken = _tokenService.GenerateAccessToken(user.Id, user.Email, roleNames, permissionCodes);
        var refreshToken = _tokenService.GenerateRefreshToken();

        user.RecordLogin();
        user.UpdateRefreshToken(refreshToken, DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays));
        await _unitOfWork.SaveChangesAsync(ct);

        var userDto = new UserDto(
            user.Id,
            user.Email,
            user.UserName,
            user.FirstName,
            user.LastName,
            user.FullName,
            user.Type.ToString(),
            user.Status.ToString(),
            user.PhoneNumber,
            user.BranchId,
            user.LastLoginAt,
            roleNames,
            permissionCodes
        );

        return ApplicationResult<LoginResponse>.Success(new LoginResponse(
            accessToken,
            refreshToken,
            DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpiryMinutes),
            userDto
        ));
    }

    public async Task<ApplicationResult<LoginResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct = default)
    {
        var users = await _userRepository.GetAllAsync(ct);
        var user = users.FirstOrDefault(u => u.RefreshToken == request.RefreshToken);

        if (user == null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
            return ApplicationResult<LoginResponse>.Failure("Invalid or expired refresh token");

        var roles = await _roleRepository.GetUserRolesAsync(user.Id, ct);
        var permissions = await _permissionRepository.GetUserPermissionsAsync(user.Id, ct);

        var roleNames = roles.Select(r => r.Name).ToList();
        var permissionCodes = permissions.Select(p => p.Code).ToList();

        var accessToken = _tokenService.GenerateAccessToken(user.Id, user.Email, roleNames, permissionCodes);
        var refreshToken = _tokenService.GenerateRefreshToken();

        user.UpdateRefreshToken(refreshToken, DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays));
        await _unitOfWork.SaveChangesAsync(ct);

        var userDto = new UserDto(
            user.Id,
            user.Email,
            user.UserName,
            user.FirstName,
            user.LastName,
            user.FullName,
            user.Type.ToString(),
            user.Status.ToString(),
            user.PhoneNumber,
            user.BranchId,
            user.LastLoginAt,
            roleNames,
            permissionCodes
        );

        return ApplicationResult<LoginResponse>.Success(new LoginResponse(
            accessToken,
            refreshToken,
            DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpiryMinutes),
            userDto
        ));
    }

    public async Task<ApplicationResult> LogoutAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user == null)
            return ApplicationResult.Failure("User not found");

        user.RevokeRefreshToken();
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }

    public async Task<ApplicationResult> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user == null)
            return ApplicationResult.Failure("User not found");

        if (!_passwordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash))
            return ApplicationResult.Failure("Current password is incorrect");

        user.SetPasswordHash(_passwordHasher.HashPassword(request.NewPassword));
        user.RevokeRefreshToken();
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}

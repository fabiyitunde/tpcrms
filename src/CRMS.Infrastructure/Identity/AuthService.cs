using CRMS.Application.Common;
using CRMS.Application.Identity.DTOs;
using CRMS.Application.Identity.Interfaces;
using CRMS.Application.Notification.Interfaces;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using Microsoft.Extensions.Logging;
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
    private readonly INotificationService _notificationService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IPermissionRepository permissionRepository,
        ITokenService tokenService,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        IOptions<JwtSettings> jwtSettings,
        INotificationService notificationService,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _permissionRepository = permissionRepository;
        _tokenService = tokenService;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
        _jwtSettings = jwtSettings.Value;
        _notificationService = notificationService;
        _logger = logger;
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

        // Block deactivated / non-active accounts. Deactivate() sets Status=Inactive but does not
        // set the lockout fields, so the IsLockedOut check above does not catch them. Checked after
        // password verification so a wrong password still returns the generic "invalid" message.
        if (user.Status != Domain.Entities.Identity.UserStatus.Active)
            return ApplicationResult<LoginResponse>.Failure("Your account is inactive. Please contact your administrator.");

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
            user.LocationId,
            user.Location?.Name,
            user.Location?.Type.ToString(),
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

        // A user deactivated while holding a valid refresh token must not be able to mint new access tokens.
        if (user.Status != Domain.Entities.Identity.UserStatus.Active)
            return ApplicationResult<LoginResponse>.Failure("Your account is inactive. Please contact your administrator.");

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
            user.LocationId,
            user.Location?.Name,
            user.Location?.Type.ToString(),
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

        var policy = ValidatePasswordPolicy(request.NewPassword);
        if (!policy.IsSuccess) return policy;

        user.SetPasswordHash(_passwordHasher.HashPassword(request.NewPassword));
        user.RevokeRefreshToken();
        await _unitOfWork.SaveChangesAsync(ct);

        // Confirmation email — enqueued (non-blocking); a notification failure must not fail the change.
        try
        {
            await _notificationService.SendEmailAsync(
                NotificationType.PasswordChanged,
                user.Email,
                user.FullName,
                "PASSWORD_CHANGED",
                new Dictionary<string, string>
                {
                    ["RecipientName"] = user.FullName,
                    ["ChangedAt"] = DateTime.UtcNow.ToString("dd MMM yyyy HH:mm 'UTC'")
                },
                recipientUserId: user.Id,
                priority: NotificationPriority.Normal,
                ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue password-changed email for {Email}", user.Email);
        }

        return ApplicationResult.Success();
    }

    public async Task<ApplicationResult> RequestPasswordResetAsync(string email, string resetUrlBase, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByEmailAsync(email, ct);

        if (user is not null && user.Status == Domain.Entities.Identity.UserStatus.Active)
        {
            // Rate limit: if a reset token was issued for this account within the throttle window,
            // don't issue/send another (prevents reset-email flooding). Token expiry is 1h, so a
            // not-yet-near-expiry token means it was just issued.
            var throttle = TimeSpan.FromSeconds(60);
            if (user.PasswordResetTokenExpiresAt.HasValue &&
                user.PasswordResetTokenExpiresAt.Value > DateTime.UtcNow.Add(TimeSpan.FromHours(1) - throttle))
            {
                _logger.LogInformation("Password reset for {Email} throttled — a link was sent recently.", user.Email);
                return ApplicationResult.Success();
            }

            var rawToken = GenerateResetToken();
            user.SetPasswordResetToken(HashToken(rawToken), DateTime.UtcNow.AddHours(1));
            await _unitOfWork.SaveChangesAsync(ct);

            var link = BuildResetLink(resetUrlBase, rawToken);
            try
            {
                // Normal priority keeps the response non-blocking and uniform in timing (anti-enumeration).
                await _notificationService.SendEmailAsync(
                    NotificationType.PasswordReset,
                    user.Email,
                    user.FullName,
                    "PASSWORD_RESET",
                    new Dictionary<string, string>
                    {
                        ["RecipientName"] = user.FullName,
                        ["ResetLink"] = link,
                        ["ExpiryMinutes"] = "60"
                    },
                    recipientUserId: user.Id,
                    priority: NotificationPriority.Normal,
                    ct: ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enqueue password-reset email for {Email}", user.Email);
            }
        }
        else
        {
            _logger.LogInformation("Password reset requested for unknown/inactive email — no-op (anti-enumeration).");
        }

        // Always succeed — never reveal whether the account exists.
        return ApplicationResult.Success();
    }

    public async Task<ApplicationResult> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return ApplicationResult.Failure("This password reset link is invalid.");

        var policy = ValidatePasswordPolicy(request.NewPassword);
        if (!policy.IsSuccess) return policy;

        // Look up by the token hash alone — the token is unique, so no email is needed in the link.
        var candidateHash = HashToken(request.Token);
        var user = await _userRepository.GetByPasswordResetTokenHashAsync(candidateHash, ct);

        if (user is null || !user.IsPasswordResetTokenValid(candidateHash))
            return ApplicationResult.Failure("This password reset link is invalid or has expired. Please request a new one.");

        user.SetPasswordHash(_passwordHasher.HashPassword(request.NewPassword));
        user.ClearPasswordResetToken();   // single-use
        user.RevokeRefreshToken();         // invalidate existing sessions
        await _unitOfWork.SaveChangesAsync(ct);

        try
        {
            await _notificationService.SendEmailAsync(
                NotificationType.PasswordChanged,
                user.Email,
                user.FullName,
                "PASSWORD_CHANGED",
                new Dictionary<string, string>
                {
                    ["RecipientName"] = user.FullName,
                    ["ChangedAt"] = DateTime.UtcNow.ToString("dd MMM yyyy HH:mm 'UTC'")
                },
                recipientUserId: user.Id,
                priority: NotificationPriority.Normal,
                ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue password-changed email after reset for {Email}", user.Email);
        }

        return ApplicationResult.Success();
    }

    /// <summary>
    /// Shared password strength policy: at least 8 characters with an uppercase letter,
    /// a lowercase letter, and a digit. Applied on password change and reset.
    /// </summary>
    private static ApplicationResult ValidatePasswordPolicy(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            return ApplicationResult.Failure("Password must be at least 8 characters.");
        if (!password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(char.IsDigit))
            return ApplicationResult.Failure(
                "Password must include an uppercase letter, a lowercase letter, and a number.");
        return ApplicationResult.Success();
    }

    private static string GenerateResetToken()
    {
        var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('='); // base64url
    }

    private static string HashToken(string rawToken)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hash); // 64-char hex
    }

    private static string BuildResetLink(string resetUrlBase, string rawToken)
    {
        // Token only — base64url is URL-safe and survives click-tracking re-encoding. No PII in the link.
        var sep = resetUrlBase.Contains('?') ? "&" : "?";
        return $"{resetUrlBase}{sep}token={Uri.EscapeDataString(rawToken)}";
    }
}

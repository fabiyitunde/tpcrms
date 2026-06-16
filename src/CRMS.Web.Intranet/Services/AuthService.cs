using System.Security.Claims;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using CRMS.Application.Identity.Interfaces;
using CRMS.Web.Intranet.Models;
using AppLoginRequest = CRMS.Application.Identity.DTOs.LoginRequest;
using AppChangePasswordRequest = CRMS.Application.Identity.DTOs.ChangePasswordRequest;
using AppResetPasswordRequest = CRMS.Application.Identity.DTOs.ResetPasswordRequest;

namespace CRMS.Web.Intranet.Services;

public class AuthService : AuthenticationStateProvider
{
    private readonly IAuthService _authService;
    private readonly ILocalStorageService _localStorage;
    private readonly ILogger<AuthService> _logger;
    
    private const string TokenKey = "authToken";
    private const string UserKey = "authUser";
    
    private AuthState _authState = new();

    public AuthService(
        IAuthService authService,
        ILocalStorageService localStorage,
        ILogger<AuthService> logger)
    {
        _authService = authService;
        _localStorage = localStorage;
        _logger = logger;
    }

    public UserInfo? CurrentUser => _authState.User;
    public bool IsAuthenticated => _authState.IsAuthenticated;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var token = await _localStorage.GetItemAsync<string>(TokenKey);
            var user = await _localStorage.GetItemAsync<UserInfo>(UserKey);

            if (string.IsNullOrEmpty(token) || user == null)
            {
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }

            if (IsTokenExpired(token))
            {
                _logger.LogInformation("JWT token expired for user {UserId} — clearing session", user.Id);
                await _localStorage.RemoveItemAsync(TokenKey);
                await _localStorage.RemoveItemAsync(UserKey);
                _authState = new AuthState();
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }

            _authState = new AuthState
            {
                IsAuthenticated = true,
                Token = token,
                User = user
            };

            var claims = BuildClaims(user);
            var identity = new ClaimsIdentity(claims, "jwt");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting authentication state");
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        try
        {
            var appRequest = new AppLoginRequest(request.Email, request.Password);
            var result = await _authService.LoginAsync(appRequest);
            
            if (!result.IsSuccess || result.Data == null)
            {
                return new LoginResponse { Success = false, Error = result.Error ?? "Invalid credentials" };
            }

            var appUser = result.Data.User;
            var user = new UserInfo
            {
                Id = appUser.Id,
                Email = appUser.Email,
                Username = appUser.UserName,
                FirstName = appUser.FirstName,
                LastName = appUser.LastName,
                PhoneNumber = appUser.PhoneNumber,
                Roles = appUser.Roles,
                Permissions = appUser.Permissions,
                LocationId = appUser.LocationId,
                LocationName = appUser.LocationName,
                LocationType = appUser.LocationType
            };

            await _localStorage.SetItemAsync(TokenKey, result.Data.AccessToken);
            await _localStorage.SetItemAsync(UserKey, user);
            
            _authState = new AuthState
            {
                IsAuthenticated = true,
                Token = result.Data.AccessToken,
                User = user
            };

            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

            return new LoginResponse { Success = true, Token = result.Data.AccessToken, User = user };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login error");
            return new LoginResponse { Success = false, Error = "Login failed. Please try again." };
        }
    }

    public async Task LogoutAsync()
    {
        // If we have a user, notify the backend
        if (_authState.User != null)
        {
            try
            {
                await _authService.LogoutAsync(_authState.User.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during logout");
            }
        }

        await _localStorage.RemoveItemAsync(TokenKey);
        await _localStorage.RemoveItemAsync(UserKey);
        
        _authState = new AuthState();
        
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task<LoginResponse> ChangePasswordAsync(string currentPassword, string newPassword)
    {
        if (_authState.User == null)
            return new LoginResponse { Success = false, Error = "Not authenticated" };
        try
        {
            var request = new AppChangePasswordRequest(currentPassword, newPassword);
            var result = await _authService.ChangePasswordAsync(_authState.User.Id, request);
            return result.IsSuccess
                ? new LoginResponse { Success = true }
                : new LoginResponse { Success = false, Error = result.Error ?? "Failed to change password" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password");
            return new LoginResponse { Success = false, Error = "Failed to change password. Please try again." };
        }
    }

    public async Task<LoginResponse> RequestPasswordResetAsync(string email, string resetUrlBase)
    {
        try
        {
            var result = await _authService.RequestPasswordResetAsync(email, resetUrlBase);
            // Always reported as success to avoid revealing whether the account exists.
            return result.IsSuccess
                ? new LoginResponse { Success = true }
                : new LoginResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error requesting password reset");
            return new LoginResponse { Success = false, Error = "Something went wrong. Please try again." };
        }
    }

    public async Task<LoginResponse> ResetPasswordAsync(string token, string newPassword)
    {
        try
        {
            var result = await _authService.ResetPasswordAsync(new AppResetPasswordRequest(token, newPassword));
            return result.IsSuccess
                ? new LoginResponse { Success = true }
                : new LoginResponse { Success = false, Error = result.Error ?? "Failed to reset password" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password");
            return new LoginResponse { Success = false, Error = "Failed to reset password. Please try again." };
        }
    }

    public async Task UpdateLocalUserAsync(string firstName, string lastName, string? phoneNumber)
    {
        if (_authState.User == null) return;
        _authState.User.FirstName = firstName;
        _authState.User.LastName = lastName;
        _authState.User.PhoneNumber = phoneNumber;
        await _localStorage.SetItemAsync(UserKey, _authState.User);
    }

    public async Task<bool> HasRoleAsync(string role)
    {
        var state = await GetAuthenticationStateAsync();
        return state.User.IsInRole(role);
    }

    public async Task<bool> HasAnyRoleAsync(params string[] roles)
    {
        var state = await GetAuthenticationStateAsync();
        return roles.Any(r => state.User.IsInRole(r));
    }

    private static bool IsTokenExpired(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3) return true;

            var payload = parts[1];
            // Pad base64url to standard base64
            payload = payload.Replace('-', '+').Replace('_', '/');
            payload += new string('=', (4 - payload.Length % 4) % 4);

            var bytes = Convert.FromBase64String(payload);
            using var doc = System.Text.Json.JsonDocument.Parse(bytes);

            if (doc.RootElement.TryGetProperty("exp", out var exp))
            {
                var expiry = DateTimeOffset.FromUnixTimeSeconds(exp.GetInt64());
                return expiry < DateTimeOffset.UtcNow;
            }
            return false;
        }
        catch
        {
            return true;
        }
    }

    private static IEnumerable<Claim> BuildClaims(UserInfo user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.GivenName, user.FirstName),
            new(ClaimTypes.Surname, user.LastName),
            new(ClaimTypes.Name, user.FullName)
        };

        foreach (var role in user.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        foreach (var permission in user.Permissions)
        {
            claims.Add(new Claim("permission", permission));
        }

        return claims;
    }
}



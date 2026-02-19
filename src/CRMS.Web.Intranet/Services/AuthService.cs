using System.Security.Claims;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using CRMS.Application.Identity.Interfaces;
using CRMS.Web.Intranet.Models;
using AppLoginRequest = CRMS.Application.Identity.DTOs.LoginRequest;

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
                FirstName = appUser.FirstName,
                LastName = appUser.LastName,
                Roles = appUser.Roles,
                Permissions = appUser.Permissions
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



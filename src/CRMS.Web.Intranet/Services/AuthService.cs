using System.Net.Http.Json;
using System.Security.Claims;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using CRMS.Web.Intranet.Models;

namespace CRMS.Web.Intranet.Services;

public class AuthService : AuthenticationStateProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILocalStorageService _localStorage;
    private readonly ILogger<AuthService> _logger;
    
    private const string TokenKey = "authToken";
    private const string UserKey = "authUser";
    
    private AuthState _authState = new();

    public AuthService(
        HttpClient httpClient,
        ILocalStorageService localStorage,
        ILogger<AuthService> logger)
    {
        _httpClient = httpClient;
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

            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

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
            var response = await _httpClient.PostAsJsonAsync("api/auth/login", request);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return new LoginResponse { Success = false, Error = "Invalid credentials" };
            }

            var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
            
            if (result?.Success == true && result.Token != null && result.User != null)
            {
                await _localStorage.SetItemAsync(TokenKey, result.Token);
                await _localStorage.SetItemAsync(UserKey, result.User);
                
                _authState = new AuthState
                {
                    IsAuthenticated = true,
                    Token = result.Token,
                    User = result.User
                };

                _httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", result.Token);

                NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
            }

            return result ?? new LoginResponse { Success = false, Error = "Unknown error" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login error");
            return new LoginResponse { Success = false, Error = "Connection error. Please try again." };
        }
    }

    public async Task LogoutAsync()
    {
        await _localStorage.RemoveItemAsync(TokenKey);
        await _localStorage.RemoveItemAsync(UserKey);
        
        _authState = new AuthState();
        _httpClient.DefaultRequestHeaders.Authorization = null;
        
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

public static class AuthServiceExtensions
{
    public static IServiceCollection AddAuthServices(this IServiceCollection services, string apiBaseUrl)
    {
        services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });
        services.AddScoped<AuthService>();
        services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<AuthService>());
        services.AddAuthorizationCore();
        
        return services;
    }
}

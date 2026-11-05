using System.Security.Claims;
using CRMS.Application.Identity.DTOs;
using CRMS.Application.Identity.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CRMS.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>Sign in with email and password.</summary>
    /// <remarks>Returns an access token and a refresh token. Send the access token as a Bearer token on every staff endpoint. Five wrong passwords in a row lock the account for 30 minutes.</remarks>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _authService.LoginAsync(request, ct);
        return result.IsSuccess ? Ok(result.Data) : Unauthorized(result.Error);
    }

    /// <summary>Exchange a refresh token for a new access token.</summary>
    /// <remarks>Use this when the access token has expired, instead of signing in again.</remarks>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        var result = await _authService.RefreshTokenAsync(request, ct);
        return result.IsSuccess ? Ok(result.Data) : Unauthorized(result.Error);
    }

    /// <summary>Sign out and revoke the current refresh token.</summary>
    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        var result = await _authService.LogoutAsync(userId.Value, ct);
        return result.IsSuccess ? NoContent() : BadRequest(result.Error);
    }

    /// <summary>Change the signed-in user's password.</summary>
    [Authorize]
    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        var result = await _authService.ChangePasswordAsync(userId.Value, request, ct);
        return result.IsSuccess ? NoContent() : BadRequest(result.Error);
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}

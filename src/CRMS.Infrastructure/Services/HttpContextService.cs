using CRMS.Domain.Interfaces;
using Microsoft.AspNetCore.Http;

namespace CRMS.Infrastructure.Services;

/// <summary>
/// Service for extracting information from HTTP context.
/// </summary>
public interface IHttpContextService : IAuditContextProvider
{
    /// <summary>
    /// Get the current user ID from claims.
    /// </summary>
    Guid? GetCurrentUserId();
    
    /// <summary>
    /// Get the current user's role from claims.
    /// </summary>
    string? GetCurrentUserRole();
}

public class HttpContextService : IHttpContextService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? GetClientIpAddress()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null) return null;

        // Check for X-Forwarded-For header (when behind a reverse proxy/load balancer)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            // X-Forwarded-For can contain multiple IPs, the first is the client
            var ip = forwardedFor.Split(',').FirstOrDefault()?.Trim();
            if (!string.IsNullOrEmpty(ip))
                return ip;
        }

        // Check for X-Real-IP header (used by some proxies)
        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
            return realIp;

        // Fall back to RemoteIpAddress
        return context.Connection.RemoteIpAddress?.ToString();
    }

    public string? GetUserAgent()
    {
        return _httpContextAccessor.HttpContext?.Request.Headers["User-Agent"].FirstOrDefault();
    }

    public Guid? GetCurrentUserId()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context?.User == null) return null;

        var userIdClaim = context.User.FindFirst("sub")?.Value 
                       ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    public string? GetCurrentUserRole()
    {
        var context = _httpContextAccessor.HttpContext;
        return context?.User?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
    }
}

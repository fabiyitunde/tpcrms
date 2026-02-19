namespace CRMS.Domain.Interfaces;

/// <summary>
/// Provides audit context information (IP, user agent) for the current request.
/// Implemented in Infrastructure layer to access HTTP context.
/// </summary>
public interface IAuditContextProvider
{
    /// <summary>
    /// Get the client IP address for the current request.
    /// Returns null for background processes without HTTP context.
    /// </summary>
    string? GetClientIpAddress();
    
    /// <summary>
    /// Get the User-Agent header for the current request.
    /// Returns null for background processes without HTTP context.
    /// </summary>
    string? GetUserAgent();
}

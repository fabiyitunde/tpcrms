using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CRMS.API.Filters;

/// <summary>
/// Restricts an endpoint to specific environments (e.g., Development, Staging).
/// Returns 404 Not Found if accessed in a disallowed environment.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class EnvironmentRestrictedAttribute : Attribute, IAuthorizationFilter
{
    private readonly string[] _allowedEnvironments;

    public EnvironmentRestrictedAttribute(params string[] allowedEnvironments)
    {
        _allowedEnvironments = allowedEnvironments;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var env = context.HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
        
        if (!_allowedEnvironments.Any(e => e.Equals(env.EnvironmentName, StringComparison.OrdinalIgnoreCase)))
        {
            context.Result = new NotFoundResult();
        }
    }
}

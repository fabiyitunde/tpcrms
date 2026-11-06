namespace CRMS.API.OpenApi;

/// <summary>
/// Documents the JSON body of an action that reads its body by hand (to keep the raw payload, or to
/// answer a malformed body with the contract's own error shape instead of the framework's). It
/// changes nothing at run time; it only tells the API reference what the body looks like.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequestBodyDocAttribute : Attribute
{
    public RequestBodyDocAttribute(Type type) => Type = type;

    public Type Type { get; }

    /// <summary>
    /// An example body, as JSON, shown in the reference and used by "try it". Keep it to what a
    /// caller must send; a first-time reader takes every field in it for a requirement.
    /// </summary>
    public string? Example { get; set; }

    /// <summary>
    /// A second example, with the optional fields filled in. When set, the reference offers both:
    /// "Required fields only" first, then "With optional fields".
    /// </summary>
    public string? FullExample { get; set; }
}

/// <summary>
/// Marks a controller or action that authenticates with an <c>X-Api-Key</c> header it checks
/// itself, rather than with a staff JWT. The API reference lists the key scheme for it.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ApiKeyAuthAttribute : Attribute
{
    public ApiKeyAuthAttribute(string whoIssuesIt) => WhoIssuesIt = whoIssuesIt;

    /// <summary>One sentence on where the caller gets its key, shown under the operation.</summary>
    public string WhoIssuesIt { get; }
}

/// <summary>Marks an action that only works in the Development environment.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class DevelopmentOnlyAttribute : Attribute;

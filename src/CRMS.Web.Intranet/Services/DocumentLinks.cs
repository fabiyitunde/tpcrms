using Microsoft.AspNetCore.DataProtection;

namespace CRMS.Web.Intranet.Services;

/// <summary>
/// Signed, time-limited links for the document addresses staff open in a new tab — corporate
/// application and collateral documents, NAMP documents, and RH-SHF offer letters, Key Facts
/// Statements and collateral evidence.
/// <para>
/// Those addresses served customer documents to anyone who had the URL — and an RH-SHF case
/// reference is a year and six digits (CRMSQA#2). They cannot simply require a login: staff
/// authentication lives in the Blazor circuit, not a cookie, so a new tab carries none.
/// Instead the page that shows the link — which only a signed-in officer can render — signs it. The
/// signature names the exact path and expires, so a bare or guessed address, an expired link, or a
/// link edited to point at another case is refused.
/// </para>
/// </summary>
public class DocumentLinks
{
    /// <summary>Long enough for a case page left open through a working session; a stale link only
    /// needs the page reloading.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(4);

    private const string TokenParameter = "t";
    private readonly ITimeLimitedDataProtector _protector;

    public DocumentLinks(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("CRMS.DocumentLinks").ToTimeLimitedDataProtector();
    }

    /// <summary>The path with a token that is valid for that path only, for <see cref="Lifetime"/>.</summary>
    public string Sign(string path) =>
        $"{path}?{TokenParameter}={Uri.EscapeDataString(_protector.Protect(Normalise(path), Lifetime))}";

    /// <summary>True when the request carries an unexpired token issued for exactly this path.</summary>
    public bool IsValid(HttpContext context)
    {
        var token = context.Request.Query[TokenParameter].ToString();
        if (string.IsNullOrEmpty(token))
            return false;

        try
        {
            return _protector.Unprotect(token) == Normalise(context.Request.Path.Value ?? "");
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return false; // expired, tampered with, or signed with keys this instance no longer holds
        }
    }

    /// <summary>The refusal every protected address returns: plain words, no hint of whether the
    /// document exists.</summary>
    public static IResult Refusal() => Results.Text(
        "This link has expired or is not valid. Reopen the document from the case in the CRMS.",
        statusCode: StatusCodes.Status403Forbidden);

    private static string Normalise(string path) => path.TrimEnd('/').ToLowerInvariant();
}

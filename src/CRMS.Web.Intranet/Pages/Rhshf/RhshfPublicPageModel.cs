using System.Security.Claims;
using CRMS.Application.Rhshf.Commands;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CRMS.Web.Intranet.Pages.Rhshf;

/// <summary>
/// Shared token-verify + "RhshfProfiling" cookie-session logic (design doc §6 #5) for every
/// anonymous, FAC-facing RH-SHF page — first used by Profiling.cshtml (Phase 3), now shared with
/// Offer.cshtml (Phase 7) rather than duplicated a second time. Completely independent of staff
/// login (AuthService/AuthenticationStateProvider are never referenced here).
/// </summary>
public abstract class RhshfPublicPageModel : PageModel
{
    protected const string SchemeName = "RhshfProfiling";
    protected const string ReferenceClaimType = "reference";

    private readonly VerifyRhshfProfilingTokenHandler _verifyHandler;

    public bool IsExpired { get; protected set; }

    protected RhshfPublicPageModel(VerifyRhshfProfilingTokenHandler verifyHandler)
    {
        _verifyHandler = verifyHandler;
    }

    /// <summary>Call from OnGetAsync. Returns a non-null result when the caller should return
    /// immediately (expired, or redirecting right after a fresh sign-in to strip ?token= from the
    /// address bar); returns null when already authorized, meaning the caller should proceed to
    /// load and render its own content.</summary>
    protected async Task<IActionResult?> TryEnterAsync(string reference, string? token, CancellationToken ct)
    {
        if (await IsAuthorizedForReferenceAsync(reference))
            return null;

        if (string.IsNullOrEmpty(token))
        {
            IsExpired = true;
            return Page();
        }

        var verifyResult = await _verifyHandler.Handle(new VerifyRhshfProfilingTokenCommand(reference, token), ct);
        if (!verifyResult.IsSuccess)
        {
            IsExpired = true;
            return Page();
        }

        var identity = new ClaimsIdentity([new Claim(ReferenceClaimType, reference)], SchemeName);
        await HttpContext.SignInAsync(SchemeName, new ClaimsPrincipal(identity));

        // Redirect so ?token=... never lingers in the address bar / browser history beyond the
        // single request that consumed it (design doc §6 #5). Resolves to the current page.
        return RedirectToPage(new { reference });
    }

    /// <summary>Call at the top of every OnPostXxxAsync — a cookie for case A must never authorize
    /// an action against case B's route, checked on every request, not just initial entry.</summary>
    protected async Task<bool> IsAuthorizedForReferenceAsync(string reference)
    {
        var authResult = await HttpContext.AuthenticateAsync(SchemeName);
        if (!authResult.Succeeded || authResult.Principal is null)
            return false;

        var claimReference = authResult.Principal.FindFirst(ReferenceClaimType)?.Value;
        return string.Equals(claimReference, reference, StringComparison.Ordinal);
    }
}

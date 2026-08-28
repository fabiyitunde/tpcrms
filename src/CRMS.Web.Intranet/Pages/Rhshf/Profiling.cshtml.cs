using System.Security.Claims;
using CRMS.Application.Rhshf.Commands;
using CRMS.Application.Rhshf.DTOs;
using CRMS.Application.Rhshf.Queries;
using CRMS.Domain.Enums;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CRMS.Web.Intranet.Pages.Rhshf;

/// <summary>
/// The FAC-facing profiling form (§4.3 of the integration brief). Token-authenticated on first
/// load only; the rest of the multi-stage session runs on the "RhshfProfiling" cookie scheme
/// (design doc §6 #5) — completely independent of staff login (AuthService/
/// AuthenticationStateProvider are never referenced here).
/// </summary>
public class ProfilingModel : PageModel
{
    private const string SchemeName = "RhshfProfiling";
    private const string ReferenceClaimType = "reference";

    private readonly VerifyRhshfProfilingTokenHandler _verifyHandler;
    private readonly GetRhshfProfilingSessionHandler _sessionHandler;
    private readonly EnsureRhshfBureauCheckHandler _bureauHandler;
    private readonly AdvanceRhshfProfilingStageHandler _advanceHandler;
    private readonly UploadRhshfSupportingDocumentHandler _uploadHandler;

    public ProfilingModel(
        VerifyRhshfProfilingTokenHandler verifyHandler,
        GetRhshfProfilingSessionHandler sessionHandler,
        EnsureRhshfBureauCheckHandler bureauHandler,
        AdvanceRhshfProfilingStageHandler advanceHandler,
        UploadRhshfSupportingDocumentHandler uploadHandler)
    {
        _verifyHandler = verifyHandler;
        _sessionHandler = sessionHandler;
        _bureauHandler = bureauHandler;
        _advanceHandler = advanceHandler;
        _uploadHandler = uploadHandler;
    }

    public RhshfProfilingSessionDto? Session { get; private set; }
    public bool IsExpired { get; private set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    [TempData]
    public string? SuccessMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(string reference, string? token, CancellationToken ct)
    {
        if (!await IsAuthorizedForReferenceAsync(reference))
        {
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

            // Redirect so ?token=... never lingers in the address bar / browser history beyond
            // the single request that consumed it (design doc §6 #5).
            return RedirectToPage(new { reference });
        }

        var loaded = await LoadSessionAsync(reference, ct);
        if (!loaded)
        {
            IsExpired = true;
            return Page();
        }

        if (Session!.CurrentStage == RhshfProfilingStage.CreditBureauCheck
            && Session.BureauCheckOutcome == RhshfBureauOutcome.NotRun)
        {
            await _bureauHandler.Handle(new EnsureRhshfBureauCheckCommand(reference), ct);
            await LoadSessionAsync(reference, ct);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostConfirmAsync(string reference, RhshfProfilingStage stage, CancellationToken ct)
    {
        if (!await IsAuthorizedForReferenceAsync(reference))
            return RedirectToPage("SessionExpired");

        var result = await _advanceHandler.Handle(new AdvanceRhshfProfilingStageCommand(reference, stage), ct);
        if (!result.IsSuccess)
            ErrorMessage = result.Error;

        return RedirectToPage(new { reference });
    }

    public async Task<IActionResult> OnPostUploadAsync(string reference, IFormFile? file, CancellationToken ct)
    {
        if (!await IsAuthorizedForReferenceAsync(reference))
            return RedirectToPage("SessionExpired");

        if (file is null || file.Length == 0)
        {
            ErrorMessage = "Please choose a file to upload.";
            return RedirectToPage(new { reference });
        }

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);

        var result = await _uploadHandler.Handle(
            new UploadRhshfSupportingDocumentCommand(reference, file.FileName, file.ContentType, ms.ToArray()), ct);

        if (!result.IsSuccess)
            ErrorMessage = result.Error;
        else
            SuccessMessage = $"\"{file.FileName}\" uploaded.";

        return RedirectToPage(new { reference });
    }

    private async Task<bool> LoadSessionAsync(string reference, CancellationToken ct)
    {
        var result = await _sessionHandler.Handle(new GetRhshfProfilingSessionQuery(reference), ct);
        if (!result.IsSuccess)
            return false;

        Session = result.Data;
        return true;
    }

    /// <summary>A cookie for case A must never authorize an action against case B's route — this
    /// is checked on every GET and every POST, not just the initial token verification.</summary>
    private async Task<bool> IsAuthorizedForReferenceAsync(string reference)
    {
        var authResult = await HttpContext.AuthenticateAsync(SchemeName);
        if (!authResult.Succeeded || authResult.Principal is null)
            return false;

        var claimReference = authResult.Principal.FindFirst(ReferenceClaimType)?.Value;
        return string.Equals(claimReference, reference, StringComparison.Ordinal);
    }
}

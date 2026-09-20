using CRMS.Application.Rhshf.Commands;
using CRMS.Application.Rhshf.DTOs;
using CRMS.Application.Rhshf.Queries;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CRMS.Web.Intranet.Pages.Rhshf;

/// <summary>
/// FAC offer-acceptance page (design doc §3.6/§6 #10, Phase 7) — its own page, not folded into the
/// profiling wizard (different shape of interaction). Shares token/cookie-session auth with
/// Profiling.cshtml via RhshfPublicPageModel.
/// </summary>
public class OfferModel : RhshfPublicPageModel
{
    private readonly GetRhshfCaseWorkspaceHandler _workspaceHandler;
    private readonly GetRhshfOfferHandler _offerHandler;
    private readonly UploadSignedOfferHandler _uploadHandler;
    private readonly AcceptRhshfOfferHandler _acceptHandler;
    private readonly RejectRhshfOfferHandler _rejectHandler;
    private readonly IFileStorageService _fileStorage;

    public OfferModel(
        VerifyRhshfProfilingTokenHandler verifyHandler,
        GetRhshfCaseWorkspaceHandler workspaceHandler,
        GetRhshfOfferHandler offerHandler,
        UploadSignedOfferHandler uploadHandler,
        AcceptRhshfOfferHandler acceptHandler,
        RejectRhshfOfferHandler rejectHandler,
        IFileStorageService fileStorage)
        : base(verifyHandler)
    {
        _workspaceHandler = workspaceHandler;
        _offerHandler = offerHandler;
        _uploadHandler = uploadHandler;
        _acceptHandler = acceptHandler;
        _rejectHandler = rejectHandler;
        _fileStorage = fileStorage;
    }

    public RhshfCaseWorkspaceDto? Workspace { get; private set; }
    public RhshfOfferDto? Offer { get; private set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    [TempData]
    public string? SuccessMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(string reference, string? token, CancellationToken ct)
    {
        var entryResult = await TryEnterAsync(reference, token, ct);
        if (entryResult is not null)
            return entryResult;

        await LoadAsync(reference, ct);
        if (Workspace is null)
        {
            IsExpired = true;
            return Page();
        }

        return Page();
    }

    /// <summary>GET /rhshf/offer/{reference}?handler=Download — streams the generated offer PDF,
    /// gated by the same cookie session as everything else on this page.</summary>
    public async Task<IActionResult> OnGetDownloadAsync(string reference, CancellationToken ct)
    {
        if (!await IsAuthorizedForReferenceAsync(reference))
            return RedirectToPage("SessionExpired");

        var offerResult = await _offerHandler.Handle(new GetRhshfOfferQuery(reference), ct);
        if (!offerResult.IsSuccess)
            return NotFound();

        var bytes = await _fileStorage.DownloadAsync(offerResult.Data!.OfferDocumentPath, ct);
        return File(bytes, "application/pdf", $"{reference}-offer.pdf");
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
            new UploadSignedOfferCommand(reference, file.FileName, file.ContentType, ms.ToArray()), ct);

        if (!result.IsSuccess)
            ErrorMessage = result.Error;
        else
            SuccessMessage = $"\"{file.FileName}\" uploaded.";

        return RedirectToPage(new { reference });
    }

    public async Task<IActionResult> OnPostAcceptAsync(string reference, string? notes, CancellationToken ct)
    {
        if (!await IsAuthorizedForReferenceAsync(reference))
            return RedirectToPage("SessionExpired");

        var result = await _acceptHandler.Handle(new AcceptRhshfOfferCommand(reference, notes), ct);
        if (!result.IsSuccess)
            ErrorMessage = result.Error;

        return RedirectToPage(new { reference });
    }

    public async Task<IActionResult> OnPostRejectAsync(string reference, string? notes, CancellationToken ct)
    {
        if (!await IsAuthorizedForReferenceAsync(reference))
            return RedirectToPage("SessionExpired");

        var result = await _rejectHandler.Handle(new RejectRhshfOfferCommand(reference, notes), ct);
        if (!result.IsSuccess)
            ErrorMessage = result.Error;

        return RedirectToPage(new { reference });
    }

    private async Task LoadAsync(string reference, CancellationToken ct)
    {
        var workspaceResult = await _workspaceHandler.Handle(new GetRhshfCaseWorkspaceQuery(reference), ct);
        Workspace = workspaceResult.IsSuccess ? workspaceResult.Data : null;

        var offerResult = await _offerHandler.Handle(new GetRhshfOfferQuery(reference), ct);
        Offer = offerResult.IsSuccess ? offerResult.Data : null;
    }
}

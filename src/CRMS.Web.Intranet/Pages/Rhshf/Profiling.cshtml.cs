using CRMS.Application.Rhshf.Commands;
using CRMS.Application.Rhshf.DTOs;
using CRMS.Application.Rhshf.Queries;
using CRMS.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace CRMS.Web.Intranet.Pages.Rhshf;

/// <summary>
/// The FAC-facing profiling form (§4.3 of the integration brief). Token/cookie-session auth is
/// shared via RhshfPublicPageModel (design doc §6 #5) — this class only handles profiling's own
/// 5-stage content.
/// </summary>
public class ProfilingModel : RhshfPublicPageModel
{
    private readonly GetRhshfProfilingSessionHandler _sessionHandler;
    private readonly EnsureRhshfBureauCheckHandler _bureauHandler;
    private readonly AdvanceRhshfProfilingStageHandler _advanceHandler;
    private readonly UploadRhshfSupportingDocumentHandler _uploadHandler;
    private readonly AddRhshfProfilingFarmPlanHandler _addFarmPlanHandler;
    private readonly RemoveRhshfProfilingFarmPlanHandler _removeFarmPlanHandler;
    private readonly RemoveRhshfProfilingDocumentHandler _removeDocumentHandler;

    public ProfilingModel(
        VerifyRhshfProfilingTokenHandler verifyHandler,
        GetRhshfProfilingSessionHandler sessionHandler,
        EnsureRhshfBureauCheckHandler bureauHandler,
        AdvanceRhshfProfilingStageHandler advanceHandler,
        UploadRhshfSupportingDocumentHandler uploadHandler,
        AddRhshfProfilingFarmPlanHandler addFarmPlanHandler,
        RemoveRhshfProfilingFarmPlanHandler removeFarmPlanHandler,
        RemoveRhshfProfilingDocumentHandler removeDocumentHandler)
        : base(verifyHandler)
    {
        _sessionHandler = sessionHandler;
        _bureauHandler = bureauHandler;
        _advanceHandler = advanceHandler;
        _uploadHandler = uploadHandler;
        _addFarmPlanHandler = addFarmPlanHandler;
        _removeFarmPlanHandler = removeFarmPlanHandler;
        _removeDocumentHandler = removeDocumentHandler;
    }

    public RhshfProfilingSessionDto? Session { get; private set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    [TempData]
    public string? SuccessMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(string reference, string? token, CancellationToken ct)
    {
        var entryResult = await TryEnterAsync(reference, token, ct);
        if (entryResult is not null)
            return entryResult;

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

        // IP/user-agent give the stage confirmation a thin audit trail — profiling is
        // token-authenticated against the case, so there is no individual user identity to record.
        var result = await _advanceHandler.Handle(
            new AdvanceRhshfProfilingStageCommand(
                reference, stage,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString()),
            ct);
        if (!result.IsSuccess)
            ErrorMessage = result.Error;

        return RedirectToPage(new { reference });
    }

    public async Task<IActionResult> OnPostAddFarmPlanAsync(
        string reference, string crop, decimal hectares, decimal yieldPerHa, decimal pricePerKg, CancellationToken ct)
    {
        if (!await IsAuthorizedForReferenceAsync(reference))
            return RedirectToPage("SessionExpired");

        var result = await _addFarmPlanHandler.Handle(
            new AddRhshfProfilingFarmPlanCommand(reference, crop, hectares, yieldPerHa, pricePerKg), ct);

        if (!result.IsSuccess)
            ErrorMessage = result.Error;
        else
            SuccessMessage = $"\"{crop}\" added to the farm plan.";

        return RedirectToPage(new { reference });
    }

    public async Task<IActionResult> OnPostRemoveFarmPlanAsync(string reference, Guid planId, CancellationToken ct)
    {
        if (!await IsAuthorizedForReferenceAsync(reference))
            return RedirectToPage("SessionExpired");

        var result = await _removeFarmPlanHandler.Handle(new RemoveRhshfProfilingFarmPlanCommand(reference, planId), ct);
        if (!result.IsSuccess)
            ErrorMessage = result.Error;

        return RedirectToPage(new { reference });
    }

    public async Task<IActionResult> OnPostRemoveDocumentAsync(string reference, Guid documentId, CancellationToken ct)
    {
        if (!await IsAuthorizedForReferenceAsync(reference))
            return RedirectToPage("SessionExpired");

        var result = await _removeDocumentHandler.Handle(new RemoveRhshfProfilingDocumentCommand(reference, documentId), ct);
        if (!result.IsSuccess)
            ErrorMessage = result.Error;

        return RedirectToPage(new { reference });
    }

    public async Task<IActionResult> OnPostUploadAsync(
        string reference, RhshfDocumentCategory category, IFormFile? file, CancellationToken ct)
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
            new UploadRhshfSupportingDocumentCommand(reference, category, file.FileName, file.ContentType, ms.ToArray()), ct);

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
}

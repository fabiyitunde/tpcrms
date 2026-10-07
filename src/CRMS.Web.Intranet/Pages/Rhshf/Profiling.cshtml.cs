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
    private readonly GoToRhshfProfilingStageHandler _goToStageHandler;
    private readonly UploadRhshfSupportingDocumentHandler _uploadHandler;
    private readonly AddRhshfProfilingFarmPlanHandler _addFarmPlanHandler;
    private readonly RemoveRhshfProfilingFarmPlanHandler _removeFarmPlanHandler;
    private readonly RemoveRhshfProfilingDocumentHandler _removeDocumentHandler;
    private readonly AddRhshfDirectorHandler _addDirectorHandler;
    private readonly UpdateRhshfDirectorHandler _updateDirectorHandler;
    private readonly RemoveRhshfDirectorHandler _removeDirectorHandler;
    private readonly PullRhshfDirectorsFromCbsHandler _pullDirectorsHandler;
    private readonly AddRhshfGuarantorHandler _addGuarantorHandler;
    private readonly RemoveRhshfGuarantorHandler _removeGuarantorHandler;
    private readonly AddRhshfProfilingCollateralHandler _addCollateralHandler;
    private readonly RemoveRhshfProfilingCollateralHandler _removeCollateralHandler;

    public ProfilingModel(
        VerifyRhshfProfilingTokenHandler verifyHandler,
        GetRhshfProfilingSessionHandler sessionHandler,
        EnsureRhshfBureauCheckHandler bureauHandler,
        AdvanceRhshfProfilingStageHandler advanceHandler,
        GoToRhshfProfilingStageHandler goToStageHandler,
        UploadRhshfSupportingDocumentHandler uploadHandler,
        AddRhshfProfilingFarmPlanHandler addFarmPlanHandler,
        RemoveRhshfProfilingFarmPlanHandler removeFarmPlanHandler,
        RemoveRhshfProfilingDocumentHandler removeDocumentHandler,
        AddRhshfDirectorHandler addDirectorHandler,
        UpdateRhshfDirectorHandler updateDirectorHandler,
        RemoveRhshfDirectorHandler removeDirectorHandler,
        PullRhshfDirectorsFromCbsHandler pullDirectorsHandler,
        AddRhshfGuarantorHandler addGuarantorHandler,
        RemoveRhshfGuarantorHandler removeGuarantorHandler,
        AddRhshfProfilingCollateralHandler addCollateralHandler,
        RemoveRhshfProfilingCollateralHandler removeCollateralHandler)
        : base(verifyHandler)
    {
        _sessionHandler = sessionHandler;
        _bureauHandler = bureauHandler;
        _advanceHandler = advanceHandler;
        _goToStageHandler = goToStageHandler;
        _uploadHandler = uploadHandler;
        _addFarmPlanHandler = addFarmPlanHandler;
        _removeFarmPlanHandler = removeFarmPlanHandler;
        _removeDocumentHandler = removeDocumentHandler;
        _addDirectorHandler = addDirectorHandler;
        _updateDirectorHandler = updateDirectorHandler;
        _removeDirectorHandler = removeDirectorHandler;
        _pullDirectorsHandler = pullDirectorsHandler;
        _addGuarantorHandler = addGuarantorHandler;
        _removeGuarantorHandler = removeGuarantorHandler;
        _addCollateralHandler = addCollateralHandler;
        _removeCollateralHandler = removeCollateralHandler;
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

    public async Task<IActionResult> OnPostGoToStageAsync(string reference, RhshfProfilingStage stage, CancellationToken ct)
    {
        if (!await IsAuthorizedForReferenceAsync(reference))
            return RedirectToPage("SessionExpired");

        var result = await _goToStageHandler.Handle(new GoToRhshfProfilingStageCommand(reference, stage), ct);
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

    public async Task<IActionResult> OnPostAddDirectorAsync(
        string reference, string fullName, string? bvn, decimal? shareholdingPercent, bool isChairman,
        string? email, string? phoneNumber, CancellationToken ct)
    {
        if (!await IsAuthorizedForReferenceAsync(reference))
            return RedirectToPage("SessionExpired");

        // The FAC is token-authenticated against the case, not an individual user — no UserId to record.
        var result = await _addDirectorHandler.Handle(
            new AddRhshfDirectorCommand(reference, fullName, bvn, shareholdingPercent, isChairman, email, phoneNumber, Guid.Empty), ct);

        if (!result.IsSuccess)
            ErrorMessage = result.Error;
        else
            SuccessMessage = $"Director \"{fullName}\" added.";

        return RedirectToPage(new { reference });
    }

    public async Task<IActionResult> OnPostUpdateDirectorBvnAsync(
        string reference, Guid directorId, string? bvn, decimal? shareholdingPercent, CancellationToken ct)
    {
        if (!await IsAuthorizedForReferenceAsync(reference))
            return RedirectToPage("SessionExpired");

        // Shareholding is carried through from the row so setting the BVN doesn't wipe it.
        var result = await _updateDirectorHandler.Handle(
            new UpdateRhshfDirectorCommand(reference, directorId, bvn, shareholdingPercent, Guid.Empty), ct);

        if (!result.IsSuccess)
            ErrorMessage = result.Error;
        else
            SuccessMessage = "Director BVN updated.";

        return RedirectToPage(new { reference });
    }

    public async Task<IActionResult> OnPostRemoveDirectorAsync(string reference, Guid directorId, CancellationToken ct)
    {
        if (!await IsAuthorizedForReferenceAsync(reference))
            return RedirectToPage("SessionExpired");

        var result = await _removeDirectorHandler.Handle(new RemoveRhshfDirectorCommand(reference, directorId, Guid.Empty), ct);
        if (!result.IsSuccess)
            ErrorMessage = result.Error;

        return RedirectToPage(new { reference });
    }

    public async Task<IActionResult> OnPostPullDirectorsAsync(string reference, CancellationToken ct)
    {
        if (!await IsAuthorizedForReferenceAsync(reference))
            return RedirectToPage("SessionExpired");

        var result = await _pullDirectorsHandler.Handle(new PullRhshfDirectorsFromCbsCommand(reference), ct);
        if (!result.IsSuccess)
            ErrorMessage = result.Error;
        else
            SuccessMessage = result.Data > 0
                ? $"{result.Data} director(s) pulled from core banking — please confirm their BVNs."
                : "No new directors with a BVN were found on the core-banking record.";

        return RedirectToPage(new { reference });
    }

    public async Task<IActionResult> OnPostAddGuarantorAsync(
        string reference, RhshfGuarantorType guarantorType, string fullName, string? bvn, string? rcNumber,
        string? relationship, decimal? guaranteeAmount, string? phoneNumber, CancellationToken ct)
    {
        if (!await IsAuthorizedForReferenceAsync(reference))
            return RedirectToPage("SessionExpired");

        var result = await _addGuarantorHandler.Handle(
            new AddRhshfGuarantorCommand(reference, Guid.Empty, guarantorType, fullName, bvn, rcNumber,
                relationship, phoneNumber, null, null, guaranteeAmount, null), ct);

        if (!result.IsSuccess)
            ErrorMessage = result.Error;
        else
            SuccessMessage = $"Guarantor \"{fullName}\" added.";

        return RedirectToPage(new { reference });
    }

    public async Task<IActionResult> OnPostRemoveGuarantorAsync(string reference, Guid guarantorId, CancellationToken ct)
    {
        if (!await IsAuthorizedForReferenceAsync(reference))
            return RedirectToPage("SessionExpired");

        var result = await _removeGuarantorHandler.Handle(new RemoveRhshfGuarantorCommand(guarantorId), ct);
        if (!result.IsSuccess)
            ErrorMessage = result.Error;

        return RedirectToPage(new { reference });
    }

    public async Task<IActionResult> OnPostAddCollateralAsync(
        string reference, RhshfCollateralType collateralType, string? referenceNumber, string? notes,
        string? guarantorBankName, decimal? guaranteeAmount, bool isUnconditional,
        decimal? crgCoveragePercentage,
        string? propertyDescription, decimal? propertyValue, string? titleReferenceNumber, CancellationToken ct)
    {
        if (!await IsAuthorizedForReferenceAsync(reference))
            return RedirectToPage("SessionExpired");

        var result = await _addCollateralHandler.Handle(
            new AddRhshfProfilingCollateralCommand(
                reference, collateralType, referenceNumber, notes,
                guarantorBankName, guaranteeAmount, isUnconditional,
                crgCoveragePercentage, propertyDescription, propertyValue, titleReferenceNumber), ct);

        if (!result.IsSuccess)
            ErrorMessage = result.Error;
        else
            SuccessMessage = "Collateral added.";

        return RedirectToPage(new { reference });
    }

    public async Task<IActionResult> OnPostRemoveCollateralAsync(string reference, Guid collateralId, CancellationToken ct)
    {
        if (!await IsAuthorizedForReferenceAsync(reference))
            return RedirectToPage("SessionExpired");

        var result = await _removeCollateralHandler.Handle(new RemoveRhshfProfilingCollateralCommand(reference, collateralId), ct);
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

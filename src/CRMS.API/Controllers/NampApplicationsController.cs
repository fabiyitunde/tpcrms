using System.Security.Claims;
using CRMS.Application.Namp.Commands;
using CRMS.Application.Namp.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace CRMS.API.Controllers;

[ApiController]
[Route("api/v1/namp/applications")]
[Authorize]
public class NampApplicationsController : ControllerBase
{
    // ── Queries ────────────────────────────────────────────────────────────
    private readonly GetNampStagingQueueHandler _getStagingQueue;
    private readonly GetNampStagingRecordByIdHandler _getStagingById;
    private readonly GetNampApplicationByIdHandler _getAppById;
    private readonly GetNampApplicationsByStatusHandler _getAppsByStatus;

    // ── Commands ───────────────────────────────────────────────────────────
    private readonly RecallNampApplicationHandler _recall;
    private readonly SubmitNampApplicationHandler _submit;
    private readonly SubmitNampTechnicalAppraisalHandler _technicalAppraisal;
    private readonly SubmitNampFinancialAppraisalHandler _financialAppraisal;
    private readonly CirculateNampToCommitteeHandler _circulate;
    private readonly RecordNampCommitteeOutcomeHandler _committeeOutcome;
    private readonly RatifyNampDecisionHandler _ratify;
    private readonly DeclineNampRatificationHandler _declineRatification;
    private readonly RecordNampOfferAcceptanceHandler _offerAcceptance;
    private readonly LapseNampOfferHandler _lapseOffer;
    private readonly BeginNampPreDeploymentVerificationHandler _beginPdv;
    private readonly CompleteNampPreDeploymentVerificationHandler _completePdv;
    private readonly CompleteNampTrainingHandler _completeTraining;
    private readonly ConfirmNampDeploymentHandler _confirmDeployment;
    private readonly UploadNampDocumentHandler _uploadDocument;
    private readonly IConfiguration _config;

    public NampApplicationsController(
        GetNampStagingQueueHandler getStagingQueue,
        GetNampStagingRecordByIdHandler getStagingById,
        GetNampApplicationByIdHandler getAppById,
        GetNampApplicationsByStatusHandler getAppsByStatus,
        RecallNampApplicationHandler recall,
        SubmitNampApplicationHandler submit,
        SubmitNampTechnicalAppraisalHandler technicalAppraisal,
        SubmitNampFinancialAppraisalHandler financialAppraisal,
        CirculateNampToCommitteeHandler circulate,
        RecordNampCommitteeOutcomeHandler committeeOutcome,
        RatifyNampDecisionHandler ratify,
        DeclineNampRatificationHandler declineRatification,
        RecordNampOfferAcceptanceHandler offerAcceptance,
        LapseNampOfferHandler lapseOffer,
        BeginNampPreDeploymentVerificationHandler beginPdv,
        CompleteNampPreDeploymentVerificationHandler completePdv,
        CompleteNampTrainingHandler completeTraining,
        ConfirmNampDeploymentHandler confirmDeployment,
        UploadNampDocumentHandler uploadDocument,
        IConfiguration config)
    {
        _getStagingQueue = getStagingQueue;
        _getStagingById = getStagingById;
        _getAppById = getAppById;
        _getAppsByStatus = getAppsByStatus;
        _recall = recall;
        _submit = submit;
        _technicalAppraisal = technicalAppraisal;
        _financialAppraisal = financialAppraisal;
        _circulate = circulate;
        _committeeOutcome = committeeOutcome;
        _ratify = ratify;
        _declineRatification = declineRatification;
        _offerAcceptance = offerAcceptance;
        _lapseOffer = lapseOffer;
        _beginPdv = beginPdv;
        _completePdv = completePdv;
        _completeTraining = completeTraining;
        _confirmDeployment = confirmDeployment;
        _uploadDocument = uploadDocument;
        _config = config;
    }

    // ── Staging Queue ──────────────────────────────────────────────────────

    [HttpGet("staging")]
    public async Task<IActionResult> GetStagingQueue([FromQuery] Guid? branchId, CancellationToken ct)
    {
        var result = await _getStagingQueue.Handle(new GetNampStagingQueueQuery(branchId), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    [HttpGet("staging/{id:guid}")]
    public async Task<IActionResult> GetStagingRecordById(Guid id, CancellationToken ct)
    {
        var result = await _getStagingById.Handle(new GetNampStagingRecordByIdQuery(id), ct);
        return result.IsSuccess ? Ok(result.Data) : NotFound(result.Error);
    }

    // ── Applications ───────────────────────────────────────────────────────

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _getAppById.Handle(new GetNampApplicationByIdQuery(id), ct);
        return result.IsSuccess ? Ok(result.Data) : NotFound(result.Error);
    }

    [HttpGet]
    public async Task<IActionResult> GetByStatus([FromQuery] string status, [FromQuery] Guid? branchId, CancellationToken ct)
    {
        var result = await _getAppsByStatus.Handle(new GetNampApplicationsByStatusQuery(status, branchId), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    // ── Stage 1: Recall ────────────────────────────────────────────────────

    [HttpPost("recall")]
    public async Task<IActionResult> Recall([FromBody] RecallRequest body, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _recall.Handle(
            new RecallNampApplicationCommand(body.StagingRecordId, userId), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    // ── Stage 1b: Submit ───────────────────────────────────────────────────

    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _submit.Handle(new SubmitNampApplicationCommand(id, userId), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    // ── Stage 2: Technical Appraisal ──────────────────────────────────────

    [HttpPost("{id:guid}/technical-appraisal")]
    public async Task<IActionResult> SubmitTechnicalAppraisal(Guid id, [FromBody] AppraisalRequest body, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _technicalAppraisal.Handle(
            new SubmitNampTechnicalAppraisalCommand(id, userId, body.IsApproved, body.Note), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    // ── Stage 3: Financial Appraisal ──────────────────────────────────────

    [HttpPost("{id:guid}/financial-appraisal")]
    public async Task<IActionResult> SubmitFinancialAppraisal(Guid id, [FromBody] AppraisalRequest body, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _financialAppraisal.Handle(
            new SubmitNampFinancialAppraisalCommand(id, userId, body.IsApproved, body.Note), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    // ── Stage 4a: Circulate to Committee ──────────────────────────────────

    [HttpPost("{id:guid}/circulate")]
    public async Task<IActionResult> CirculateToCommittee(Guid id, [FromBody] CirculateRequest body, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _circulate.Handle(
            new CirculateNampToCommitteeCommand(id, userId, body.Members, body.RequiredVotes, body.MinimumApprovalVotes, body.DeadlineHours), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    // ── Stage 4b: Record Committee Outcome ────────────────────────────────

    [HttpPost("{id:guid}/committee-outcome")]
    public async Task<IActionResult> RecordCommitteeOutcome(Guid id, [FromBody] OutcomeRequest body, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _committeeOutcome.Handle(
            new RecordNampCommitteeOutcomeCommand(id, userId, body.IsApproved, body.Note), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    // ── Stage 5: Ratification ─────────────────────────────────────────────

    [HttpPost("{id:guid}/ratify")]
    public async Task<IActionResult> Ratify(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var bankName = _config["BankSettings:BankName"] ?? "The Bank";
        var branchName = _config["BankSettings:BranchName"] ?? "";
        var result = await _ratify.Handle(
            new RatifyNampDecisionCommand(id, userId, bankName, branchName), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    [HttpPost("{id:guid}/decline-ratification")]
    public async Task<IActionResult> DeclineRatification(Guid id, [FromBody] NoteRequest? body, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _declineRatification.Handle(
            new DeclineNampRatificationCommand(id, userId, body?.Note), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    // ── Stage 5b: Offer ───────────────────────────────────────────────────

    [HttpPost("{id:guid}/accept-offer")]
    public async Task<IActionResult> AcceptOffer(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _offerAcceptance.Handle(new RecordNampOfferAcceptanceCommand(id, userId), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    [HttpPost("{id:guid}/lapse-offer")]
    public async Task<IActionResult> LapseOffer(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _lapseOffer.Handle(new LapseNampOfferCommand(id, userId), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    // ── Stage 6: Pre-Deployment Verification ──────────────────────────────

    [HttpPost("{id:guid}/begin-pdv")]
    public async Task<IActionResult> BeginPreDeploymentVerification(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _beginPdv.Handle(new BeginNampPreDeploymentVerificationCommand(id, userId), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    [HttpPost("{id:guid}/complete-pdv")]
    public async Task<IActionResult> CompletePreDeploymentVerification(Guid id, [FromBody] NoteRequest? body, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _completePdv.Handle(
            new CompleteNampPreDeploymentVerificationCommand(id, userId, body?.Note), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    // ── Stage 7: Training ─────────────────────────────────────────────────

    [HttpPost("{id:guid}/complete-training")]
    public async Task<IActionResult> CompleteTraining(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _completeTraining.Handle(new CompleteNampTrainingCommand(id, userId), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    // ── Stage 8: Deployment ───────────────────────────────────────────────

    [HttpPost("{id:guid}/confirm-deployment")]
    public async Task<IActionResult> ConfirmDeployment(Guid id, [FromBody] DeploymentRequest body, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _confirmDeployment.Handle(
            new ConfirmNampDeploymentCommand(id, userId, body.GpsActivated, body.Note), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    // ── Documents ─────────────────────────────────────────────────────────

    [HttpPost("{id:guid}/documents")]
    public async Task<IActionResult> UploadDocument(Guid id, [FromBody] NampUploadDocumentRequest body, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _uploadDocument.Handle(
            new UploadNampDocumentCommand(id, userId, body.Stage, body.FileName, body.ContentType, body.FileSize, body.StoragePath, body.Description), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }
}

// ── Request models ────────────────────────────────────────────────────────

public record RecallRequest(Guid StagingRecordId);
public record AppraisalRequest(bool IsApproved, string? Note);
public record OutcomeRequest(bool IsApproved, string? Note);
public record NoteRequest(string? Note);
public record DeploymentRequest(bool GpsActivated, string? Note);
public record NampUploadDocumentRequest(string Stage, string FileName, string ContentType, long FileSize, string StoragePath, string? Description);
public record CirculateRequest(
    List<NampCommitteeMemberInput> Members,
    int RequiredVotes,
    int MinimumApprovalVotes,
    int DeadlineHours = 72);

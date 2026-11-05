using System.Security.Claims;
using CRMS.Application.Namp.Commands;
using CRMS.Application.Namp.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using CRMS.Application.CreditBureau.Commands;
using CRMS.Application.Namp.DTOs;

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
    private readonly SubmitNampFinancialAppraisalHandler _financialAppraisal;
    private readonly CirculateNampToCommitteeHandler _circulate;
    private readonly RatifyNampDecisionHandler _ratify;
    private readonly DeclineNampRatificationHandler _declineRatification;
    private readonly RecordNampOfferAcceptanceHandler _offerAcceptance;
    private readonly LapseNampOfferHandler _lapseOffer;
    private readonly BeginNampPreDeploymentVerificationHandler _beginPdv;
    private readonly CompleteNampPreDeploymentVerificationHandler _completePdv;
    private readonly ConfirmNampDeploymentHandler _confirmDeployment;
    private readonly UploadNampDocumentHandler _uploadDocument;
    private readonly CRMS.Application.CreditBureau.Commands.ProcessNampCreditChecksHandler _processCreditChecks;
    private readonly IConfiguration _config;

    public NampApplicationsController(
        GetNampStagingQueueHandler getStagingQueue,
        GetNampStagingRecordByIdHandler getStagingById,
        GetNampApplicationByIdHandler getAppById,
        GetNampApplicationsByStatusHandler getAppsByStatus,
        RecallNampApplicationHandler recall,
        SubmitNampApplicationHandler submit,
        SubmitNampFinancialAppraisalHandler financialAppraisal,
        CirculateNampToCommitteeHandler circulate,
        RatifyNampDecisionHandler ratify,
        DeclineNampRatificationHandler declineRatification,
        RecordNampOfferAcceptanceHandler offerAcceptance,
        LapseNampOfferHandler lapseOffer,
        BeginNampPreDeploymentVerificationHandler beginPdv,
        CompleteNampPreDeploymentVerificationHandler completePdv,
        ConfirmNampDeploymentHandler confirmDeployment,
        UploadNampDocumentHandler uploadDocument,
        CRMS.Application.CreditBureau.Commands.ProcessNampCreditChecksHandler processCreditChecks,
        IConfiguration config)
    {
        _getStagingQueue = getStagingQueue;
        _getStagingById = getStagingById;
        _getAppById = getAppById;
        _getAppsByStatus = getAppsByStatus;
        _recall = recall;
        _submit = submit;
        _financialAppraisal = financialAppraisal;
        _circulate = circulate;
        _ratify = ratify;
        _declineRatification = declineRatification;
        _offerAcceptance = offerAcceptance;
        _lapseOffer = lapseOffer;
        _beginPdv = beginPdv;
        _completePdv = completePdv;
        _confirmDeployment = confirmDeployment;
        _uploadDocument = uploadDocument;
        _processCreditChecks = processCreditChecks;
        _config = config;
    }

    // ── Staging Queue ──────────────────────────────────────────────────────

    /// <summary>List the NAMP applications waiting in the staging queue.</summary>
    /// <remarks>An application the portal sends lands here first, and stays until a loan officer recalls it.</remarks>
    [HttpGet("staging")]
    [ProducesResponseType(typeof(List<NampStagingRecordSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetStagingQueue([FromQuery] Guid? branchId, CancellationToken ct)
    {
        var result = await _getStagingQueue.Handle(new GetNampStagingQueueQuery(branchId), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>Get one staged NAMP application as the portal sent it.</summary>
    [HttpGet("staging/{id:guid}")]
    [ProducesResponseType(typeof(NampStagingRecordDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStagingRecordById(Guid id, CancellationToken ct)
    {
        var result = await _getStagingById.Handle(new GetNampStagingRecordByIdQuery(id), ct);
        return result.IsSuccess ? Ok(result.Data) : NotFound(result.Error);
    }

    // ── Applications ───────────────────────────────────────────────────────

    /// <summary>Get one NAMP application.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(NampApplicationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _getAppById.Handle(new GetNampApplicationByIdQuery(id), ct);
        return result.IsSuccess ? Ok(result.Data) : NotFound(result.Error);
    }

    /// <summary>List NAMP applications, optionally only those in a status.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<NampApplicationSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetByStatus([FromQuery] string status, [FromQuery] Guid? branchId, CancellationToken ct)
    {
        var result = await _getAppsByStatus.Handle(new GetNampApplicationsByStatusQuery(status, branchId), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    // ── Stage 1: Recall ────────────────────────────────────────────────────

    /// <summary>Recall a staged application into the CRMS as a NAMP application.</summary>
    [HttpPost("recall")]
    [ProducesResponseType(typeof(NampApplicationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Recall([FromBody] RecallRequest body, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _recall.Handle(
            new RecallNampApplicationCommand(body.StagingRecordId, userId), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    // ── Stage 1b: Submit ───────────────────────────────────────────────────

    /// <summary>Submit a recalled NAMP application for appraisal.</summary>
    [HttpPost("{id:guid}/submit")]
    [ProducesResponseType(typeof(NampApplicationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _submit.Handle(new SubmitNampApplicationCommand(id, userId), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    // ── Stage 2: Financial Appraisal Decision ─────────────────────────────

    /// <summary>Record the financial appraisal of a NAMP application.</summary>
    [HttpPost("{id:guid}/financial-appraisal")]
    [ProducesResponseType(typeof(NampApplicationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitFinancialAppraisal(Guid id, [FromBody] AppraisalRequest body, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _financialAppraisal.Handle(
            new SubmitNampFinancialAppraisalCommand(id, userId, body.IsApproved, body.Note), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    // ── Stage 4a: Circulate to Committee ──────────────────────────────────

    /// <summary>Send a NAMP application to its committee.</summary>
    [HttpPost("{id:guid}/circulate")]
    [ProducesResponseType(typeof(NampApplicationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CirculateToCommittee(Guid id, [FromBody] CirculateRequest body, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _circulate.Handle(
            new CirculateNampToCommitteeCommand(id, userId, body.Members, body.RequiredVotes, body.MinimumApprovalVotes, body.DeadlineHours), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    // ── Stage 5: Ratification ─────────────────────────────────────────────

    /// <summary>Ratify a committee-approved NAMP application.</summary>
    [HttpPost("{id:guid}/ratify")]
    [ProducesResponseType(typeof(NampApplicationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Ratify(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var bankName = _config["BankSettings:BankName"] ?? "The Bank";
        var branchName = _config["BankSettings:BranchName"] ?? "";
        var result = await _ratify.Handle(
            new RatifyNampDecisionCommand(id, userId, bankName, branchName), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>Decline to ratify a NAMP application.</summary>
    [HttpPost("{id:guid}/decline-ratification")]
    [ProducesResponseType(typeof(NampApplicationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeclineRatification(Guid id, [FromBody] NoteRequest? body, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _declineRatification.Handle(
            new DeclineNampRatificationCommand(id, userId, body?.Note), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    // ── Stage 5b: Offer ───────────────────────────────────────────────────

    /// <summary>Record that the applicant accepted the offer.</summary>
    [HttpPost("{id:guid}/accept-offer")]
    [ProducesResponseType(typeof(NampApplicationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AcceptOffer(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _offerAcceptance.Handle(new RecordNampOfferAcceptanceCommand(id, userId), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>Record that the offer lapsed without being accepted.</summary>
    [HttpPost("{id:guid}/lapse-offer")]
    [ProducesResponseType(typeof(NampApplicationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LapseOffer(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _lapseOffer.Handle(new LapseNampOfferCommand(id, userId), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    // ── Stage 6: Pre-Deployment Verification ──────────────────────────────

    /// <summary>Start pre-deployment verification.</summary>
    [HttpPost("{id:guid}/begin-pdv")]
    [ProducesResponseType(typeof(NampApplicationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> BeginPreDeploymentVerification(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _beginPdv.Handle(new BeginNampPreDeploymentVerificationCommand(id, userId), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>Complete pre-deployment verification.</summary>
    [HttpPost("{id:guid}/complete-pdv")]
    [ProducesResponseType(typeof(NampApplicationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CompletePreDeploymentVerification(Guid id, [FromBody] NoteRequest? body, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _completePdv.Handle(
            new CompleteNampPreDeploymentVerificationCommand(id, userId, body?.Note), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    // ── Stage 7: Deployment ───────────────────────────────────────────────

    /// <summary>Confirm that the equipment has been deployed.</summary>
    [HttpPost("{id:guid}/confirm-deployment")]
    [ProducesResponseType(typeof(NampApplicationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmDeployment(Guid id, [FromBody] DeploymentRequest body, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _confirmDeployment.Handle(
            new ConfirmNampDeploymentCommand(id, userId, body.GpsActivated, body.Note), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    // ── Credit Bureau Checks ──────────────────────────────────────────────

    /// <summary>Run credit bureau checks on the parties to a NAMP application.</summary>
    /// <remarks>Each party checked is a billed bureau call.</remarks>
    [HttpPost("{id:guid}/run-credit-checks")]
    [ProducesResponseType(typeof(NampCreditCheckBatchResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RunCreditChecks(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _processCreditChecks.Handle(
            new CRMS.Application.CreditBureau.Commands.ProcessNampCreditChecksCommand(id, userId), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    // ── Documents ─────────────────────────────────────────────────────────

    /// <summary>Attach a document to a NAMP application.</summary>
    [HttpPost("{id:guid}/documents")]
    [ProducesResponseType(typeof(NampApplicationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadDocument(Guid id, [FromBody] NampUploadDocumentRequest body, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _uploadDocument.Handle(
            new UploadNampDocumentCommand(id, userId, body.Stage, body.FileName, body.ContentType, body.FileSize, body.StoragePath, body.Category, body.Description), ct);
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
public record NoteRequest(string? Note);
public record DeploymentRequest(bool GpsActivated, string? Note);
public record NampUploadDocumentRequest(string Stage, string FileName, string ContentType, long FileSize, string StoragePath, string Category = "General", string? Description = null);
public record CirculateRequest(
    List<NampCommitteeMemberInput> Members,
    int RequiredVotes,
    int MinimumApprovalVotes,
    int DeadlineHours = 72);

using CRMS.Application.Common;
using CRMS.Application.LoanApplication.Commands;
using CRMS.Application.LoanApplication.DTOs;
using CRMS.Application.LoanApplication.Queries;
using CRMS.Domain.Enums;
using CRMS.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRMS.API.Controllers;

[ApiController]
[Route("api/v1/loan-applications")]
[Authorize]
public class LoanApplicationsController : ControllerBase
{
    private readonly IRequestHandler<InitiateCorporateLoanCommand, ApplicationResult<LoanApplicationDto>> _initiateHandler;
    private readonly IRequestHandler<SubmitLoanApplicationCommand, ApplicationResult> _submitHandler;
    private readonly IRequestHandler<ApproveBranchCommand, ApplicationResult> _approveBranchHandler;
    private readonly IRequestHandler<ReturnFromBranchCommand, ApplicationResult> _returnBranchHandler;
    private readonly IRequestHandler<UploadDocumentCommand, ApplicationResult<LoanApplicationDocumentDto>> _uploadDocHandler;
    private readonly IRequestHandler<VerifyDocumentCommand, ApplicationResult> _verifyDocHandler;
    private readonly IRequestHandler<GetLoanApplicationByIdQuery, ApplicationResult<LoanApplicationDto>> _getByIdHandler;
    private readonly IRequestHandler<GetLoanApplicationByNumberQuery, ApplicationResult<LoanApplicationDto>> _getByNumberHandler;
    private readonly IRequestHandler<GetLoanApplicationsByStatusQuery, ApplicationResult<List<LoanApplicationSummaryDto>>> _getByStatusHandler;
    private readonly IRequestHandler<GetMyLoanApplicationsQuery, ApplicationResult<List<LoanApplicationSummaryDto>>> _getMyAppsHandler;
    private readonly IRequestHandler<GetPendingBranchReviewQuery, ApplicationResult<List<LoanApplicationSummaryDto>>> _getPendingBranchHandler;

    public LoanApplicationsController(
        IRequestHandler<InitiateCorporateLoanCommand, ApplicationResult<LoanApplicationDto>> initiateHandler,
        IRequestHandler<SubmitLoanApplicationCommand, ApplicationResult> submitHandler,
        IRequestHandler<ApproveBranchCommand, ApplicationResult> approveBranchHandler,
        IRequestHandler<ReturnFromBranchCommand, ApplicationResult> returnBranchHandler,
        IRequestHandler<UploadDocumentCommand, ApplicationResult<LoanApplicationDocumentDto>> uploadDocHandler,
        IRequestHandler<VerifyDocumentCommand, ApplicationResult> verifyDocHandler,
        IRequestHandler<GetLoanApplicationByIdQuery, ApplicationResult<LoanApplicationDto>> getByIdHandler,
        IRequestHandler<GetLoanApplicationByNumberQuery, ApplicationResult<LoanApplicationDto>> getByNumberHandler,
        IRequestHandler<GetLoanApplicationsByStatusQuery, ApplicationResult<List<LoanApplicationSummaryDto>>> getByStatusHandler,
        IRequestHandler<GetMyLoanApplicationsQuery, ApplicationResult<List<LoanApplicationSummaryDto>>> getMyAppsHandler,
        IRequestHandler<GetPendingBranchReviewQuery, ApplicationResult<List<LoanApplicationSummaryDto>>> getPendingBranchHandler)
    {
        _initiateHandler = initiateHandler;
        _submitHandler = submitHandler;
        _approveBranchHandler = approveBranchHandler;
        _returnBranchHandler = returnBranchHandler;
        _uploadDocHandler = uploadDocHandler;
        _verifyDocHandler = verifyDocHandler;
        _getByIdHandler = getByIdHandler;
        _getByNumberHandler = getByNumberHandler;
        _getByStatusHandler = getByStatusHandler;
        _getMyAppsHandler = getMyAppsHandler;
        _getPendingBranchHandler = getPendingBranchHandler;
    }

    /// <summary>Start a corporate loan application.</summary>
    /// <remarks>Creates the application as a draft for the customer on the given account, pulling the company record from core banking.</remarks>
    [HttpPost("corporate")]
    [ProducesResponseType(typeof(LoanApplicationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> InitiateCorporateLoan([FromBody] InitiateCorporateLoanRequest request, CancellationToken ct)
    {
        var command = new InitiateCorporateLoanCommand(
            request.LoanProductId,
            request.ProductCode,
            request.AccountNumber,
            request.RequestedAmount,
            request.Currency,
            request.RequestedTenorMonths,
            request.InterestRatePerAnnum,
            request.InterestRateType,
            request.InitiatedByUserId,
            request.BranchId,
            request.Purpose
        );

        var result = await _initiateHandler.Handle(command, ct);
        return result.IsSuccess ? CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data) : BadRequest(result.Error);
    }

    /// <summary>Submit a draft application for branch review.</summary>
    [HttpPost("{id}/submit")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Submit(Guid id, [FromBody] SubmitRequest request, CancellationToken ct)
    {
        var result = await _submitHandler.Handle(new SubmitLoanApplicationCommand(id, request.UserId), ct);
        return result.IsSuccess ? Ok() : BadRequest(result.Error);
    }

    /// <summary>Approve an application at branch level.</summary>
    /// <remarks>Moves it on to head office and queues the credit bureau checks on its parties.</remarks>
    [HttpPost("{id}/branch-approve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ApproveBranch(Guid id, [FromBody] BranchApprovalRequest request, CancellationToken ct)
    {
        var result = await _approveBranchHandler.Handle(new ApproveBranchCommand(id, request.UserId, request.Comment), ct);
        return result.IsSuccess ? Ok() : BadRequest(result.Error);
    }

    /// <summary>Return an application from branch review to its loan officer, with the reason.</summary>
    [HttpPost("{id}/branch-return")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReturnFromBranch(Guid id, [FromBody] BranchReturnRequest request, CancellationToken ct)
    {
        var result = await _returnBranchHandler.Handle(new ReturnFromBranchCommand(id, request.UserId, request.Reason), ct);
        return result.IsSuccess ? Ok() : BadRequest(result.Error);
    }

    /// <summary>Attach a document to an application.</summary>
    [HttpPost("{id}/documents")]
    [ProducesResponseType(typeof(LoanApplicationDocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadDocument(Guid id, [FromBody] UploadDocumentRequest request, CancellationToken ct)
    {
        var command = new UploadDocumentCommand(
            id,
            request.Category,
            request.FileName,
            request.FilePath,
            request.FileSize,
            request.ContentType,
            request.UploadedByUserId,
            request.Description
        );

        var result = await _uploadDocHandler.Handle(command, ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>Mark one of an application's documents as verified.</summary>
    [HttpPost("{id}/documents/{documentId}/verify")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyDocument(Guid id, Guid documentId, [FromBody] VerifyDocumentRequest request, CancellationToken ct)
    {
        var result = await _verifyDocHandler.Handle(new VerifyDocumentCommand(id, documentId, request.UserId), ct);
        return result.IsSuccess ? Ok() : BadRequest(result.Error);
    }

    /// <summary>Get one loan application with its parties and documents.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(LoanApplicationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _getByIdHandler.Handle(new GetLoanApplicationByIdQuery(id), ct);
        return result.IsSuccess ? Ok(result.Data) : NotFound(result.Error);
    }

    /// <summary>Get a loan application by its application number.</summary>
    [HttpGet("by-number/{applicationNumber}")]
    [ProducesResponseType(typeof(LoanApplicationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByNumber(string applicationNumber, CancellationToken ct)
    {
        var result = await _getByNumberHandler.Handle(new GetLoanApplicationByNumberQuery(applicationNumber), ct);
        return result.IsSuccess ? Ok(result.Data) : NotFound(result.Error);
    }

    /// <summary>List the loan applications in a status.</summary>
    [HttpGet("by-status/{status}")]
    [ProducesResponseType(typeof(List<LoanApplicationSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetByStatus(LoanApplicationStatus status, CancellationToken ct)
    {
        var result = await _getByStatusHandler.Handle(new GetLoanApplicationsByStatusQuery(status), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>List the applications the signed-in user initiated.</summary>
    [HttpGet("my-applications")]
    [ProducesResponseType(typeof(List<LoanApplicationSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMyApplications([FromQuery] Guid userId, CancellationToken ct)
    {
        var result = await _getMyAppsHandler.Handle(new GetMyLoanApplicationsQuery(userId), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>List the applications waiting for branch review.</summary>
    [HttpGet("pending-branch-review")]
    [ProducesResponseType(typeof(List<LoanApplicationSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetPendingBranchReview([FromQuery] Guid? branchId, CancellationToken ct)
    {
        // Extract user location and role from JWT claims for visibility filtering
        var userLocationIdClaim = User.FindFirst("LocationId")?.Value;
        var userLocationId = Guid.TryParse(userLocationIdClaim, out var locId) ? locId : (Guid?)null;
        var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        
        var result = await _getPendingBranchHandler.Handle(
            new GetPendingBranchReviewQuery(branchId, userLocationId, userRole), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }
}

// Request DTOs
public record InitiateCorporateLoanRequest(
    Guid LoanProductId,
    string ProductCode,
    string AccountNumber,
    decimal RequestedAmount,
    string Currency,
    int RequestedTenorMonths,
    decimal InterestRatePerAnnum,
    InterestRateType InterestRateType,
    Guid InitiatedByUserId,
    Guid? BranchId,
    string? Purpose
);

public record SubmitRequest(Guid UserId);
public record BranchApprovalRequest(Guid UserId, string? Comment);
public record BranchReturnRequest(Guid UserId, string Reason);

public record UploadDocumentRequest(
    DocumentCategory Category,
    string FileName,
    string FilePath,
    long FileSize,
    string ContentType,
    Guid UploadedByUserId,
    string? Description
);

public record VerifyDocumentRequest(Guid UserId);

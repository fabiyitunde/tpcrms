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

    [HttpPost("corporate")]
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

    [HttpPost("{id}/submit")]
    public async Task<IActionResult> Submit(Guid id, [FromBody] SubmitRequest request, CancellationToken ct)
    {
        var result = await _submitHandler.Handle(new SubmitLoanApplicationCommand(id, request.UserId), ct);
        return result.IsSuccess ? Ok() : BadRequest(result.Error);
    }

    [HttpPost("{id}/branch-approve")]
    public async Task<IActionResult> ApproveBranch(Guid id, [FromBody] BranchApprovalRequest request, CancellationToken ct)
    {
        var result = await _approveBranchHandler.Handle(new ApproveBranchCommand(id, request.UserId, request.Comment), ct);
        return result.IsSuccess ? Ok() : BadRequest(result.Error);
    }

    [HttpPost("{id}/branch-return")]
    public async Task<IActionResult> ReturnFromBranch(Guid id, [FromBody] BranchReturnRequest request, CancellationToken ct)
    {
        var result = await _returnBranchHandler.Handle(new ReturnFromBranchCommand(id, request.UserId, request.Reason), ct);
        return result.IsSuccess ? Ok() : BadRequest(result.Error);
    }

    [HttpPost("{id}/documents")]
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

    [HttpPost("{id}/documents/{documentId}/verify")]
    public async Task<IActionResult> VerifyDocument(Guid id, Guid documentId, [FromBody] VerifyDocumentRequest request, CancellationToken ct)
    {
        var result = await _verifyDocHandler.Handle(new VerifyDocumentCommand(id, documentId, request.UserId), ct);
        return result.IsSuccess ? Ok() : BadRequest(result.Error);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _getByIdHandler.Handle(new GetLoanApplicationByIdQuery(id), ct);
        return result.IsSuccess ? Ok(result.Data) : NotFound(result.Error);
    }

    [HttpGet("by-number/{applicationNumber}")]
    public async Task<IActionResult> GetByNumber(string applicationNumber, CancellationToken ct)
    {
        var result = await _getByNumberHandler.Handle(new GetLoanApplicationByNumberQuery(applicationNumber), ct);
        return result.IsSuccess ? Ok(result.Data) : NotFound(result.Error);
    }

    [HttpGet("by-status/{status}")]
    public async Task<IActionResult> GetByStatus(LoanApplicationStatus status, CancellationToken ct)
    {
        var result = await _getByStatusHandler.Handle(new GetLoanApplicationsByStatusQuery(status), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    [HttpGet("my-applications")]
    public async Task<IActionResult> GetMyApplications([FromQuery] Guid userId, CancellationToken ct)
    {
        var result = await _getMyAppsHandler.Handle(new GetMyLoanApplicationsQuery(userId), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    [HttpGet("pending-branch-review")]
    public async Task<IActionResult> GetPendingBranchReview([FromQuery] Guid? branchId, CancellationToken ct)
    {
        var result = await _getPendingBranchHandler.Handle(new GetPendingBranchReviewQuery(branchId), ct);
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

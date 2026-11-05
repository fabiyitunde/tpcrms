using CRMS.Application.Common;
using CRMS.Application.CreditBureau.Commands;
using CRMS.Application.CreditBureau.DTOs;
using CRMS.Application.CreditBureau.Interfaces;
using CRMS.Application.CreditBureau.Queries;
using CRMS.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CRMS.API.OpenApi;

namespace CRMS.API.Controllers;

[ApiController]
[Route("api/v1/credit-bureau")]
[Authorize]
public class CreditBureauController : ControllerBase
{
    private readonly IRequestHandler<RequestBureauReportCommand, ApplicationResult<BureauReportDto>> _requestReportHandler;
    private readonly IRequestHandler<GetBureauReportByIdQuery, ApplicationResult<BureauReportDto>> _getByIdHandler;
    private readonly IRequestHandler<GetBureauReportsByLoanApplicationQuery, ApplicationResult<List<BureauReportSummaryDto>>> _getByLoanAppHandler;
    private readonly IRequestHandler<SearchBureauByBVNQuery, ApplicationResult<BureauSearchResultDto>> _searchHandler;
    private readonly IRequestHandler<ProcessLoanCreditChecksCommand, ApplicationResult<CreditCheckBatchResultDto>> _processLoanChecksHandler;
    private readonly ICreditCheckQueue _creditCheckQueue;

    public CreditBureauController(
        IRequestHandler<RequestBureauReportCommand, ApplicationResult<BureauReportDto>> requestReportHandler,
        IRequestHandler<GetBureauReportByIdQuery, ApplicationResult<BureauReportDto>> getByIdHandler,
        IRequestHandler<GetBureauReportsByLoanApplicationQuery, ApplicationResult<List<BureauReportSummaryDto>>> getByLoanAppHandler,
        IRequestHandler<SearchBureauByBVNQuery, ApplicationResult<BureauSearchResultDto>> searchHandler,
        IRequestHandler<ProcessLoanCreditChecksCommand, ApplicationResult<CreditCheckBatchResultDto>> processLoanChecksHandler,
        ICreditCheckQueue creditCheckQueue)
    {
        _requestReportHandler = requestReportHandler;
        _getByIdHandler = getByIdHandler;
        _getByLoanAppHandler = getByLoanAppHandler;
        _searchHandler = searchHandler;
        _processLoanChecksHandler = processLoanChecksHandler;
        _creditCheckQueue = creditCheckQueue;
    }

    /// <summary>Request a credit bureau report for a person by BVN.</summary>
    /// <remarks>This calls the bureau and is billed per report.</remarks>
    [HttpPost("reports")]
    [ProducesResponseType(typeof(BureauReportDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RequestReport([FromBody] RequestBureauReportRequest request, CancellationToken ct)
    {
        var command = new RequestBureauReportCommand(
            request.BVN,
            request.SubjectName,
            request.RequestedByUserId,
            request.Provider,
            request.LoanApplicationId,
            request.IncludePdf
        );

        var result = await _requestReportHandler.Handle(command, ct);
        return result.IsSuccess ? CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data) : BadRequest(result.Error);
    }

    /// <summary>Get one bureau report.</summary>
    [HttpGet("reports/{id}")]
    [ProducesResponseType(typeof(BureauReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _getByIdHandler.Handle(new GetBureauReportByIdQuery(id), ct);
        return result.IsSuccess ? Ok(result.Data) : NotFound(result.Error);
    }

    /// <summary>List the bureau reports on a loan application.</summary>
    [HttpGet("reports/by-loan-application/{loanApplicationId}")]
    [ProducesResponseType(typeof(List<BureauReportSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetByLoanApplication(Guid loanApplicationId, CancellationToken ct)
    {
        var result = await _getByLoanAppHandler.Handle(new GetBureauReportsByLoanApplicationQuery(loanApplicationId), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>Search the credit bureau for a person by BVN.</summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(BureauSearchResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SearchByBVN([FromQuery] string bvn, CancellationToken ct)
    {
        var result = await _searchHandler.Handle(new SearchBureauByBVNQuery(bvn), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>
    /// Process all credit checks for a loan application synchronously.
    /// Use this for manual re-run or debugging. Normally checks are processed automatically.
    /// </summary>
    [HttpPost("loan-applications/{loanApplicationId}/process")]
    [ProducesResponseType(typeof(CreditCheckBatchResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ProcessLoanCreditChecks(Guid loanApplicationId, [FromBody] ProcessCreditChecksRequest request, CancellationToken ct)
    {
        var command = new ProcessLoanCreditChecksCommand(loanApplicationId, request.UserId);
        var result = await _processLoanChecksHandler.Handle(command, ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>
    /// Queue credit checks for a loan application to be processed in background.
    /// </summary>
    [HttpPost("loan-applications/{loanApplicationId}/queue")]
    [ProducesResponseType(typeof(CreditChecksQueued), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> QueueLoanCreditChecks(Guid loanApplicationId, [FromBody] ProcessCreditChecksRequest request, CancellationToken ct)
    {
        await _creditCheckQueue.QueueCreditCheckAsync(loanApplicationId, request.UserId, ct);
        return Accepted(new { Message = "Credit checks queued for processing", LoanApplicationId = loanApplicationId });
    }
}

public record RequestBureauReportRequest(
    string BVN,
    string SubjectName,
    Guid RequestedByUserId,
    CreditBureauProvider Provider = CreditBureauProvider.CreditRegistry,
    Guid? LoanApplicationId = null,
    bool IncludePdf = false
);

public record ProcessCreditChecksRequest(Guid UserId);

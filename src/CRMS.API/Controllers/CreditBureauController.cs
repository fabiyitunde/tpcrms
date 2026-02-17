using CRMS.Application.Common;
using CRMS.Application.CreditBureau.Commands;
using CRMS.Application.CreditBureau.DTOs;
using CRMS.Application.CreditBureau.Queries;
using CRMS.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

    public CreditBureauController(
        IRequestHandler<RequestBureauReportCommand, ApplicationResult<BureauReportDto>> requestReportHandler,
        IRequestHandler<GetBureauReportByIdQuery, ApplicationResult<BureauReportDto>> getByIdHandler,
        IRequestHandler<GetBureauReportsByLoanApplicationQuery, ApplicationResult<List<BureauReportSummaryDto>>> getByLoanAppHandler,
        IRequestHandler<SearchBureauByBVNQuery, ApplicationResult<BureauSearchResultDto>> searchHandler)
    {
        _requestReportHandler = requestReportHandler;
        _getByIdHandler = getByIdHandler;
        _getByLoanAppHandler = getByLoanAppHandler;
        _searchHandler = searchHandler;
    }

    [HttpPost("reports")]
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

    [HttpGet("reports/{id}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _getByIdHandler.Handle(new GetBureauReportByIdQuery(id), ct);
        return result.IsSuccess ? Ok(result.Data) : NotFound(result.Error);
    }

    [HttpGet("reports/by-loan-application/{loanApplicationId}")]
    public async Task<IActionResult> GetByLoanApplication(Guid loanApplicationId, CancellationToken ct)
    {
        var result = await _getByLoanAppHandler.Handle(new GetBureauReportsByLoanApplicationQuery(loanApplicationId), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchByBVN([FromQuery] string bvn, CancellationToken ct)
    {
        var result = await _searchHandler.Handle(new SearchBureauByBVNQuery(bvn), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
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

using CRMS.Application.Advisory.Commands;
using CRMS.Application.Advisory.DTOs;
using CRMS.Application.Advisory.Queries;
using CRMS.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRMS.API.Controllers;

[ApiController]
[Route("api/v1/advisory")]
[Authorize]
public class AdvisoryController : ControllerBase
{
    private readonly IRequestHandler<GenerateCreditAdvisoryCommand, ApplicationResult<CreditAdvisoryDto>> _generateHandler;
    private readonly IRequestHandler<GetCreditAdvisoryByIdQuery, ApplicationResult<CreditAdvisoryDto>> _getByIdHandler;
    private readonly IRequestHandler<GetLatestAdvisoryByLoanApplicationQuery, ApplicationResult<CreditAdvisoryDto>> _getLatestHandler;
    private readonly IRequestHandler<GetAdvisoryHistoryByLoanApplicationQuery, ApplicationResult<List<CreditAdvisorySummaryDto>>> _getHistoryHandler;
    private readonly IRequestHandler<GetScoreMatrixQuery, ApplicationResult<ScoreMatrixDto>> _getScoreMatrixHandler;

    public AdvisoryController(
        IRequestHandler<GenerateCreditAdvisoryCommand, ApplicationResult<CreditAdvisoryDto>> generateHandler,
        IRequestHandler<GetCreditAdvisoryByIdQuery, ApplicationResult<CreditAdvisoryDto>> getByIdHandler,
        IRequestHandler<GetLatestAdvisoryByLoanApplicationQuery, ApplicationResult<CreditAdvisoryDto>> getLatestHandler,
        IRequestHandler<GetAdvisoryHistoryByLoanApplicationQuery, ApplicationResult<List<CreditAdvisorySummaryDto>>> getHistoryHandler,
        IRequestHandler<GetScoreMatrixQuery, ApplicationResult<ScoreMatrixDto>> getScoreMatrixHandler)
    {
        _generateHandler = generateHandler;
        _getByIdHandler = getByIdHandler;
        _getLatestHandler = getLatestHandler;
        _getHistoryHandler = getHistoryHandler;
        _getScoreMatrixHandler = getScoreMatrixHandler;
    }

    /// <summary>
    /// Generate a new AI-powered credit advisory for a loan application.
    /// </summary>
    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateAdvisoryRequest request, CancellationToken ct)
    {
        var command = new GenerateCreditAdvisoryCommand(request.LoanApplicationId, request.GeneratedByUserId);
        var result = await _generateHandler.Handle(command, ct);
        
        return result.IsSuccess 
            ? CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data) 
            : BadRequest(result.Error);
    }

    /// <summary>
    /// Get a credit advisory by ID.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _getByIdHandler.Handle(new GetCreditAdvisoryByIdQuery(id), ct);
        return result.IsSuccess ? Ok(result.Data) : NotFound(result.Error);
    }

    /// <summary>
    /// Get the latest completed credit advisory for a loan application.
    /// </summary>
    [HttpGet("by-loan-application/{loanApplicationId}/latest")]
    public async Task<IActionResult> GetLatestByLoanApplication(Guid loanApplicationId, CancellationToken ct)
    {
        var result = await _getLatestHandler.Handle(new GetLatestAdvisoryByLoanApplicationQuery(loanApplicationId), ct);
        return result.IsSuccess ? Ok(result.Data) : NotFound(result.Error);
    }

    /// <summary>
    /// Get all credit advisories for a loan application (history).
    /// </summary>
    [HttpGet("by-loan-application/{loanApplicationId}/history")]
    public async Task<IActionResult> GetHistoryByLoanApplication(Guid loanApplicationId, CancellationToken ct)
    {
        var result = await _getHistoryHandler.Handle(new GetAdvisoryHistoryByLoanApplicationQuery(loanApplicationId), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>
    /// Get the score matrix for a specific advisory.
    /// </summary>
    [HttpGet("{id}/score-matrix")]
    public async Task<IActionResult> GetScoreMatrix(Guid id, CancellationToken ct)
    {
        var result = await _getScoreMatrixHandler.Handle(new GetScoreMatrixQuery(id), ct);
        return result.IsSuccess ? Ok(result.Data) : NotFound(result.Error);
    }
}

public record GenerateAdvisoryRequest(Guid LoanApplicationId, Guid GeneratedByUserId);

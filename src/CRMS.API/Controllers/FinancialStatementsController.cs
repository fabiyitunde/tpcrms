using CRMS.Application.Common;
using CRMS.Application.FinancialAnalysis.Commands;
using CRMS.Application.FinancialAnalysis.DTOs;
using CRMS.Application.FinancialAnalysis.Queries;
using CRMS.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRMS.API.Controllers;

[ApiController]
[Route("api/v1/financial-statements")]
[Authorize]
public class FinancialStatementsController : ControllerBase
{
    private readonly IRequestHandler<CreateFinancialStatementCommand, ApplicationResult<FinancialStatementDto>> _createHandler;
    private readonly IRequestHandler<SetBalanceSheetCommand, ApplicationResult<FinancialStatementDto>> _setBalanceSheetHandler;
    private readonly IRequestHandler<SetIncomeStatementCommand, ApplicationResult<FinancialStatementDto>> _setIncomeStatementHandler;
    private readonly IRequestHandler<SetCashFlowStatementCommand, ApplicationResult<FinancialStatementDto>> _setCashFlowHandler;
    private readonly IRequestHandler<SubmitFinancialStatementCommand, ApplicationResult<FinancialStatementDto>> _submitHandler;
    private readonly IRequestHandler<VerifyFinancialStatementCommand, ApplicationResult<FinancialStatementDto>> _verifyHandler;
    private readonly IRequestHandler<GetFinancialStatementByIdQuery, ApplicationResult<FinancialStatementDto>> _getByIdHandler;
    private readonly IRequestHandler<GetFinancialStatementsByLoanApplicationQuery, ApplicationResult<List<FinancialStatementSummaryDto>>> _getByLoanAppHandler;
    private readonly IRequestHandler<GetFinancialRatiosTrendQuery, ApplicationResult<FinancialRatiosTrendDto>> _getTrendHandler;

    public FinancialStatementsController(
        IRequestHandler<CreateFinancialStatementCommand, ApplicationResult<FinancialStatementDto>> createHandler,
        IRequestHandler<SetBalanceSheetCommand, ApplicationResult<FinancialStatementDto>> setBalanceSheetHandler,
        IRequestHandler<SetIncomeStatementCommand, ApplicationResult<FinancialStatementDto>> setIncomeStatementHandler,
        IRequestHandler<SetCashFlowStatementCommand, ApplicationResult<FinancialStatementDto>> setCashFlowHandler,
        IRequestHandler<SubmitFinancialStatementCommand, ApplicationResult<FinancialStatementDto>> submitHandler,
        IRequestHandler<VerifyFinancialStatementCommand, ApplicationResult<FinancialStatementDto>> verifyHandler,
        IRequestHandler<GetFinancialStatementByIdQuery, ApplicationResult<FinancialStatementDto>> getByIdHandler,
        IRequestHandler<GetFinancialStatementsByLoanApplicationQuery, ApplicationResult<List<FinancialStatementSummaryDto>>> getByLoanAppHandler,
        IRequestHandler<GetFinancialRatiosTrendQuery, ApplicationResult<FinancialRatiosTrendDto>> getTrendHandler)
    {
        _createHandler = createHandler;
        _setBalanceSheetHandler = setBalanceSheetHandler;
        _setIncomeStatementHandler = setIncomeStatementHandler;
        _setCashFlowHandler = setCashFlowHandler;
        _submitHandler = submitHandler;
        _verifyHandler = verifyHandler;
        _getByIdHandler = getByIdHandler;
        _getByLoanAppHandler = getByLoanAppHandler;
        _getTrendHandler = getTrendHandler;
    }

    /// <summary>Start a financial statement for a year on a loan application.</summary>
    /// <remarks>Creates the statement empty. Enter its balance sheet, income statement and cash flow statement with the three PUT operations, then submit it.</remarks>
    [HttpPost]
    [ProducesResponseType(typeof(FinancialStatementDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateFinancialStatementRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<FinancialYearType>(request.YearType, true, out var yearType))
            return BadRequest("Invalid year type");

        if (!Enum.TryParse<InputMethod>(request.InputMethod, true, out var inputMethod))
            return BadRequest("Invalid input method");

        var command = new CreateFinancialStatementCommand(
            request.LoanApplicationId,
            request.FinancialYear,
            request.YearEndDate,
            yearType,
            inputMethod,
            request.SubmittedByUserId,
            request.Currency ?? "NGN",
            request.AuditorName,
            request.AuditorFirm,
            request.AuditDate,
            request.AuditOpinion,
            request.OriginalFileName,
            request.FilePath
        );

        var result = await _createHandler.Handle(command, ct);
        return result.IsSuccess 
            ? CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data) 
            : BadRequest(result.Error);
    }

    /// <summary>Get one financial statement with its ratios.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(FinancialStatementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _getByIdHandler.Handle(new GetFinancialStatementByIdQuery(id), ct);
        return result.IsSuccess ? Ok(result.Data) : NotFound(result.Error);
    }

    /// <summary>List the financial statements on a loan application.</summary>
    [HttpGet("by-loan-application/{loanApplicationId}")]
    [ProducesResponseType(typeof(List<FinancialStatementSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetByLoanApplication(Guid loanApplicationId, CancellationToken ct)
    {
        var result = await _getByLoanAppHandler.Handle(new GetFinancialStatementsByLoanApplicationQuery(loanApplicationId), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>Get the year-on-year trend of a borrower's financial ratios.</summary>
    [HttpGet("by-loan-application/{loanApplicationId}/trend")]
    [ProducesResponseType(typeof(FinancialRatiosTrendDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetRatiosTrend(Guid loanApplicationId, CancellationToken ct)
    {
        var result = await _getTrendHandler.Handle(new GetFinancialRatiosTrendQuery(loanApplicationId), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>Enter or replace a statement's balance sheet.</summary>
    [HttpPut("{id}/balance-sheet")]
    [ProducesResponseType(typeof(FinancialStatementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetBalanceSheet(Guid id, [FromBody] SubmitBalanceSheetRequest request, CancellationToken ct)
    {
        var result = await _setBalanceSheetHandler.Handle(new SetBalanceSheetCommand(id, request), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>Enter or replace a statement's income statement.</summary>
    [HttpPut("{id}/income-statement")]
    [ProducesResponseType(typeof(FinancialStatementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetIncomeStatement(Guid id, [FromBody] SubmitIncomeStatementRequest request, CancellationToken ct)
    {
        var result = await _setIncomeStatementHandler.Handle(new SetIncomeStatementCommand(id, request), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>Enter or replace a statement's cash flow statement.</summary>
    [HttpPut("{id}/cash-flow-statement")]
    [ProducesResponseType(typeof(FinancialStatementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetCashFlowStatement(Guid id, [FromBody] SubmitCashFlowStatementRequest request, CancellationToken ct)
    {
        var result = await _setCashFlowHandler.Handle(new SetCashFlowStatementCommand(id, request), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>Submit a financial statement for verification.</summary>
    [HttpPost("{id}/submit")]
    [ProducesResponseType(typeof(FinancialStatementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
    {
        var result = await _submitHandler.Handle(new SubmitFinancialStatementCommand(id), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>Verify a submitted financial statement.</summary>
    [HttpPost("{id}/verify")]
    [ProducesResponseType(typeof(FinancialStatementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Verify(Guid id, [FromBody] VerifyFinancialStatementRequest request, CancellationToken ct)
    {
        var result = await _verifyHandler.Handle(new VerifyFinancialStatementCommand(id, request.VerifiedByUserId, request.Notes), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }
}

public record CreateFinancialStatementRequest(
    Guid LoanApplicationId,
    int FinancialYear,
    DateTime YearEndDate,
    string YearType,
    string InputMethod,
    Guid SubmittedByUserId,
    string? Currency = null,
    string? AuditorName = null,
    string? AuditorFirm = null,
    DateTime? AuditDate = null,
    string? AuditOpinion = null,
    string? OriginalFileName = null,
    string? FilePath = null
);

public record VerifyFinancialStatementRequest(Guid VerifiedByUserId, string? Notes = null);

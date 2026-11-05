using CRMS.Application.Common;
using CRMS.Application.StatementAnalysis.Commands;
using CRMS.Application.StatementAnalysis.DTOs;
using CRMS.Application.StatementAnalysis.Queries;
using CRMS.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CRMS.API.OpenApi;
using StatementTransactionType = CRMS.Domain.Enums.StatementTransactionType;

namespace CRMS.API.Controllers;

[ApiController]
[Route("api/v1/statements")]
[Authorize]
public class StatementAnalysisController : ControllerBase
{
    private readonly IRequestHandler<UploadStatementCommand, ApplicationResult<BankStatementDto>> _uploadHandler;
    private readonly IRequestHandler<AddTransactionsCommand, ApplicationResult<int>> _addTransactionsHandler;
    private readonly IRequestHandler<AnalyzeStatementCommand, ApplicationResult<StatementAnalysisResultDto>> _analyzeHandler;
    private readonly IRequestHandler<GetStatementByIdQuery, ApplicationResult<BankStatementDto>> _getByIdHandler;
    private readonly IRequestHandler<GetStatementTransactionsQuery, ApplicationResult<List<StatementTransactionDto>>> _getTransactionsHandler;
    private readonly IRequestHandler<GetStatementsByLoanApplicationQuery, ApplicationResult<List<BankStatementSummaryDto>>> _getByLoanAppHandler;

    public StatementAnalysisController(
        IRequestHandler<UploadStatementCommand, ApplicationResult<BankStatementDto>> uploadHandler,
        IRequestHandler<AddTransactionsCommand, ApplicationResult<int>> addTransactionsHandler,
        IRequestHandler<AnalyzeStatementCommand, ApplicationResult<StatementAnalysisResultDto>> analyzeHandler,
        IRequestHandler<GetStatementByIdQuery, ApplicationResult<BankStatementDto>> getByIdHandler,
        IRequestHandler<GetStatementTransactionsQuery, ApplicationResult<List<StatementTransactionDto>>> getTransactionsHandler,
        IRequestHandler<GetStatementsByLoanApplicationQuery, ApplicationResult<List<BankStatementSummaryDto>>> getByLoanAppHandler)
    {
        _uploadHandler = uploadHandler;
        _addTransactionsHandler = addTransactionsHandler;
        _analyzeHandler = analyzeHandler;
        _getByIdHandler = getByIdHandler;
        _getTransactionsHandler = getTransactionsHandler;
        _getByLoanAppHandler = getByLoanAppHandler;
    }

    /// <summary>Register a bank statement for a loan application.</summary>
    /// <remarks>Registers the statement and its period. Add its transactions next, then analyse it.</remarks>
    [HttpPost]
    [ProducesResponseType(typeof(BankStatementDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadStatement([FromBody] UploadStatementRequest request, CancellationToken ct)
    {
        var command = new UploadStatementCommand(
            request.AccountNumber,
            request.AccountName,
            request.BankName,
            request.PeriodStart,
            request.PeriodEnd,
            request.OpeningBalance,
            request.ClosingBalance,
            request.Format,
            request.Source,
            request.UploadedByUserId,
            request.OriginalFileName,
            request.FilePath,
            request.LoanApplicationId,
            request.Currency
        );

        var result = await _uploadHandler.Handle(command, ct);
        return result.IsSuccess ? CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data) : BadRequest(result.Error);
    }

    /// <summary>Add transactions to a registered bank statement.</summary>
    [HttpPost("{id}/transactions")]
    [ProducesResponseType(typeof(TransactionsAdded), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddTransactions(Guid id, [FromBody] AddTransactionsRequest request, CancellationToken ct)
    {
        var transactions = request.Transactions.Select(t => new TransactionInput(
            t.Date, t.Description, t.Amount, t.Type, t.RunningBalance, t.Reference
        )).ToList();

        var result = await _addTransactionsHandler.Handle(new AddTransactionsCommand(id, transactions), ct);
        return result.IsSuccess ? Ok(new { AddedCount = result.Data }) : BadRequest(result.Error);
    }

    /// <summary>Analyse a bank statement's cashflow.</summary>
    /// <remarks>Set useLLM to true to categorise transactions with the language model rather than the rules.</remarks>
    [HttpPost("{id}/analyze")]
    [ProducesResponseType(typeof(StatementAnalysisResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AnalyzeStatement(Guid id, [FromQuery] bool useLLM = false, CancellationToken ct = default)
    {
        var result = await _analyzeHandler.Handle(new AnalyzeStatementCommand(id, useLLM), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>Get one bank statement and its analysis.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(BankStatementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _getByIdHandler.Handle(new GetStatementByIdQuery(id), ct);
        return result.IsSuccess ? Ok(result.Data) : NotFound(result.Error);
    }

    /// <summary>List a bank statement's transactions.</summary>
    [HttpGet("{id}/transactions")]
    [ProducesResponseType(typeof(List<StatementTransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTransactions(Guid id, CancellationToken ct)
    {
        var result = await _getTransactionsHandler.Handle(new GetStatementTransactionsQuery(id), ct);
        return result.IsSuccess ? Ok(result.Data) : NotFound(result.Error);
    }

    /// <summary>List the bank statements on a loan application.</summary>
    [HttpGet("by-loan-application/{loanApplicationId}")]
    [ProducesResponseType(typeof(List<BankStatementSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetByLoanApplication(Guid loanApplicationId, CancellationToken ct)
    {
        var result = await _getByLoanAppHandler.Handle(new GetStatementsByLoanApplicationQuery(loanApplicationId), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }
}

// Request DTOs
public record UploadStatementRequest(
    string AccountNumber,
    string AccountName,
    string BankName,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    decimal OpeningBalance,
    decimal ClosingBalance,
    StatementFormat Format,
    StatementSource Source,
    Guid UploadedByUserId,
    string? OriginalFileName,
    string? FilePath,
    Guid? LoanApplicationId,
    string Currency = "NGN"
);

public record AddTransactionsRequest(List<TransactionInputDto> Transactions);

public record TransactionInputDto(
    DateTime Date,
    string Description,
    decimal Amount,
    StatementTransactionType Type,
    decimal RunningBalance,
    string? Reference
);

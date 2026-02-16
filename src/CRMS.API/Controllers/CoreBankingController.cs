using CRMS.Application.Common;
using CRMS.Application.CoreBanking.DTOs;
using CRMS.Application.CoreBanking.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRMS.API.Controllers;

[ApiController]
[Route("api/v1/core-banking")]
[Authorize]
public class CoreBankingController : ControllerBase
{
    private readonly IRequestHandler<GetCorporateAccountDataQuery, ApplicationResult<CorporateAccountDataDto>> _getCorporateDataHandler;
    private readonly IRequestHandler<GetAccountInfoQuery, ApplicationResult<AccountInfoDto>> _getAccountInfoHandler;
    private readonly IRequestHandler<GetAccountStatementQuery, ApplicationResult<AccountStatementDto>> _getStatementHandler;

    public CoreBankingController(
        IRequestHandler<GetCorporateAccountDataQuery, ApplicationResult<CorporateAccountDataDto>> getCorporateDataHandler,
        IRequestHandler<GetAccountInfoQuery, ApplicationResult<AccountInfoDto>> getAccountInfoHandler,
        IRequestHandler<GetAccountStatementQuery, ApplicationResult<AccountStatementDto>> getStatementHandler)
    {
        _getCorporateDataHandler = getCorporateDataHandler;
        _getAccountInfoHandler = getAccountInfoHandler;
        _getStatementHandler = getStatementHandler;
    }

    [HttpGet("corporate/{accountNumber}")]
    public async Task<IActionResult> GetCorporateAccountData(
        string accountNumber,
        [FromQuery] bool includeStatement = true,
        [FromQuery] int statementMonths = 6,
        CancellationToken ct = default)
    {
        var query = new GetCorporateAccountDataQuery(accountNumber, includeStatement, statementMonths);
        var result = await _getCorporateDataHandler.Handle(query, ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    [HttpGet("accounts/{accountNumber}")]
    public async Task<IActionResult> GetAccountInfo(string accountNumber, CancellationToken ct)
    {
        var result = await _getAccountInfoHandler.Handle(new GetAccountInfoQuery(accountNumber), ct);
        return result.IsSuccess ? Ok(result.Data) : NotFound(result.Error);
    }

    [HttpGet("accounts/{accountNumber}/statement")]
    public async Task<IActionResult> GetAccountStatement(
        string accountNumber,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken ct)
    {
        var to = toDate ?? DateTime.UtcNow;
        var from = fromDate ?? to.AddMonths(-6);

        var query = new GetAccountStatementQuery(accountNumber, from, to);
        var result = await _getStatementHandler.Handle(query, ct);
        return result.IsSuccess ? Ok(result.Data) : NotFound(result.Error);
    }
}

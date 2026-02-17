using CRMS.Application.Collateral.Commands;
using CRMS.Application.Collateral.DTOs;
using CRMS.Application.Collateral.Queries;
using CRMS.Application.Common;
using CRMS.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRMS.API.Controllers;

[ApiController]
[Route("api/v1/collaterals")]
[Authorize]
public class CollateralController : ControllerBase
{
    private readonly IRequestHandler<AddCollateralCommand, ApplicationResult<CollateralDto>> _addHandler;
    private readonly IRequestHandler<SetCollateralValuationCommand, ApplicationResult<CollateralDto>> _setValuationHandler;
    private readonly IRequestHandler<ApproveCollateralCommand, ApplicationResult<CollateralDto>> _approveHandler;
    private readonly IRequestHandler<RecordPerfectionCommand, ApplicationResult<CollateralDto>> _perfectionHandler;
    private readonly IRequestHandler<GetCollateralByIdQuery, ApplicationResult<CollateralDto>> _getByIdHandler;
    private readonly IRequestHandler<GetCollateralByLoanApplicationQuery, ApplicationResult<List<CollateralSummaryDto>>> _getByLoanAppHandler;
    private readonly IRequestHandler<CalculateLTVQuery, ApplicationResult<LTVCalculationDto>> _calculateLTVHandler;

    public CollateralController(
        IRequestHandler<AddCollateralCommand, ApplicationResult<CollateralDto>> addHandler,
        IRequestHandler<SetCollateralValuationCommand, ApplicationResult<CollateralDto>> setValuationHandler,
        IRequestHandler<ApproveCollateralCommand, ApplicationResult<CollateralDto>> approveHandler,
        IRequestHandler<RecordPerfectionCommand, ApplicationResult<CollateralDto>> perfectionHandler,
        IRequestHandler<GetCollateralByIdQuery, ApplicationResult<CollateralDto>> getByIdHandler,
        IRequestHandler<GetCollateralByLoanApplicationQuery, ApplicationResult<List<CollateralSummaryDto>>> getByLoanAppHandler,
        IRequestHandler<CalculateLTVQuery, ApplicationResult<LTVCalculationDto>> calculateLTVHandler)
    {
        _addHandler = addHandler;
        _setValuationHandler = setValuationHandler;
        _approveHandler = approveHandler;
        _perfectionHandler = perfectionHandler;
        _getByIdHandler = getByIdHandler;
        _getByLoanAppHandler = getByLoanAppHandler;
        _calculateLTVHandler = calculateLTVHandler;
    }

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] AddCollateralApiRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<CollateralType>(request.Type, true, out var type))
            return BadRequest("Invalid collateral type");

        var command = new AddCollateralCommand(
            request.LoanApplicationId,
            type,
            request.Description,
            request.CreatedByUserId,
            request.AssetIdentifier,
            request.Location,
            request.OwnerName,
            request.OwnershipType
        );

        var result = await _addHandler.Handle(command, ct);
        return result.IsSuccess ? CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data) : BadRequest(result.Error);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _getByIdHandler.Handle(new GetCollateralByIdQuery(id), ct);
        return result.IsSuccess ? Ok(result.Data) : NotFound(result.Error);
    }

    [HttpGet("by-loan-application/{loanApplicationId}")]
    public async Task<IActionResult> GetByLoanApplication(Guid loanApplicationId, CancellationToken ct)
    {
        var result = await _getByLoanAppHandler.Handle(new GetCollateralByLoanApplicationQuery(loanApplicationId), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    [HttpPut("{id}/valuation")]
    public async Task<IActionResult> SetValuation(Guid id, [FromBody] SetCollateralValuationRequest request, CancellationToken ct)
    {
        var command = new SetCollateralValuationCommand(
            id,
            request.MarketValue,
            request.ForcedSaleValue,
            request.Currency,
            request.HaircutPercentage
        );

        var result = await _setValuationHandler.Handle(command, ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    [HttpPost("{id}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveCollateralApiRequest request, CancellationToken ct)
    {
        var result = await _approveHandler.Handle(new ApproveCollateralCommand(id, request.ApprovedByUserId), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    [HttpPost("{id}/perfection")]
    public async Task<IActionResult> RecordPerfection(Guid id, [FromBody] RecordPerfectionRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<LienType>(request.LienType, true, out var lienType))
            return BadRequest("Invalid lien type");

        var command = new RecordPerfectionCommand(
            id,
            lienType,
            request.LienReference,
            request.RegistrationAuthority,
            request.RegistrationDate
        );

        var result = await _perfectionHandler.Handle(command, ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    [HttpGet("ltv/{loanApplicationId}")]
    public async Task<IActionResult> CalculateLTV(Guid loanApplicationId, [FromQuery] decimal loanAmount, [FromQuery] string currency = "NGN", CancellationToken ct = default)
    {
        var result = await _calculateLTVHandler.Handle(new CalculateLTVQuery(loanApplicationId, loanAmount, currency), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }
}

public record AddCollateralApiRequest(
    Guid LoanApplicationId,
    string Type,
    string Description,
    Guid CreatedByUserId,
    string? AssetIdentifier = null,
    string? Location = null,
    string? OwnerName = null,
    string? OwnershipType = null
);

public record ApproveCollateralApiRequest(Guid ApprovedByUserId);

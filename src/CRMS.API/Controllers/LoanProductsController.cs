using CRMS.Application.Common;
using CRMS.Application.ProductCatalog.Commands;
using CRMS.Application.ProductCatalog.DTOs;
using CRMS.Application.ProductCatalog.Queries;
using CRMS.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace CRMS.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class LoanProductsController : ControllerBase
{
    private readonly IRequestHandler<CreateLoanProductCommand, ApplicationResult<LoanProductDto>> _createHandler;
    private readonly IRequestHandler<UpdateLoanProductCommand, ApplicationResult<LoanProductDto>> _updateHandler;
    private readonly IRequestHandler<ActivateLoanProductCommand, ApplicationResult> _activateHandler;
    private readonly IRequestHandler<AddPricingTierCommand, ApplicationResult<PricingTierDto>> _addPricingTierHandler;
    private readonly IRequestHandler<GetLoanProductByIdQuery, ApplicationResult<LoanProductDto>> _getByIdHandler;
    private readonly IRequestHandler<GetLoanProductByCodeQuery, ApplicationResult<LoanProductDto>> _getByCodeHandler;
    private readonly IRequestHandler<GetAllLoanProductsQuery, ApplicationResult<List<LoanProductSummaryDto>>> _getAllHandler;
    private readonly IRequestHandler<GetLoanProductsByTypeQuery, ApplicationResult<List<LoanProductSummaryDto>>> _getByTypeHandler;
    private readonly IRequestHandler<GetActiveLoanProductsByTypeQuery, ApplicationResult<List<LoanProductSummaryDto>>> _getActiveByTypeHandler;

    public LoanProductsController(
        IRequestHandler<CreateLoanProductCommand, ApplicationResult<LoanProductDto>> createHandler,
        IRequestHandler<UpdateLoanProductCommand, ApplicationResult<LoanProductDto>> updateHandler,
        IRequestHandler<ActivateLoanProductCommand, ApplicationResult> activateHandler,
        IRequestHandler<AddPricingTierCommand, ApplicationResult<PricingTierDto>> addPricingTierHandler,
        IRequestHandler<GetLoanProductByIdQuery, ApplicationResult<LoanProductDto>> getByIdHandler,
        IRequestHandler<GetLoanProductByCodeQuery, ApplicationResult<LoanProductDto>> getByCodeHandler,
        IRequestHandler<GetAllLoanProductsQuery, ApplicationResult<List<LoanProductSummaryDto>>> getAllHandler,
        IRequestHandler<GetLoanProductsByTypeQuery, ApplicationResult<List<LoanProductSummaryDto>>> getByTypeHandler,
        IRequestHandler<GetActiveLoanProductsByTypeQuery, ApplicationResult<List<LoanProductSummaryDto>>> getActiveByTypeHandler)
    {
        _createHandler = createHandler;
        _updateHandler = updateHandler;
        _activateHandler = activateHandler;
        _addPricingTierHandler = addPricingTierHandler;
        _getByIdHandler = getByIdHandler;
        _getByCodeHandler = getByCodeHandler;
        _getAllHandler = getAllHandler;
        _getByTypeHandler = getByTypeHandler;
        _getActiveByTypeHandler = getActiveByTypeHandler;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _getAllHandler.Handle(new GetAllLoanProductsQuery(), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _getByIdHandler.Handle(new GetLoanProductByIdQuery(id), ct);
        return result.IsSuccess ? Ok(result.Data) : NotFound(result.Error);
    }

    [HttpGet("code/{code}")]
    public async Task<IActionResult> GetByCode(string code, CancellationToken ct)
    {
        var result = await _getByCodeHandler.Handle(new GetLoanProductByCodeQuery(code), ct);
        return result.IsSuccess ? Ok(result.Data) : NotFound(result.Error);
    }

    [HttpGet("type/{type}")]
    public async Task<IActionResult> GetByType(LoanProductType type, CancellationToken ct)
    {
        var result = await _getByTypeHandler.Handle(new GetLoanProductsByTypeQuery(type), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    [HttpGet("type/{type}/active")]
    public async Task<IActionResult> GetActiveByType(LoanProductType type, CancellationToken ct)
    {
        var result = await _getActiveByTypeHandler.Handle(new GetActiveLoanProductsByTypeQuery(type), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLoanProductRequest request, CancellationToken ct)
    {
        var command = new CreateLoanProductCommand(
            request.Code,
            request.Name,
            request.Description,
            request.Type,
            request.MinAmount,
            request.MaxAmount,
            request.Currency,
            request.MinTenorMonths,
            request.MaxTenorMonths
        );

        var result = await _createHandler.Handle(command, ct);
        return result.IsSuccess 
            ? CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data) 
            : BadRequest(result.Error);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLoanProductRequest request, CancellationToken ct)
    {
        var command = new UpdateLoanProductCommand(
            id,
            request.Name,
            request.Description,
            request.MinAmount,
            request.MaxAmount,
            request.Currency,
            request.MinTenorMonths,
            request.MaxTenorMonths
        );

        var result = await _updateHandler.Handle(command, ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        var result = await _activateHandler.Handle(new ActivateLoanProductCommand(id), ct);
        return result.IsSuccess ? NoContent() : BadRequest(result.Error);
    }

    [HttpPost("{id:guid}/pricing-tiers")]
    public async Task<IActionResult> AddPricingTier(Guid id, [FromBody] AddPricingTierRequest request, CancellationToken ct)
    {
        var command = new AddPricingTierCommand(
            id,
            request.Name,
            request.InterestRatePerAnnum,
            request.RateType,
            request.ProcessingFeePercent,
            request.ProcessingFeeFixed,
            request.ProcessingFeeCurrency,
            request.MinCreditScore,
            request.MaxCreditScore
        );

        var result = await _addPricingTierHandler.Handle(command, ct);
        return result.IsSuccess ? Created("", result.Data) : BadRequest(result.Error);
    }
}

public record CreateLoanProductRequest(
    string Code,
    string Name,
    string Description,
    LoanProductType Type,
    decimal MinAmount,
    decimal MaxAmount,
    string Currency,
    int MinTenorMonths,
    int MaxTenorMonths
);

public record UpdateLoanProductRequest(
    string Name,
    string Description,
    decimal MinAmount,
    decimal MaxAmount,
    string Currency,
    int MinTenorMonths,
    int MaxTenorMonths
);

public record AddPricingTierRequest(
    string Name,
    decimal InterestRatePerAnnum,
    Domain.ValueObjects.InterestRateType RateType,
    decimal? ProcessingFeePercent,
    decimal? ProcessingFeeFixed,
    string? ProcessingFeeCurrency,
    int? MinCreditScore,
    int? MaxCreditScore
);

using CRMS.Application.Common;
using CRMS.Application.ProductCatalog.Commands;
using CRMS.Application.ProductCatalog.DTOs;
using CRMS.Application.ProductCatalog.Queries;
using CRMS.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRMS.API.Controllers;

/// <summary>
/// The product catalogue. Any signed-in member of staff may read it; changing it takes the roles
/// that may open Admin > Products in the intranet (<see cref="ManageRoles"/>). Keep the two in step.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class LoanProductsController : ControllerBase
{
    private const string ManageRoles = "SystemAdmin,RiskManager";

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

    /// <summary>List loan products.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<LoanProductSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _getAllHandler.Handle(new GetAllLoanProductsQuery(), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>Get one loan product with its pricing tiers.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(LoanProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _getByIdHandler.Handle(new GetLoanProductByIdQuery(id), ct);
        return result.IsSuccess ? Ok(result.Data) : NotFound(result.Error);
    }

    /// <summary>Get a loan product by its code.</summary>
    [HttpGet("code/{code}")]
    [ProducesResponseType(typeof(LoanProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByCode(string code, CancellationToken ct)
    {
        var result = await _getByCodeHandler.Handle(new GetLoanProductByCodeQuery(code), ct);
        return result.IsSuccess ? Ok(result.Data) : NotFound(result.Error);
    }

    /// <summary>List the loan products of a type.</summary>
    [HttpGet("type/{type}")]
    [ProducesResponseType(typeof(List<LoanProductSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetByType(LoanProductType type, CancellationToken ct)
    {
        var result = await _getByTypeHandler.Handle(new GetLoanProductsByTypeQuery(type), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>List the active loan products of a type.</summary>
    [HttpGet("type/{type}/active")]
    [ProducesResponseType(typeof(List<LoanProductSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetActiveByType(LoanProductType type, CancellationToken ct)
    {
        var result = await _getActiveByTypeHandler.Handle(new GetActiveLoanProductsByTypeQuery(type), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>Create a loan product.</summary>
    /// <remarks>The product starts inactive. Activate it once its pricing and its core banking product are set.</remarks>
    [HttpPost]
    [Authorize(Roles = ManageRoles)]
    [ProducesResponseType(typeof(LoanProductDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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

    /// <summary>Update a loan product.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = ManageRoles)]
    [ProducesResponseType(typeof(LoanProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLoanProductRequest request, CancellationToken ct)
    {
        var command = new UpdateLoanProductCommand(
            id,
            request.Name,
            request.Description,
            request.Type,
            request.MinAmount,
            request.MaxAmount,
            request.Currency,
            request.MinTenorMonths,
            request.MaxTenorMonths
        );

        var result = await _updateHandler.Handle(command, ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>Activate a loan product so applications can use it.</summary>
    [HttpPost("{id:guid}/activate")]
    [Authorize(Roles = ManageRoles)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        var result = await _activateHandler.Handle(new ActivateLoanProductCommand(id), ct);
        return result.IsSuccess ? NoContent() : BadRequest(result.Error);
    }

    /// <summary>Add a pricing tier to a loan product.</summary>
    [HttpPost("{id:guid}/pricing-tiers")]
    [Authorize(Roles = ManageRoles)]
    [ProducesResponseType(typeof(PricingTierDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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
    LoanProductType Type,
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

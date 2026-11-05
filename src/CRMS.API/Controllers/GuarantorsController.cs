using CRMS.Application.Common;
using CRMS.Application.Guarantor.Commands;
using CRMS.Application.Guarantor.DTOs;
using CRMS.Application.Guarantor.Queries;
using CRMS.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRMS.API.Controllers;

[ApiController]
[Route("api/v1/guarantors")]
[Authorize]
public class GuarantorsController : ControllerBase
{
    private readonly IRequestHandler<AddIndividualGuarantorCommand, ApplicationResult<GuarantorDto>> _addIndividualHandler;
    private readonly IRequestHandler<RunGuarantorCreditCheckCommand, ApplicationResult<GuarantorCreditCheckResultDto>> _creditCheckHandler;
    private readonly IRequestHandler<ApproveGuarantorCommand, ApplicationResult<GuarantorDto>> _approveHandler;
    private readonly IRequestHandler<RejectGuarantorCommand, ApplicationResult<GuarantorDto>> _rejectHandler;
    private readonly IRequestHandler<GetGuarantorByIdQuery, ApplicationResult<GuarantorDto>> _getByIdHandler;
    private readonly IRequestHandler<GetGuarantorsByLoanApplicationQuery, ApplicationResult<List<GuarantorSummaryDto>>> _getByLoanAppHandler;
    private readonly IRequestHandler<GetGuarantorsByBVNQuery, ApplicationResult<List<GuarantorSummaryDto>>> _getByBVNHandler;

    public GuarantorsController(
        IRequestHandler<AddIndividualGuarantorCommand, ApplicationResult<GuarantorDto>> addIndividualHandler,
        IRequestHandler<RunGuarantorCreditCheckCommand, ApplicationResult<GuarantorCreditCheckResultDto>> creditCheckHandler,
        IRequestHandler<ApproveGuarantorCommand, ApplicationResult<GuarantorDto>> approveHandler,
        IRequestHandler<RejectGuarantorCommand, ApplicationResult<GuarantorDto>> rejectHandler,
        IRequestHandler<GetGuarantorByIdQuery, ApplicationResult<GuarantorDto>> getByIdHandler,
        IRequestHandler<GetGuarantorsByLoanApplicationQuery, ApplicationResult<List<GuarantorSummaryDto>>> getByLoanAppHandler,
        IRequestHandler<GetGuarantorsByBVNQuery, ApplicationResult<List<GuarantorSummaryDto>>> getByBVNHandler)
    {
        _addIndividualHandler = addIndividualHandler;
        _creditCheckHandler = creditCheckHandler;
        _approveHandler = approveHandler;
        _rejectHandler = rejectHandler;
        _getByIdHandler = getByIdHandler;
        _getByLoanAppHandler = getByLoanAppHandler;
        _getByBVNHandler = getByBVNHandler;
    }

    /// <summary>Add an individual guarantor to a loan application.</summary>
    [HttpPost("individual")]
    [ProducesResponseType(typeof(GuarantorDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddIndividual([FromBody] AddIndividualGuarantorRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<GuaranteeType>(request.GuaranteeType, true, out var guaranteeType))
            return BadRequest("Invalid guarantee type");

        var command = new AddIndividualGuarantorCommand(
            request.LoanApplicationId,
            request.FullName,
            request.BVN,
            guaranteeType,
            request.CreatedByUserId,
            request.RelationshipToApplicant,
            request.Email,
            request.Phone,
            request.Address,
            request.GuaranteeLimit,
            request.Currency ?? "NGN",
            request.IsDirector,
            request.IsShareholder,
            request.ShareholdingPercentage,
            request.DeclaredNetWorth,
            request.Occupation,
            request.EmployerName,
            request.MonthlyIncome
        );

        var result = await _addIndividualHandler.Handle(command, ct);
        return result.IsSuccess ? CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data) : BadRequest(result.Error);
    }

    /// <summary>Get one guarantor.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(GuarantorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _getByIdHandler.Handle(new GetGuarantorByIdQuery(id), ct);
        return result.IsSuccess ? Ok(result.Data) : NotFound(result.Error);
    }

    /// <summary>List the guarantors on a loan application.</summary>
    [HttpGet("by-loan-application/{loanApplicationId}")]
    [ProducesResponseType(typeof(List<GuarantorSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetByLoanApplication(Guid loanApplicationId, CancellationToken ct)
    {
        var result = await _getByLoanAppHandler.Handle(new GetGuarantorsByLoanApplicationQuery(loanApplicationId), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>List the guarantees a person has given, by BVN.</summary>
    [HttpGet("by-bvn/{bvn}")]
    [ProducesResponseType(typeof(List<GuarantorSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetByBVN(string bvn, CancellationToken ct)
    {
        var result = await _getByBVNHandler.Handle(new GetGuarantorsByBVNQuery(bvn), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>Run a credit bureau check on a guarantor.</summary>
    /// <remarks>Legacy single-guarantor check. Queueing the checks for the whole application (Credit bureau > queue) is preferred.</remarks>
    [HttpPost("{id}/credit-check")]
    [ProducesResponseType(typeof(GuarantorCreditCheckResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RunCreditCheck(Guid id, [FromBody] CreditCheckApiRequest request, CancellationToken ct)
    {
        var result = await _creditCheckHandler.Handle(new RunGuarantorCreditCheckCommand(id, request.RequestedByUserId, request.ConsentRecordId), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>Approve a guarantor.</summary>
    [HttpPost("{id}/approve")]
    [ProducesResponseType(typeof(GuarantorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveGuarantorApiRequest request, CancellationToken ct)
    {
        var command = new ApproveGuarantorCommand(id, request.ApprovedByUserId, request.VerifiedNetWorth, request.Currency ?? "NGN");
        var result = await _approveHandler.Handle(command, ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>Reject a guarantor, with the reason.</summary>
    [HttpPost("{id}/reject")]
    [ProducesResponseType(typeof(GuarantorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectGuarantorApiRequest request, CancellationToken ct)
    {
        var result = await _rejectHandler.Handle(new RejectGuarantorCommand(id, request.RejectedByUserId, request.Reason), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }
}

public record AddIndividualGuarantorRequest(
    Guid LoanApplicationId,
    string FullName,
    string? BVN,
    string GuaranteeType,
    Guid CreatedByUserId,
    string? RelationshipToApplicant = null,
    string? Email = null,
    string? Phone = null,
    string? Address = null,
    decimal? GuaranteeLimit = null,
    string? Currency = null,
    bool IsDirector = false,
    bool IsShareholder = false,
    decimal? ShareholdingPercentage = null,
    decimal? DeclaredNetWorth = null,
    string? Occupation = null,
    string? EmployerName = null,
    decimal? MonthlyIncome = null
);

public record CreditCheckApiRequest(Guid RequestedByUserId, Guid ConsentRecordId);

public record ApproveGuarantorApiRequest(Guid ApprovedByUserId, decimal? VerifiedNetWorth = null, string? Currency = null);

public record RejectGuarantorApiRequest(Guid RejectedByUserId, string Reason);

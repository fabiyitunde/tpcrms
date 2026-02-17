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

    [HttpPost("individual")]
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

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _getByIdHandler.Handle(new GetGuarantorByIdQuery(id), ct);
        return result.IsSuccess ? Ok(result.Data) : NotFound(result.Error);
    }

    [HttpGet("by-loan-application/{loanApplicationId}")]
    public async Task<IActionResult> GetByLoanApplication(Guid loanApplicationId, CancellationToken ct)
    {
        var result = await _getByLoanAppHandler.Handle(new GetGuarantorsByLoanApplicationQuery(loanApplicationId), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    [HttpGet("by-bvn/{bvn}")]
    public async Task<IActionResult> GetByBVN(string bvn, CancellationToken ct)
    {
        var result = await _getByBVNHandler.Handle(new GetGuarantorsByBVNQuery(bvn), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    [HttpPost("{id}/credit-check")]
    public async Task<IActionResult> RunCreditCheck(Guid id, [FromBody] CreditCheckApiRequest request, CancellationToken ct)
    {
        var result = await _creditCheckHandler.Handle(new RunGuarantorCreditCheckCommand(id, request.RequestedByUserId), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    [HttpPost("{id}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveGuarantorApiRequest request, CancellationToken ct)
    {
        var command = new ApproveGuarantorCommand(id, request.ApprovedByUserId, request.VerifiedNetWorth, request.Currency ?? "NGN");
        var result = await _approveHandler.Handle(command, ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    [HttpPost("{id}/reject")]
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

public record CreditCheckApiRequest(Guid RequestedByUserId);

public record ApproveGuarantorApiRequest(Guid ApprovedByUserId, decimal? VerifiedNetWorth = null, string? Currency = null);

public record RejectGuarantorApiRequest(Guid RejectedByUserId, string Reason);

using System.Security.Claims;
using CRMS.Application.LoanPack.Commands;
using CRMS.Application.LoanPack.Queries;
using CRMS.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRMS.API.Controllers;

/// <summary>
/// Endpoints for generating and retrieving loan pack PDFs.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LoanPackController : ControllerBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IFileStorageService _fileStorage;

    public LoanPackController(IServiceProvider serviceProvider, IFileStorageService fileStorage)
    {
        _serviceProvider = serviceProvider;
        _fileStorage = fileStorage;
    }

    /// <summary>
    /// Generate a new loan pack PDF for an application.
    /// Restricted to credit officers, HO reviewers, risk managers, and system admins.
    /// </summary>
    [HttpPost("generate/{loanApplicationId:guid}")]
    [Authorize(Roles = "CreditOfficer,HOReviewer,RiskManager,SystemAdmin")]
    [ProducesResponseType(typeof(LoanPackResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Generate(Guid loanApplicationId, CancellationToken ct)
    {
        // Get user info from JWT claims
        var userIdClaim = User.FindFirst("sub")?.Value 
                       ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized("User identity claim is invalid or missing.");

        var userName = User.Identity?.Name 
                    ?? User.FindFirst(ClaimTypes.Name)?.Value 
                    ?? User.FindFirst("name")?.Value 
                    ?? "Unknown";

        var handler = _serviceProvider.GetRequiredService<GenerateLoanPackHandler>();
        var result = await handler.Handle(new GenerateLoanPackCommand(loanApplicationId, userId, userName), ct);

        if (!result.IsSuccess)
            return BadRequest(result.Error);

        return Ok(result.Data);
    }

    /// <summary>
    /// Get loan pack metadata by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(LoanPackDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var handler = _serviceProvider.GetRequiredService<GetLoanPackByIdHandler>();
        var result = await handler.Handle(new GetLoanPackByIdQuery(id), ct);

        return result.IsSuccess ? Ok(result.Data) : NotFound(result.Error);
    }

    /// <summary>
    /// Get the latest loan pack for an application.
    /// </summary>
    [HttpGet("latest/{loanApplicationId:guid}")]
    [ProducesResponseType(typeof(LoanPackDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLatest(Guid loanApplicationId, CancellationToken ct)
    {
        var handler = _serviceProvider.GetRequiredService<GetLatestLoanPackHandler>();
        var result = await handler.Handle(new GetLatestLoanPackQuery(loanApplicationId), ct);

        return result.IsSuccess ? Ok(result.Data) : NotFound(result.Error);
    }

    /// <summary>
    /// Get all loan pack versions for an application.
    /// </summary>
    [HttpGet("versions/{loanApplicationId:guid}")]
    [ProducesResponseType(typeof(List<LoanPackSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVersions(Guid loanApplicationId, CancellationToken ct)
    {
        var handler = _serviceProvider.GetRequiredService<GetLoanPackVersionsHandler>();
        var result = await handler.Handle(new GetLoanPackVersionsQuery(loanApplicationId), ct);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>
    /// Download a loan pack PDF.
    /// Restricted to roles that legitimately need to view loan packs.
    /// </summary>
    [HttpGet("download/{id:guid}")]
    [Authorize(Roles = "CreditOfficer,HOReviewer,RiskManager,CommitteeMember,FinalApprover,SystemAdmin")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var handler = _serviceProvider.GetRequiredService<GetLoanPackByIdHandler>();
        var result = await handler.Handle(new GetLoanPackByIdQuery(id), ct);

        if (!result.IsSuccess)
            return NotFound(result.Error);

        var pack = result.Data!;

        if (string.IsNullOrEmpty(pack.StoragePath))
            return NotFound("Loan pack file has not been generated yet");

        // Check if file exists
        if (!await _fileStorage.ExistsAsync(pack.StoragePath, ct))
            return NotFound("Loan pack file not found in storage");

        // Download file from storage
        var fileBytes = await _fileStorage.DownloadAsync(pack.StoragePath, ct);

        return File(fileBytes, "application/pdf", pack.FileName);
    }
}

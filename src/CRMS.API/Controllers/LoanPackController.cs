using CRMS.Application.LoanPack.Commands;
using CRMS.Application.LoanPack.Queries;
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

    public LoanPackController(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Generate a new loan pack PDF for an application.
    /// </summary>
    [HttpPost("generate/{loanApplicationId:guid}")]
    [ProducesResponseType(typeof(LoanPackResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Generate(Guid loanApplicationId, CancellationToken ct)
    {
        // TODO: Get user info from claims
        var userId = Guid.NewGuid();
        var userName = User.Identity?.Name ?? "System";

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
    /// </summary>
    [HttpGet("download/{id:guid}")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var handler = _serviceProvider.GetRequiredService<GetLoanPackByIdHandler>();
        var result = await handler.Handle(new GetLoanPackByIdQuery(id), ct);

        if (!result.IsSuccess)
            return NotFound(result.Error);

        var pack = result.Data!;

        // TODO: Retrieve actual PDF from file storage using pack.StoragePath
        // For now, return a placeholder response
        return NotFound("PDF file storage not implemented - file would be at: " + pack.StoragePath);
    }
}

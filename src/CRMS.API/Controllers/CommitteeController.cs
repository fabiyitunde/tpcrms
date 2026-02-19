using CRMS.Application.Committee.Commands;
using CRMS.Application.Committee.DTOs;
using CRMS.Application.Committee.Queries;
using CRMS.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRMS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CommitteeController : ControllerBase
{
    private readonly IServiceProvider _serviceProvider;

    public CommitteeController(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Get committee review by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CommitteeReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var handler = _serviceProvider.GetRequiredService<GetCommitteeReviewByIdHandler>();
        var result = await handler.Handle(new GetCommitteeReviewByIdQuery(id), ct);

        return result.IsSuccess ? Ok(result.Data) : NotFound(result.Error);
    }

    /// <summary>
    /// Get committee review by loan application ID.
    /// </summary>
    [HttpGet("by-loan-application/{loanApplicationId:guid}")]
    [ProducesResponseType(typeof(CommitteeReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByLoanApplication(Guid loanApplicationId, CancellationToken ct)
    {
        var handler = _serviceProvider.GetRequiredService<GetCommitteeReviewByLoanApplicationHandler>();
        var result = await handler.Handle(new GetCommitteeReviewByLoanApplicationQuery(loanApplicationId), ct);

        return result.IsSuccess ? Ok(result.Data) : NotFound(result.Error);
    }

    /// <summary>
    /// Get current user's pending votes.
    /// </summary>
    [HttpGet("my-pending-votes")]
    [ProducesResponseType(typeof(List<CommitteeReviewSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyPendingVotes(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var handler = _serviceProvider.GetRequiredService<GetMyPendingVotesHandler>();
        var result = await handler.Handle(new GetMyPendingVotesQuery(userId), ct);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>
    /// Get current user's committee reviews.
    /// </summary>
    [HttpGet("my-reviews")]
    [ProducesResponseType(typeof(List<CommitteeReviewSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyReviews(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var handler = _serviceProvider.GetRequiredService<GetMyCommitteeReviewsHandler>();
        var result = await handler.Handle(new GetMyCommitteeReviewsQuery(userId), ct);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>
    /// Get reviews by status.
    /// </summary>
    [HttpGet("by-status/{status}")]
    [Authorize(Roles = "CreditOfficer,RiskManager,SystemAdmin")]
    [ProducesResponseType(typeof(List<CommitteeReviewSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByStatus(string status, CancellationToken ct)
    {
        if (!Enum.TryParse<CommitteeReviewStatus>(status, out var statusEnum))
            return BadRequest($"Invalid status: {status}");

        var handler = _serviceProvider.GetRequiredService<GetCommitteeReviewsByStatusHandler>();
        var result = await handler.Handle(new GetCommitteeReviewsByStatusQuery(statusEnum), ct);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>
    /// Get overdue reviews.
    /// </summary>
    [HttpGet("overdue")]
    [Authorize(Roles = "CreditOfficer,RiskManager,SystemAdmin")]
    [ProducesResponseType(typeof(List<CommitteeReviewSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOverdue(CancellationToken ct)
    {
        var handler = _serviceProvider.GetRequiredService<GetOverdueCommitteeReviewsHandler>();
        var result = await handler.Handle(new GetOverdueCommitteeReviewsQuery(), ct);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>
    /// Get voting summary for a review.
    /// </summary>
    [HttpGet("{id:guid}/voting-summary")]
    [ProducesResponseType(typeof(VotingSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVotingSummary(Guid id, CancellationToken ct)
    {
        var handler = _serviceProvider.GetRequiredService<GetVotingSummaryHandler>();
        var result = await handler.Handle(new GetVotingSummaryQuery(id), ct);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>
    /// Create a new committee review.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "CreditOfficer,RiskManager,SystemAdmin")]
    [ProducesResponseType(typeof(CommitteeReviewDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateCommitteeReviewRequest request,
        CancellationToken ct)
    {
        if (!Enum.TryParse<CommitteeType>(request.CommitteeType, out var committeeType))
            return BadRequest($"Invalid committee type: {request.CommitteeType}");

        var userId = GetCurrentUserId();
        var handler = _serviceProvider.GetRequiredService<CreateCommitteeReviewHandler>();
        
        // Note: ApplicationNumber should be fetched from loan application in real implementation
        var result = await handler.Handle(new CreateCommitteeReviewCommand(
            request.LoanApplicationId,
            $"LA-{request.LoanApplicationId.ToString()[..8].ToUpper()}",
            committeeType,
            userId,
            request.RequiredVotes,
            request.MinimumApprovalVotes,
            request.DeadlineHours), ct);

        return result.IsSuccess 
            ? CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data) 
            : BadRequest(result.Error);
    }

    /// <summary>
    /// Add a member to committee review.
    /// </summary>
    [HttpPost("{id:guid}/members")]
    [Authorize(Roles = "CreditOfficer,RiskManager,SystemAdmin")]
    [ProducesResponseType(typeof(CommitteeReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddMember(
        Guid id,
        [FromBody] AddCommitteeMemberRequest request,
        CancellationToken ct)
    {
        var handler = _serviceProvider.GetRequiredService<AddCommitteeMemberHandler>();
        var result = await handler.Handle(new AddCommitteeMemberCommand(
            id,
            request.UserId,
            request.UserName,
            request.Role,
            request.IsChairperson), ct);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>
    /// Start voting on a committee review.
    /// </summary>
    [HttpPost("{id:guid}/start-voting")]
    [Authorize(Roles = "CreditOfficer,RiskManager,SystemAdmin")]
    [ProducesResponseType(typeof(CommitteeReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> StartVoting(Guid id, CancellationToken ct)
    {
        var handler = _serviceProvider.GetRequiredService<StartVotingHandler>();
        var result = await handler.Handle(new StartVotingCommand(id), ct);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>
    /// Cast a vote on a committee review.
    /// </summary>
    [HttpPost("{id:guid}/vote")]
    [Authorize(Roles = "CommitteeMember,CreditOfficer,RiskManager,SystemAdmin")]
    [ProducesResponseType(typeof(CommitteeReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CastVote(
        Guid id,
        [FromBody] CastVoteRequest request,
        CancellationToken ct)
    {
        if (!Enum.TryParse<CommitteeVote>(request.Vote, out var vote))
            return BadRequest($"Invalid vote: {request.Vote}. Valid values: Approve, Reject, Abstain");

        var userId = GetCurrentUserId();
        var handler = _serviceProvider.GetRequiredService<CastVoteHandler>();
        var result = await handler.Handle(new CastVoteCommand(id, userId, vote, request.Comment), ct);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>
    /// Add a comment to committee review.
    /// </summary>
    [HttpPost("{id:guid}/comments")]
    [ProducesResponseType(typeof(CommitteeReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddComment(
        Guid id,
        [FromBody] AddCommentRequest request,
        CancellationToken ct)
    {
        if (!Enum.TryParse<CommentVisibility>(request.Visibility, out var visibility))
            visibility = CommentVisibility.Committee;

        var userId = GetCurrentUserId();
        var handler = _serviceProvider.GetRequiredService<AddCommitteeCommentHandler>();
        var result = await handler.Handle(new AddCommitteeCommentCommand(id, userId, request.Content, visibility), ct);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>
    /// Record final decision on committee review.
    /// </summary>
    [HttpPost("{id:guid}/decision")]
    [Authorize(Roles = "CommitteeMember,CreditOfficer,RiskManager,SystemAdmin")]
    [ProducesResponseType(typeof(CommitteeReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RecordDecision(
        Guid id,
        [FromBody] RecordDecisionRequest request,
        CancellationToken ct)
    {
        if (!Enum.TryParse<CommitteeDecision>(request.Decision, out var decision))
            return BadRequest($"Invalid decision: {request.Decision}");

        var userId = GetCurrentUserId();
        var handler = _serviceProvider.GetRequiredService<RecordCommitteeDecisionHandler>();
        var result = await handler.Handle(new RecordCommitteeDecisionCommand(
            id,
            userId,
            decision,
            request.Rationale,
            request.ApprovedAmount,
            request.ApprovedTenorMonths,
            request.ApprovedInterestRate,
            request.Conditions), ct);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    /// <summary>
    /// Close a committee review.
    /// </summary>
    [HttpPost("{id:guid}/close")]
    [Authorize(Roles = "CreditOfficer,RiskManager,SystemAdmin")]
    [ProducesResponseType(typeof(CommitteeReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Close(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var handler = _serviceProvider.GetRequiredService<CloseCommitteeReviewHandler>();
        var result = await handler.Handle(new CloseCommitteeReviewCommand(id, userId), ct);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("sub") ?? User.FindFirst("userId");
        return userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId)
            ? userId
            : Guid.Empty;
    }
}

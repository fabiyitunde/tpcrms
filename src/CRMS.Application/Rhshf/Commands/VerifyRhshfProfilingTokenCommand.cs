using CRMS.Application.Common;
using CRMS.Application.Rhshf.DTOs;
using CRMS.Application.Rhshf.Interfaces;
using CRMS.Domain.Interfaces;
using static CRMS.Application.Rhshf.RhshfStatusMapper;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>
/// Authenticates the profiling form's initial page load (§4.3) — not yet wired to a controller
/// (Phase 3 will call this), built now as the underlying single-use verification capability.
/// Validates the token's signature/expiry, confirms it was issued for THIS case (not another one),
/// and consumes it (single-use, design doc §6 #5).
/// </summary>
public record VerifyRhshfProfilingTokenCommand(string Reference, string Token)
    : IRequest<ApplicationResult<RhshfTokenVerificationResultDto>>;

public class VerifyRhshfProfilingTokenHandler
    : IRequestHandler<VerifyRhshfProfilingTokenCommand, ApplicationResult<RhshfTokenVerificationResultDto>>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfTokenService _tokenService;
    private readonly IUnitOfWork _uow;

    public VerifyRhshfProfilingTokenHandler(IRhshfCreditProfileRepository repo, IRhshfTokenService tokenService, IUnitOfWork uow)
    {
        _repo = repo;
        _tokenService = tokenService;
        _uow = uow;
    }

    public async Task<ApplicationResult<RhshfTokenVerificationResultDto>> Handle(
        VerifyRhshfProfilingTokenCommand request, CancellationToken ct = default)
    {
        var claims = _tokenService.ValidateToken(request.Token);
        if (claims is null)
            return ApplicationResult<RhshfTokenVerificationResultDto>.Failure("Token is invalid or expired.");

        // A token for case A must never open case B, even if both tokens are individually valid.
        if (!string.Equals(claims.Reference, request.Reference, StringComparison.Ordinal))
            return ApplicationResult<RhshfTokenVerificationResultDto>.Failure("Token was not issued for this case.");

        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult<RhshfTokenVerificationResultDto>.Failure("Case not found.");

        var consumeResult = profile.ConsumeToken(claims.Jti);
        if (consumeResult.IsFailure)
            return ApplicationResult<RhshfTokenVerificationResultDto>.Failure(consumeResult.Error);

        await _uow.SaveChangesAsync(ct);

        return ApplicationResult<RhshfTokenVerificationResultDto>.Success(
            new RhshfTokenVerificationResultDto(profile.Reference, profile.Status.ToWireFormat(), profile.CurrentStage?.ToString()));
    }
}

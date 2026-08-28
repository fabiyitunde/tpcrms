using CRMS.Application.Common;
using CRMS.Application.Rhshf.DTOs;
using CRMS.Application.Rhshf.Interfaces;
using CRMS.Domain.Interfaces;
using static CRMS.Application.Rhshf.RhshfStatusMapper;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>§4.6 of the integration brief — issues a fresh token for an existing, non-terminal
/// case without altering its status/stage.</summary>
public record RefreshRhshfTokenCommand(string Reference) : IRequest<ApplicationResult<RhshfTokenRefreshResultDto>>;

public class RefreshRhshfTokenHandler : IRequestHandler<RefreshRhshfTokenCommand, ApplicationResult<RhshfTokenRefreshResultDto>>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfTokenService _tokenService;
    private readonly IUnitOfWork _uow;

    public RefreshRhshfTokenHandler(IRhshfCreditProfileRepository repo, IRhshfTokenService tokenService, IUnitOfWork uow)
    {
        _repo = repo;
        _tokenService = tokenService;
        _uow = uow;
    }

    public async Task<ApplicationResult<RhshfTokenRefreshResultDto>> Handle(
        RefreshRhshfTokenCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult<RhshfTokenRefreshResultDto>.Failure("Case not found.");

        if (profile.IsTerminal)
            return ApplicationResult<RhshfTokenRefreshResultDto>.Failure(
                $"Case is already {profile.Status.ToWireFormat()} — no further profiling is possible.");

        var issued = _tokenService.IssueToken(profile.Id, profile.Reference, profile.FacId, profile.ProgrammeCode);
        profile.IssueToken(issued.Jti, DateTime.UtcNow, issued.ExpiresAt);
        await _uow.SaveChangesAsync(ct);

        return ApplicationResult<RhshfTokenRefreshResultDto>.Success(
            new RhshfTokenRefreshResultDto(issued.Token, issued.ExpiresAt));
    }
}

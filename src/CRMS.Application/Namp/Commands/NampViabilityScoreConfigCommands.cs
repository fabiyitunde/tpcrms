using CRMS.Application.Common;
using CRMS.Application.Namp.DTOs;
using CRMS.Domain.Aggregates.Namp;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Namp.Commands;

// ── Update ────────────────────────────────────────────────────────────────

public record UpdateNampViabilityScoreConfigCommand(
    Guid Id,
    decimal Score,
    decimal CategoryWeight,
    string? Description
) : IRequest<ApplicationResult<NampViabilityScoreConfigDto>>;

public class UpdateNampViabilityScoreConfigHandler
    : IRequestHandler<UpdateNampViabilityScoreConfigCommand, ApplicationResult<NampViabilityScoreConfigDto>>
{
    private readonly INampViabilityScoreConfigRepository _repo;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateNampViabilityScoreConfigHandler(
        INampViabilityScoreConfigRepository repo, IUnitOfWork unitOfWork)
    {
        _repo = repo;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<NampViabilityScoreConfigDto>> Handle(
        UpdateNampViabilityScoreConfigCommand request, CancellationToken ct = default)
    {
        if (request.Score < 0 || request.Score > 100)
            return ApplicationResult<NampViabilityScoreConfigDto>.Failure("Score must be between 0 and 100.");
        if (request.CategoryWeight <= 0)
            return ApplicationResult<NampViabilityScoreConfigDto>.Failure("Category weight must be greater than 0.");

        var config = await _repo.GetByIdAsync(request.Id, ct);
        if (config is null)
            return ApplicationResult<NampViabilityScoreConfigDto>.Failure("Viability score config not found.");

        config.Update(request.Score, request.CategoryWeight, request.Description);
        _repo.Update(config);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<NampViabilityScoreConfigDto>.Success(MapToDto(config));
    }

    internal static NampViabilityScoreConfigDto MapToDto(NampViabilityScoreConfig c) => new(
        c.Id,
        c.ViabilityRating.ToString(),
        c.Score,
        c.CategoryWeight,
        c.Description,
        c.IsActive,
        c.CreatedAt,
        c.ModifiedAt
    );
}

// ── Toggle Active ─────────────────────────────────────────────────────────

public record ToggleNampViabilityScoreConfigCommand(Guid Id, bool Activate)
    : IRequest<ApplicationResult<NampViabilityScoreConfigDto>>;

public class ToggleNampViabilityScoreConfigHandler
    : IRequestHandler<ToggleNampViabilityScoreConfigCommand, ApplicationResult<NampViabilityScoreConfigDto>>
{
    private readonly INampViabilityScoreConfigRepository _repo;
    private readonly IUnitOfWork _unitOfWork;

    public ToggleNampViabilityScoreConfigHandler(
        INampViabilityScoreConfigRepository repo, IUnitOfWork unitOfWork)
    {
        _repo = repo;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<NampViabilityScoreConfigDto>> Handle(
        ToggleNampViabilityScoreConfigCommand request, CancellationToken ct = default)
    {
        var config = await _repo.GetByIdAsync(request.Id, ct);
        if (config is null)
            return ApplicationResult<NampViabilityScoreConfigDto>.Failure("Viability score config not found.");

        if (request.Activate) config.Activate();
        else config.Deactivate();

        _repo.Update(config);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<NampViabilityScoreConfigDto>.Success(
            UpdateNampViabilityScoreConfigHandler.MapToDto(config));
    }
}

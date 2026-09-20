using CRMS.Application.Common;
using CRMS.Application.Rhshf.DTOs;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

public record CreateRhshfRoutingConfigCommand(
    string Tier,
    decimal MinEopValue,
    decimal MaxEopValue,
    int Priority,
    Guid CreatedByUserId
) : IRequest<ApplicationResult<RhshfRoutingConfigDto>>;

public class CreateRhshfRoutingConfigHandler
    : IRequestHandler<CreateRhshfRoutingConfigCommand, ApplicationResult<RhshfRoutingConfigDto>>
{
    private readonly IRhshfRoutingConfigRepository _repo;
    private readonly IUnitOfWork _uow;

    public CreateRhshfRoutingConfigHandler(IRhshfRoutingConfigRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult<RhshfRoutingConfigDto>> Handle(
        CreateRhshfRoutingConfigCommand request, CancellationToken ct = default)
    {
        if (!Enum.TryParse<CommitteeType>(request.Tier, ignoreCase: true, out var tier))
            return ApplicationResult<RhshfRoutingConfigDto>.Failure($"Unknown committee tier: '{request.Tier}'.");

        var createResult = RhshfRoutingConfig.Create(tier, request.MinEopValue, request.MaxEopValue, request.Priority);
        if (createResult.IsFailure)
            return ApplicationResult<RhshfRoutingConfigDto>.Failure(createResult.Error);

        var config = createResult.Value;
        config.SetAuditInfo(request.CreatedByUserId.ToString(), isNew: true);

        await _repo.AddAsync(config, ct);
        await _uow.SaveChangesAsync(ct);

        return ApplicationResult<RhshfRoutingConfigDto>.Success(MapToDto(config));
    }

    internal static RhshfRoutingConfigDto MapToDto(RhshfRoutingConfig c) => new(
        c.Id, c.Tier.ToString(), c.MinEopValue, c.MaxEopValue, c.Priority, c.IsActive, c.CreatedAt);
}

public record UpdateRhshfRoutingConfigCommand(
    Guid Id,
    decimal MinEopValue,
    decimal MaxEopValue,
    int Priority,
    Guid UpdatedByUserId
) : IRequest<ApplicationResult<RhshfRoutingConfigDto>>;

public class UpdateRhshfRoutingConfigHandler
    : IRequestHandler<UpdateRhshfRoutingConfigCommand, ApplicationResult<RhshfRoutingConfigDto>>
{
    private readonly IRhshfRoutingConfigRepository _repo;
    private readonly IUnitOfWork _uow;

    public UpdateRhshfRoutingConfigHandler(IRhshfRoutingConfigRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult<RhshfRoutingConfigDto>> Handle(
        UpdateRhshfRoutingConfigCommand request, CancellationToken ct = default)
    {
        var config = await _repo.GetByIdAsync(request.Id, ct);
        if (config is null) return ApplicationResult<RhshfRoutingConfigDto>.Failure("Routing config not found.");

        var updateResult = config.Update(request.MinEopValue, request.MaxEopValue, request.Priority);
        if (updateResult.IsFailure)
            return ApplicationResult<RhshfRoutingConfigDto>.Failure(updateResult.Error);

        config.SetAuditInfo(request.UpdatedByUserId.ToString());
        _repo.Update(config);
        await _uow.SaveChangesAsync(ct);

        return ApplicationResult<RhshfRoutingConfigDto>.Success(CreateRhshfRoutingConfigHandler.MapToDto(config));
    }
}

public record ToggleRhshfRoutingConfigCommand(Guid Id, bool Activate, Guid UserId)
    : IRequest<ApplicationResult<RhshfRoutingConfigDto>>;

public class ToggleRhshfRoutingConfigHandler
    : IRequestHandler<ToggleRhshfRoutingConfigCommand, ApplicationResult<RhshfRoutingConfigDto>>
{
    private readonly IRhshfRoutingConfigRepository _repo;
    private readonly IUnitOfWork _uow;

    public ToggleRhshfRoutingConfigHandler(IRhshfRoutingConfigRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult<RhshfRoutingConfigDto>> Handle(
        ToggleRhshfRoutingConfigCommand request, CancellationToken ct = default)
    {
        var config = await _repo.GetByIdAsync(request.Id, ct);
        if (config is null) return ApplicationResult<RhshfRoutingConfigDto>.Failure("Routing config not found.");

        if (request.Activate) config.Activate(); else config.Deactivate();
        config.SetAuditInfo(request.UserId.ToString());
        _repo.Update(config);
        await _uow.SaveChangesAsync(ct);

        return ApplicationResult<RhshfRoutingConfigDto>.Success(CreateRhshfRoutingConfigHandler.MapToDto(config));
    }
}

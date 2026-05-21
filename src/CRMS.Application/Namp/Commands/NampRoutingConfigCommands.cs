using CRMS.Application.Common;
using CRMS.Application.Namp.DTOs;
using CRMS.Domain.Aggregates.Namp;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Namp.Commands;

public record CreateNampRoutingConfigCommand(
    string ApplicantCategory,
    string CommitteeTier,
    decimal MinEquipmentValue,
    decimal MaxEquipmentValue,
    int Priority,
    Guid CreatedByUserId
) : IRequest<ApplicationResult<NampRoutingConfigDto>>;

public class CreateNampRoutingConfigHandler
    : IRequestHandler<CreateNampRoutingConfigCommand, ApplicationResult<NampRoutingConfigDto>>
{
    private readonly INampRoutingConfigRepository _repo;
    private readonly IUnitOfWork _uow;

    public CreateNampRoutingConfigHandler(INampRoutingConfigRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult<NampRoutingConfigDto>> Handle(
        CreateNampRoutingConfigCommand request, CancellationToken ct = default)
    {
        if (!Enum.TryParse<NampApplicantCategory>(request.ApplicantCategory, ignoreCase: true, out var category))
            return ApplicationResult<NampRoutingConfigDto>.Failure($"Unknown applicant category: '{request.ApplicantCategory}'.");

        if (!Enum.TryParse<NampCommitteeTier>(request.CommitteeTier, ignoreCase: true, out var tier))
            return ApplicationResult<NampRoutingConfigDto>.Failure($"Unknown committee tier: '{request.CommitteeTier}'.");

        if (request.MinEquipmentValue < 0 || request.MaxEquipmentValue <= request.MinEquipmentValue)
            return ApplicationResult<NampRoutingConfigDto>.Failure("Max equipment value must be greater than min.");

        var config = NampRoutingConfig.Create(category, tier, request.MinEquipmentValue, request.MaxEquipmentValue, request.Priority);
        config.SetAuditInfo(request.CreatedByUserId.ToString(), isNew: true);

        await _repo.AddAsync(config, ct);
        await _uow.SaveChangesAsync(ct);

        return ApplicationResult<NampRoutingConfigDto>.Success(MapToDto(config));
    }

    private static NampRoutingConfigDto MapToDto(NampRoutingConfig c) => new(
        c.Id, c.ApplicantCategory.ToString(), c.CommitteeTier.ToString(),
        c.MinEquipmentValue, c.MaxEquipmentValue, c.Priority, c.IsActive, c.CreatedAt);
}

public record UpdateNampRoutingConfigCommand(
    Guid Id,
    decimal MinEquipmentValue,
    decimal MaxEquipmentValue,
    int Priority,
    Guid UpdatedByUserId
) : IRequest<ApplicationResult<NampRoutingConfigDto>>;

public class UpdateNampRoutingConfigHandler
    : IRequestHandler<UpdateNampRoutingConfigCommand, ApplicationResult<NampRoutingConfigDto>>
{
    private readonly INampRoutingConfigRepository _repo;
    private readonly IUnitOfWork _uow;

    public UpdateNampRoutingConfigHandler(INampRoutingConfigRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult<NampRoutingConfigDto>> Handle(
        UpdateNampRoutingConfigCommand request, CancellationToken ct = default)
    {
        var config = await _repo.GetByIdAsync(request.Id, ct);
        if (config is null) return ApplicationResult<NampRoutingConfigDto>.Failure("Routing config not found.");

        if (request.MaxEquipmentValue <= request.MinEquipmentValue)
            return ApplicationResult<NampRoutingConfigDto>.Failure("Max equipment value must be greater than min.");

        config.Update(request.MinEquipmentValue, request.MaxEquipmentValue, request.Priority);
        config.SetAuditInfo(request.UpdatedByUserId.ToString());
        _repo.Update(config);
        await _uow.SaveChangesAsync(ct);

        return ApplicationResult<NampRoutingConfigDto>.Success(new NampRoutingConfigDto(
            config.Id, config.ApplicantCategory.ToString(), config.CommitteeTier.ToString(),
            config.MinEquipmentValue, config.MaxEquipmentValue, config.Priority, config.IsActive, config.CreatedAt));
    }
}

public record ToggleNampRoutingConfigCommand(Guid Id, bool Activate, Guid UserId)
    : IRequest<ApplicationResult<NampRoutingConfigDto>>;

public class ToggleNampRoutingConfigHandler
    : IRequestHandler<ToggleNampRoutingConfigCommand, ApplicationResult<NampRoutingConfigDto>>
{
    private readonly INampRoutingConfigRepository _repo;
    private readonly IUnitOfWork _uow;

    public ToggleNampRoutingConfigHandler(INampRoutingConfigRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult<NampRoutingConfigDto>> Handle(
        ToggleNampRoutingConfigCommand request, CancellationToken ct = default)
    {
        var config = await _repo.GetByIdAsync(request.Id, ct);
        if (config is null) return ApplicationResult<NampRoutingConfigDto>.Failure("Routing config not found.");

        if (request.Activate) config.Activate(); else config.Deactivate();
        config.SetAuditInfo(request.UserId.ToString());
        _repo.Update(config);
        await _uow.SaveChangesAsync(ct);

        return ApplicationResult<NampRoutingConfigDto>.Success(new NampRoutingConfigDto(
            config.Id, config.ApplicantCategory.ToString(), config.CommitteeTier.ToString(),
            config.MinEquipmentValue, config.MaxEquipmentValue, config.Priority, config.IsActive, config.CreatedAt));
    }
}

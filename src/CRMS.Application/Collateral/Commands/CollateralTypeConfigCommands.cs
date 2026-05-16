using CRMS.Application.Collateral.DTOs;
using CRMS.Application.Common;
using CRMS.Domain.Aggregates.Collateral;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Collateral.Commands;

public record CreateCollateralTypeConfigCommand(
    string Name,
    string Code,
    decimal HaircutRate,
    string ValuationBasis,
    Guid CreatedByUserId,
    string? Description = null,
    int SortOrder = 0
) : IRequest<ApplicationResult<CollateralTypeConfigDto>>;

public class CreateCollateralTypeConfigHandler : IRequestHandler<CreateCollateralTypeConfigCommand, ApplicationResult<CollateralTypeConfigDto>>
{
    private readonly ICollateralTypeConfigRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCollateralTypeConfigHandler(ICollateralTypeConfigRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<CollateralTypeConfigDto>> Handle(CreateCollateralTypeConfigCommand request, CancellationToken ct = default)
    {
        var existing = await _repository.GetByCodeAsync(request.Code, ct);
        if (existing != null)
            return ApplicationResult<CollateralTypeConfigDto>.Failure($"A collateral type with code '{request.Code}' already exists.");

        var result = CollateralTypeConfig.Create(
            request.Name, request.Code, request.HaircutRate, request.ValuationBasis,
            request.CreatedByUserId, request.Description, request.SortOrder);

        if (result.IsFailure)
            return ApplicationResult<CollateralTypeConfigDto>.Failure(result.Error);

        await _repository.AddAsync(result.Value, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<CollateralTypeConfigDto>.Success(MapToDto(result.Value));
    }

    private static CollateralTypeConfigDto MapToDto(CollateralTypeConfig c) => new(
        c.Id, c.Name, c.Code, c.Description, c.HaircutRate, c.ValuationBasis, c.IsActive, c.SortOrder);
}

public record UpdateCollateralTypeConfigCommand(
    Guid Id,
    string Name,
    decimal HaircutRate,
    string ValuationBasis,
    Guid ModifiedByUserId,
    string? Description = null,
    int? SortOrder = null
) : IRequest<ApplicationResult>;

public class UpdateCollateralTypeConfigHandler : IRequestHandler<UpdateCollateralTypeConfigCommand, ApplicationResult>
{
    private readonly ICollateralTypeConfigRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCollateralTypeConfigHandler(ICollateralTypeConfigRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(UpdateCollateralTypeConfigCommand request, CancellationToken ct = default)
    {
        var config = await _repository.GetByIdAsync(request.Id, ct);
        if (config == null)
            return ApplicationResult.Failure("Collateral type configuration not found.");

        var result = config.Update(request.Name, request.HaircutRate, request.ValuationBasis,
            request.ModifiedByUserId, request.Description, request.SortOrder);

        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        _repository.Update(config);
        await _unitOfWork.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}

public record ToggleCollateralTypeConfigCommand(
    Guid Id,
    bool Activate,
    Guid ModifiedByUserId
) : IRequest<ApplicationResult>;

public class ToggleCollateralTypeConfigHandler : IRequestHandler<ToggleCollateralTypeConfigCommand, ApplicationResult>
{
    private readonly ICollateralTypeConfigRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ToggleCollateralTypeConfigHandler(ICollateralTypeConfigRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(ToggleCollateralTypeConfigCommand request, CancellationToken ct = default)
    {
        var config = await _repository.GetByIdAsync(request.Id, ct);
        if (config == null)
            return ApplicationResult.Failure("Collateral type configuration not found.");

        if (request.Activate)
            config.Activate(request.ModifiedByUserId);
        else
            config.Deactivate(request.ModifiedByUserId);

        _repository.Update(config);
        await _unitOfWork.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}

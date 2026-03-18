using CRMS.Application.Common;
using CRMS.Application.Location.DTOs;
using CRMS.Domain.Aggregates.Location;
using CRMS.Domain.Interfaces;
using Loc = CRMS.Domain.Aggregates.Location;

namespace CRMS.Application.Location.Commands;

public record CreateLocationCommand(
    string Code,
    string Name,
    LocationType Type,
    Guid? ParentLocationId,
    string? Address,
    string? ManagerName,
    string? ContactPhone,
    string? ContactEmail,
    int SortOrder = 0
) : IRequest<ApplicationResult<LocationDto>>;

public class CreateLocationHandler : IRequestHandler<CreateLocationCommand, ApplicationResult<LocationDto>>
{
    private readonly ILocationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateLocationHandler(ILocationRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<LocationDto>> Handle(CreateLocationCommand request, CancellationToken ct = default)
    {
        // Check if code already exists
        if (await _repository.CodeExistsAsync(request.Code, null, ct))
            return ApplicationResult<LocationDto>.Failure($"Location code '{request.Code}' already exists");

        // Validate parent exists if specified
        Loc.Location? parent = null;
        if (request.ParentLocationId.HasValue)
        {
            parent = await _repository.GetByIdAsync(request.ParentLocationId.Value, ct);
            if (parent == null)
                return ApplicationResult<LocationDto>.Failure("Parent location not found");
        }

        // Create location
        var result = Loc.Location.Create(
            request.Code,
            request.Name,
            request.Type,
            request.ParentLocationId,
            request.Address,
            request.ManagerName,
            request.ContactPhone,
            request.ContactEmail,
            request.SortOrder
        );

        if (result.IsFailure)
            return ApplicationResult<LocationDto>.Failure(result.Error);

        var location = result.Value;

        // Validate parent type if parent exists
        if (parent != null)
        {
            var validateResult = location.ValidateParentType(parent.Type);
            if (validateResult.IsFailure)
                return ApplicationResult<LocationDto>.Failure(validateResult.Error);
        }

        await _repository.AddAsync(location, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<LocationDto>.Success(MapToDto(location, parent?.Name));
    }

    private static LocationDto MapToDto(Loc.Location loc, string? parentName) => new(
        loc.Id,
        loc.Code,
        loc.Name,
        loc.Type.ToString(),
        loc.ParentLocationId,
        parentName,
        loc.IsActive,
        loc.Address,
        loc.ManagerName,
        loc.ContactPhone,
        loc.ContactEmail,
        loc.SortOrder,
        loc.Children.Count
    );
}

public record UpdateLocationCommand(
    Guid Id,
    string Name,
    string? Address,
    string? ManagerName,
    string? ContactPhone,
    string? ContactEmail,
    int SortOrder
) : IRequest<ApplicationResult<LocationDto>>;

public class UpdateLocationHandler : IRequestHandler<UpdateLocationCommand, ApplicationResult<LocationDto>>
{
    private readonly ILocationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateLocationHandler(ILocationRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<LocationDto>> Handle(UpdateLocationCommand request, CancellationToken ct = default)
    {
        var location = await _repository.GetByIdAsync(request.Id, ct);
        if (location == null)
            return ApplicationResult<LocationDto>.Failure("Location not found");

        var result = location.Update(
            request.Name,
            request.Address,
            request.ManagerName,
            request.ContactPhone,
            request.ContactEmail,
            request.SortOrder
        );

        if (result.IsFailure)
            return ApplicationResult<LocationDto>.Failure(result.Error);

        _repository.Update(location);
        await _unitOfWork.SaveChangesAsync(ct);

        // Get parent name for DTO
        string? parentName = null;
        if (location.ParentLocationId.HasValue)
        {
            var parent = await _repository.GetByIdAsync(location.ParentLocationId.Value, ct);
            parentName = parent?.Name;
        }

        return ApplicationResult<LocationDto>.Success(new LocationDto(
            location.Id,
            location.Code,
            location.Name,
            location.Type.ToString(),
            location.ParentLocationId,
            parentName,
            location.IsActive,
            location.Address,
            location.ManagerName,
            location.ContactPhone,
            location.ContactEmail,
            location.SortOrder,
            location.Children.Count
        ));
    }
}

public record ActivateLocationCommand(Guid Id) : IRequest<ApplicationResult>;

public class ActivateLocationHandler : IRequestHandler<ActivateLocationCommand, ApplicationResult>
{
    private readonly ILocationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ActivateLocationHandler(ILocationRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(ActivateLocationCommand request, CancellationToken ct = default)
    {
        var location = await _repository.GetByIdAsync(request.Id, ct);
        if (location == null)
            return ApplicationResult.Failure("Location not found");

        var result = location.Activate();
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        _repository.Update(location);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}

public record DeactivateLocationCommand(Guid Id) : IRequest<ApplicationResult>;

public class DeactivateLocationHandler : IRequestHandler<DeactivateLocationCommand, ApplicationResult>
{
    private readonly ILocationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public DeactivateLocationHandler(ILocationRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(DeactivateLocationCommand request, CancellationToken ct = default)
    {
        var location = await _repository.GetByIdAsync(request.Id, ct);
        if (location == null)
            return ApplicationResult.Failure("Location not found");

        var result = location.Deactivate();
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        _repository.Update(location);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}

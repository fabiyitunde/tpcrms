using CRMS.Application.Common;
using CRMS.Application.Location.DTOs;
using CRMS.Domain.Aggregates.Location;
using CRMS.Domain.Interfaces;
using Loc = CRMS.Domain.Aggregates.Location;

namespace CRMS.Application.Location.Queries;

public record GetLocationByIdQuery(Guid Id) : IRequest<ApplicationResult<LocationDto>>;

public class GetLocationByIdHandler : IRequestHandler<GetLocationByIdQuery, ApplicationResult<LocationDto>>
{
    private readonly ILocationRepository _repository;

    public GetLocationByIdHandler(ILocationRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<LocationDto>> Handle(GetLocationByIdQuery request, CancellationToken ct = default)
    {
        var location = await _repository.GetByIdWithChildrenAsync(request.Id, ct);
        if (location == null)
            return ApplicationResult<LocationDto>.Failure("Location not found");

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

public record GetAllLocationsQuery(bool IncludeInactive = false) : IRequest<ApplicationResult<List<LocationDto>>>;

public class GetAllLocationsHandler : IRequestHandler<GetAllLocationsQuery, ApplicationResult<List<LocationDto>>>
{
    private readonly ILocationRepository _repository;

    public GetAllLocationsHandler(ILocationRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<List<LocationDto>>> Handle(GetAllLocationsQuery request, CancellationToken ct = default)
    {
        var locations = request.IncludeInactive
            ? await _repository.GetAllAsync(ct)
            : await _repository.GetAllActiveAsync(ct);

        // Build lookup for parent names
        var lookup = locations.ToDictionary(l => l.Id, l => l.Name);

        var dtos = locations.Select(loc => new LocationDto(
            loc.Id,
            loc.Code,
            loc.Name,
            loc.Type.ToString(),
            loc.ParentLocationId,
            loc.ParentLocationId.HasValue && lookup.TryGetValue(loc.ParentLocationId.Value, out var pn) ? pn : null,
            loc.IsActive,
            loc.Address,
            loc.ManagerName,
            loc.ContactPhone,
            loc.ContactEmail,
            loc.SortOrder,
            0 // Children count not loaded in flat query
        )).ToList();

        return ApplicationResult<List<LocationDto>>.Success(dtos);
    }
}

public record GetLocationsByTypeQuery(LocationType Type) : IRequest<ApplicationResult<List<LocationSummaryDto>>>;

public class GetLocationsByTypeHandler : IRequestHandler<GetLocationsByTypeQuery, ApplicationResult<List<LocationSummaryDto>>>
{
    private readonly ILocationRepository _repository;

    public GetLocationsByTypeHandler(ILocationRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<List<LocationSummaryDto>>> Handle(GetLocationsByTypeQuery request, CancellationToken ct = default)
    {
        var locations = await _repository.GetByTypeAsync(request.Type, ct);

        var dtos = locations.Select(loc => new LocationSummaryDto(
            loc.Id,
            loc.Code,
            loc.Name,
            loc.Type.ToString(),
            loc.IsActive
        )).ToList();

        return ApplicationResult<List<LocationSummaryDto>>.Success(dtos);
    }
}

public record GetLocationTreeQuery() : IRequest<ApplicationResult<LocationTreeNodeDto?>>;

public class GetLocationTreeHandler : IRequestHandler<GetLocationTreeQuery, ApplicationResult<LocationTreeNodeDto?>>
{
    private readonly ILocationRepository _repository;

    public GetLocationTreeHandler(ILocationRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<LocationTreeNodeDto?>> Handle(GetLocationTreeQuery request, CancellationToken ct = default)
    {
        var root = await _repository.GetHierarchyTreeAsync(ct);
        if (root == null)
            return ApplicationResult<LocationTreeNodeDto?>.Success(null);

        var dto = MapToTreeNode(root);
        return ApplicationResult<LocationTreeNodeDto?>.Success(dto);
    }

    private static LocationTreeNodeDto MapToTreeNode(Loc.Location location)
    {
        return new LocationTreeNodeDto(
            location.Id,
            location.Code,
            location.Name,
            location.Type.ToString(),
            location.IsActive,
            location.ManagerName,
            location.SortOrder,
            location.Children
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .Select(MapToTreeNode)
                .ToList()
        );
    }
}

public record GetChildLocationsQuery(Guid ParentId) : IRequest<ApplicationResult<List<LocationSummaryDto>>>;

public class GetChildLocationsHandler : IRequestHandler<GetChildLocationsQuery, ApplicationResult<List<LocationSummaryDto>>>
{
    private readonly ILocationRepository _repository;

    public GetChildLocationsHandler(ILocationRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<List<LocationSummaryDto>>> Handle(GetChildLocationsQuery request, CancellationToken ct = default)
    {
        var children = await _repository.GetChildrenAsync(request.ParentId, ct);

        var dtos = children.Select(loc => new LocationSummaryDto(
            loc.Id,
            loc.Code,
            loc.Name,
            loc.Type.ToString(),
            loc.IsActive
        )).ToList();

        return ApplicationResult<List<LocationSummaryDto>>.Success(dtos);
    }
}

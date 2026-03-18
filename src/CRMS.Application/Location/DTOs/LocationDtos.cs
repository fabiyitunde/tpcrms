using CRMS.Domain.Aggregates.Location;

namespace CRMS.Application.Location.DTOs;

public record LocationDto(
    Guid Id,
    string Code,
    string Name,
    string Type,
    Guid? ParentLocationId,
    string? ParentLocationName,
    bool IsActive,
    string? Address,
    string? ManagerName,
    string? ContactPhone,
    string? ContactEmail,
    int SortOrder,
    int ChildCount
);

public record LocationSummaryDto(
    Guid Id,
    string Code,
    string Name,
    string Type,
    bool IsActive
);

public record LocationTreeNodeDto(
    Guid Id,
    string Code,
    string Name,
    string Type,
    bool IsActive,
    string? ManagerName,
    int SortOrder,
    List<LocationTreeNodeDto> Children
);

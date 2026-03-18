using CRMS.Domain.Common;

namespace CRMS.Domain.Aggregates.Location;

/// <summary>
/// Represents a location in the organizational hierarchy.
/// Hierarchy: HeadOffice → Region → Zone → Branch
/// </summary>
public class Location : AggregateRoot
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public LocationType Type { get; private set; }
    public Guid? ParentLocationId { get; private set; }
    public bool IsActive { get; private set; }
    public string? Address { get; private set; }
    public string? ManagerName { get; private set; }
    public string? ContactPhone { get; private set; }
    public string? ContactEmail { get; private set; }
    public int SortOrder { get; private set; }

    // Navigation properties
    public Location? Parent { get; private set; }
    
    private readonly List<Location> _children = [];
    public IReadOnlyCollection<Location> Children => _children.AsReadOnly();

    private Location() { }

    public static Result<Location> Create(
        string code,
        string name,
        LocationType type,
        Guid? parentLocationId = null,
        string? address = null,
        string? managerName = null,
        string? contactPhone = null,
        string? contactEmail = null,
        int sortOrder = 0)
    {
        if (string.IsNullOrWhiteSpace(code))
            return Result.Failure<Location>("Location code is required");

        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<Location>("Location name is required");

        // Validate hierarchy rules
        if (type == LocationType.HeadOffice && parentLocationId.HasValue)
            return Result.Failure<Location>("HeadOffice cannot have a parent location");

        if (type != LocationType.HeadOffice && !parentLocationId.HasValue)
            return Result.Failure<Location>($"{type} must have a parent location");

        return Result.Success(new Location
        {
            Code = code.ToUpperInvariant(),
            Name = name,
            Type = type,
            ParentLocationId = parentLocationId,
            IsActive = true,
            Address = address,
            ManagerName = managerName,
            ContactPhone = contactPhone,
            ContactEmail = contactEmail,
            SortOrder = sortOrder
        });
    }

    /// <summary>
    /// Creates a HeadOffice location (root of hierarchy).
    /// </summary>
    public static Result<Location> CreateHeadOffice(string code, string name, string? address = null)
    {
        return Create(code, name, LocationType.HeadOffice, null, address);
    }

    /// <summary>
    /// Creates a Region under HeadOffice.
    /// </summary>
    public static Result<Location> CreateRegion(string code, string name, Guid headOfficeId, int sortOrder = 0)
    {
        return Create(code, name, LocationType.Region, headOfficeId, sortOrder: sortOrder);
    }

    /// <summary>
    /// Creates a Zone under a Region.
    /// </summary>
    public static Result<Location> CreateZone(string code, string name, Guid regionId, int sortOrder = 0)
    {
        return Create(code, name, LocationType.Zone, regionId, sortOrder: sortOrder);
    }

    /// <summary>
    /// Creates a Branch under a Zone.
    /// </summary>
    public static Result<Location> CreateBranch(
        string code, 
        string name, 
        Guid zoneId, 
        string? address = null,
        string? managerName = null,
        string? contactPhone = null,
        int sortOrder = 0)
    {
        return Create(code, name, LocationType.Branch, zoneId, address, managerName, contactPhone, sortOrder: sortOrder);
    }

    public Result Update(
        string name,
        string? address,
        string? managerName,
        string? contactPhone,
        string? contactEmail,
        int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure("Location name is required");

        Name = name;
        Address = address;
        ManagerName = managerName;
        ContactPhone = contactPhone;
        ContactEmail = contactEmail;
        SortOrder = sortOrder;

        return Result.Success();
    }

    public Result Activate()
    {
        if (IsActive)
            return Result.Failure("Location is already active");

        IsActive = true;
        return Result.Success();
    }

    public Result Deactivate()
    {
        if (!IsActive)
            return Result.Failure("Location is already inactive");

        IsActive = false;
        return Result.Success();
    }

    /// <summary>
    /// Validates that the parent type is correct for this location type.
    /// </summary>
    public Result ValidateParentType(LocationType parentType)
    {
        var expectedParentType = Type switch
        {
            LocationType.Region => LocationType.HeadOffice,
            LocationType.Zone => LocationType.Region,
            LocationType.Branch => LocationType.Zone,
            _ => (LocationType?)null
        };

        if (expectedParentType.HasValue && parentType != expectedParentType.Value)
            return Result.Failure($"{Type} must have a {expectedParentType.Value} as parent, not {parentType}");

        return Result.Success();
    }

    /// <summary>
    /// Adds a child location to this location's children collection.
    /// Used for building the hierarchy tree in memory.
    /// </summary>
    public void AddChild(Location child)
    {
        if (!_children.Contains(child))
            _children.Add(child);
    }
}

/// <summary>
/// Type of location in the organizational hierarchy.
/// </summary>
public enum LocationType
{
    HeadOffice = 0,
    Region = 1,
    Zone = 2,
    Branch = 3
}

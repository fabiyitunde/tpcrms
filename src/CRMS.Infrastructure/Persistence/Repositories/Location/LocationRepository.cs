using CRMS.Domain.Aggregates.Location;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories.Location;

public class LocationRepository : ILocationRepository
{
    private readonly CRMSDbContext _context;

    public LocationRepository(CRMSDbContext context)
    {
        _context = context;
    }

    public async Task<Domain.Aggregates.Location.Location?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Locations
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<Domain.Aggregates.Location.Location?> GetByIdWithChildrenAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Locations
            .Include(x => x.Children)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<Domain.Aggregates.Location.Location?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var normalizedCode = code.ToUpperInvariant();
        return await _context.Locations
            .FirstOrDefaultAsync(x => x.Code == normalizedCode, ct);
    }

    public async Task<IReadOnlyList<Domain.Aggregates.Location.Location>> GetByTypeAsync(LocationType type, CancellationToken ct = default)
    {
        return await _context.Locations
            .Where(x => x.Type == type && x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Domain.Aggregates.Location.Location>> GetChildrenAsync(Guid parentId, CancellationToken ct = default)
    {
        return await _context.Locations
            .Where(x => x.ParentLocationId == parentId && x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Domain.Aggregates.Location.Location>> GetAllActiveAsync(CancellationToken ct = default)
    {
        return await _context.Locations
            .Where(x => x.IsActive)
            .OrderBy(x => x.Type)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Domain.Aggregates.Location.Location>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.Locations
            .OrderBy(x => x.Type)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> GetDescendantBranchIdsAsync(Guid locationId, CancellationToken ct = default)
    {
        var location = await _context.Locations
            .FirstOrDefaultAsync(x => x.Id == locationId, ct);

        if (location == null)
            return [];

        return location.Type switch
        {
            LocationType.Branch => [locationId],
            LocationType.Zone => await GetBranchIdsInZoneAsync(locationId, ct),
            LocationType.Region => await GetBranchIdsInRegionAsync(locationId, ct),
            LocationType.HeadOffice => await GetAllBranchIdsAsync(ct),
            _ => []
        };
    }

    private async Task<IReadOnlyList<Guid>> GetBranchIdsInZoneAsync(Guid zoneId, CancellationToken ct)
    {
        return await _context.Locations
            .Where(x => x.ParentLocationId == zoneId && x.Type == LocationType.Branch && x.IsActive)
            .Select(x => x.Id)
            .ToListAsync(ct);
    }

    private async Task<IReadOnlyList<Guid>> GetBranchIdsInRegionAsync(Guid regionId, CancellationToken ct)
    {
        // Get all zones in this region
        var zoneIds = await _context.Locations
            .Where(x => x.ParentLocationId == regionId && x.Type == LocationType.Zone && x.IsActive)
            .Select(x => x.Id)
            .ToListAsync(ct);

        // Get all branches in those zones
        return await _context.Locations
            .Where(x => zoneIds.Contains(x.ParentLocationId!.Value) && x.Type == LocationType.Branch && x.IsActive)
            .Select(x => x.Id)
            .ToListAsync(ct);
    }

    private async Task<IReadOnlyList<Guid>> GetAllBranchIdsAsync(CancellationToken ct)
    {
        return await _context.Locations
            .Where(x => x.Type == LocationType.Branch && x.IsActive)
            .Select(x => x.Id)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> GetAncestorIdsAsync(Guid locationId, CancellationToken ct = default)
    {
        var ancestors = new List<Guid>();
        var currentId = locationId;

        while (true)
        {
            var location = await _context.Locations
                .Where(x => x.Id == currentId)
                .Select(x => new { x.ParentLocationId })
                .FirstOrDefaultAsync(ct);

            if (location?.ParentLocationId == null)
                break;

            ancestors.Add(location.ParentLocationId.Value);
            currentId = location.ParentLocationId.Value;
        }

        return ancestors;
    }

    public async Task<Domain.Aggregates.Location.Location?> GetHierarchyTreeAsync(CancellationToken ct = default)
    {
        // Get all locations
        var allLocations = await _context.Locations
            .Where(x => x.IsActive)
            .OrderBy(x => x.Type)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(ct);

        if (allLocations.Count == 0)
            return null;

        // Build a lookup dictionary by ID
        var locationById = allLocations.ToDictionary(x => x.Id);

        // Build the tree by connecting parents to children
        foreach (var location in allLocations)
        {
            if (location.ParentLocationId.HasValue && 
                locationById.TryGetValue(location.ParentLocationId.Value, out var parent))
            {
                parent.AddChild(location);
            }
        }

        // Find and return the root (HeadOffice)
        return allLocations.FirstOrDefault(x => x.Type == LocationType.HeadOffice);
    }

    public async Task AddAsync(Domain.Aggregates.Location.Location location, CancellationToken ct = default)
    {
        await _context.Locations.AddAsync(location, ct);
    }

    public void Update(Domain.Aggregates.Location.Location location)
    {
        _context.Locations.Update(location);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Locations.AnyAsync(x => x.Id == id, ct);
    }

    public async Task<bool> CodeExistsAsync(string code, Guid? excludeId = null, CancellationToken ct = default)
    {
        var normalizedCode = code.ToUpperInvariant();
        var query = _context.Locations.Where(x => x.Code == normalizedCode);
        
        if (excludeId.HasValue)
            query = query.Where(x => x.Id != excludeId.Value);

        return await query.AnyAsync(ct);
    }
}

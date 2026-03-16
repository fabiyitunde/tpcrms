using CRMS.Domain.Aggregates.Location;

namespace CRMS.Domain.Interfaces;

public interface ILocationRepository
{
    Task<Location?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Location?> GetByIdWithChildrenAsync(Guid id, CancellationToken ct = default);
    Task<Location?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<IReadOnlyList<Location>> GetByTypeAsync(LocationType type, CancellationToken ct = default);
    Task<IReadOnlyList<Location>> GetChildrenAsync(Guid parentId, CancellationToken ct = default);
    Task<IReadOnlyList<Location>> GetAllActiveAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Location>> GetAllAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Gets all branch IDs that are descendants of the given location.
    /// For a Branch: returns just that branch ID.
    /// For a Zone: returns all branch IDs in that zone.
    /// For a Region: returns all branch IDs in all zones in that region.
    /// For HeadOffice: returns all branch IDs.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetDescendantBranchIdsAsync(Guid locationId, CancellationToken ct = default);
    
    /// <summary>
    /// Gets all ancestor location IDs for a given location (parent, grandparent, etc.).
    /// </summary>
    Task<IReadOnlyList<Guid>> GetAncestorIdsAsync(Guid locationId, CancellationToken ct = default);
    
    /// <summary>
    /// Gets the full hierarchy tree starting from HeadOffice.
    /// </summary>
    Task<Location?> GetHierarchyTreeAsync(CancellationToken ct = default);
    
    Task AddAsync(Location location, CancellationToken ct = default);
    void Update(Location location);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
    Task<bool> CodeExistsAsync(string code, Guid? excludeId = null, CancellationToken ct = default);
}

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using CRMS.Domain.Aggregates.Location;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CRMS.Infrastructure.Persistence;

/// <summary>
/// Seeds CRMS locations from a Fineract offices JSON export.
///
/// The Fineract hierarchy is a tree whose depth is encoded in each office's
/// "hierarchy" field (e.g. ".2.127.128.").  This seeder maps each depth level
/// to a CRMS <see cref="LocationType"/> via a caller-supplied dictionary, making
/// it institution-agnostic:
///
///   BOA (3-level):  1 → HeadOffice  |  2 → Zone  |  3 → Branch
///   Full (4-level): 1 → HeadOffice  |  2 → Region |  3 → Zone  |  4 → Branch
///
/// Depth 0 is always the Fineract system root and is skipped.
/// Offices whose depth has no entry in the map are also skipped.
///
/// Upsert key: the Fineract <c>externalId</c> (stored as the CRMS location Code).
/// For offices without an externalId the name is used as the code.
/// </summary>
public static class FineractOfficeSeeder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task SeedAsync(
        CRMSDbContext context,
        ILogger logger,
        string embeddedResourceName,
        Dictionary<int, LocationType> depthMap)
    {
        logger.LogInformation("FineractOfficeSeeder: loading offices from embedded resource '{Resource}'", embeddedResourceName);

        var offices = LoadOffices(embeddedResourceName, logger);
        if (offices is null || offices.Count == 0)
        {
            logger.LogError("FineractOfficeSeeder: no offices loaded — aborting location seed");
            return;
        }

        // Load existing CRMS locations keyed by Code for upsert
        var existing = await context.Locations
            .ToDictionaryAsync(l => l.Code, StringComparer.OrdinalIgnoreCase);

        // fineractId → CRMS Guid (built as we process, used for parent lookups)
        var idMap = new Dictionary<int, Guid>();

        // Pre-populate idMap for any offices that already exist in the DB
        // so that children of already-seeded offices resolve their parent correctly.
        foreach (var office in offices)
        {
            var code = OfficeCode(office);
            if (existing.TryGetValue(code, out var loc))
                idMap[office.Id] = loc.Id;
        }

        // Process in depth order so parents are always created before children
        var ordered = offices
            .Select(o => (office: o, depth: Depth(o.Hierarchy)))
            .Where(x => x.depth > 0 && depthMap.ContainsKey(x.depth))
            .OrderBy(x => x.depth)
            .ToList();

        int created = 0, updated = 0, skipped = 0;

        foreach (var (office, depth) in ordered)
        {
            var locType = depthMap[depth];
            var code = OfficeCode(office);

            Guid? parentGuid = null;
            if (office.ParentId.HasValue)
            {
                if (!idMap.TryGetValue(office.ParentId.Value, out var pg))
                {
                    // Parent was skipped (e.g. Fineract root at depth 0) — walk up to find
                    // the nearest ancestor that WAS mapped
                    pg = ResolveAncestor(office.ParentId.Value, offices, idMap);
                }
                if (pg != Guid.Empty)
                    parentGuid = pg;
            }

            if (existing.TryGetValue(code, out var existingLoc))
            {
                // Update name in case it changed in Fineract
                existingLoc.Update(office.Name, existingLoc.Address,
                    existingLoc.ManagerName, existingLoc.ContactPhone,
                    existingLoc.ContactEmail, existingLoc.SortOrder);
                idMap[office.Id] = existingLoc.Id;
                updated++;
            }
            else
            {
                var result = Location.Create(
                    code: code,
                    name: office.Name,
                    type: locType,
                    parentLocationId: parentGuid,
                    sortOrder: office.Id);

                if (result.IsFailure)
                {
                    logger.LogWarning("FineractOfficeSeeder: skipped office '{Name}' (id:{Id}) — {Error}",
                        office.Name, office.Id, result.Error);
                    skipped++;
                    continue;
                }

                await context.Locations.AddAsync(result.Value);
                idMap[office.Id] = result.Value.Id;
                existing[code] = result.Value;
                created++;
            }
        }

        if (created > 0 || updated > 0)
        {
            try
            {
                await context.SaveChangesAsync();
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
                when (ex.InnerException?.Message.Contains("Duplicate entry") == true)
            {
                // Another app instance seeded concurrently (EBS multi-instance startup).
                // The locations are already in the DB — this is safe to ignore.
                logger.LogWarning("FineractOfficeSeeder: duplicate key on save — another instance likely seeded concurrently. Skipping.");
                return;
            }
        }

        logger.LogInformation(
            "FineractOfficeSeeder: done — {Created} created, {Updated} updated, {Skipped} skipped",
            created, updated, skipped);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static int Depth(string hierarchy)
        => hierarchy.Count(c => c == '.') - 1;

    private static string OfficeCode(FineractOffice office)
    {
        if (!string.IsNullOrWhiteSpace(office.ExternalId))
            return office.ExternalId.ToUpperInvariant();

        // No externalId — build a compact code from initials + Fineract id to guarantee uniqueness.
        // e.g. "Bank Of Agriculture HQ" + id:2 → "BOA-HQ-2"
        var words = office.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var initials = string.Concat(words.Select(w => w[0])).ToUpperInvariant();
        return $"{initials}-{office.Id}";
    }

    /// <summary>
    /// Walks up the parent chain to find the nearest ancestor already in idMap.
    /// Used when an intermediate level was skipped by the depth map (e.g. the
    /// Fineract root "Head Office" at depth 0).
    /// </summary>
    private static Guid ResolveAncestor(int fineractParentId, List<FineractOffice> all, Dictionary<int, Guid> idMap)
    {
        var byId = all.ToDictionary(o => o.Id);
        var current = fineractParentId;

        while (true)
        {
            if (idMap.TryGetValue(current, out var guid))
                return guid;

            if (!byId.TryGetValue(current, out var parent) || !parent.ParentId.HasValue)
                return Guid.Empty;

            current = parent.ParentId.Value;
        }
    }

    private static List<FineractOffice>? LoadOffices(string resourceName, ILogger logger)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Support both fully-qualified and partial resource names
        var fullName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(resourceName, StringComparison.OrdinalIgnoreCase));

        if (fullName is null)
        {
            logger.LogError("FineractOfficeSeeder: embedded resource '{Name}' not found. Available: {All}",
                resourceName, string.Join(", ", assembly.GetManifestResourceNames()));
            return null;
        }

        using var stream = assembly.GetManifestResourceStream(fullName)!;
        return JsonSerializer.Deserialize<List<FineractOffice>>(stream, JsonOptions);
    }

    // ── DTO ───────────────────────────────────────────────────────────────────

    private sealed class FineractOffice
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("externalId")]
        public string? ExternalId { get; set; }

        public string Hierarchy { get; set; } = string.Empty;
        public int? ParentId { get; set; }
    }
}

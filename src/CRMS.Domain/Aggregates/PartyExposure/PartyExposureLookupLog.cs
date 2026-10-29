using CRMS.Domain.Common;

namespace CRMS.Domain.Aggregates.PartyExposure;

/// <summary>
/// One party-exposure lookup asked of the CRMS, kept for audit (CRMSQA#4 §6). Identifiers are stored
/// only as keyed hashes, so the log shows which person was looked up when the same identifiers are
/// presented again, but cannot be read back into a BVN, NIN or account number.
/// </summary>
public class PartyExposureLookupLog : AggregateRoot
{
    public string RequestId { get; private set; } = string.Empty;
    public string Purpose { get; private set; } = string.Empty;
    public string? RequestedByUserId { get; private set; }
    public string? RequestedByRole { get; private set; }
    public string? SubjectRef { get; private set; }
    /// <summary>Set when the lookup was one subject of a batch.</summary>
    public Guid? BatchId { get; private set; }
    public string? BvnHash { get; private set; }
    public string? NinHash { get; private set; }
    /// <summary>Comma-separated keyed hashes, one per account given.</summary>
    public string? NubanHashes { get; private set; }
    public string MatchStatus { get; private set; } = string.Empty;
    public int RelationshipCount { get; private set; }
    public int DirectFacilities { get; private set; }
    public int IndirectFacilities { get; private set; }
    public bool Truncated { get; private set; }
    public bool IdentityConflict { get; private set; }
    public int DurationMs { get; private set; }
    /// <summary>Why a lookup produced no answer — never contains an identifier.</summary>
    public string? Error { get; private set; }

    private PartyExposureLookupLog() { }

    public static PartyExposureLookupLog Record(
        string requestId, string purpose, string? requestedByUserId, string? requestedByRole, string? subjectRef,
        Guid? batchId, string? bvnHash, string? ninHash, IReadOnlyList<string> nubanHashes,
        string matchStatus, int relationshipCount, int directFacilities, int indirectFacilities,
        bool truncated, bool identityConflict, int durationMs, string? error)
        => new()
        {
            RequestId = Cap(requestId, 100)!,
            Purpose = Cap(purpose, 50)!,
            RequestedByUserId = Cap(requestedByUserId, 100),
            RequestedByRole = Cap(requestedByRole, 100),
            SubjectRef = Cap(subjectRef, 200),
            BatchId = batchId,
            BvnHash = bvnHash,
            NinHash = ninHash,
            NubanHashes = nubanHashes.Count == 0 ? null : Cap(string.Join(",", nubanHashes), 1000),
            MatchStatus = matchStatus,
            RelationshipCount = relationshipCount,
            DirectFacilities = directFacilities,
            IndirectFacilities = indirectFacilities,
            Truncated = truncated,
            IdentityConflict = identityConflict,
            DurationMs = durationMs,
            Error = Cap(error, 1000),
        };

    private static string? Cap(string? value, int max) => value is null || value.Length <= max ? value : value[..max];
}

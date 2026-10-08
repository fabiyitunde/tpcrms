using CRMS.Domain.Common;

namespace CRMS.Domain.Aggregates.Rhshf;

/// <summary>
/// A saved snapshot of the directors cross-check (FAC-declared vs CAC registry vs core banking).
/// The CAC lookup is billed per call, so the result is persisted the first time an officer runs it
/// and projected through the life of the case — revisiting the tab shows the saved snapshot instead
/// of silently re-billing CAC. Re-running is an explicit, deliberate refresh that overwrites this.
///
/// The snapshot is an opaque serialized read-model (the Application layer owns its shape); the domain
/// only holds the blob plus provenance (when, by whom). One row per profile — the latest cross-check.
/// </summary>
public class RhshfDirectorCrossCheck : AggregateRoot
{
    public Guid RhshfCreditProfileId { get; private set; }
    public string SnapshotJson { get; private set; } = string.Empty;
    public DateTime FetchedAt { get; private set; }
    public Guid FetchedByUserId { get; private set; }

    protected RhshfDirectorCrossCheck() { }

    public RhshfDirectorCrossCheck(Guid rhshfCreditProfileId, string snapshotJson, Guid fetchedByUserId)
    {
        RhshfCreditProfileId = rhshfCreditProfileId;
        SnapshotJson = snapshotJson;
        FetchedByUserId = fetchedByUserId;
        FetchedAt = DateTime.UtcNow;
    }

    /// <summary>Overwrite with a freshly fetched snapshot (an explicit re-run).</summary>
    public void Replace(string snapshotJson, Guid fetchedByUserId)
    {
        SnapshotJson = snapshotJson;
        FetchedByUserId = fetchedByUserId;
        FetchedAt = DateTime.UtcNow;
    }
}

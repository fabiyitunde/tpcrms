using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Interfaces;

public interface IRhshfCreditProfileRepository
{
    Task<RhshfCreditProfile?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<RhshfCreditProfile?> GetByReferenceAsync(string reference, CancellationToken ct = default);
    Task<RhshfCreditProfile?> GetBySubmissionIdAsync(Guid submissionId, CancellationToken ct = default);
    Task AddAsync(RhshfCreditProfile profile, CancellationToken ct = default);

    /// <summary>Cases currently at the given internal pipeline stage, optionally scoped to one
    /// branch (VisibilityScope.Branch, same pattern every other CRMS queue uses) — null means
    /// global/HO visibility, resolved by the caller from the current user's role.</summary>
    Task<IReadOnlyList<RhshfCreditProfile>> GetQueueAsync(RhshfInternalStage stage, Guid? branchId, CancellationToken ct = default);

    /// <summary>All cases (any status/stage) for the admin/HO list — light, no collection graph loaded.</summary>
    Task<IReadOnlyList<RhshfCreditProfile>> GetAllForListAsync(Guid? branchId, CancellationToken ct = default);

    /// <summary>Direct lookup for staff document download — RhshfSupportingDocument is a child
    /// entity with no independent aggregate root, so this avoids loading the whole profile (with
    /// every other collection) just to find one document by id.</summary>
    Task<RhshfSupportingDocument?> GetSupportingDocumentByIdAsync(Guid documentId, CancellationToken ct = default);

    /// <summary>
    /// Documents in one category that this FAC supplied on its other cases, newest first — the
    /// candidates offered for carry-over so an unchanged document need not be re-uploaded each
    /// season. Scoped by FacId, never by company name: the FAC's identity on the portal is the only
    /// thing that makes two cases the same aggregator.
    /// </summary>
    Task<IReadOnlyList<RhshfReusableDocument>> GetReusableDocumentsForFacAsync(
        Guid facId, Guid excludeProfileId, RhshfDocumentCategory category, CancellationToken ct = default);
}

/// <summary>A document from one of this FAC's earlier cases, offered for carry-over. Carries the
/// source case's reference, session and status so the FAC and the reviewer can judge whether it is
/// still the right document — the programme defines no expiry, so nothing is filtered on age.</summary>
public record RhshfReusableDocument(
    Guid DocumentId,
    RhshfDocumentCategory Category,
    string FileName,
    string ContentType,
    string StoragePath,
    long SizeBytes,
    DateTime UploadedAt,
    string SourceReference,
    string SourceSessionName,
    RhshfCaseStatus SourceStatus);

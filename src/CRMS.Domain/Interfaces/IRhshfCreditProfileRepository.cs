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
}

using CRMS.Domain.Aggregates.Rhshf;

namespace CRMS.Domain.Interfaces;

public interface IRhshfOfferRepository
{
    Task<RhshfOffer?> GetByProfileAndCycleAsync(Guid rhshfCreditProfileId, int cycleNumber, CancellationToken ct = default);
    Task AddAsync(RhshfOffer offer, CancellationToken ct = default);

    /// <summary>Direct lookup for staff document download — avoids reloading the whole offer via
    /// profile+cycle just to find one signed-copy document by id.</summary>
    Task<RhshfOfferDocument?> GetDocumentByIdAsync(Guid documentId, CancellationToken ct = default);

    /// <summary>Every offer issued on the case, oldest first, with its signed copies — the version
    /// history, superseded offers included.</summary>
    Task<IReadOnlyList<RhshfOffer>> GetAllByProfileAsync(Guid rhshfCreditProfileId, CancellationToken ct = default);
}

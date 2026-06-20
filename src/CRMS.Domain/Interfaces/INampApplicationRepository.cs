using CRMS.Domain.Aggregates.Namp;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Interfaces;

public interface INampApplicationRepository
{
    Task<NampApplication?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<NampApplication?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);
    Task<NampApplication?> GetByCommitteeReviewIdAsync(Guid committeeReviewId, CancellationToken ct = default);
    Task<NampApplication?> GetByApplicationReferenceAsync(string applicationReference, CancellationToken ct = default);
    Task<NampApplication?> GetByApplicationReferenceWithHistoryAsync(string applicationReference, CancellationToken ct = default);
    Task<NampApplication?> GetByApplicationNumberWithHistoryAsync(string applicationNumber, CancellationToken ct = default);
    Task<IReadOnlyList<NampApplication>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<NampApplication>> GetByStatusAsync(NampApplicationStatus status, Guid? branchId = null, CancellationToken ct = default);
    Task<IReadOnlyList<NampApplication>> GetByStatusAndTierAsync(NampApplicationStatus status, NampCommitteeTier tier, Guid? branchId = null, CancellationToken ct = default);
    Task<IReadOnlyList<NampApplication>> GetByBranchAsync(Guid branchId, CancellationToken ct = default);
    Task<IReadOnlyList<NampApplication>> GetByCommitteeMembershipAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<NampApplication>> GetByParticipationAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(NampApplication application, CancellationToken ct = default);
    void Update(NampApplication application);
    Task AddNampDocumentAsync(NampDocument document, CancellationToken ct = default);
    Task<NampDocument?> GetNampDocumentByIdAsync(Guid documentId, CancellationToken ct = default);
    Task AddDirectorAsync(NampDirector director, CancellationToken ct = default);
    Task<NampDirector?> GetDirectorByIdAsync(Guid directorId, CancellationToken ct = default);
    void RemoveDirector(NampDirector director);
    Task<NampFinancialAppraisalReport?> GetFinancialAppraisalReportAsync(Guid nampApplicationId, CancellationToken ct = default);
    Task AddFinancialAppraisalReportAsync(NampFinancialAppraisalReport report, CancellationToken ct = default);
    Task AddPreDeploymentChecklistItemsAsync(IEnumerable<NampPreDeploymentChecklistItem> items, CancellationToken ct = default);
}

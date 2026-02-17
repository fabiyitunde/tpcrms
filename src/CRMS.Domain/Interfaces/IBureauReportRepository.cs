using CRMS.Domain.Aggregates.CreditBureau;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Interfaces;

public interface IBureauReportRepository
{
    Task<BureauReport?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<BureauReport?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<BureauReport>> GetByLoanApplicationIdAsync(Guid loanApplicationId, CancellationToken ct = default);
    Task<IReadOnlyList<BureauReport>> GetByBVNAsync(string bvn, CancellationToken ct = default);
    Task<BureauReport?> GetLatestByBVNAsync(string bvn, CreditBureauProvider? provider = null, CancellationToken ct = default);
    Task AddAsync(BureauReport report, CancellationToken ct = default);
    void Update(BureauReport report);
}

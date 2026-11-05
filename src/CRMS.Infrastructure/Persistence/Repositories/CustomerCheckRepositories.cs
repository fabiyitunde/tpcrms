using CRMS.Domain.Aggregates.CustomerCheck;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories;

public class ApiConsumerRepository : IApiConsumerRepository
{
    private readonly CRMSDbContext _context;

    public ApiConsumerRepository(CRMSDbContext context) => _context = context;

    public Task<ApiConsumer?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.ApiConsumers.FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<ApiConsumer?> GetByKeyHashAsync(string keyHash, CancellationToken ct = default)
        => _context.ApiConsumers.FirstOrDefaultAsync(x => x.KeyHash == keyHash, ct);

    public Task<ApiConsumer?> GetByNameAsync(string name, CancellationToken ct = default)
        => _context.ApiConsumers.FirstOrDefaultAsync(x => x.Name == name, ct);

    public Task<List<ApiConsumer>> GetAllAsync(CancellationToken ct = default)
        => _context.ApiConsumers.OrderBy(x => x.Name).ToListAsync(ct);

    public async Task AddAsync(ApiConsumer consumer, CancellationToken ct = default)
        => await _context.ApiConsumers.AddAsync(consumer, ct);

    public void Update(ApiConsumer consumer) => _context.ApiConsumers.Update(consumer);
}

public class CustomerCheckRepository : ICustomerCheckRepository
{
    private readonly CRMSDbContext _context;

    public CustomerCheckRepository(CRMSDbContext context) => _context = context;

    public async Task AddLogAsync(CustomerCheckLog log, CancellationToken ct = default)
        => await _context.CustomerCheckLogs.AddAsync(log, ct);

    public Task<CustomerBureauReport?> FindBureauReportAsync(string bvnHash, string bureau, DateTime notBefore, CancellationToken ct = default)
        => _context.CustomerBureauReports
            .Where(x => x.BvnHash == bvnHash && x.Bureau == bureau && x.FetchedAt >= notBefore)
            .OrderByDescending(x => x.FetchedAt)
            .FirstOrDefaultAsync(ct);

    public async Task AddBureauReportAsync(CustomerBureauReport report, CancellationToken ct = default)
        => await _context.CustomerBureauReports.AddAsync(report, ct);
}

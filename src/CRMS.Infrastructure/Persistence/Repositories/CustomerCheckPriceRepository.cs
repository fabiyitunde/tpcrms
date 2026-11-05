using CRMS.Domain.Aggregates.CustomerCheck;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence.Repositories;

public class CustomerCheckPriceRepository : ICustomerCheckPriceRepository
{
    private readonly CRMSDbContext _context;

    public CustomerCheckPriceRepository(CRMSDbContext context) => _context = context;

    public Task<List<CustomerCheckPrice>> GetAllAsync(CancellationToken ct = default)
        => _context.CustomerCheckPrices.OrderBy(p => p.Item).ToListAsync(ct);

    public async Task AddAsync(CustomerCheckPrice price, CancellationToken ct = default)
        => await _context.CustomerCheckPrices.AddAsync(price, ct);

    public void Update(CustomerCheckPrice price) => _context.CustomerCheckPrices.Update(price);
}

using CRMS.Domain.Aggregates.ProductCatalog;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Persistence;

public class CRMSDbContext : DbContext, IUnitOfWork
{
    public CRMSDbContext(DbContextOptions<CRMSDbContext> options) : base(options)
    {
    }

    public DbSet<LoanProduct> LoanProducts => Set<LoanProduct>();
    public DbSet<PricingTier> PricingTiers => Set<PricingTier>();
    public DbSet<EligibilityRule> EligibilityRules => Set<EligibilityRule>();
    public DbSet<DocumentRequirement> DocumentRequirements => Set<DocumentRequirement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CRMSDbContext).Assembly);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await base.SaveChangesAsync(cancellationToken);
    }
}

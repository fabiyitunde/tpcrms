using CRMS.Domain.Aggregates.ProductCatalog;
using CRMS.Domain.Entities.Identity;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using LA = CRMS.Domain.Aggregates.LoanApplication;

namespace CRMS.Infrastructure.Persistence;

public class CRMSDbContext : DbContext, IUnitOfWork
{
    public CRMSDbContext(DbContextOptions<CRMSDbContext> options) : base(options)
    {
    }

    // ProductCatalog
    public DbSet<LoanProduct> LoanProducts => Set<LoanProduct>();
    public DbSet<PricingTier> PricingTiers => Set<PricingTier>();
    public DbSet<EligibilityRule> EligibilityRules => Set<EligibilityRule>();
    public DbSet<DocumentRequirement> DocumentRequirements => Set<DocumentRequirement>();

    // Identity
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<ApplicationRole> Roles => Set<ApplicationRole>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<ApplicationUserRole> UserRoles => Set<ApplicationUserRole>();
    public DbSet<ApplicationRolePermission> RolePermissions => Set<ApplicationRolePermission>();

    // LoanApplication
    public DbSet<LA.LoanApplication> LoanApplications => Set<LA.LoanApplication>();
    public DbSet<LA.LoanApplicationDocument> LoanApplicationDocuments => Set<LA.LoanApplicationDocument>();
    public DbSet<LA.LoanApplicationParty> LoanApplicationParties => Set<LA.LoanApplicationParty>();
    public DbSet<LA.LoanApplicationComment> LoanApplicationComments => Set<LA.LoanApplicationComment>();
    public DbSet<LA.LoanApplicationStatusHistory> LoanApplicationStatusHistory => Set<LA.LoanApplicationStatusHistory>();

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

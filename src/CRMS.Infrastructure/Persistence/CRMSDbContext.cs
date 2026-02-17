using CRMS.Domain.Aggregates.CreditBureau;
using CRMS.Domain.Aggregates.ProductCatalog;
using CRMS.Domain.Entities.Identity;
using CRMS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using CL = CRMS.Domain.Aggregates.Collateral;
using FS = CRMS.Domain.Aggregates.FinancialStatement;
using GR = CRMS.Domain.Aggregates.Guarantor;
using LA = CRMS.Domain.Aggregates.LoanApplication;
using SA = CRMS.Domain.Aggregates.StatementAnalysis;

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

    // StatementAnalysis
    public DbSet<SA.BankStatement> BankStatements => Set<SA.BankStatement>();
    public DbSet<SA.StatementTransaction> StatementTransactions => Set<SA.StatementTransaction>();

    // CreditBureau
    public DbSet<BureauReport> BureauReports => Set<BureauReport>();
    public DbSet<BureauAccount> BureauAccounts => Set<BureauAccount>();
    public DbSet<BureauScoreFactor> BureauScoreFactors => Set<BureauScoreFactor>();

    // Collateral
    public DbSet<CL.Collateral> Collaterals => Set<CL.Collateral>();
    public DbSet<CL.CollateralValuation> CollateralValuations => Set<CL.CollateralValuation>();
    public DbSet<CL.CollateralDocument> CollateralDocuments => Set<CL.CollateralDocument>();

    // Guarantor
    public DbSet<GR.Guarantor> Guarantors => Set<GR.Guarantor>();
    public DbSet<GR.GuarantorDocument> GuarantorDocuments => Set<GR.GuarantorDocument>();

    // FinancialStatement
    public DbSet<FS.FinancialStatement> FinancialStatements => Set<FS.FinancialStatement>();
    public DbSet<FS.BalanceSheet> BalanceSheets => Set<FS.BalanceSheet>();
    public DbSet<FS.IncomeStatement> IncomeStatements => Set<FS.IncomeStatement>();
    public DbSet<FS.CashFlowStatement> CashFlowStatements => Set<FS.CashFlowStatement>();

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

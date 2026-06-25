using CRMS.Domain.Aggregates.CreditBureau;
using CRMS.Domain.Aggregates.ProductCatalog;
using CRMS.Domain.Entities.Identity;
using CRMS.Domain.Interfaces;
using CRMS.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using AD = CRMS.Domain.Aggregates.Advisory;
using AU = CRMS.Domain.Aggregates.Audit;
using CF = CRMS.Domain.Aggregates.Configuration;
using CL = CRMS.Domain.Aggregates.Collateral;
using CM = CRMS.Domain.Aggregates.Committee;
using CN = CRMS.Domain.Aggregates.Consent;
using FS = CRMS.Domain.Aggregates.FinancialStatement;
using GR = CRMS.Domain.Aggregates.Guarantor;
using LA = CRMS.Domain.Aggregates.LoanApplication;
using LO = CRMS.Domain.Aggregates.Location;
using LP = CRMS.Domain.Aggregates.LoanPack;
using NA = CRMS.Domain.Aggregates.Namp;
using NF = CRMS.Domain.Aggregates.Notification;
using SA = CRMS.Domain.Aggregates.StatementAnalysis;
using OL = CRMS.Domain.Aggregates.OfferLetter;
using WF = CRMS.Domain.Aggregates.Workflow;

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
    public DbSet<DisbursementChecklistTemplate> DisbursementChecklistTemplates => Set<DisbursementChecklistTemplate>();

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
    public DbSet<LA.DisbursementChecklistItem> DisbursementChecklistItems => Set<LA.DisbursementChecklistItem>();
    public DbSet<LA.ApprovalOverrideRecord> ApprovalOverrideRecords => Set<LA.ApprovalOverrideRecord>();

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
    public DbSet<CL.CollateralTypeConfig> CollateralTypeConfigs => Set<CL.CollateralTypeConfig>();

    // Guarantor
    public DbSet<GR.Guarantor> Guarantors => Set<GR.Guarantor>();
    public DbSet<GR.GuarantorDocument> GuarantorDocuments => Set<GR.GuarantorDocument>();

    // FinancialStatement
    public DbSet<FS.FinancialStatement> FinancialStatements => Set<FS.FinancialStatement>();
    public DbSet<FS.BalanceSheet> BalanceSheets => Set<FS.BalanceSheet>();
    public DbSet<FS.IncomeStatement> IncomeStatements => Set<FS.IncomeStatement>();
    public DbSet<FS.CashFlowStatement> CashFlowStatements => Set<FS.CashFlowStatement>();

    // Advisory
    public DbSet<AD.CreditAdvisory> CreditAdvisories => Set<AD.CreditAdvisory>();

    // Configuration
    public DbSet<CF.ScoringParameter> ScoringParameters => Set<CF.ScoringParameter>();
    public DbSet<CF.ScoringParameterHistory> ScoringParameterHistory => Set<CF.ScoringParameterHistory>();

    // Workflow
    public DbSet<WF.WorkflowDefinition> WorkflowDefinitions => Set<WF.WorkflowDefinition>();
    public DbSet<WF.WorkflowStage> WorkflowStages => Set<WF.WorkflowStage>();
    public DbSet<WF.WorkflowTransition> WorkflowTransitions => Set<WF.WorkflowTransition>();
    public DbSet<WF.WorkflowInstance> WorkflowInstances => Set<WF.WorkflowInstance>();
    public DbSet<WF.WorkflowTransitionLog> WorkflowTransitionLogs => Set<WF.WorkflowTransitionLog>();

    // Committee
    public DbSet<CM.CommitteeReview> CommitteeReviews => Set<CM.CommitteeReview>();
    public DbSet<CM.CommitteeMember> CommitteeMembers => Set<CM.CommitteeMember>();
    public DbSet<CM.CommitteeComment> CommitteeComments => Set<CM.CommitteeComment>();
    public DbSet<CM.StandingCommittee> StandingCommittees => Set<CM.StandingCommittee>();
    public DbSet<CM.StandingCommitteeMember> StandingCommitteeMembers => Set<CM.StandingCommitteeMember>();
    public DbSet<CM.CommitteeDocument> CommitteeDocuments => Set<CM.CommitteeDocument>();

    // Audit
    public DbSet<AU.AuditLog> AuditLogs => Set<AU.AuditLog>();
    public DbSet<AU.DataAccessLog> DataAccessLogs => Set<AU.DataAccessLog>();

    // LoanPack
    public DbSet<LP.LoanPack> LoanPacks => Set<LP.LoanPack>();

    // OfferLetter
    public DbSet<OL.OfferLetter> OfferLetters => Set<OL.OfferLetter>();

    // Notification
    public DbSet<NF.Notification> Notifications => Set<NF.Notification>();
    public DbSet<NF.NotificationTemplate> NotificationTemplates => Set<NF.NotificationTemplate>();

    // Consent
    public DbSet<CN.ConsentRecord> ConsentRecords => Set<CN.ConsentRecord>();

    // Location
    public DbSet<LO.Location> Locations => Set<LO.Location>();

    // NAMP
    public DbSet<NA.NampStagingRecord> NampStagingRecords => Set<NA.NampStagingRecord>();
    public DbSet<NA.NampRoutingConfig> NampRoutingConfigs => Set<NA.NampRoutingConfig>();
    public DbSet<NA.NampApplication> NampApplications => Set<NA.NampApplication>();
    public DbSet<NA.NampDocument> NampDocuments => Set<NA.NampDocument>();
    public DbSet<NA.NampStatusHistory> NampStatusHistory => Set<NA.NampStatusHistory>();
    public DbSet<NA.NampWorkflowConfig> NampWorkflowConfigs => Set<NA.NampWorkflowConfig>();
    public DbSet<NA.NampWorkflowInstance> NampWorkflowInstances => Set<NA.NampWorkflowInstance>();
    public DbSet<NA.NampGuarantor> NampGuarantors => Set<NA.NampGuarantor>();
    public DbSet<NA.NampDirector> NampDirectors => Set<NA.NampDirector>();
    public DbSet<NA.NampCollateral> NampCollaterals => Set<NA.NampCollateral>();
    public DbSet<NA.NampFinancialStatement> NampFinancialStatements => Set<NA.NampFinancialStatement>();
    public DbSet<NA.NampFinancialAppraisalReport> NampFinancialAppraisalReports => Set<NA.NampFinancialAppraisalReport>();
    public DbSet<NA.NampPreDeploymentChecklistTemplate> NampPreDeploymentChecklistTemplates => Set<NA.NampPreDeploymentChecklistTemplate>();
    public DbSet<NA.NampPreDeploymentChecklistItem> NampPreDeploymentChecklistItems => Set<NA.NampPreDeploymentChecklistItem>();
    public DbSet<NA.NampAdvisory> NampAdvisories => Set<NA.NampAdvisory>();
    public DbSet<NA.NampDocumentTemplate> NampDocumentTemplates => Set<NA.NampDocumentTemplate>();

    // Outbox
    public DbSet<CreditCheckOutboxEntry> CreditCheckOutbox => Set<CreditCheckOutboxEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CRMSDbContext).Assembly);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // EF Core attaches new entities discovered through collection navigations with non-default
        // GUID keys as Modified (not Added), generating UPDATE for non-existent rows.
        // These types are immutable (append-only), so Modified state always means a new record.
        foreach (var entry in ChangeTracker.Entries<NA.NampStatusHistory>()
            .Where(e => e.State == EntityState.Modified))
        {
            entry.State = EntityState.Added;
        }
        foreach (var entry in ChangeTracker.Entries<ApplicationUserRole>()
            .Where(e => e.State == EntityState.Modified))
        {
            entry.State = EntityState.Added;
        }
        foreach (var entry in ChangeTracker.Entries<CM.CommitteeComment>()
            .Where(e => e.State == EntityState.Modified))
        {
            entry.State = EntityState.Added;
        }
        foreach (var entry in ChangeTracker.Entries<CM.StandingCommitteeMember>()
            .Where(e => e.State == EntityState.Modified))
        {
            entry.State = EntityState.Added;
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}

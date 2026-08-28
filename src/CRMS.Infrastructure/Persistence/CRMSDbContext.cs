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
using RH = CRMS.Domain.Aggregates.Rhshf;
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
    public DbSet<LA.SecurityPerfectionDocument> SecurityPerfectionDocuments => Set<LA.SecurityPerfectionDocument>();

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

    // RH-SHF — own loan track, independent of NAMP
    public DbSet<RH.RhshfCreditProfile> RhshfCreditProfiles => Set<RH.RhshfCreditProfile>();
    public DbSet<RH.RhshfEopLine> RhshfEopLines => Set<RH.RhshfEopLine>();
    public DbSet<RH.RhshfIssuedToken> RhshfIssuedTokens => Set<RH.RhshfIssuedToken>();
    public DbSet<RH.RhshfSupportingDocument> RhshfSupportingDocuments => Set<RH.RhshfSupportingDocument>();
    public DbSet<RH.RhshfAppraisal> RhshfAppraisals => Set<RH.RhshfAppraisal>();
    public DbSet<RH.RhshfRiskReview> RhshfRiskReviews => Set<RH.RhshfRiskReview>();
    public DbSet<RH.RhshfCommitteeReview> RhshfCommitteeReviews => Set<RH.RhshfCommitteeReview>();
    public DbSet<RH.RhshfCommitteeVote> RhshfCommitteeVotes => Set<RH.RhshfCommitteeVote>();
    public DbSet<RH.RhshfRatification> RhshfRatifications => Set<RH.RhshfRatification>();
    public DbSet<RH.RhshfOffer> RhshfOffers => Set<RH.RhshfOffer>();

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
        // RhshfIssuedToken is NOT append-only like the others here — Consume() legitimately
        // UPDATEs an existing row's ConsumedAt. A blanket Modified->Added flip (like the ones
        // above) would wrongly re-INSERT that already-existing row, causing a duplicate-key error
        // (confirmed live: "Duplicate entry ... for key 'rhshfissuedtokens.PRIMARY'"). Distinguish
        // the two cases instead: a genuinely NEW token mis-tracked via navigation-discovery has no
        // real DB snapshot to diff against, so EF marks every property modified; a genuinely
        // EXISTING token that was actually loaded (via GetByReferenceAsync's Include) and then
        // Consume()'d only has the properties that really changed (ConsumedAt + audit fields)
        // marked modified.
        foreach (var entry in ChangeTracker.Entries<RH.RhshfIssuedToken>()
            .Where(e => e.State == EntityState.Modified && e.Properties.All(p => p.IsModified)))
        {
            entry.State = EntityState.Added;
        }
        foreach (var entry in ChangeTracker.Entries<RH.RhshfEopLine>()
            .Where(e => e.State == EntityState.Modified))
        {
            entry.State = EntityState.Added;
        }
        foreach (var entry in ChangeTracker.Entries<RH.RhshfSupportingDocument>()
            .Where(e => e.State == EntityState.Modified))
        {
            entry.State = EntityState.Added;
        }
        // RhshfAppraisal/RhshfRiskReview ARE genuinely append-only (unlike RhshfIssuedToken above) —
        // never updated after creation, so the blanket flip is safe here.
        foreach (var entry in ChangeTracker.Entries<RH.RhshfAppraisal>()
            .Where(e => e.State == EntityState.Modified))
        {
            entry.State = EntityState.Added;
        }
        foreach (var entry in ChangeTracker.Entries<RH.RhshfRiskReview>()
            .Where(e => e.State == EntityState.Modified))
        {
            entry.State = EntityState.Added;
        }
        // RhshfCommitteeVote is genuinely append-only (never updated after being cast).
        // RhshfCommitteeReview itself is NOT in this list — it's an independent aggregate root
        // loaded/tracked via its own DbSet, not discovered through another aggregate's navigation,
        // so it never hits this mis-tracking bug in the first place (see RhshfIssuedToken's comment
        // above for the contrast: that one WAS a child entity of RhshfCreditProfile).
        foreach (var entry in ChangeTracker.Entries<RH.RhshfCommitteeVote>()
            .Where(e => e.State == EntityState.Modified))
        {
            entry.State = EntityState.Added;
        }
        // RhshfRatification is genuinely append-only (never updated after creation). RhshfOffer is
        // NOT in this list — own aggregate root, own DbSet, same reasoning as RhshfCommitteeReview.
        foreach (var entry in ChangeTracker.Entries<RH.RhshfRatification>()
            .Where(e => e.State == EntityState.Modified))
        {
            entry.State = EntityState.Added;
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}

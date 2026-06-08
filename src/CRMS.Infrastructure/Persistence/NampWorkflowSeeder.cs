using CRMS.Domain.Aggregates.Namp;
using CRMS.Domain.Constants;
using CRMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CRMS.Infrastructure.Persistence;

/// <summary>
/// Seeds NampWorkflowConfig rows (one per NampApplicationStatus) and default NampRoutingConfig rows.
/// Idempotent — skips if data already exists.
/// </summary>
public static class NampWorkflowSeeder
{
    public static async Task SeedAsync(CRMSDbContext context, ILogger logger)
    {
        await SeedWorkflowConfigAsync(context, logger);
        await SeedRoutingConfigAsync(context, logger);
        await SeedPreDeploymentChecklistAsync(context, logger);
    }

    // ── Stage config ──────────────────────────────────────────────────────

    private static async Task SeedWorkflowConfigAsync(CRMSDbContext context, ILogger logger)
    {
        if (await context.NampWorkflowConfigs.AnyAsync())
        {
            logger.LogInformation("NAMP workflow config already seeded, skipping.");
            return;
        }

        logger.LogInformation("Seeding NAMP workflow stage config...");

        var stages = new[]
        {
            // ── Pre-stage (staging only; included for completeness) ────────
            Stage(NampApplicationStatus.Received, "Received from PAYS", "Inbound webhook received; awaiting Loan Officer recall.", Roles.LoanOfficer, slaHours: 48, sort: 0),
            Stage(NampApplicationStatus.RecallPending, "Recall Pending", "Loan Officer recall queue.", Roles.LoanOfficer, slaHours: 48, sort: 1),

            // ── Stage 1: Loan Officer ─────────────────────────────────────
            Stage(NampApplicationStatus.Draft, "Draft", "Recalled from staging; Loan Officer reviewing before submission.", Roles.LoanOfficer, slaHours: 48, sort: 10),
            Stage(NampApplicationStatus.Submitted, "Submitted for Financial Appraisal", "Awaiting Credit Officer review.", Roles.CreditOfficer, slaHours: 72, sort: 20),

            // ── Stage 2: Financial Appraisal ──────────────────────────────
            Stage(NampApplicationStatus.FinancialAppraisal, "Financial Appraisal", "Credit Officer reviewing financial viability.", Roles.CreditOfficer, slaHours: 72, sort: 40),
            Stage(NampApplicationStatus.FinancialDeclined, "Financial Appraisal Declined", "Application failed financial appraisal.", Roles.SystemAdmin, slaHours: 0, sort: 45, isTerminal: true),

            // ── Stage 4: Committee Circulation ───────────────────────────
            Stage(NampApplicationStatus.BranchCommitteeCirculation, "Branch Committee Review", "Branch Credit Committee voting in progress.", Roles.CommitteeMember, slaHours: 120, sort: 50),
            Stage(NampApplicationStatus.BranchCommitteeDeclined, "Branch Committee Declined", "Branch Credit Committee declined the application.", Roles.SystemAdmin, slaHours: 0, sort: 55, isTerminal: true),
            Stage(NampApplicationStatus.ZonalCommitteeCirculation, "Zonal Committee Review", "Zonal Credit Committee voting in progress.", Roles.CommitteeMember, slaHours: 120, sort: 60),
            Stage(NampApplicationStatus.ZonalCommitteeDeclined, "Zonal Committee Declined", "Zonal Credit Committee declined the application.", Roles.SystemAdmin, slaHours: 0, sort: 65, isTerminal: true),
            Stage(NampApplicationStatus.RegionalCommitteeCirculation, "Regional Committee Review", "Regional Credit Committee voting in progress.", Roles.CommitteeMember, slaHours: 120, sort: 70),
            Stage(NampApplicationStatus.RegionalCommitteeDeclined, "Regional Committee Declined", "Regional Credit Committee declined the application.", Roles.SystemAdmin, slaHours: 0, sort: 75, isTerminal: true),
            Stage(NampApplicationStatus.HOCommitteeCirculation, "Head Office Committee Review", "HO Credit Committee voting in progress.", Roles.CommitteeMember, slaHours: 120, sort: 80),
            Stage(NampApplicationStatus.HOCommitteeDeclined, "HO Committee Declined", "HO Credit Committee declined the application.", Roles.SystemAdmin, slaHours: 0, sort: 85, isTerminal: true),

            // ── Stage 5: Ratification & Offer ─────────────────────────────
            Stage(NampApplicationStatus.Ratification, "Ratification", "Final Approver (Branch Manager / Zonal Manager / Regional Manager / MD-CEO) ratifying committee vote.", Roles.FinalApprover, slaHours: 48, sort: 90),
            Stage(NampApplicationStatus.RatificationDeclined, "Ratification Declined", "Final Approver declined to ratify the committee decision.", Roles.SystemAdmin, slaHours: 0, sort: 95, isTerminal: true),
            Stage(NampApplicationStatus.OfferGenerated, "Offer Letter Generated", "Offer letter sent to applicant; awaiting countersignature.", Roles.LoanOfficer, slaHours: 168, sort: 100),
            Stage(NampApplicationStatus.OfferAccepted, "Offer Accepted", "Applicant countersigned offer letter; moving to pre-deployment.", Roles.DeploymentOfficer, slaHours: 48, sort: 105),
            Stage(NampApplicationStatus.OfferLapsed, "Offer Lapsed", "Applicant did not countersign within SLA.", Roles.SystemAdmin, slaHours: 0, sort: 108, isTerminal: true),

            // ── Stage 5: Pre-Deployment Verification ──────────────────────
            Stage(NampApplicationStatus.PreDeploymentVerification, "Pre-Deployment Verification", "Deployment Officer verifying 4 gate conditions before equipment deployment.", Roles.DeploymentOfficer, slaHours: 48, sort: 110),

            // ── Stage 6: Deployment ───────────────────────────────────────
            Stage(NampApplicationStatus.Deployment, "Deployment", "Deployment Officer tracking equipment delivery and GPS activation.", Roles.DeploymentOfficer, slaHours: 168, sort: 130),

            // ── Stage 9: Active ───────────────────────────────────────────
            Stage(NampApplicationStatus.Active, "Active", "GPS confirmed; PAYS repayment cycle running.", Roles.SystemAdmin, slaHours: 0, sort: 140),

            // ── Terminal ──────────────────────────────────────────────────
            Stage(NampApplicationStatus.Closed, "Closed", "Full PAYS repayment completed.", Roles.SystemAdmin, slaHours: 0, sort: 150, isTerminal: true),
            Stage(NampApplicationStatus.Declined, "Declined", "Application declined (outbound NAMP callback sent).", Roles.SystemAdmin, slaHours: 0, sort: 160, isTerminal: true),
        };

        await context.NampWorkflowConfigs.AddRangeAsync(stages);
        await context.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} NAMP workflow stage config rows.", stages.Length);
    }

    // ── Routing config (default bands) ────────────────────────────────────

    private static async Task SeedRoutingConfigAsync(CRMSDbContext context, ILogger logger)
    {
        if (await context.NampRoutingConfigs.AnyAsync())
        {
            logger.LogInformation("NAMP routing config already seeded, skipping.");
            return;
        }

        logger.LogInformation("Seeding default NAMP routing config...");

        // Default routing bands (₦ values). Adjust via admin UI after seeding.
        //  Youth / Women Agripreneurs:  ≤ ₦5M → Branch, ≤ ₦20M → Zonal, ≤ ₦50M → Regional, > ₦50M → HO
        //  Agro-Service Companies:      ≤ ₦20M → Zonal, ≤ ₦100M → Regional, > ₦100M → HO

        var configs = new[]
        {
            // Youth Agripreneur
            Routing(NampApplicantCategory.YouthAgripreneur, NampCommitteeTier.Branch,      0m,           5_000_000m,   priority: 0),
            Routing(NampApplicantCategory.YouthAgripreneur, NampCommitteeTier.Zonal,       5_000_001m,   20_000_000m,  priority: 1),
            Routing(NampApplicantCategory.YouthAgripreneur, NampCommitteeTier.Regional,    20_000_001m,  50_000_000m,  priority: 2),
            Routing(NampApplicantCategory.YouthAgripreneur, NampCommitteeTier.HeadOffice,  50_000_001m,  999_999_999_999_999m, priority: 3),

            // Women Agripreneur — same bands as Youth
            Routing(NampApplicantCategory.WomenAgripreneur, NampCommitteeTier.Branch,      0m,           5_000_000m,   priority: 0),
            Routing(NampApplicantCategory.WomenAgripreneur, NampCommitteeTier.Zonal,       5_000_001m,   20_000_000m,  priority: 1),
            Routing(NampApplicantCategory.WomenAgripreneur, NampCommitteeTier.Regional,    20_000_001m,  50_000_000m,  priority: 2),
            Routing(NampApplicantCategory.WomenAgripreneur, NampCommitteeTier.HeadOffice,  50_000_001m,  999_999_999_999_999m, priority: 3),

            // Agro-Service Company — higher starting tier
            Routing(NampApplicantCategory.AgroServiceCompany, NampCommitteeTier.Zonal,      0m,           20_000_000m,  priority: 0),
            Routing(NampApplicantCategory.AgroServiceCompany, NampCommitteeTier.Regional,   20_000_001m,  100_000_000m, priority: 1),
            Routing(NampApplicantCategory.AgroServiceCompany, NampCommitteeTier.HeadOffice, 100_000_001m, 999_999_999_999_999m, priority: 2),
        };

        await context.NampRoutingConfigs.AddRangeAsync(configs);
        await context.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} NAMP routing config rows.", configs.Length);
    }

    // ── Pre-Deployment Checklist ──────────────────────────────────────────

    private static async Task SeedPreDeploymentChecklistAsync(CRMSDbContext context, ILogger logger)
    {
        if (await context.NampPreDeploymentChecklistTemplates.AnyAsync())
        {
            logger.LogInformation("NAMP pre-deployment checklist already seeded, skipping.");
            return;
        }

        logger.LogInformation("Seeding NAMP pre-deployment checklist templates...");

        var items = new[]
        {
            Checklist(
                title: "Equity Deposit Confirmed",
                description: "Confirm that the applicant has paid the required equity deposit and a receipt has been received at the branch.",
                requiresDoc: true,
                docCategory: NampDocumentCategory.EquityDepositReceipt,
                isMandatory: true,
                sortOrder: 10),
            Checklist(
                title: "Lease / Hire-Purchase Agreement Signed",
                description: "Confirm that the applicant has signed the equipment lease or hire-purchase agreement and a copy is on file.",
                requiresDoc: true,
                docCategory: NampDocumentCategory.LeaseAgreement,
                isMandatory: true,
                sortOrder: 20),
            Checklist(
                title: "GPS Tracking Consent Obtained",
                description: "Confirm that the applicant has signed the GPS tracking consent form authorising installation and monitoring of the equipment.",
                requiresDoc: true,
                docCategory: NampDocumentCategory.GpsConsentForm,
                isMandatory: true,
                sortOrder: 30),
            Checklist(
                title: "NAIC Insurance In Place",
                description: "Confirm that a valid NAIC (Nigerian Agricultural Insurance Corporation) policy is in place and the certificate has been received.",
                requiresDoc: true,
                docCategory: NampDocumentCategory.InsuranceCertificate,
                isMandatory: true,
                sortOrder: 40),
            Checklist(
                title: "Signed NAMP Offer Letter Returned",
                description: "Confirm that the applicant has signed and returned the official NAMP offer letter.",
                requiresDoc: true,
                docCategory: NampDocumentCategory.SignedNampOfferLetter,
                isMandatory: true,
                sortOrder: 50),
        };

        await context.NampPreDeploymentChecklistTemplates.AddRangeAsync(items);
        await context.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} NAMP pre-deployment checklist template items.", items.Length);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static NampWorkflowConfig Stage(
        NampApplicationStatus status,
        string displayName,
        string description,
        string assignedRole,
        int slaHours,
        int sort,
        bool isTerminal = false)
    {
        var config = NampWorkflowConfig.Create(status, displayName, description, assignedRole, slaHours, sort, isTerminal);
        config.SetAuditInfo("seed", isNew: true);
        return config;
    }

    private static NampRoutingConfig Routing(
        NampApplicantCategory category,
        NampCommitteeTier tier,
        decimal min,
        decimal max,
        int priority)
    {
        var config = NampRoutingConfig.Create(category, tier, min, max, priority);
        config.SetAuditInfo("seed", isNew: true);
        return config;
    }

    private static NampPreDeploymentChecklistTemplate Checklist(
        string title,
        string description,
        bool requiresDoc,
        NampDocumentCategory? docCategory,
        bool isMandatory,
        int sortOrder)
    {
        var result = NampPreDeploymentChecklistTemplate.Create(title, description, requiresDoc, docCategory, isMandatory, sortOrder);
        var item = result.Value;
        item.SetAuditInfo("seed", isNew: true);
        return item;
    }
}

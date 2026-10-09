using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CRMS.Infrastructure.Persistence;

/// <summary>Seeds RH-SHF workflow configuration — mirrors NampWorkflowSeeder's pattern (idempotent,
/// called from SeedData.SeedAsync at startup, not a dev-only endpoint — see the NAMP seeder's own
/// history of the "dev-only endpoint → empty prod" bug this avoids).</summary>
public static class RhshfWorkflowSeeder
{
    public static async Task SeedAsync(CRMSDbContext context, ILogger logger)
    {
        await SeedRoutingConfigAsync(context, logger);
        await SeedPreDeploymentChecklistAsync(context, logger);
        await SeedAppraisalThresholdsAsync(context, logger);
        await SeedDocumentRequirementsAsync(context, logger);
    }

    // ── Required documents for FAC profiling ───────────────────────────────

    private static async Task SeedDocumentRequirementsAsync(CRMSDbContext context, ILogger logger)
    {
        if (await context.RhshfDocumentRequirements.AnyAsync())
        {
            logger.LogInformation("RH-SHF document requirements already seeded, skipping.");
            return;
        }

        logger.LogInformation("Seeding default RH-SHF document requirements...");

        // Mapped to the programme's own eligibility criteria (RSHSF_Programme_Details.docx S/N 15).
        // Mandatory flags are a starting position — tune via Admin > RH-SHF Document Requirements.
        var requirements = new[]
        {
            Requirement(RhshfDocumentCategory.CacCertificate, "CAC Certificate of Incorporation",
                "Proof of full incorporation with the Corporate Affairs Commission.", isMandatory: true, sortOrder: 10),
            Requirement(RhshfDocumentCategory.AuditedFinancials, "Audited Financial Statements",
                "Most recent audited accounts and statutory compliance filings.", isMandatory: true, sortOrder: 20),
            Requirement(RhshfDocumentCategory.OffTakeAgreement, "Off-Take / Market Linkage Agreement",
                "Formalised, legally binding agreement covering the produce being financed.", isMandatory: true, sortOrder: 30),
            Requirement(RhshfDocumentCategory.FarmerRegister, "Out-Grower Register",
                "Register of the smallholders under this aggregator, with biometric capture evidence.", isMandatory: true, sortOrder: 40),
            Requirement(RhshfDocumentCategory.BankStatement, "BOA Account Statement",
                "Statements for the operational Bank of Agriculture account.", isMandatory: true, sortOrder: 50),
            Requirement(RhshfDocumentCategory.WarehousingEvidence, "Warehousing & Transport Evidence",
                "Evidence of adequate, secure storage and haulage capacity.", isMandatory: false, sortOrder: 60),
            Requirement(RhshfDocumentCategory.BoardResolution, "Board Resolution",
                "Resolution authorising the facility and naming the signatories.", isMandatory: false, sortOrder: 70),
            Requirement(RhshfDocumentCategory.LandDocumentation, "Land Documentation",
                "Title, lease or documented usage rights over the hectares under cultivation.", isMandatory: false, sortOrder: 80),
        };

        await context.RhshfDocumentRequirements.AddRangeAsync(requirements);
        await context.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} RH-SHF document requirements.", requirements.Length);
    }

    private static RhshfDocumentRequirement Requirement(
        RhshfDocumentCategory category, string title, string description, bool isMandatory, int sortOrder)
    {
        var result = RhshfDocumentRequirement.Create(category, title, description, isMandatory, sortOrder);
        result.Value.SetAuditInfo("System Seeder", isNew: true);
        return result.Value;
    }

    // ── Appraisal viability thresholds ─────────────────────────────────────

    private static async Task SeedAppraisalThresholdsAsync(CRMSDbContext context, ILogger logger)
    {
        if (await context.RhshfAppraisalThresholds.AnyAsync())
        {
            logger.LogInformation("RH-SHF appraisal thresholds already seeded, skipping.");
            return;
        }

        logger.LogInformation("Seeding default RH-SHF appraisal thresholds...");

        // STARTING VALUES, not a credit policy decision — the bank tunes these via
        // Admin > RH-SHF Appraisal Thresholds. They exist as data precisely so they are not
        // hard-coded in a view file the way NAMP's are.
        var result = RhshfAppraisalThresholds.Create(
            minDscr: 1.25m,                  // 25% cushion over the amount due at harvest
            minGrossMarginPercent: 15m,      // enterprise must clear 15% margin on revenue
            hurdleRatePercent: 15m,          // IRR bar and NPV discount rate
            minYieldHeadroomPercent: 20m,    // expected yield must sit 20% above break-even
            minPriceHeadroomPercent: 15m);   // expected farm-gate price 15% above break-even

        result.Value.SetAuditInfo("System Seeder", isNew: true);
        await context.RhshfAppraisalThresholds.AddAsync(result.Value);
        await context.SaveChangesAsync();
        logger.LogInformation("Seeded default RH-SHF appraisal thresholds (placeholder policy values).");
    }

    // ── Committee routing config (placeholder bands) ────────────────────────

    private static async Task SeedRoutingConfigAsync(CRMSDbContext context, ILogger logger)
    {
        if (await context.RhshfRoutingConfigs.AnyAsync())
        {
            logger.LogInformation("RH-SHF routing config already seeded, skipping.");
            return;
        }

        logger.LogInformation("Seeding default RH-SHF routing config...");

        // PLACEHOLDER bands (₦, against TotalEopValue) — input-financing tickets run much smaller
        // than NAMP's equipment loans, so these are deliberately narrower. Adjust via
        // Admin > RH-SHF Routing Config after seeding; these are a starting point, not a business
        // decision baked into code.
        var configs = new[]
        {
            Routing(CommitteeType.BranchCredit,     0m,            2_000_000m,           priority: 0),
            Routing(CommitteeType.ZonalCredit,      2_000_001m,    10_000_000m,          priority: 1),
            Routing(CommitteeType.RegionalCredit,   10_000_001m,   30_000_000m,          priority: 2),
            Routing(CommitteeType.HeadOfficeCredit, 30_000_001m,   999_999_999_999m,     priority: 3),
        };

        await context.RhshfRoutingConfigs.AddRangeAsync(configs);
        await context.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} RH-SHF routing config rows (placeholder bands).", configs.Length);
    }

    private static RhshfRoutingConfig Routing(CommitteeType tier, decimal min, decimal max, int priority)
    {
        var result = RhshfRoutingConfig.Create(tier, min, max, priority);
        result.Value.SetAuditInfo("System Seeder", isNew: true);
        return result.Value;
    }

    // ── Pre-Deployment gate checklist (default templates) ───────────────────

    private static async Task SeedPreDeploymentChecklistAsync(CRMSDbContext context, ILogger logger)
    {
        if (await context.RhshfPreDeploymentChecklistTemplates.AnyAsync())
        {
            logger.LogInformation("RH-SHF pre-deployment checklist already seeded, skipping.");
            return;
        }

        logger.LogInformation("Seeding default RH-SHF pre-deployment checklist templates...");

        var items = new[]
        {
            Checklist(
                "BOA Disbursement Account Verified",
                "Review the BOA account details pulled from Core Banking and confirm the account name/number match the offer.",
                isMandatory: true, sortOrder: 10, kind: RhshfPreDeploymentVerificationKind.AccountConfirmation),
            Checklist(
                "Collateral Reviewed & Documents on File",
                "Review the collateral records and their uploaded documents, then confirm. Not applicable if no collateral was taken.",
                isMandatory: false, sortOrder: 20, kind: RhshfPreDeploymentVerificationKind.CollateralReview),
            Checklist(
                "Signed Offer Letter & KFS Received",
                "Auto-verified from the FAC's signed offer documents on the offer.",
                isMandatory: true, sortOrder: 30, kind: RhshfPreDeploymentVerificationKind.OfferDocuments),
            Checklist(
                "Compliance / AML Sign-Off",
                "Confirm compliance and AML checks for this disbursement are complete.",
                isMandatory: true, sortOrder: 40, kind: RhshfPreDeploymentVerificationKind.Manual),
        };

        await context.RhshfPreDeploymentChecklistTemplates.AddRangeAsync(items);
        await context.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} RH-SHF pre-deployment checklist templates.", items.Length);
    }

    private static RhshfPreDeploymentChecklistTemplate Checklist(
        string title, string description, bool isMandatory, int sortOrder, RhshfPreDeploymentVerificationKind kind)
    {
        var result = RhshfPreDeploymentChecklistTemplate.Create(title, description, isMandatory, sortOrder, kind);
        result.Value.SetAuditInfo("System Seeder", isNew: true);
        return result.Value;
    }
}

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
        await SeedDocumentTemplatesAsync(context, logger);
    }

    // ── Stage config ──────────────────────────────────────────────────────

    private static async Task SeedWorkflowConfigAsync(CRMSDbContext context, ILogger logger)
    {
        logger.LogInformation("Seeding NAMP workflow stage config (add-missing-rows)...");

        var existing = await context.NampWorkflowConfigs
            .Select(c => c.Status)
            .ToListAsync();

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

            // ── Stage 3: Risk Review ──────────────────────────────────────
            Stage(NampApplicationStatus.RiskReview,   "Risk Review",   "Risk Officer reviewing application before committee circulation.", Roles.BranchRiskOfficer, slaHours: 48, sort: 47),
            Stage(NampApplicationStatus.RiskDeclined, "Risk Declined", "Application declined by Risk Officer.", Roles.SystemAdmin, slaHours: 0, sort: 49, isTerminal: true),

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
            Stage(NampApplicationStatus.OfferAccepted, "Offer Accepted", "Applicant countersigned offer letter; auto-routed to legal clearance.", Roles.LegalOfficer, slaHours: 0, sort: 105),
            Stage(NampApplicationStatus.OfferLapsed, "Offer Lapsed", "Applicant did not countersign within SLA.", Roles.SystemAdmin, slaHours: 0, sort: 106, isTerminal: true),

            // ── Stage 5c: Legal Clearance ──────────────────────────────────
            Stage(NampApplicationStatus.LegalClearance, "Legal Clearance", "Legal Officer reviewing application before pre-deployment.", Roles.LegalOfficer, slaHours: 72, sort: 107),
            Stage(NampApplicationStatus.LegalReturned, "Legal Returned", "Legal Officer returned application to Loan Officer for remediation.", Roles.LoanOfficer, slaHours: 48, sort: 108),
            Stage(NampApplicationStatus.LegalDeclined, "Legal Declined", "Legal Officer declined the application.", Roles.SystemAdmin, slaHours: 0, sort: 109, isTerminal: true),

            // ── Stage 6: Pre-Deployment Verification ──────────────────────
            Stage(NampApplicationStatus.PreDeploymentVerification, "Pre-Deployment Verification", "Deployment Officer verifying 4 gate conditions before equipment deployment.", Roles.DeploymentOfficer, slaHours: 48, sort: 110),

            // ── Stage 6: Deployment ───────────────────────────────────────
            Stage(NampApplicationStatus.Deployment, "Deployment", "Deployment Officer tracking equipment delivery and GPS activation.", Roles.DeploymentOfficer, slaHours: 168, sort: 130),

            // ── Stage 9: Active ───────────────────────────────────────────
            Stage(NampApplicationStatus.Active, "Active", "GPS confirmed; PAYS repayment cycle running.", Roles.LoanOfficer, slaHours: 0, sort: 140),

            // ── Terminal ──────────────────────────────────────────────────
            Stage(NampApplicationStatus.Closed, "Closed", "Full PAYS repayment completed.", Roles.SystemAdmin, slaHours: 0, sort: 150, isTerminal: true),
            Stage(NampApplicationStatus.Declined, "Declined", "Application declined (outbound NAMP callback sent).", Roles.SystemAdmin, slaHours: 0, sort: 160, isTerminal: true),
        };

        var missing = stages.Where(s => !existing.Contains(s.Status)).ToArray();
        if (missing.Length == 0)
        {
            logger.LogInformation("NAMP workflow config up to date — no missing rows.");
            return;
        }

        await context.NampWorkflowConfigs.AddRangeAsync(missing);
        await context.SaveChangesAsync();
        logger.LogInformation("Added {Count} missing NAMP workflow stage config row(s): {Statuses}",
            missing.Length, string.Join(", ", missing.Select(s => s.Status)));
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
        // SortOrder → (RequiresDoc, DocCategory)
        // Equity deposit (sort 10) is confirmed by the NAMP portal before the webhook is sent;
        // the deployment officer only needs to tick a checkbox — no document upload required here.
        var expectedBySort = new Dictionary<int, (bool RequiresDoc, NampDocumentCategory? DocCategory)>
        {
            { 10, (false, null) },
            { 20, (true,  NampDocumentCategory.LeaseAgreement) },
            { 30, (true,  NampDocumentCategory.GpsConsentForm) },
            { 40, (true,  NampDocumentCategory.InsuranceCertificate) },
            { 50, (true,  NampDocumentCategory.SignedNampOfferLetter) },
        };

        if (await context.NampPreDeploymentChecklistTemplates.AnyAsync())
        {
            // Repair drift: enum ordinal shifts or policy changes since initial seed
            var existing = await context.NampPreDeploymentChecklistTemplates.ToListAsync();
            bool anyFixed = false;

            foreach (var t in existing)
            {
                if (!expectedBySort.TryGetValue(t.SortOrder, out var expected)) continue;
                if (t.RequiresDocumentUpload == expected.RequiresDoc && t.DocumentCategory == expected.DocCategory) continue;

                t.Update(t.Title, t.Description, expected.RequiresDoc, expected.DocCategory, t.IsMandatory, t.SortOrder);
                anyFixed = true;
                logger.LogWarning(
                    "Repaired checklist template SortOrder={Sort}: RequiresDoc={R}, DocCategory={C}",
                    t.SortOrder, expected.RequiresDoc, expected.DocCategory);
            }

            if (anyFixed)
            {
                await context.SaveChangesAsync();

                // Propagate to per-application instances
                foreach (var t in existing)
                {
                    if (!expectedBySort.TryGetValue(t.SortOrder, out var expected)) continue;
                    if (expected.DocCategory.HasValue)
                    {
                        await context.Database.ExecuteSqlRawAsync(
                            "UPDATE NampPreDeploymentChecklistItems SET RequiresDocumentUpload = {0}, DocumentCategory = {1} WHERE TemplateItemId = {2}",
                            expected.RequiresDoc ? 1 : 0, (int)expected.DocCategory.Value, t.Id.ToString());
                    }
                    else
                    {
                        await context.Database.ExecuteSqlRawAsync(
                            "UPDATE NampPreDeploymentChecklistItems SET RequiresDocumentUpload = {0}, DocumentCategory = NULL WHERE TemplateItemId = {1}",
                            expected.RequiresDoc ? 1 : 0, t.Id.ToString());
                    }
                }

                logger.LogInformation("Repaired {Count} checklist template(s) and their application instances.", existing.Count(t => expectedBySort.ContainsKey(t.SortOrder)));
            }

            logger.LogInformation("NAMP pre-deployment checklist already seeded, skipping.");
            return;
        }

        logger.LogInformation("Seeding NAMP pre-deployment checklist templates...");

        var items = new[]
        {
            Checklist(
                title: "Equity Deposit Confirmed",
                description: "Confirm that the applicant's equity deposit has been paid via the NAMP portal prior to this application being submitted.",
                requiresDoc: false,
                docCategory: null,
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

    // ── Document templates ────────────────────────────────────────────────────

    private static async Task SeedDocumentTemplatesAsync(CRMSDbContext context, ILogger logger)
    {
        logger.LogInformation("Seeding NAMP document templates...");

        var defaults = new[]
        {
            (
                Type: NampDocumentType.OfferLetter,
                Title: "OFFER LETTER",
                Body: """
Dear {{ApplicantName}},

We are pleased to inform you that your application under the National Agricultural Mechanisation Programme (NAMP) has been approved by the Credit Committee of the Bank of Agriculture Limited.

The terms and conditions of the approved credit facility, including the proposed repayment schedule, are set out below for your review and acceptance.

Please note that this offer is subject to the conditions stated herein and is valid for thirty (30) days from the date of this letter.
""",
                Conditions: """
GENERAL CONDITIONS

1. The equipment shall be used solely for agricultural purposes as stated in your application and in accordance with the objectives of the National Agricultural Mechanisation Programme.

2. You shall maintain adequate insurance cover on the equipment throughout the tenor of this facility. Proof of insurance shall be submitted to the Bank prior to equipment deployment.

3. The Bank reserves the right to conduct periodic inspections of the equipment and your farming operations at any time during the tenor of this facility.

4. A GPS tracking device shall be installed and activated on the equipment prior to deployment. You hereby consent to the monitoring of the equipment's location throughout the facility tenor.

5. Repayment obligations commence in accordance with the schedule set out above. Default in repayment attracts penalty charges as specified in the Bank's approved schedule of charges.

6. This offer is valid for thirty (30) days from the date of issue. Failure to accept within this period renders this offer null and void.

7. The standard terms and conditions of the Bank applicable to agricultural credit facilities apply to this facility and are incorporated herein by reference.

ACCEPTANCE

I/We hereby accept the terms and conditions of this offer as stated above, including the repayment schedule set out herein. I/We confirm that the information provided in support of this application is true and accurate to the best of my/our knowledge and belief.
"""
            ),
            (
                Type: NampDocumentType.LeaseAgreement,
                Title: "HIRE PURCHASE / LEASE AGREEMENT",
                Body: """
HIRE PURCHASE / LEASE AGREEMENT

This Hire Purchase Agreement ("Agreement") is entered into as of {{Date}} between the Bank of Agriculture Limited ("Lessor" or "the Bank") and {{ApplicantName}}, BOA Account Number {{BoaAccountNumber}} ("Lessee" or "Borrower").

RECITALS

The Bank has agreed to finance the acquisition of the equipment described herein under the National Agricultural Mechanisation Programme (NAMP) on a hire-purchase basis, subject to the terms and conditions set out in this Agreement.

ARTICLE 1 — EQUIPMENT

The equipment subject to this Agreement is as follows:

Description: {{EquipmentDescription}}
Total Equipment Value: NGN {{EquipmentValue}}
Borrower's Equity Contribution: NGN {{EquityAmount}}
Financed Amount (Loan): NGN {{LoanAmount}}

ARTICLE 2 — FACILITY TERMS

2.1 Loan Amount: The Bank shall finance the sum of NGN {{LoanAmount}} (the "Loan Amount") representing the balance of the equipment cost after deduction of the Borrower's equity contribution.

2.2 Tenor: The loan shall be repayable over a period of {{TenorMonths}} months from the date of disbursement.

2.3 Interest Rate: Interest shall accrue on the outstanding loan balance at the rate of {{InterestRate}}% per annum (reducing balance).

2.4 Repayment: The Borrower shall repay the loan in equal monthly installments of NGN {{MonthlyInstallment}}, due on the same day of each month commencing one (1) month from the date of equipment deployment.

ARTICLE 3 — OWNERSHIP AND POSSESSION

3.1 The equipment shall remain the property of the Bank until all amounts due under this Agreement have been fully paid.

3.2 The Borrower shall have possession and use of the equipment during the term of this Agreement, subject to the conditions herein.

3.3 Upon full repayment of all amounts due, ownership of the equipment shall transfer to the Borrower.

ARTICLE 4 — OBLIGATIONS OF THE BORROWER

4.1 The Borrower shall use the equipment solely for agricultural purposes and in accordance with the objectives of the NAMP.

4.2 The Borrower shall maintain the equipment in good working condition and shall bear all costs of maintenance, repair, and insurance.

4.3 The Borrower shall not sell, transfer, assign, encumber, or otherwise dispose of the equipment without the prior written consent of the Bank.

4.4 The Borrower shall permit the Bank and its authorised representatives to inspect the equipment at any reasonable time.

4.5 The Borrower shall maintain valid insurance on the equipment throughout the term of this Agreement and shall provide evidence of such insurance to the Bank on request.

ARTICLE 5 — GPS TRACKING

5.1 A GPS tracking device shall be installed on the equipment by the Bank or its designated agent prior to deployment.

5.2 The Borrower hereby consents to the continuous monitoring of the equipment's location throughout the term of this Agreement.

5.3 The Borrower shall not tamper with, disable, or remove the GPS tracking device. Any breach of this clause shall constitute an event of default.

ARTICLE 6 — DEFAULT

6.1 An event of default shall occur if the Borrower fails to make any repayment when due, breaches any term of this Agreement, or if the equipment is damaged, destroyed, or disposed of without the Bank's consent.

6.2 Upon the occurrence of an event of default, the Bank may, without prejudice to any other remedies, demand immediate repayment of all outstanding amounts and/or repossess the equipment.

ARTICLE 7 — GOVERNING LAW

This Agreement shall be governed by and construed in accordance with the laws of the Federal Republic of Nigeria. Any disputes arising under this Agreement shall be subject to the jurisdiction of the courts of the Federal Republic of Nigeria.

ARTICLE 8 — ENTIRE AGREEMENT

This Agreement constitutes the entire agreement between the parties with respect to the subject matter hereof and supersedes all prior negotiations, representations, and agreements.
""",
                Conditions: (string?)null
            ),
            (
                Type: NampDocumentType.GpsConsentForm,
                Title: "GPS TRACKING CONSENT FORM",
                Body: """
GPS TRACKING CONSENT FORM

Reference: {{ApplicationNumber}}
Date: {{Date}}

BORROWER DETAILS

Name: {{ApplicantName}}
BOA Account Number: {{BoaAccountNumber}}
Equipment: {{EquipmentDescription}}

CONSENT DECLARATION

I, {{ApplicantName}}, being the beneficiary of the equipment financed under the National Agricultural Mechanisation Programme (NAMP) administered by the Bank of Agriculture Limited, hereby freely and voluntarily give my informed consent to the following:

1. GPS DEVICE INSTALLATION

I consent to the installation of a Global Positioning System (GPS) tracking device on the above-described equipment by the Bank of Agriculture Limited or its authorised agent. I understand that this device will be installed prior to the deployment of the equipment to me.

2. LOCATION MONITORING

I consent to the continuous monitoring of the location of the equipment via the installed GPS tracking device throughout the tenor of my facility agreement with the Bank of Agriculture Limited.

3. DATA USE

I understand and agree that the location data collected by the GPS device will be used solely for the purposes of:
(a) Monitoring the deployment and utilisation of the equipment;
(b) Ensuring the equipment is used for its intended agricultural purpose;
(c) Assisting in the recovery of the equipment in the event of default or misuse;
(d) Supporting the Bank's portfolio monitoring obligations under the NAMP.

4. OBLIGATIONS

I agree that I shall:
(a) Not tamper with, disable, remove, or attempt to defeat the GPS tracking device in any manner;
(b) Notify the Bank immediately in the event of theft, loss, or damage to the equipment;
(c) Allow authorised Bank personnel or their agents to inspect the GPS device on the equipment at any reasonable time.

5. CONSEQUENCES OF BREACH

I understand that tampering with or disabling the GPS tracking device constitutes a material breach of my facility agreement with the Bank of Agriculture Limited and may result in immediate demand for repayment of the outstanding facility balance and/or repossession of the equipment.

6. VOLUNTARY CONSENT

I confirm that this consent is given freely, voluntarily, and without duress. I have read and understood the above declaration and agree to be bound by its terms.
""",
                Conditions: (string?)null
            ),
        };

        var systemUserId = Guid.Empty;

        foreach (var (type, title, body, conditions) in defaults)
        {
            var exists = await context.NampDocumentTemplates
                .AnyAsync(t => t.DocumentType == type);

            if (exists) continue;

            var template = NampDocumentTemplate.Create(type, title, body, systemUserId, conditions);
            template.SetAuditInfo("seed", isNew: true);
            await context.NampDocumentTemplates.AddAsync(template);
            logger.LogInformation("Seeded NAMP document template: {Type}", type);
        }

        await context.SaveChangesAsync();
    }
}

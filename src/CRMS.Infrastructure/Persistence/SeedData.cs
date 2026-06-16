using CRMS.Domain.Aggregates.Location;
using CRMS.Domain.Aggregates.ProductCatalog;
using CRMS.Domain.Common;
using CRMS.Domain.Constants;
using CRMS.Domain.Entities.Identity;
using CRMS.Domain.Enums;
using CRMS.Domain.ValueObjects;
using CRMS.Application.Identity.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using InterestRateType = CRMS.Domain.ValueObjects.InterestRateType;

namespace CRMS.Infrastructure.Persistence;

/// <summary>
/// Provides seed data for initial system setup.
/// </summary>
public static class SeedData
{
    // Depth → LocationType mapping for Bank of Agriculture.
    // BOA's Fineract hierarchy has no Region level, so Zones sit directly under HO.
    // Adjust this map when deploying for institutions with different hierarchies.
    private static readonly Dictionary<int, LocationType> BoaDepthMap = new()
    {
        [1] = LocationType.HeadOffice,
        [2] = LocationType.Zone,
        [3] = LocationType.Branch,
    };

    public static async Task SeedAsync(CRMSDbContext context, ILogger logger, IPasswordHasher? passwordHasher = null, bool isDevelopment = false)
    {
        await SeedLocationsAsync(context, logger);
        await SeedRolesAsync(context, logger);
        await SeedSecurityEmailTemplatesAsync(context, logger);
        await SeedNampWorkflowEmailTemplatesAsync(context, logger);

        // NAMP workflow stage config, routing bands, and pre-deployment checklist templates.
        // System-generated and required in every environment — idempotent (skips if already seeded).
        await NampWorkflowSeeder.SeedAsync(context, logger);

        // Loan products and committees are configured by the admin via the UI in production.
        // In development they are seeded with mock data for testing convenience.
        if (isDevelopment)
        {
            await SeedLoanProductsAsync(context, logger);
            await SeedStandingCommitteesAsync(context, logger);
        }

        if (passwordHasher != null)
        {
            await SeedBootstrapAdminAsync(context, logger, passwordHasher);
            if (isDevelopment)
                await SeedTestUsersAsync(context, logger, passwordHasher);
        }
    }

    private static async Task SeedLocationsAsync(CRMSDbContext context, ILogger logger)
    {
        await FineractOfficeSeeder.SeedAsync(
            context,
            logger,
            embeddedResourceName: "boaoffices.json",
            depthMap: BoaDepthMap);
    }

    private static async Task SeedRolesAsync(CRMSDbContext context, ILogger logger)
    {
        var existingRoleNames = await context.Roles.Select(r => r.Name).ToListAsync();
        var missingRoles = Roles.AllRoles.Where(r => !existingRoleNames.Contains(r)).ToList();

        if (missingRoles.Count == 0)
        {
            logger.LogInformation("Roles already seeded, skipping");
            return;
        }

        logger.LogInformation("Seeding {Count} missing role(s): {Roles}", missingRoles.Count, string.Join(", ", missingRoles));

        foreach (var roleName in missingRoles)
        {
            var description = Roles.RoleDescriptions.GetValueOrDefault(roleName, roleName);
            var role = ApplicationRole.Create(roleName, description, RoleType.System);
            await context.Roles.AddAsync(role);
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Roles seeded successfully");
    }

    /// <summary>
    /// Seeds/refreshes the identity/security email templates (account created, password changed,
    /// password reset) in every environment. Upserts on each startup so design changes propagate.
    /// </summary>
    private static async Task SeedSecurityEmailTemplatesAsync(CRMSDbContext context, ILogger logger)
    {
        var actorId = (await context.Users.FirstOrDefaultAsync(u => u.Email == "admin@crms.ng"))?.Id
            ?? Guid.Empty;

        var accountInner =
            "<p style=\"margin:0 0 16px;\">Dear {{RecipientName}},</p>" +
            "<p style=\"margin:0 0 16px;\">An account has been created for you on the <strong>Bank of Agriculture Credit Risk Management System</strong>. Use the credentials below to sign in.</p>" +
            CredBox("Sign in with", "{{LoginEmail}}", "Temporary password", "{{TempPassword}}") +
            "<p style=\"margin:16px 0;\">For your security, please change your password immediately after your first sign-in.</p>" +
            "<p style=\"margin:16px 0 0;color:#6b7280;font-size:13px;\">If you did not expect this email, please contact your administrator.</p>";

        var changedInner =
            "<p style=\"margin:0 0 16px;\">Dear {{RecipientName}},</p>" +
            "<p style=\"margin:0 0 16px;\">This confirms that the password for your <strong>Bank of Agriculture CRMS</strong> account was changed on <strong>{{ChangedAt}}</strong>.</p>" +
            "<p style=\"margin:16px 0 0;color:#6b7280;font-size:13px;\">If you did not make this change, contact your administrator immediately.</p>";

        var resetInner =
            "<p style=\"margin:0 0 16px;\">Dear {{RecipientName}},</p>" +
            "<p style=\"margin:0 0 8px;\">We received a request to reset your <strong>Bank of Agriculture CRMS</strong> password. Click the button below to choose a new one. This link is valid for {{ExpiryMinutes}} minutes.</p>" +
            Button("Reset Password", "{{ResetLink}}") +
            "<p style=\"margin:8px 0;color:#6b7280;font-size:12px;\">If the button doesn't work, copy and paste this link into your browser:<br/>" +
            "<a href=\"{{ResetLink}}\" style=\"color:#1f7a3d;word-break:break-all;\">{{ResetLink}}</a></p>" +
            "<p style=\"margin:16px 0 0;color:#6b7280;font-size:13px;\">If you did not request this, you can safely ignore this email — your password will not change.</p>";

        var defs = new (string Code, string Name, NotificationType Type, string Subject, string Body, string Html)[]
        {
            ("ACCOUNT_CREATED", "Account Created", NotificationType.AccountCreated,
                "Your Bank of Agriculture CRMS account is ready",
                "Dear {{RecipientName}},\n\nAn account has been created for you on the Bank of Agriculture CRMS.\n\n" +
                "Sign in with: {{LoginEmail}}\nTemporary password: {{TempPassword}}\n\n" +
                "Please change your password immediately after your first sign-in.\n\n" +
                "If you did not expect this, contact your administrator.\n\nRegards,\nBank of Agriculture CRMS",
                Shell(accountInner)),

            ("PASSWORD_CHANGED", "Password Changed", NotificationType.PasswordChanged,
                "Your CRMS password was changed",
                "Dear {{RecipientName}},\n\nThis confirms that the password for your Bank of Agriculture CRMS account " +
                "was changed on {{ChangedAt}}.\n\nIf you did not make this change, contact your administrator immediately.\n\n" +
                "Regards,\nBank of Agriculture CRMS",
                Shell(changedInner)),

            ("PASSWORD_RESET", "Password Reset", NotificationType.PasswordReset,
                "Reset your CRMS password",
                "Dear {{RecipientName}},\n\nWe received a request to reset your Bank of Agriculture CRMS password.\n\n" +
                "Use this link to set a new password (valid for {{ExpiryMinutes}} minutes):\n{{ResetLink}}\n\n" +
                "If you did not request this, you can safely ignore this email — your password will not change.\n\n" +
                "Regards,\nBank of Agriculture CRMS",
                Shell(resetInner)),
        };

        var changes = 0;
        foreach (var d in defs)
        {
            var existing = await context.NotificationTemplates
                .FirstOrDefaultAsync(t => t.Code == d.Code && t.Channel == NotificationChannel.Email);

            if (existing is null)
            {
                var result = Domain.Aggregates.Notification.NotificationTemplate.Create(
                    d.Code, d.Name, $"Template for {d.Name}", d.Type, NotificationChannel.Email,
                    d.Body, actorId, subject: d.Subject, bodyHtmlTemplate: d.Html);
                if (result.IsSuccess)
                {
                    await context.NotificationTemplates.AddAsync(result.Value);
                    changes++;
                }
            }
            else if (existing.Subject != d.Subject || existing.BodyTemplate != d.Body || existing.BodyHtmlTemplate != d.Html)
            {
                existing.Update(d.Name, $"Template for {d.Name}", d.Body, actorId, subject: d.Subject, bodyHtmlTemplate: d.Html);
                context.NotificationTemplates.Update(existing);
                changes++;
            }
        }

        if (changes > 0)
        {
            await context.SaveChangesAsync();
            logger.LogInformation("Seeded/updated {Count} security email template(s)", changes);
        }
    }

    /// <summary>
    /// Seeds/refreshes the NAMP workflow notification email templates in every environment.
    /// Upserts on each startup (same pattern as the security templates) so design changes propagate.
    /// Three templates back the whole NAMP notification matrix:
    ///   NAMP_ACTION_REQUIRED — generic "your stage is ready" mail to the next responsible role;
    ///   NAMP_COMMITTEE_VOTE  — vote-required fan-out to each committee member;
    ///   NAMP_DECLINED        — decline notice to the loan officer.
    /// </summary>
    private static async Task SeedNampWorkflowEmailTemplatesAsync(CRMSDbContext context, ILogger logger)
    {
        var actorId = (await context.Users.FirstOrDefaultAsync(u => u.Email == "admin@crms.ng"))?.Id
            ?? Guid.Empty;

        var queueInner =
            "<p style=\"margin:0 0 16px;\">Dear {{RecipientName}},</p>" +
            "<p style=\"margin:0 0 16px;\">A new NAMP application has arrived in your branch queue and is awaiting recall and review.</p>" +
            CredBox("Application", "{{ApplicationNumber}}", "Applicant", "{{ApplicantName}}") +
            Button("Open the NAMP queue", "{{ActionUrl}}") +
            "<p style=\"margin:8px 0 0;color:#6b7280;font-size:12px;\">If the button doesn't work, sign in to the CRMS and open the NAMP queue.</p>";

        var actionInner =
            "<p style=\"margin:0 0 16px;\">Dear {{RecipientName}},</p>" +
            "<p style=\"margin:0 0 16px;\">A NAMP application has reached the <strong>{{StageName}}</strong> stage and is awaiting your action.</p>" +
            CredBox("Application", "{{ApplicationNumber}}", "Applicant", "{{ApplicantName}}") +
            "<p style=\"margin:16px 0 0;\">Please action it within <strong>{{Sla}}</strong>.</p>" +
            Button("Open application", "{{ActionUrl}}") +
            "<p style=\"margin:8px 0 0;color:#6b7280;font-size:12px;\">If the button doesn't work, sign in to the CRMS and open the application from your queue.</p>";

        var voteInner =
            "<p style=\"margin:0 0 16px;\">Dear {{RecipientName}},</p>" +
            "<p style=\"margin:0 0 16px;\">You are a member of the <strong>{{CommitteeType}}</strong> reviewing the NAMP application below. Your vote is required.</p>" +
            CredBox("Application", "{{ApplicationNumber}}", "Applicant", "{{ApplicantName}}") +
            "<p style=\"margin:16px 0 0;\">Voting deadline: <strong>{{Deadline}}</strong>.</p>" +
            Button("Review &amp; vote", "{{ActionUrl}}");

        var declinedInner =
            "<p style=\"margin:0 0 16px;\">Dear {{RecipientName}},</p>" +
            "<p style=\"margin:0 0 16px;\">The NAMP application below has been <strong>declined</strong> at the {{StageName}} stage.</p>" +
            CredBox("Application", "{{ApplicationNumber}}", "Applicant", "{{ApplicantName}}") +
            "<p style=\"margin:16px 0 8px;\"><strong>Reason</strong></p>" +
            "<p style=\"margin:0 0 16px;padding:12px 16px;background:#fdf2f2;border:1px solid #f5d0d0;border-radius:8px;color:#7a1f1f;\">{{Reason}}</p>" +
            "<p style=\"margin:16px 0 0;color:#6b7280;font-size:13px;\">No further action is required unless you intend to follow up with the applicant.</p>";

        var defs = new (string Code, string Name, NotificationType Type, string Subject, string Body, string Html)[]
        {
            ("NAMP_NEW_IN_QUEUE", "NAMP New Application In Queue", NotificationType.WorkflowAssigned,
                "New NAMP application {{ApplicationNumber}} in your branch queue",
                "Dear {{RecipientName}},\n\nA new NAMP application has arrived in your branch queue and is awaiting recall and review.\n\n" +
                "Application: {{ApplicationNumber}}\nApplicant: {{ApplicantName}}\n\n" +
                "Open the NAMP queue here: {{ActionUrl}}\n\nRegards,\nBank of Agriculture CRMS",
                Shell(queueInner)),

            ("NAMP_ACTION_REQUIRED", "NAMP Action Required", NotificationType.WorkflowAssigned,
                "NAMP {{ApplicationNumber}} — {{StageName}} awaiting your action",
                "Dear {{RecipientName}},\n\nA NAMP application has reached the {{StageName}} stage and is awaiting your action.\n\n" +
                "Application: {{ApplicationNumber}}\nApplicant: {{ApplicantName}}\n\n" +
                "Please action it within {{Sla}}.\n\nOpen it here: {{ActionUrl}}\n\nRegards,\nBank of Agriculture CRMS",
                Shell(actionInner)),

            ("NAMP_COMMITTEE_VOTE", "NAMP Committee Vote Required", NotificationType.CommitteeVoteRequired,
                "NAMP {{ApplicationNumber}} — your committee vote is required",
                "Dear {{RecipientName}},\n\nYou are a member of the {{CommitteeType}} reviewing a NAMP application. Your vote is required.\n\n" +
                "Application: {{ApplicationNumber}}\nApplicant: {{ApplicantName}}\nVoting deadline: {{Deadline}}\n\n" +
                "Review and vote here: {{ActionUrl}}\n\nRegards,\nBank of Agriculture CRMS",
                Shell(voteInner)),

            ("NAMP_DECLINED", "NAMP Application Declined", NotificationType.ApplicationRejected,
                "NAMP {{ApplicationNumber}} — declined at {{StageName}}",
                "Dear {{RecipientName}},\n\nThe NAMP application below has been declined at the {{StageName}} stage.\n\n" +
                "Application: {{ApplicationNumber}}\nApplicant: {{ApplicantName}}\n\nReason: {{Reason}}\n\n" +
                "Regards,\nBank of Agriculture CRMS",
                Shell(declinedInner)),
        };

        var changes = 0;
        foreach (var d in defs)
        {
            var existing = await context.NotificationTemplates
                .FirstOrDefaultAsync(t => t.Code == d.Code && t.Channel == NotificationChannel.Email);

            if (existing is null)
            {
                var result = Domain.Aggregates.Notification.NotificationTemplate.Create(
                    d.Code, d.Name, $"Template for {d.Name}", d.Type, NotificationChannel.Email,
                    d.Body, actorId, subject: d.Subject, bodyHtmlTemplate: d.Html);
                if (result.IsSuccess)
                {
                    await context.NotificationTemplates.AddAsync(result.Value);
                    changes++;
                }
            }
            else if (existing.Subject != d.Subject || existing.BodyTemplate != d.Body || existing.BodyHtmlTemplate != d.Html)
            {
                existing.Update(d.Name, $"Template for {d.Name}", d.Body, actorId, subject: d.Subject, bodyHtmlTemplate: d.Html);
                context.NotificationTemplates.Update(existing);
                changes++;
            }
        }

        if (changes > 0)
        {
            await context.SaveChangesAsync();
            logger.LogInformation("Seeded/updated {Count} NAMP workflow email template(s)", changes);
        }
    }

    // ── Branded email building blocks (Bank of Agriculture green theme) ──────

    private const string LogoUrl = "https://tpclientassets.s3.eu-central-1.amazonaws.com/bankofagriculture/logo.png";

    private static string Shell(string inner) =>
        "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"></head>" +
        "<body style=\"margin:0;padding:0;background:#f4f6f4;\">" +
        "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:#f4f6f4;padding:24px 0;font-family:Arial,Helvetica,sans-serif;\"><tr><td align=\"center\">" +
        "<table role=\"presentation\" width=\"600\" cellpadding=\"0\" cellspacing=\"0\" style=\"max-width:600px;width:100%;background:#ffffff;border-radius:10px;overflow:hidden;box-shadow:0 2px 6px rgba(0,0,0,0.08);\">" +
        // Header
        "<tr><td style=\"background:#14532d;padding:22px 32px;\">" +
        "<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\"><tr>" +
        "<td style=\"vertical-align:middle;\"><div style=\"background:#ffffff;border-radius:8px;padding:6px 8px;display:inline-block;\">" +
        "<img src=\"" + LogoUrl + "\" alt=\"Bank of Agriculture\" height=\"40\" style=\"display:block;height:40px;width:auto;border:0;\"/></div></td>" +
        "<td style=\"vertical-align:middle;padding-left:14px;\">" +
        "<div style=\"color:#ffffff;font-size:17px;font-weight:bold;letter-spacing:0.5px;\">BANK OF AGRICULTURE</div>" +
        "<div style=\"color:#a7d3b5;font-size:11px;letter-spacing:0.5px;\">Credit Risk Management System</div>" +
        "</td></tr></table></td></tr>" +
        // Body
        "<tr><td style=\"padding:32px;color:#1f2937;font-size:14px;line-height:1.6;\">" + inner + "</td></tr>" +
        // Footer
        "<tr><td style=\"background:#f0f4f1;padding:18px 32px;color:#8a948c;font-size:11px;line-height:1.5;border-top:1px solid #e3e9e4;\">" +
        "This is an automated message from the Bank of Agriculture CRMS — please do not reply. " +
        "If you did not expect this email, contact your administrator.</td></tr>" +
        "</table></td></tr></table></body></html>";

    private static string Button(string text, string url) =>
        "<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" style=\"margin:24px 0;\"><tr>" +
        "<td style=\"background:#1f7a3d;border-radius:6px;\">" +
        "<a href=\"" + url + "\" style=\"display:inline-block;padding:13px 30px;color:#ffffff;font-size:14px;font-weight:bold;text-decoration:none;font-family:Arial,Helvetica,sans-serif;\">" + text + "</a>" +
        "</td></tr></table>";

    private static string CredBox(string label1, string value1, string label2, string value2) =>
        "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:#f0f4f1;border:1px solid #e3e9e4;border-radius:8px;margin:8px 0;\"><tr>" +
        "<td style=\"padding:16px 20px;font-size:14px;color:#1f2937;line-height:1.8;\">" +
        "<span style=\"color:#6b7280;font-size:12px;text-transform:uppercase;letter-spacing:0.04em;\">" + label1 + "</span><br/><strong>" + value1 + "</strong><br/>" +
        "<span style=\"color:#6b7280;font-size:12px;text-transform:uppercase;letter-spacing:0.04em;\">" + label2 + "</span><br/><strong>" + value2 + "</strong>" +
        "</td></tr></table>";

    private static async Task SeedLoanProductsAsync(CRMSDbContext context, ILogger logger)
    {
        if (await context.LoanProducts.AnyAsync())
        {
            logger.LogInformation("Loan products already seeded, skipping");
            return;
        }

        logger.LogInformation("Seeding loan products...");

        // Corporate Loan Products
        var termLoan = LoanProduct.Create(
            "CORP-TERM-001",
            "Corporate Term Loan",
            "Standard corporate term loan for business expansion and capital expenditure",
            LoanProductType.Corporate,
            Money.Create(50_000_000m, "NGN"),
            Money.Create(5_000_000_000m, "NGN"),
            12, 84);

        var workingCapital = LoanProduct.Create(
            "CORP-WC-001",
            "Working Capital Finance",
            "Short-term facility for operational working capital needs",
            LoanProductType.Corporate,
            Money.Create(10_000_000m, "NGN"),
            Money.Create(500_000_000m, "NGN"),
            3, 12);

        var overdraft = LoanProduct.Create(
            "CORP-OD-001",
            "Corporate Overdraft",
            "Revolving overdraft facility for cash flow management",
            LoanProductType.Corporate,
            Money.Create(5_000_000m, "NGN"),
            Money.Create(200_000_000m, "NGN"),
            1, 12);

        var assetFinance = LoanProduct.Create(
            "CORP-AF-001",
            "Asset Finance",
            "Financing for acquisition of machinery, equipment, and vehicles",
            LoanProductType.Corporate,
            Money.Create(20_000_000m, "NGN"),
            Money.Create(1_000_000_000m, "NGN"),
            24, 60);

        var projectFinance = LoanProduct.Create(
            "CORP-PF-001",
            "Project Finance",
            "Long-term financing for large-scale infrastructure and development projects",
            LoanProductType.Corporate,
            Money.Create(500_000_000m, "NGN"),
            Money.Create(50_000_000_000m, "NGN"),
            36, 120);

        // Retail Loan Products (for Phase 2)
        var personalLoan = LoanProduct.Create(
            "RET-PL-001",
            "Personal Loan",
            "Unsecured personal loan for salaried employees",
            LoanProductType.Retail,
            Money.Create(100_000m, "NGN"),
            Money.Create(10_000_000m, "NGN"),
            3, 48);

        var salaryAdvance = LoanProduct.Create(
            "RET-SA-001",
            "Salary Advance",
            "Short-term advance against confirmed salary",
            LoanProductType.Retail,
            Money.Create(50_000m, "NGN"),
            Money.Create(2_000_000m, "NGN"),
            1, 3);

        var products = new[] { termLoan, workingCapital, overdraft, assetFinance, projectFinance, personalLoan, salaryAdvance };
        var seededCount = 0;

        foreach (var productResult in products)
        {
            if (productResult.IsSuccess)
            {
                // Add a default pricing tier
                productResult.Value.AddPricingTier(
                    "Standard",
                    16.5m, // Interest rate
                    InterestRateType.Reducing,
                    1.0m, // Processing fee %
                    null,
                    null, null);
                    
                await context.LoanProducts.AddAsync(productResult.Value);
                seededCount++;
            }
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Loan products seeded successfully ({Count} products)", seededCount);
    }

    private static async Task SeedBootstrapAdminAsync(CRMSDbContext context, ILogger logger, IPasswordHasher passwordHasher)
    {
        const string adminEmail = "admin@crms.ng";
        const string adminUsername = "sysadmin";

        if (await context.Users.AnyAsync(u => u.Email == adminEmail))
        {
            logger.LogInformation("Bootstrap admin already exists, skipping");
            return;
        }

        // Look up by type, not code — the code varies per institution (e.g. "BOA-HQ-2")
        var ho = await context.Locations.FirstOrDefaultAsync(l => l.Type == LocationType.HeadOffice);
        var sysAdminRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == Roles.SystemAdmin);

        if (sysAdminRole is null)
        {
            logger.LogError("Cannot seed bootstrap admin — SystemAdmin role not found. Ensure roles are seeded first.");
            return;
        }

        if (ho is null)
        {
            logger.LogWarning("Bootstrap admin: no HeadOffice location found — location seeding may have failed. Admin will be created without a location once locations are seeded.");
            return;
        }

        var userResult = ApplicationUser.Create(
            adminEmail, adminUsername, "System", "Administrator",
            UserType.Staff, string.Empty, ho.Id);

        if (userResult.IsFailure)
        {
            logger.LogError("Failed to create bootstrap admin: {Error}", userResult.Error);
            return;
        }

        userResult.Value.SetPasswordHash(passwordHasher.HashPassword("Admin@CRMS2026!"));
        userResult.Value.AddRole(sysAdminRole);
        await context.Users.AddAsync(userResult.Value);
        await context.SaveChangesAsync();

        logger.LogInformation("Bootstrap admin seeded: {Email} — change password immediately after first login", adminEmail);
    }

    private static async Task SeedTestUsersAsync(CRMSDbContext context, ILogger logger, IPasswordHasher passwordHasher)
    {
        if (await context.Users.AnyAsync(u => u.Email != "admin@crms.ng"))
        {
            logger.LogInformation("Test users already seeded, skipping");
            return;
        }

        logger.LogInformation("Seeding test users...");

        // Get locations for assignment — pick by type rather than hardcoded codes
        // so the test seeder works regardless of which institution's offices are loaded
        var branches = await context.Locations
            .Where(l => l.Type == LocationType.Branch && l.IsActive)
            .OrderBy(l => l.SortOrder)
            .ToListAsync();
        var lagosMain = branches.FirstOrDefault();
        var abujaMain = branches.Skip(1).FirstOrDefault() ?? lagosMain;
        var ho = await context.Locations.FirstOrDefaultAsync(l => l.Type == LocationType.HeadOffice);

        // Get roles
        var loanOfficerRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == Roles.LoanOfficer);
        var branchApproverRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == Roles.BranchApprover);
        var creditOfficerRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == Roles.CreditOfficer);
        var hoReviewerRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == Roles.HOReviewer);
        var sysAdminRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == Roles.SystemAdmin);

        var seededCount = 0;
        var defaultPassword = passwordHasher.HashPassword("Test@123");

        // Test Loan Officer (Lagos Main Branch)
        if (lagosMain != null && loanOfficerRole != null)
        {
            var userResult = ApplicationUser.Create(
                "loanofficer@crms.test", "loanofficer", "Test", "LoanOfficer",
                UserType.Staff, "08011111111", lagosMain.Id);
            if (userResult.IsSuccess)
            {
                userResult.Value.SetPasswordHash(defaultPassword);
                userResult.Value.AddRole(loanOfficerRole);
                await context.Users.AddAsync(userResult.Value);
                seededCount++;
            }
        }

        // Test Branch Approver (Lagos Main Branch)
        if (lagosMain != null && branchApproverRole != null)
        {
            var userResult = ApplicationUser.Create(
                "branchapprover@crms.test", "branchapprover", "Test", "BranchApprover",
                UserType.Staff, "08022222222", lagosMain.Id);
            if (userResult.IsSuccess)
            {
                userResult.Value.SetPasswordHash(defaultPassword);
                userResult.Value.AddRole(branchApproverRole);
                await context.Users.AddAsync(userResult.Value);
                seededCount++;
            }
        }

        // Test Loan Officer (Abuja Main Branch - different location)
        if (abujaMain != null && loanOfficerRole != null)
        {
            var userResult = ApplicationUser.Create(
                "loanofficer.abuja@crms.test", "loanofficer_abuja", "Test", "LoanOfficerAbuja",
                UserType.Staff, "08033333333", abujaMain.Id);
            if (userResult.IsSuccess)
            {
                userResult.Value.SetPasswordHash(defaultPassword);
                userResult.Value.AddRole(loanOfficerRole);
                await context.Users.AddAsync(userResult.Value);
                seededCount++;
            }
        }

        // Test Credit Officer (Head Office - global visibility)
        if (ho != null && creditOfficerRole != null)
        {
            var userResult = ApplicationUser.Create(
                "creditofficer@crms.test", "creditofficer", "Test", "CreditOfficer",
                UserType.Staff, "08044444444", ho.Id);
            if (userResult.IsSuccess)
            {
                userResult.Value.SetPasswordHash(defaultPassword);
                userResult.Value.AddRole(creditOfficerRole);
                await context.Users.AddAsync(userResult.Value);
                seededCount++;
            }
        }

        // Test HO Reviewer (Head Office - global visibility)
        if (ho != null && hoReviewerRole != null)
        {
            var userResult = ApplicationUser.Create(
                "horeviewer@crms.test", "horeviewer", "Test", "HOReviewer",
                UserType.Staff, "08055555555", ho.Id);
            if (userResult.IsSuccess)
            {
                userResult.Value.SetPasswordHash(defaultPassword);
                userResult.Value.AddRole(hoReviewerRole);
                await context.Users.AddAsync(userResult.Value);
                seededCount++;
            }
        }

        // Test System Admin (Head Office - full access)
        if (ho != null && sysAdminRole != null)
        {
            var userResult = ApplicationUser.Create(
                "admin@crms.test", "admin", "System", "Administrator",
                UserType.Staff, "08066666666", ho.Id);
            if (userResult.IsSuccess)
            {
                userResult.Value.SetPasswordHash(defaultPassword);
                userResult.Value.AddRole(sysAdminRole);
                await context.Users.AddAsync(userResult.Value);
                seededCount++;
            }
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Test users seeded successfully ({Count} users). Default password: Test@123", seededCount);
    }

    private static async Task SeedStandingCommitteesAsync(CRMSDbContext context, ILogger logger)
    {
        if (await context.StandingCommittees.AnyAsync())
        {
            logger.LogInformation("Standing committees already seeded, skipping");
            return;
        }

        logger.LogInformation("Seeding standing committees...");

        var committees = new[]
        {
            (Name: "Branch Credit Committee", Type: CommitteeType.BranchCredit,
             ReqVotes: 3, MinApproval: 2, Deadline: 48, Min: 0m, Max: (decimal?)50_000_000m),

            (Name: "Regional Credit Committee", Type: CommitteeType.RegionalCredit,
             ReqVotes: 3, MinApproval: 2, Deadline: 72, Min: 50_000_000m, Max: (decimal?)200_000_000m),

            (Name: "Head Office Credit Committee", Type: CommitteeType.HeadOfficeCredit,
             ReqVotes: 5, MinApproval: 3, Deadline: 72, Min: 200_000_000m, Max: (decimal?)500_000_000m),

            (Name: "Management Credit Committee", Type: CommitteeType.ManagementCredit,
             ReqVotes: 5, MinApproval: 4, Deadline: 120, Min: 500_000_000m, Max: (decimal?)2_000_000_000m),

            (Name: "Board Credit Committee", Type: CommitteeType.BoardCredit,
             ReqVotes: 7, MinApproval: 5, Deadline: 168, Min: 2_000_000_000m, Max: (decimal?)null),
        };

        foreach (var c in committees)
        {
            var result = Domain.Aggregates.Committee.StandingCommittee.Create(
                c.Name, c.Type, c.ReqVotes, c.MinApproval, c.Deadline, c.Min, c.Max);
            if (result.IsSuccess)
                await context.StandingCommittees.AddAsync(result.Value);
            else
                logger.LogWarning("Failed to create standing committee {Name}: {Error}", c.Name, result.Error);
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Standing committees seeded successfully (5 committees)");
    }
}

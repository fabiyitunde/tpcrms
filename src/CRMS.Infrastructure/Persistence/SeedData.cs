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

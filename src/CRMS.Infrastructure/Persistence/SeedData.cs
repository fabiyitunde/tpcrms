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
    public static async Task SeedAsync(CRMSDbContext context, ILogger logger, IPasswordHasher? passwordHasher = null)
    {
        await SeedLocationsAsync(context, logger);
        await SeedRolesAsync(context, logger);
        await SeedLoanProductsAsync(context, logger);
        await SeedStandingCommitteesAsync(context, logger);
        if (passwordHasher != null)
        {
            await SeedTestUsersAsync(context, logger, passwordHasher);
        }
    }

    private static async Task SeedLocationsAsync(CRMSDbContext context, ILogger logger)
    {
        if (await context.Locations.AnyAsync())
        {
            logger.LogInformation("Locations already seeded, skipping");
            return;
        }

        logger.LogInformation("Seeding locations...");

        // Create HeadOffice (root)
        var hoResult = Location.CreateHeadOffice("HO", "Head Office", "Corporate Headquarters, Victoria Island, Lagos");
        if (hoResult.IsFailure) { logger.LogError("Failed to create HeadOffice: {Error}", hoResult.Error); return; }
        var ho = hoResult.Value;
        await context.Locations.AddAsync(ho);
        await context.SaveChangesAsync(); // Save to get the ID

        // Create Regions
        var southernResult = Location.CreateRegion("RG-SOUTH", "Southern Region", ho.Id, 1);
        var northernResult = Location.CreateRegion("RG-NORTH", "Northern Region", ho.Id, 2);
        
        if (southernResult.IsFailure || northernResult.IsFailure)
        {
            logger.LogError("Failed to create regions");
            return;
        }
        
        var southern = southernResult.Value;
        var northern = northernResult.Value;
        await context.Locations.AddRangeAsync(southern, northern);
        await context.SaveChangesAsync();

        // Create Zones under Southern Region
        var swZoneResult = Location.CreateZone("ZN-SW", "South-West Zone", southern.Id, 1);
        var seZoneResult = Location.CreateZone("ZN-SE", "South-East Zone", southern.Id, 2);
        var ssZoneResult = Location.CreateZone("ZN-SS", "South-South Zone", southern.Id, 3);
        
        // Create Zones under Northern Region
        var ncZoneResult = Location.CreateZone("ZN-NC", "North-Central Zone", northern.Id, 1);
        var nwZoneResult = Location.CreateZone("ZN-NW", "North-West Zone", northern.Id, 2);
        var neZoneResult = Location.CreateZone("ZN-NE", "North-East Zone", northern.Id, 3);

        var zones = new[] { swZoneResult, seZoneResult, ssZoneResult, ncZoneResult, nwZoneResult, neZoneResult };
        foreach (var zoneResult in zones)
        {
            if (zoneResult.IsSuccess)
                await context.Locations.AddAsync(zoneResult.Value);
        }
        await context.SaveChangesAsync();

        var swZone = swZoneResult.Value;
        var seZone = seZoneResult.Value;
        var ssZone = ssZoneResult.Value;
        var ncZone = ncZoneResult.Value;
        var nwZone = nwZoneResult.Value;
        var neZone = neZoneResult.Value;

        // Create Branches under South-West Zone (Lagos)
        var branches = new List<Result<Location>>
        {
            Location.CreateBranch("BR-LAG-001", "Lagos Main Branch", swZone.Id, "123 Marina Road, Lagos Island", "John Adeyemi", "08012345678", 1),
            Location.CreateBranch("BR-LAG-002", "Victoria Island Branch", swZone.Id, "Plot 45, Adeola Odeku Street, VI", "Sarah Okonkwo", "08023456789", 2),
            Location.CreateBranch("BR-LAG-003", "Ikeja Branch", swZone.Id, "15 Allen Avenue, Ikeja", "Michael Eze", "08034567890", 3),
            Location.CreateBranch("BR-LAG-004", "Lekki Branch", swZone.Id, "Admiralty Way, Lekki Phase 1", "Grace Adekunle", "08045678901", 4),
            Location.CreateBranch("BR-IBD-001", "Ibadan Branch", swZone.Id, "Ring Road, Ibadan", "Peter Uche", "08056789012", 5),
            
            // South-East Zone (Port Harcourt, Enugu)
            Location.CreateBranch("BR-PH-001", "Port Harcourt Main Branch", seZone.Id, "Aba Road, Port Harcourt", "Amina Ibrahim", "08067890123", 1),
            Location.CreateBranch("BR-ENU-001", "Enugu Branch", seZone.Id, "Ogui Road, Enugu", "David Okafor", "08078901234", 2),
            
            // South-South Zone
            Location.CreateBranch("BR-BEN-001", "Benin Branch", ssZone.Id, "Airport Road, Benin City", "Comfort Idowu", "08089012345", 1),
            
            // North-Central Zone (Abuja)
            Location.CreateBranch("BR-ABJ-001", "Abuja Main Branch", ncZone.Id, "Central Business District, Abuja", "Hassan Mohammed", "08090123456", 1),
            Location.CreateBranch("BR-ABJ-002", "Wuse Branch", ncZone.Id, "Zone 5, Wuse, Abuja", "Fatima Bello", "08001234567", 2),
            
            // North-West Zone
            Location.CreateBranch("BR-KAN-001", "Kano Branch", nwZone.Id, "Murtala Mohammed Way, Kano", "Usman Yakubu", "08012345679", 1),
            Location.CreateBranch("BR-KAD-001", "Kaduna Branch", nwZone.Id, "Ahmadu Bello Way, Kaduna", "Musa Garba", "08023456780", 2),
            
            // North-East Zone (GAP-5 fix: add branches under NE zone)
            Location.CreateBranch("BR-MAI-001", "Maiduguri Branch", neZone.Id, "Bama Road, Maiduguri", "Ibrahim Shettima", "08034567891", 1),
            Location.CreateBranch("BR-BAU-001", "Bauchi Branch", neZone.Id, "Jos Road, Bauchi", "Abubakar Tafawa", "08045678902", 2)
        };

        var branchCount = 0;
        foreach (var branchResult in branches)
        {
            if (branchResult.IsSuccess)
            {
                await context.Locations.AddAsync(branchResult.Value);
                branchCount++;
            }
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Locations seeded successfully (1 HO, 2 Regions, 6 Zones, {BranchCount} Branches)", branchCount);
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

    private static async Task SeedTestUsersAsync(CRMSDbContext context, ILogger logger, IPasswordHasher passwordHasher)
    {
        if (await context.Users.AnyAsync())
        {
            logger.LogInformation("Users already seeded, skipping");
            return;
        }

        logger.LogInformation("Seeding test users...");

        // Get locations for assignment
        var lagosMain = await context.Locations.FirstOrDefaultAsync(l => l.Code == "BR-LAG-001");
        var abujaMain = await context.Locations.FirstOrDefaultAsync(l => l.Code == "BR-ABJ-001");
        var swZone = await context.Locations.FirstOrDefaultAsync(l => l.Code == "ZN-SW");
        var ho = await context.Locations.FirstOrDefaultAsync(l => l.Code == "HO");

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

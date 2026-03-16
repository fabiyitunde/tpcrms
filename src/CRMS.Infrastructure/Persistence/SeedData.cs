using CRMS.Domain.Aggregates.Location;
using CRMS.Domain.Aggregates.ProductCatalog;
using CRMS.Domain.Common;
using CRMS.Domain.Constants;
using CRMS.Domain.Entities.Identity;
using CRMS.Domain.Enums;
using CRMS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using InterestRateType = CRMS.Domain.ValueObjects.InterestRateType;

namespace CRMS.Infrastructure.Persistence;

/// <summary>
/// Provides seed data for initial system setup.
/// </summary>
public static class SeedData
{
    public static async Task SeedAsync(CRMSDbContext context, ILogger logger)
    {
        await SeedLocationsAsync(context, logger);
        await SeedRolesAsync(context, logger);
        await SeedLoanProductsAsync(context, logger);
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
            Location.CreateBranch("BR-KAD-001", "Kaduna Branch", nwZone.Id, "Ahmadu Bello Way, Kaduna", "Musa Garba", "08023456780", 2)
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
        if (await context.Roles.AnyAsync())
        {
            logger.LogInformation("Roles already seeded, skipping");
            return;
        }

        logger.LogInformation("Seeding roles...");

        // Use Roles.cs constants to ensure consistency with authorization attributes
        foreach (var roleName in Roles.AllRoles)
        {
            var description = Roles.RoleDescriptions.GetValueOrDefault(roleName, roleName);
            var role = ApplicationRole.Create(roleName, description, RoleType.System);
            await context.Roles.AddAsync(role);
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Roles seeded successfully ({Count} roles)", Roles.AllRoles.Length);
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
}

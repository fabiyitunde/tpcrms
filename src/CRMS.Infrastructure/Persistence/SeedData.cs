using CRMS.Domain.Aggregates.ProductCatalog;
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
        await SeedRolesAsync(context, logger);
        await SeedLoanProductsAsync(context, logger);
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

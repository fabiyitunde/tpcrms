using CRMS.Application.Identity.Interfaces;
using CRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRMS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SeedController : ControllerBase
{
    private readonly CRMSDbContext _context;
    private readonly ILogger<SeedController> _logger;
    private readonly IWebHostEnvironment _environment;
    private readonly IPasswordHasher _passwordHasher;

    public SeedController(
        CRMSDbContext context,
        ILogger<SeedController> logger,
        IWebHostEnvironment environment,
        IPasswordHasher passwordHasher)
    {
        _context = context;
        _logger = logger;
        _environment = environment;
        _passwordHasher = passwordHasher;
    }

    /// <summary>
    /// Seeds the database with comprehensive test data.
    /// Only available in Development environment.
    /// </summary>
    [HttpPost("comprehensive")]
    [AllowAnonymous]
    public async Task<IActionResult> SeedComprehensiveData()
    {
        if (!_environment.IsDevelopment())
        {
            return BadRequest(new { error = "Comprehensive seeding is only available in Development environment" });
        }

        try
        {
            _logger.LogInformation("Starting comprehensive data seeding via API...");
            await ComprehensiveDataSeeder.SeedComprehensiveDataAsync(_context, _logger);
            return Ok(new { 
                success = true, 
                message = "Comprehensive data seeding completed successfully",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during comprehensive data seeding");
            return StatusCode(500, new { 
                error = "Seeding failed", 
                message = ex.Message,
                innerMessage = ex.InnerException?.Message
            });
        }
    }

    /// <summary>
    /// Resets all user passwords to a known value.
    /// Only available in Development environment.
    /// </summary>
    [HttpPost("reset-passwords")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPasswords([FromQuery] string password = "Password1$$$")
    {
        if (!_environment.IsDevelopment())
        {
            return BadRequest(new { error = "Password reset is only available in Development environment" });
        }

        try
        {
            var users = await _context.Users.ToListAsync();
            var passwordHash = _passwordHasher.HashPassword(password);
            
            foreach (var user in users)
            {
                user.SetPasswordHash(passwordHash);
            }
            
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Reset passwords for {Count} users to '{Password}'", users.Count, password);
            
            return Ok(new { 
                success = true, 
                message = $"Reset passwords for {users.Count} users",
                password = password,
                users = users.Select(u => new { u.Email, u.UserName }).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting passwords");
            return StatusCode(500, new { error = "Password reset failed", message = ex.Message });
        }
    }

    /// <summary>
    /// Seeds basic data (roles and loan products).
    /// Only available in Development environment.
    /// </summary>
    [HttpPost("basic")]
    [AllowAnonymous]
    public async Task<IActionResult> SeedBasicData()
    {
        if (!_environment.IsDevelopment())
        {
            return BadRequest(new { error = "Basic seeding is only available in Development environment" });
        }

        try
        {
            _logger.LogInformation("Starting basic data seeding via API...");
            await SeedData.SeedAsync(_context, _logger);
            return Ok(new { 
                success = true, 
                message = "Basic data seeding completed successfully",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during basic data seeding");
            return StatusCode(500, new { 
                error = "Seeding failed", 
                message = ex.Message,
                innerMessage = ex.InnerException?.Message
            });
        }
    }

    /// <summary>
    /// Returns the row counts for all seeded tables.
    /// </summary>
    [HttpGet("status")]
    [AllowAnonymous]
    public async Task<IActionResult> GetSeedStatus()
    {
        var counts = new Dictionary<string, int>
        {
            ["Roles"] = await _context.Roles.CountAsync(),
            ["LoanProducts"] = await _context.LoanProducts.CountAsync(),
            ["PricingTiers"] = await _context.PricingTiers.CountAsync(),
            ["Users"] = await _context.Users.CountAsync(),
            ["UserRoles"] = await _context.UserRoles.CountAsync(),
            ["Permissions"] = await _context.Permissions.CountAsync(),
            ["RolePermissions"] = await _context.RolePermissions.CountAsync(),
            ["WorkflowDefinitions"] = await _context.WorkflowDefinitions.CountAsync(),
            ["WorkflowStages"] = await _context.WorkflowStages.CountAsync(),
            ["WorkflowTransitions"] = await _context.WorkflowTransitions.CountAsync(),
            ["NotificationTemplates"] = await _context.NotificationTemplates.CountAsync(),
            ["ScoringParameters"] = await _context.ScoringParameters.CountAsync(),
            ["ScoringParameterHistory"] = await _context.ScoringParameterHistory.CountAsync(),
            ["EligibilityRules"] = await _context.EligibilityRules.CountAsync(),
            ["DocumentRequirements"] = await _context.DocumentRequirements.CountAsync(),
            ["LoanApplications"] = await _context.LoanApplications.CountAsync(),
            ["LoanApplicationParties"] = await _context.LoanApplicationParties.CountAsync(),
            ["ConsentRecords"] = await _context.ConsentRecords.CountAsync(),
            ["BureauReports"] = await _context.BureauReports.CountAsync(),
            ["BureauAccounts"] = await _context.BureauAccounts.CountAsync(),
            ["BureauScoreFactors"] = await _context.BureauScoreFactors.CountAsync(),
            ["BankStatements"] = await _context.BankStatements.CountAsync(),
            ["StatementTransactions"] = await _context.StatementTransactions.CountAsync(),
            ["FinancialStatements"] = await _context.FinancialStatements.CountAsync(),
            ["Collaterals"] = await _context.Collaterals.CountAsync(),
            ["CollateralValuations"] = await _context.CollateralValuations.CountAsync(),
            ["CollateralDocuments"] = await _context.CollateralDocuments.CountAsync(),
            ["Guarantors"] = await _context.Guarantors.CountAsync(),
            ["GuarantorDocuments"] = await _context.GuarantorDocuments.CountAsync(),
            ["CreditAdvisories"] = await _context.CreditAdvisories.CountAsync(),
            ["CommitteeReviews"] = await _context.CommitteeReviews.CountAsync(),
            ["CommitteeMembers"] = await _context.CommitteeMembers.CountAsync(),
            ["CommitteeComments"] = await _context.CommitteeComments.CountAsync(),
            ["CommitteeDocuments"] = await _context.CommitteeDocuments.CountAsync(),
            ["LoanPacks"] = await _context.LoanPacks.CountAsync(),
            ["WorkflowInstances"] = await _context.WorkflowInstances.CountAsync(),
            ["WorkflowTransitionLogs"] = await _context.WorkflowTransitionLogs.CountAsync(),
            ["AuditLogs"] = await _context.AuditLogs.CountAsync(),
            ["DataAccessLogs"] = await _context.DataAccessLogs.CountAsync(),
            ["Notifications"] = await _context.Notifications.CountAsync()
        };

        var totalRows = counts.Values.Sum();
        var tablesWithData = counts.Count(c => c.Value > 0);
        var tablesEmpty = counts.Count(c => c.Value == 0);

        return Ok(new
        {
            timestamp = DateTime.UtcNow,
            summary = new
            {
                totalTables = counts.Count,
                tablesWithData,
                tablesEmpty,
                totalRows
            },
            tables = counts.OrderByDescending(c => c.Value).ToDictionary(c => c.Key, c => c.Value)
        });
    }
}

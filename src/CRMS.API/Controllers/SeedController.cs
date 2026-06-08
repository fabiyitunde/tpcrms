using CRMS.Application.Identity.Interfaces;
using CRMS.Domain.Aggregates.Namp;
using CRMS.Domain.Enums;
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
            await SeedData.SeedAsync(_context, _logger, _passwordHasher);
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
    /// Force-applies the CleanupNampWorkflow schema migration via raw SQL.
    /// Wipes test data, drops removed tables, drops removed columns, converts Status columns to VARCHAR(50).
    /// Dev-only. Idempotent — safe to run multiple times.
    /// </summary>
    [HttpPost("namp-cleanup-migration")]
    [AllowAnonymous]
    public async Task<IActionResult> ApplyNampCleanupMigration()
    {
        if (!_environment.IsDevelopment())
            return BadRequest(new { error = "Only available in Development environment" });

        try
        {
            var db = _context.Database;

            // Wipe test data
            await db.ExecuteSqlRawAsync("DELETE FROM NampApplications;");
            await db.ExecuteSqlRawAsync("DELETE FROM NampWorkflowConfigs;");
            await db.ExecuteSqlRawAsync("DELETE FROM NampRoutingConfigs;");
            await db.ExecuteSqlRawAsync("DELETE FROM NampStagingRecords;");

            // Drop removed tables
            await db.ExecuteSqlRawAsync("DROP TABLE IF EXISTS NampTechnicalAppraisalReports;");
            await db.ExecuteSqlRawAsync("DROP TABLE IF EXISTS NampViabilityScoreConfigs;");

            // Drop removed columns (ignore errors if already dropped)
            var dropCols = new[]
            {
                "ALTER TABLE NampApplications DROP COLUMN IF EXISTS TechnicalAppraisalByUserId;",
                "ALTER TABLE NampApplications DROP COLUMN IF EXISTS TechnicalAppraisalAt;",
                "ALTER TABLE NampApplications DROP COLUMN IF EXISTS TechnicalAppraisalNote;",
                "ALTER TABLE NampApplications DROP COLUMN IF EXISTS TrainingCompletedByUserId;",
                "ALTER TABLE NampApplications DROP COLUMN IF EXISTS TrainingCompletedAt;",
            };
            foreach (var sql in dropCols)
            {
                try { await db.ExecuteSqlRawAsync(sql); } catch { /* column may already be gone */ }
            }

            // Convert Status columns to VARCHAR(50) — only if currently int
            var alterCols = new[]
            {
                "ALTER TABLE NampApplications MODIFY COLUMN Status VARCHAR(50) NOT NULL;",
                "ALTER TABLE NampStatusHistory MODIFY COLUMN Status VARCHAR(50) NOT NULL;",
                "ALTER TABLE NampWorkflowConfigs MODIFY COLUMN Status VARCHAR(50) NOT NULL;",
            };
            foreach (var sql in alterCols)
            {
                try { await db.ExecuteSqlRawAsync(sql); } catch { /* column may already be VARCHAR */ }
            }

            // Re-seed workflow and routing configs
            await NampWorkflowSeeder.SeedAsync(_context, _logger);

            // Mark migration as applied if not already in history
            await db.ExecuteSqlRawAsync("""
                INSERT IGNORE INTO __EFMigrationsHistory (MigrationId, ProductVersion)
                VALUES ('20260608100000_CleanupNampWorkflow', '9.0.0');
                """);

            return Ok(new { success = true, message = "NAMP cleanup migration applied and re-seeded.", timestamp = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying NAMP cleanup migration");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Seeds NAMP workflow stage config and default routing config.
    /// Safe to run on an already-seeded database — idempotent.
    /// </summary>
    [HttpPost("namp")]
    [AllowAnonymous]
    public async Task<IActionResult> SeedNamp()
    {
        if (!_environment.IsDevelopment())
            return BadRequest(new { error = "Seeding is only available in Development environment" });

        try
        {
            _logger.LogInformation("Seeding NAMP workflow config...");
            await NampWorkflowSeeder.SeedAsync(_context, _logger);
            return Ok(new { success = true, message = "NAMP workflow config seeded.", timestamp = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during NAMP seeding");
            return StatusCode(500, new { error = "NAMP seeding failed", message = ex.Message });
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
            ["Notifications"] = await _context.Notifications.CountAsync(),
            ["NampStagingRecords"] = await _context.NampStagingRecords.CountAsync(),
            ["NampRoutingConfigs"] = await _context.NampRoutingConfigs.CountAsync(),
            ["NampApplications"] = await _context.NampApplications.CountAsync(),
            ["NampDocuments"] = await _context.NampDocuments.CountAsync(),
            ["NampStatusHistory"] = await _context.NampStatusHistory.CountAsync(),
            ["NampWorkflowConfigs"] = await _context.NampWorkflowConfigs.CountAsync(),
            ["NampWorkflowInstances"] = await _context.NampWorkflowInstances.CountAsync(),
            ["NampAdvisories"] = await _context.NampAdvisories.CountAsync()
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

    /// <summary>
    /// Seeds pre-deployment checklist items for any NAMP application that is in
    /// PreDeploymentVerification status but has no checklist items yet.
    /// Safe to run multiple times — skips applications that already have items.
    /// </summary>
    [HttpPost("namp-repair-checklist")]
    [AllowAnonymous]
    public async Task<IActionResult> RepairNampChecklist()
    {
        if (!_environment.IsDevelopment())
            return BadRequest(new { error = "Seeding is only available in Development environment" });

        try
        {
            var templates = await _context.NampPreDeploymentChecklistTemplates
                .Where(t => t.IsActive)
                .OrderBy(t => t.SortOrder)
                .ToListAsync();

            if (templates.Count == 0)
                return BadRequest(new { error = "No active checklist templates found. Run /api/seed/namp first." });

            var appsNeedingChecklist = await _context.NampApplications
                .Where(a => a.Status == NampApplicationStatus.PreDeploymentVerification)
                .Include(a => a.PreDeploymentChecklist)
                .ToListAsync();

            var seededApps = new List<string>();
            var skippedApps = new List<string>();

            foreach (var app in appsNeedingChecklist)
            {
                if (app.PreDeploymentChecklist.Any())
                {
                    skippedApps.Add(app.ApplicationNumber);
                    continue;
                }

                var items = templates
                    .Select(t => NampPreDeploymentChecklistItem.FromTemplate(app.Id, t))
                    .ToList();

                await _context.NampPreDeploymentChecklistItems.AddRangeAsync(items);
                seededApps.Add(app.ApplicationNumber);
            }

            if (seededApps.Count > 0)
                await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = $"Seeded checklist for {seededApps.Count} application(s). Skipped {skippedApps.Count} (already had items).",
                seeded = seededApps,
                skipped = skippedApps,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during NAMP checklist repair");
            return StatusCode(500, new { error = "Repair failed", message = ex.Message });
        }
    }
}

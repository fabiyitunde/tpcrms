using CRMS.Application.Identity.Interfaces;
using CRMS.Domain.Aggregates.Advisory;
using CRMS.Domain.Aggregates.Audit;
using CRMS.Domain.Aggregates.Collateral;
using CRMS.Domain.Aggregates.Committee;
using CRMS.Domain.Aggregates.Configuration;
using CRMS.Domain.Aggregates.Consent;
using CRMS.Domain.Aggregates.CreditBureau;
using CRMS.Domain.Aggregates.FinancialStatement;
using CRMS.Domain.Aggregates.Guarantor;
using CRMS.Domain.Aggregates.LoanApplication;
using CRMS.Domain.Aggregates.LoanPack;
using CRMS.Domain.Aggregates.Notification;
using CRMS.Domain.Aggregates.ProductCatalog;
using CRMS.Domain.Aggregates.StatementAnalysis;
using CRMS.Domain.Aggregates.Workflow;
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
/// Comprehensive data seeder that covers 100% of application entities.
/// Creates realistic test data for Nigerian corporate loan scenarios.
/// </summary>
public static class ComprehensiveDataSeeder
{
    private static readonly Random _random = new(42); // Fixed seed for reproducibility
    private static readonly string[] NigerianCompanies = 
    [
        "Dangote Industries Limited", "MTN Nigeria Communications PLC", "Access Bank PLC",
        "Zenith Bank PLC", "Nigerian Breweries PLC", "Nestle Nigeria PLC",
        "BUA Cement PLC", "Seplat Petroleum Development Company", "Flour Mills of Nigeria PLC",
        "Transcorp Hotels PLC", "Oando PLC", "FBN Holdings PLC",
        "United Bank for Africa PLC", "Stanbic IBTC Holdings PLC"
    ];

    private static readonly string[] NigerianNames = 
    [
        "Adewale Olusegun", "Chukwuemeka Nnamdi", "Fatima Abdullahi", "Blessing Okafor",
        "Oluwaseun Adeyemi", "Uche Okonkwo", "Amina Ibrahim", "Emeka Eze",
        "Folake Balogun", "Yusuf Mohammed", "Chioma Nwachukwu", "Tunde Bakare"
    ];

    private static readonly string[] BVNs = 
    [
        "22111111111", "22222222222", "22333333333", "22444444444",
        "22555555555", "22666666666", "22777777777", "22888888888"
    ];

    private static IPasswordHasher? _passwordHasher;
    
    public static async Task SeedComprehensiveDataAsync(CRMSDbContext context, ILogger logger, IPasswordHasher? passwordHasher = null)
    {
        _passwordHasher = passwordHasher;
        logger.LogInformation("Starting comprehensive data seeding (100% table coverage)...");

        // 1. Identity - Users, Permissions, RolePermissions (roles already seeded)
        var users = await SeedUsersAsync(context, logger);
        await SeedPermissionsAndRolePermissionsAsync(context, logger);
        
        // 2. Workflow Definitions
        var workflowDef = await SeedWorkflowDefinitionAsync(context, logger);
        
        // 3. Notification Templates
        await SeedNotificationTemplatesAsync(context, logger, users.SystemAdmin.Id);
        
        // 4. Scoring Parameters with History
        await SeedScoringParametersAsync(context, logger, users.SystemAdmin.Id);

        // 5. Get loan products and add EligibilityRules + DocumentRequirements
        var products = await context.LoanProducts.ToListAsync();
        if (!products.Any())
        {
            logger.LogWarning("No loan products found. Running base seed first...");
            await SeedData.SeedAsync(context, logger);
            products = await context.LoanProducts.ToListAsync();
        }
        await SeedProductRulesAndRequirementsAsync(context, logger, products);

        // 5.5. Seed global mock consent records for test BVNs (allows credit checks to succeed on new UI-created applications)
        await SeedMockConsentRecordsAsync(context, logger, users.LoanOfficer);

        // 6. Seed loan applications at various stages (covers most related tables)
        await SeedLoanApplicationsAsync(context, logger, users, products, workflowDef);

        // 7. Audit logs and Data Access Logs
        await SeedAuditLogsAsync(context, logger, users);
        await SeedDataAccessLogsAsync(context, logger, users);

        // 8. Notifications (actual notification records)
        await SeedNotificationsAsync(context, logger, users);

        await context.SaveChangesAsync();
        logger.LogInformation("Comprehensive data seeding completed successfully! (100% table coverage)");
    }

    // All user accounts the system needs — shared between fresh-seed and supplement paths.
    // Tuple: (userName, firstName, lastName, roleName, email, userType)
    private static readonly (string UserName, string FirstName, string LastName, string RoleName, string Email, UserType UserType)[] AllUserDefinitions =
    [
        ("admin",          "System",      "Administrator", Roles.SystemAdmin,      "admin@crms.ng",              UserType.Staff),
        ("loanofficer",    "Adewale",     "Johnson",       Roles.LoanOfficer,      "adewale.johnson@crms.ng",    UserType.Staff),
        ("loanofficer2",   "Chioma",      "Okonkwo",       Roles.LoanOfficer,      "chioma.okonkwo@crms.ng",     UserType.Staff),
        ("branchapprover", "Oluwaseun",   "Adeyemi",       Roles.BranchApprover,   "oluwaseun.adeyemi@crms.ng",  UserType.Staff),
        ("creditofficer",  "Uche",        "Eze",           Roles.CreditOfficer,    "uche.eze@crms.ng",           UserType.Staff),
        ("horeviewer",     "Fatima",      "Ibrahim",       Roles.HOReviewer,       "fatima.ibrahim@crms.ng",     UserType.Staff),
        ("committee1",     "Emeka",       "Nnamdi",        Roles.CommitteeMember,  "emeka.nnamdi@crms.ng",       UserType.Staff),
        ("committee2",     "Blessing",    "Okafor",        Roles.CommitteeMember,  "blessing.okafor@crms.ng",    UserType.Staff),
        ("committee3",     "Tunde",       "Bakare",        Roles.CommitteeMember,  "tunde.bakare@crms.ng",       UserType.Staff),
        ("finalapprover",  "Yusuf",       "Mohammed",      Roles.FinalApprover,    "yusuf.mohammed@crms.ng",     UserType.Staff),
        ("operations",     "Folake",      "Balogun",       Roles.Operations,       "folake.balogun@crms.ng",     UserType.Staff),
        ("gmfinance",      "Chidi",       "Okafor",        Roles.GMFinance,        "chidi.okafor@crms.ng",       UserType.Staff),
        ("legalofficer",   "Adaeze",      "Nwosu",         Roles.LegalOfficer,     "adaeze.nwosu@crms.ng",       UserType.Staff),
        ("headoflegal",    "Obiageli",    "Okonkwo",       Roles.HeadOfLegal,      "obiageli.okonkwo@crms.ng",   UserType.Staff),
        ("riskmanager",    "Chukwuemeka", "Obi",           Roles.RiskManager,      "chukwuemeka.obi@crms.ng",    UserType.Staff),
        ("auditor",        "Amina",       "Suleiman",      Roles.Auditor,          "amina.suleiman@crms.ng",     UserType.Staff),
        ("customer",       "Demo",        "Customer",      Roles.Customer,         "customer@crms.ng",           UserType.Customer),
    ];

    private static async Task<(ApplicationUser SystemAdmin, ApplicationUser LoanOfficer, ApplicationUser BranchApprover,
        ApplicationUser CreditOfficer, ApplicationUser HOReviewer, ApplicationUser LegalOfficer, ApplicationUser HeadOfLegal,
        ApplicationUser CommitteeMember1, ApplicationUser CommitteeMember2, ApplicationUser CommitteeMember3,
        ApplicationUser FinalApprover, ApplicationUser Operations, ApplicationUser GMFinance, ApplicationUser RiskManager, ApplicationUser Auditor)>
        SeedUsersAsync(CRMSDbContext context, ILogger logger)
    {
        var roles = await context.Roles.ToListAsync();

        if (await context.Users.AnyAsync())
        {
            logger.LogInformation("Users already exist, checking for missing role accounts...");
            var existingUsers = await context.Users
                .Include(u => u.UserRoles)
                .ToListAsync();

            // Fix any users with mocked password hashes
            if (_passwordHasher != null)
            {
                var usersWithMockedPasswords = existingUsers
                    .Where(u => u.PasswordHash.StartsWith("AQAAAAIAAYagAAAAEMocked"))
                    .ToList();

                if (usersWithMockedPasswords.Any())
                {
                    logger.LogInformation("Fixing {Count} users with mocked password hashes...", usersWithMockedPasswords.Count);
                    var validHash = _passwordHasher.HashPassword("Password1$$$");
                    foreach (var user in usersWithMockedPasswords)
                        user.SetPasswordHash(validHash);
                    await context.SaveChangesAsync();
                    logger.LogInformation("Password hashes updated successfully. Password for all users: Password1$$$");
                }
            }

            // Supplement: add any accounts that are absent (e.g. only the basic seed ran before this)
            var existingUserNames = existingUsers.Select(u => u.UserName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var locationId = existingUsers.FirstOrDefault()?.LocationId;
            bool addedAny = false;

            foreach (var def in AllUserDefinitions)
            {
                if (existingUserNames.Contains(def.UserName)) continue;

                logger.LogInformation("Adding missing user: {UserName} ({Role})", def.UserName, def.RoleName);
                var userResult = ApplicationUser.Create(
                    def.Email, def.UserName, def.FirstName, def.LastName,
                    def.UserType, "+234801234" + _random.Next(1000, 9999), locationId);
                if (userResult.IsSuccess)
                {
                    var passwordHash = _passwordHasher?.HashPassword("Password1$$$")
                        ?? "AQAAAAIAAYagAAAAEMocked" + Guid.NewGuid().ToString("N");
                    userResult.Value.SetPasswordHash(passwordHash);
                    var role = roles.FirstOrDefault(r => r.Name == def.RoleName);
                    if (role != null) userResult.Value.AddRole(role);
                    await context.Users.AddAsync(userResult.Value);
                    existingUsers.Add(userResult.Value);
                    addedAny = true;
                }
            }

            if (addedAny)
            {
                await context.SaveChangesAsync();
                logger.LogInformation("Missing user accounts added successfully.");
            }

            return (
                existingUsers.FirstOrDefault(u => u.UserName == "admin")          ?? existingUsers.First(),
                existingUsers.FirstOrDefault(u => u.UserName == "loanofficer")    ?? existingUsers.First(),
                existingUsers.FirstOrDefault(u => u.UserName == "branchapprover") ?? existingUsers.First(),
                existingUsers.FirstOrDefault(u => u.UserName == "creditofficer")  ?? existingUsers.First(),
                existingUsers.FirstOrDefault(u => u.UserName == "horeviewer")     ?? existingUsers.First(),
                existingUsers.FirstOrDefault(u => u.UserName == "legalofficer")   ?? existingUsers.First(),
                existingUsers.FirstOrDefault(u => u.UserName == "headoflegal")    ?? existingUsers.First(),
                existingUsers.FirstOrDefault(u => u.UserName == "committee1")     ?? existingUsers.First(),
                existingUsers.FirstOrDefault(u => u.UserName == "committee2")     ?? existingUsers.First(),
                existingUsers.FirstOrDefault(u => u.UserName == "committee3")     ?? existingUsers.First(),
                existingUsers.FirstOrDefault(u => u.UserName == "finalapprover")  ?? existingUsers.First(),
                existingUsers.FirstOrDefault(u => u.UserName == "operations")     ?? existingUsers.First(),
                existingUsers.FirstOrDefault(u => u.UserName == "gmfinance")      ?? existingUsers.First(),
                existingUsers.FirstOrDefault(u => u.UserName == "riskmanager")    ?? existingUsers.First(),
                existingUsers.FirstOrDefault(u => u.UserName == "auditor")        ?? existingUsers.First()
            );
        }

        logger.LogInformation("Seeding users...");
        var locationIdFresh = (Guid?)null; // HO/global users have no specific branch

        var createdUsers = new List<ApplicationUser>();
        foreach (var def in AllUserDefinitions)
        {
            var userResult = ApplicationUser.Create(
                def.Email, def.UserName, def.FirstName, def.LastName,
                def.UserType, "+234801234" + _random.Next(1000, 9999), locationIdFresh);
            if (userResult.IsSuccess)
            {
                var passwordHash = _passwordHasher?.HashPassword("Password1$$$")
                    ?? "AQAAAAIAAYagAAAAEMocked" + Guid.NewGuid().ToString("N");
                userResult.Value.SetPasswordHash(passwordHash);
                var role = roles.FirstOrDefault(r => r.Name == def.RoleName);
                if (role != null) userResult.Value.AddRole(role);
                await context.Users.AddAsync(userResult.Value);
                createdUsers.Add(userResult.Value);
            }
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Users seeded successfully ({Count} users)", createdUsers.Count);

        // Indices into createdUsers (matches AllUserDefinitions order):
        // 0=admin, 1=loanofficer, 2=loanofficer2, 3=branchapprover, 4=creditofficer,
        // 5=horeviewer, 6=committee1, 7=committee2, 8=committee3, 9=finalapprover,
        // 10=operations, 11=gmfinance, 12=legalofficer, 13=headoflegal, 14=riskmanager, 15=auditor, 16=customer
        return (
            createdUsers[0],  createdUsers[1],  createdUsers[3],  createdUsers[4],
            createdUsers[5],  createdUsers[12], createdUsers[13], createdUsers[6],
            createdUsers[7],  createdUsers[8],  createdUsers[9],  createdUsers[10],
            createdUsers[11], createdUsers[14], createdUsers[15]
        );
    }

    private static async Task<WorkflowDefinition?> SeedWorkflowDefinitionAsync(CRMSDbContext context, ILogger logger)
    {
        if (await context.WorkflowDefinitions.AnyAsync())
        {
            var existing = await context.WorkflowDefinitions
                .Include(w => w.Stages)
                .Include(w => w.Transitions)
                .FirstOrDefaultAsync(w => w.ApplicationType == LoanApplicationType.Corporate);

            // All upgrade blocks use raw SQL exclusively to avoid DbUpdateConcurrencyException.
            // EF SaveChangesAsync is never called in the upgrade path — only ExecuteSqlRawAsync.
            // Raw SQL DELETEs are idempotent (no error if row is absent).
            // INSERT IGNORE is idempotent for WorkflowStages (unique index on WorkflowDefinitionId+Status).
            // InsertTransitionIfMissingAsync is idempotent for WorkflowTransitions (WHERE NOT EXISTS guard).

            // Upgrade: add FinalApproval stage if missing
            if (existing != null && !existing.Stages.Any(s => s.Status == LoanApplicationStatus.FinalApproval))
            {
                logger.LogInformation("Upgrading workflow definition: adding FinalApproval stage...");
                var wfId = existing.Id;
                var now = DateTime.UtcNow;

                // Remove old direct transitions that are replaced by the FinalApproval step
                await context.Database.ExecuteSqlRawAsync(
                    "DELETE FROM WorkflowTransitions WHERE WorkflowDefinitionId = @p0 AND FromStatus = 'CommitteeCirculation' AND ToStatus = 'CommitteeApproved' AND Action = 'MoveToNextStage'",
                    wfId);
                await context.Database.ExecuteSqlRawAsync(
                    "DELETE FROM WorkflowTransitions WHERE WorkflowDefinitionId = @p0 AND FromStatus = 'CommitteeCirculation' AND ToStatus = 'Rejected' AND Action = 'Reject'",
                    wfId);
                await context.Database.ExecuteSqlRawAsync(
                    "DELETE FROM WorkflowTransitions WHERE WorkflowDefinitionId = @p0 AND FromStatus = 'CommitteeApproved' AND ToStatus = 'Approved' AND Action = 'Approve'",
                    wfId);

                // Add FinalApproval stage (INSERT IGNORE is idempotent via unique index)
                await context.Database.ExecuteSqlRawAsync(
                    "INSERT IGNORE INTO WorkflowStages (Id, WorkflowDefinitionId, Status, DisplayName, Description, AssignedRole, SLAHours, SortOrder, RequiresComment, IsTerminal, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy) " +
                    "VALUES (@p0, @p1, 'FinalApproval', 'Final Approval', 'Awaiting MD/CEO executive sign-off', 'FinalApprover', 24, 9, 0, 0, @p2, '', NULL, NULL)",
                    Guid.NewGuid(), wfId, now);

                // Re-add replacement transitions plus new FinalApproval transitions
                await InsertTransitionIfMissingAsync(context, wfId, "CommitteeCirculation", "CommitteeApproved", "MoveToNextStage", Roles.SystemAdmin, now);
                await InsertTransitionIfMissingAsync(context, wfId, "CommitteeCirculation", "Rejected",          "Reject",          Roles.SystemAdmin,  now);
                await InsertTransitionIfMissingAsync(context, wfId, "CommitteeApproved",    "FinalApproval",     "MoveToNextStage", Roles.SystemAdmin,  now);
                await InsertTransitionIfMissingAsync(context, wfId, "FinalApproval",        "Approved",          "Approve",         Roles.FinalApprover, now);
                await InsertTransitionIfMissingAsync(context, wfId, "FinalApproval",        "Rejected",          "Reject",          Roles.FinalApprover, now);

                logger.LogInformation("Workflow definition upgraded with FinalApproval stage successfully.");
            }

            // Upgrade: add OfferGenerated and OfferAccepted stages if missing
            if (existing != null && !existing.Stages.Any(s => s.Status == LoanApplicationStatus.OfferGenerated))
            {
                logger.LogInformation("Upgrading workflow definition: adding OfferGenerated/OfferAccepted stages...");
                var wfId = existing.Id;
                var now = DateTime.UtcNow;

                // Remove old direct Approved→Disbursed shortcut
                await context.Database.ExecuteSqlRawAsync(
                    "DELETE FROM WorkflowTransitions WHERE WorkflowDefinitionId = @p0 AND FromStatus = 'Approved' AND ToStatus = 'Disbursed' AND Action = 'Complete'",
                    wfId);

                // Add OfferGenerated and OfferAccepted stages (INSERT IGNORE is idempotent)
                await context.Database.ExecuteSqlRawAsync(
                    "INSERT IGNORE INTO WorkflowStages (Id, WorkflowDefinitionId, Status, DisplayName, Description, AssignedRole, SLAHours, SortOrder, RequiresComment, IsTerminal, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy) " +
                    "VALUES (@p0, @p1, 'OfferGenerated', 'Offer Letter Issued', 'Offer letter issued to customer, awaiting signed acceptance', 'LoanOfficer', 72, 11, 0, 0, @p2, '', NULL, NULL)",
                    Guid.NewGuid(), wfId, now);
                await context.Database.ExecuteSqlRawAsync(
                    "INSERT IGNORE INTO WorkflowStages (Id, WorkflowDefinitionId, Status, DisplayName, Description, AssignedRole, SLAHours, SortOrder, RequiresComment, IsTerminal, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy) " +
                    "VALUES (@p0, @p1, 'OfferAccepted', 'Offer Accepted', 'Customer accepted offer, pending disbursement', 'Operations', 48, 12, 0, 0, @p2, '', NULL, NULL)",
                    Guid.NewGuid(), wfId, now);

                // Add the new three-step post-approval transitions
                await InsertTransitionIfMissingAsync(context, wfId, "Approved",       "OfferGenerated", "MoveToNextStage", Roles.LoanOfficer, now);
                await InsertTransitionIfMissingAsync(context, wfId, "OfferGenerated", "OfferAccepted",  "MoveToNextStage", Roles.Operations, now);
                await InsertTransitionIfMissingAsync(context, wfId, "OfferAccepted",  "Disbursed",      "Complete",        Roles.Operations, now);

                logger.LogInformation("Offer letter workflow stages added successfully.");
            }

            // Upgrade: add LegalReview and LegalApproval stages if missing
            if (existing != null && !existing.Stages.Any(s => s.Status == LoanApplicationStatus.LegalReview))
            {
                logger.LogInformation("Upgrading workflow definition: adding LegalReview and LegalApproval stages...");
                var wfId = existing.Id;
                var now = DateTime.UtcNow;

                // Insert LegalReview and LegalApproval stages (INSERT IGNORE is idempotent)
                await context.Database.ExecuteSqlRawAsync(
                    "INSERT IGNORE INTO WorkflowStages (Id, WorkflowDefinitionId, Status, DisplayName, Description, AssignedRole, SLAHours, SortOrder, RequiresComment, IsTerminal, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy) " +
                    "VALUES (@p0, @p1, 'LegalReview', 'Legal Review', 'Legal officer preparing opinion', 'LegalOfficer', 48, 7, 0, 0, @p2, '', NULL, NULL)",
                    Guid.NewGuid(), wfId, now);
                await context.Database.ExecuteSqlRawAsync(
                    "INSERT IGNORE INTO WorkflowStages (Id, WorkflowDefinitionId, Status, DisplayName, Description, AssignedRole, SLAHours, SortOrder, RequiresComment, IsTerminal, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy) " +
                    "VALUES (@p0, @p1, 'LegalApproval', 'Legal Approval', 'Head of Legal countersigning opinion', 'HeadOfLegal', 24, 8, 1, 0, @p2, '', NULL, NULL)",
                    Guid.NewGuid(), wfId, now);

                // Replace HOReview→CommitteeCirculation with HOReview→LegalReview
                await context.Database.ExecuteSqlRawAsync(
                    "DELETE FROM WorkflowTransitions WHERE WorkflowDefinitionId = @p0 AND FromStatus = 'HOReview' AND ToStatus = 'CommitteeCirculation'",
                    wfId);
                await InsertTransitionIfMissingAsync(context, wfId, "HOReview",       "LegalReview",          "Approve", Roles.HOReviewer,    now);
                await InsertTransitionIfMissingAsync(context, wfId, "LegalReview",    "LegalApproval",        "Approve", Roles.LegalOfficer,  now);
                await InsertTransitionIfMissingAsync(context, wfId, "LegalApproval",  "CommitteeCirculation", "Approve", Roles.HeadOfLegal,   now);
                await InsertTransitionIfMissingAsync(context, wfId, "LegalApproval",  "LegalReview",          "Return",  Roles.HeadOfLegal,   now);
                await InsertTransitionIfMissingAsync(context, wfId, "LegalReview",    "HOReview",             "Return",  Roles.LegalOfficer,  now);

                logger.LogInformation("Legal Review workflow stages added successfully.");
            }

            // Upgrade: add Security Perfection + Disbursement maker-checker stages if missing
            if (existing != null && !existing.Stages.Any(s => s.Status == LoanApplicationStatus.SecurityPerfection))
            {
                logger.LogInformation("Upgrading workflow definition: adding Security Perfection and Disbursement stages...");
                var wfId = existing.Id;
                var now = DateTime.UtcNow;

                // Insert new stages (INSERT IGNORE is idempotent)
                await context.Database.ExecuteSqlRawAsync(
                    "INSERT IGNORE INTO WorkflowStages (Id, WorkflowDefinitionId, Status, DisplayName, Description, AssignedRole, SLAHours, SortOrder, RequiresComment, IsTerminal, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy) " +
                    "VALUES (@p0, @p1, 'SecurityPerfection', 'Security Perfection', 'Legal officer perfecting security instruments', 'LegalOfficer', 72, 15, 0, 0, @p2, '', NULL, NULL)",
                    Guid.NewGuid(), wfId, now);
                await context.Database.ExecuteSqlRawAsync(
                    "INSERT IGNORE INTO WorkflowStages (Id, WorkflowDefinitionId, Status, DisplayName, Description, AssignedRole, SLAHours, SortOrder, RequiresComment, IsTerminal, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy) " +
                    "VALUES (@p0, @p1, 'SecurityApproval', 'Security Approval', 'Head of Legal countersigning security perfection', 'HeadOfLegal', 24, 16, 1, 0, @p2, '', NULL, NULL)",
                    Guid.NewGuid(), wfId, now);
                await context.Database.ExecuteSqlRawAsync(
                    "INSERT IGNORE INTO WorkflowStages (Id, WorkflowDefinitionId, Status, DisplayName, Description, AssignedRole, SLAHours, SortOrder, RequiresComment, IsTerminal, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy) " +
                    "VALUES (@p0, @p1, 'DisbursementPending', 'Disbursement Pending', 'Operations preparing disbursement memo', 'Operations', 24, 17, 0, 0, @p2, '', NULL, NULL)",
                    Guid.NewGuid(), wfId, now);
                await context.Database.ExecuteSqlRawAsync(
                    "INSERT IGNORE INTO WorkflowStages (Id, WorkflowDefinitionId, Status, DisplayName, Description, AssignedRole, SLAHours, SortOrder, RequiresComment, IsTerminal, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy) " +
                    "VALUES (@p0, @p1, 'DisbursementBranchApproval', 'Disbursement — Branch Auth', 'Branch Manager authorising disbursement', 'BranchApprover', 24, 18, 1, 0, @p2, '', NULL, NULL)",
                    Guid.NewGuid(), wfId, now);
                await context.Database.ExecuteSqlRawAsync(
                    "INSERT IGNORE INTO WorkflowStages (Id, WorkflowDefinitionId, Status, DisplayName, Description, AssignedRole, SLAHours, SortOrder, RequiresComment, IsTerminal, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy) " +
                    "VALUES (@p0, @p1, 'DisbursementHQApproval', 'Disbursement — HQ Auth', 'GM Finance releasing funds', 'GMFinance', 24, 19, 1, 0, @p2, '', NULL, NULL)",
                    Guid.NewGuid(), wfId, now);

                // Replace OfferAccepted→Disbursed with the full chain
                await context.Database.ExecuteSqlRawAsync(
                    "DELETE FROM WorkflowTransitions WHERE WorkflowDefinitionId = @p0 AND FromStatus = 'OfferAccepted' AND ToStatus = 'Disbursed'",
                    wfId);
                await InsertTransitionIfMissingAsync(context, wfId, "OfferAccepted",             "SecurityPerfection",        "MoveToNextStage", Roles.SystemAdmin,    now);
                await InsertTransitionIfMissingAsync(context, wfId, "SecurityPerfection",        "SecurityApproval",          "Approve",         Roles.LegalOfficer,   now);
                await InsertTransitionIfMissingAsync(context, wfId, "SecurityApproval",          "DisbursementPending",       "Approve",         Roles.HeadOfLegal,    now);
                await InsertTransitionIfMissingAsync(context, wfId, "SecurityApproval",          "SecurityPerfection",        "Return",          Roles.HeadOfLegal,    now);
                await InsertTransitionIfMissingAsync(context, wfId, "DisbursementPending",       "DisbursementBranchApproval","Approve",         Roles.Operations,     now);
                await InsertTransitionIfMissingAsync(context, wfId, "DisbursementBranchApproval","DisbursementHQApproval",    "Approve",         Roles.BranchApprover, now);
                await InsertTransitionIfMissingAsync(context, wfId, "DisbursementBranchApproval","DisbursementPending",       "Return",          Roles.BranchApprover, now);
                await InsertTransitionIfMissingAsync(context, wfId, "DisbursementHQApproval",    "Disbursed",                 "Complete",        Roles.GMFinance,      now);

                logger.LogInformation("Security Perfection and Disbursement stages added successfully.");
            }
            else if (existing != null)
            {
                logger.LogInformation("Workflow definition already current, skipping upgrade.");
            }

            // Correction: Approved→OfferGenerated must be performed by LoanOfficer, not Operations.
            // Idempotent — only updates the row if it currently has the wrong role.
            if (existing != null)
            {
                await context.Database.ExecuteSqlRawAsync(
                    "UPDATE WorkflowTransitions SET RequiredRole = 'LoanOfficer' " +
                    "WHERE WorkflowDefinitionId = @p0 AND FromStatus = 'Approved' AND ToStatus = 'OfferGenerated' AND RequiredRole != 'LoanOfficer'",
                    existing.Id);
            }

            // Reload from DB so the returned entity reflects all raw-SQL changes made above.
            // ChangeTracker.Clear() is required so EF doesn't serve the stale cached instance.
            context.ChangeTracker.Clear();
            return await context.WorkflowDefinitions
                .Include(w => w.Stages)
                .Include(w => w.Transitions)
                .FirstOrDefaultAsync(w => w.ApplicationType == LoanApplicationType.Corporate);
        }

        logger.LogInformation("Seeding corporate loan workflow definition...");
        var workflowResult = WorkflowDefinition.Create(
            "Corporate Loan Workflow",
            "Standard approval workflow for corporate loan applications",
            LoanApplicationType.Corporate);

        if (workflowResult.IsFailure)
        {
            logger.LogError("Failed to create workflow definition: {Error}", workflowResult.Error);
            return null;
        }

        var workflow = workflowResult.Value;

        // Add all stages
        var stages = new[]
        {
            (LoanApplicationStatus.Draft, "Draft", "Application draft", Roles.LoanOfficer, 0, 1, false, false),
            (LoanApplicationStatus.Submitted, "Submitted", "Submitted for review", Roles.LoanOfficer, 24, 2, false, false),
            (LoanApplicationStatus.BranchReview, "Branch Review", "Under branch review", Roles.BranchApprover, 48, 3, true, false),
            (LoanApplicationStatus.BranchApproved, "Branch Approved", "Approved by branch", Roles.CreditOfficer, 24, 4, false, false),
            (LoanApplicationStatus.CreditAnalysis, "Credit Analysis", "Credit analysis in progress", Roles.CreditOfficer, 72, 5, false, false),
            (LoanApplicationStatus.HOReview, "HO Review", "Head Office review", Roles.HOReviewer, 48, 6, true, false),
            (LoanApplicationStatus.LegalReview, "Legal Review", "Legal officer preparing opinion", Roles.LegalOfficer, 48, 7, false, false),
            (LoanApplicationStatus.LegalApproval, "Legal Approval", "Head of Legal countersigning opinion", Roles.HeadOfLegal, 24, 8, true, false),
            (LoanApplicationStatus.CommitteeCirculation, "Committee", "Committee review", Roles.CommitteeMember, 72, 9, false, false),
            (LoanApplicationStatus.CommitteeApproved, "Committee Approved", "Committee decision recorded, pending final sign-off", Roles.SystemAdmin, 0, 10, false, false),
            (LoanApplicationStatus.FinalApproval, "Final Approval", "Awaiting MD/CEO executive sign-off", Roles.FinalApprover, 24, 11, false, false),
            (LoanApplicationStatus.Approved, "Approved", "Final approval granted", Roles.Operations, 24, 12, false, false),
            (LoanApplicationStatus.OfferGenerated, "Offer Letter Issued", "Offer letter issued to customer, awaiting signed acceptance", Roles.LoanOfficer, 72, 13, false, false),
            (LoanApplicationStatus.OfferAccepted, "Offer Accepted", "Customer accepted offer, pending security perfection", Roles.LegalOfficer, 48, 14, false, false),
            (LoanApplicationStatus.SecurityPerfection, "Security Perfection", "Legal officer perfecting security instruments", Roles.LegalOfficer, 72, 15, false, false),
            (LoanApplicationStatus.SecurityApproval, "Security Approval", "Head of Legal countersigning security perfection", Roles.HeadOfLegal, 24, 16, true, false),
            (LoanApplicationStatus.DisbursementPending, "Disbursement Pending", "Operations preparing disbursement memo", Roles.Operations, 24, 17, false, false),
            (LoanApplicationStatus.DisbursementBranchApproval, "Disbursement — Branch Auth", "Branch Manager authorising disbursement", Roles.BranchApprover, 24, 18, true, false),
            (LoanApplicationStatus.DisbursementHQApproval, "Disbursement — HQ Auth", "GM Finance releasing funds", Roles.GMFinance, 24, 19, true, false),
            (LoanApplicationStatus.Disbursed, "Disbursed", "Loan disbursed", Roles.Operations, 0, 20, false, true),
            (LoanApplicationStatus.Rejected, "Rejected", "Application rejected", Roles.LoanOfficer, 0, 21, false, true)
        };

        foreach (var (status, name, desc, role, sla, order, requiresComment, isTerminal) in stages)
        {
            workflow.AddStage(status, name, desc, role, sla, order, requiresComment, isTerminal);
        }

        // Add transitions
        var transitions = new[]
        {
            (LoanApplicationStatus.Draft, LoanApplicationStatus.Submitted, WorkflowAction.Submit, Roles.LoanOfficer),
            (LoanApplicationStatus.Submitted, LoanApplicationStatus.BranchReview, WorkflowAction.MoveToNextStage, Roles.LoanOfficer),
            (LoanApplicationStatus.BranchReview, LoanApplicationStatus.BranchApproved, WorkflowAction.Approve, Roles.BranchApprover),
            (LoanApplicationStatus.BranchReview, LoanApplicationStatus.Rejected, WorkflowAction.Reject, Roles.BranchApprover),
            (LoanApplicationStatus.BranchApproved, LoanApplicationStatus.CreditAnalysis, WorkflowAction.MoveToNextStage, Roles.SystemAdmin),
            (LoanApplicationStatus.CreditAnalysis, LoanApplicationStatus.HOReview, WorkflowAction.Approve, Roles.CreditOfficer),
            (LoanApplicationStatus.CreditAnalysis, LoanApplicationStatus.BranchReview, WorkflowAction.Return, Roles.CreditOfficer),
            (LoanApplicationStatus.HOReview, LoanApplicationStatus.LegalReview, WorkflowAction.Approve, Roles.HOReviewer),
            (LoanApplicationStatus.HOReview, LoanApplicationStatus.CreditAnalysis, WorkflowAction.Return, Roles.HOReviewer),
            (LoanApplicationStatus.HOReview, LoanApplicationStatus.Rejected, WorkflowAction.Reject, Roles.HOReviewer),
            (LoanApplicationStatus.LegalReview, LoanApplicationStatus.LegalApproval, WorkflowAction.Approve, Roles.LegalOfficer),
            (LoanApplicationStatus.LegalReview, LoanApplicationStatus.HOReview, WorkflowAction.Return, Roles.LegalOfficer),
            (LoanApplicationStatus.LegalApproval, LoanApplicationStatus.CommitteeCirculation, WorkflowAction.Approve, Roles.HeadOfLegal),
            (LoanApplicationStatus.LegalApproval, LoanApplicationStatus.LegalReview, WorkflowAction.Return, Roles.HeadOfLegal),
            (LoanApplicationStatus.CommitteeCirculation, LoanApplicationStatus.CommitteeApproved, WorkflowAction.MoveToNextStage, Roles.SystemAdmin),
            (LoanApplicationStatus.CommitteeCirculation, LoanApplicationStatus.Rejected, WorkflowAction.Reject, Roles.SystemAdmin),
            (LoanApplicationStatus.CommitteeApproved, LoanApplicationStatus.FinalApproval, WorkflowAction.MoveToNextStage, Roles.SystemAdmin),
            (LoanApplicationStatus.FinalApproval, LoanApplicationStatus.Approved, WorkflowAction.Approve, Roles.FinalApprover),
            (LoanApplicationStatus.FinalApproval, LoanApplicationStatus.Rejected, WorkflowAction.Reject, Roles.FinalApprover),
            (LoanApplicationStatus.Approved, LoanApplicationStatus.OfferGenerated, WorkflowAction.MoveToNextStage, Roles.LoanOfficer),
            (LoanApplicationStatus.OfferGenerated, LoanApplicationStatus.OfferAccepted, WorkflowAction.MoveToNextStage, Roles.Operations),
            (LoanApplicationStatus.OfferAccepted, LoanApplicationStatus.SecurityPerfection, WorkflowAction.MoveToNextStage, Roles.SystemAdmin),
            (LoanApplicationStatus.SecurityPerfection, LoanApplicationStatus.SecurityApproval, WorkflowAction.Approve, Roles.LegalOfficer),
            (LoanApplicationStatus.SecurityApproval, LoanApplicationStatus.DisbursementPending, WorkflowAction.Approve, Roles.HeadOfLegal),
            (LoanApplicationStatus.SecurityApproval, LoanApplicationStatus.SecurityPerfection, WorkflowAction.Return, Roles.HeadOfLegal),
            (LoanApplicationStatus.DisbursementPending, LoanApplicationStatus.DisbursementBranchApproval, WorkflowAction.Approve, Roles.Operations),
            (LoanApplicationStatus.DisbursementBranchApproval, LoanApplicationStatus.DisbursementHQApproval, WorkflowAction.Approve, Roles.BranchApprover),
            (LoanApplicationStatus.DisbursementBranchApproval, LoanApplicationStatus.DisbursementPending, WorkflowAction.Return, Roles.BranchApprover),
            (LoanApplicationStatus.DisbursementHQApproval, LoanApplicationStatus.Disbursed, WorkflowAction.Complete, Roles.GMFinance)
        };

        foreach (var (from, to, action, role) in transitions)
        {
            workflow.AddTransition(from, to, action, role);
        }

        await context.WorkflowDefinitions.AddAsync(workflow);
        await context.SaveChangesAsync();
        logger.LogInformation("Workflow definition seeded successfully");
        return workflow;
    }

    private static async Task SeedNotificationTemplatesAsync(CRMSDbContext context, ILogger logger, Guid createdByUserId)
    {
        if (await context.NotificationTemplates.AnyAsync())
        {
            logger.LogInformation("Notification templates already exist, skipping...");
            return;
        }

        logger.LogInformation("Seeding notification templates...");
        var templates = new[]
        {
            ("APP_SUBMITTED", "Application Submitted", NotificationType.ApplicationSubmitted, NotificationChannel.Email, 
                "Application {{ApplicationNumber}} Submitted", 
                "Dear {{RecipientName}},\n\nYour loan application {{ApplicationNumber}} for {{CustomerName}} has been submitted successfully.\n\nAmount: {{Amount}}\nProduct: {{ProductName}}\n\nRegards,\nCRMS Team"),
            ("APP_APPROVED", "Application Approved", NotificationType.ApplicationApproved, NotificationChannel.Email,
                "Application {{ApplicationNumber}} Approved",
                "Dear {{RecipientName}},\n\nCongratulations! Loan application {{ApplicationNumber}} has been approved.\n\nApproved Amount: {{ApprovedAmount}}\n\nRegards,\nCRMS Team"),
            ("APP_REJECTED", "Application Rejected", NotificationType.ApplicationRejected, NotificationChannel.Email,
                "Application {{ApplicationNumber}} Rejected",
                "Dear {{RecipientName}},\n\nWe regret to inform you that loan application {{ApplicationNumber}} has been rejected.\n\nReason: {{RejectionReason}}\n\nRegards,\nCRMS Team"),
            ("WF_ASSIGNED", "Workflow Assigned", NotificationType.WorkflowAssigned, NotificationChannel.Email,
                "New Task Assigned - {{ApplicationNumber}}",
                "Dear {{RecipientName}},\n\nA new task has been assigned to you.\n\nApplication: {{ApplicationNumber}}\nStage: {{StageName}}\nDue: {{DueDate}}\n\nPlease review at your earliest convenience.\n\nRegards,\nCRMS Team"),
            ("COMMITTEE_VOTE", "Committee Vote Required", NotificationType.CommitteeVoteRequired, NotificationChannel.Email,
                "Vote Required - {{ApplicationNumber}}",
                "Dear {{RecipientName}},\n\nYour vote is required for loan application {{ApplicationNumber}}.\n\nCustomer: {{CustomerName}}\nAmount: {{Amount}}\nDeadline: {{Deadline}}\n\nPlease cast your vote before the deadline.\n\nRegards,\nCRMS Team"),
            ("SLA_WARNING", "SLA Warning", NotificationType.WorkflowSLAWarning, NotificationChannel.Email,
                "SLA Warning - {{ApplicationNumber}}",
                "Dear {{RecipientName}},\n\nApplication {{ApplicationNumber}} is approaching SLA breach.\n\nCurrent Stage: {{StageName}}\nTime Remaining: {{TimeRemaining}}\n\nPlease take action immediately.\n\nRegards,\nCRMS Team"),
            ("APP_SUBMITTED_SMS", "Application Submitted SMS", NotificationType.ApplicationSubmitted, NotificationChannel.SMS,
                "",
                "CRMS: Your loan application {{ApplicationNumber}} has been submitted successfully. Amount: {{Amount}}"),
            ("APP_APPROVED_SMS", "Application Approved SMS", NotificationType.ApplicationApproved, NotificationChannel.SMS,
                "",
                "CRMS: Congratulations! Your loan application {{ApplicationNumber}} has been APPROVED. Amount: {{ApprovedAmount}}")
        };

        foreach (var (code, name, type, channel, subject, body) in templates)
        {
            var templateResult = NotificationTemplate.Create(
                code, name, $"Template for {name}", type, channel, body, createdByUserId, 
                channel == NotificationChannel.Email ? subject : null);
            
            if (templateResult.IsSuccess)
            {
                await context.NotificationTemplates.AddAsync(templateResult.Value);
            }
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Notification templates seeded successfully");
    }

    private static async Task SeedScoringParametersAsync(CRMSDbContext context, ILogger logger, Guid createdByUserId)
    {
        if (await context.ScoringParameters.AnyAsync())
        {
            logger.LogInformation("Scoring parameters already exist, skipping...");
            return;
        }

        logger.LogInformation("Seeding comprehensive scoring parameters (all categories)...");
        
        // All parameters: (Category, Key, DisplayName, Description, DataType, Value, MinValue, MaxValue, SortOrder)
        var allParameters = new List<(string Category, string Key, string DisplayName, string Description, ParameterDataType DataType, decimal Value, decimal? Min, decimal? Max, int Sort)>
        {
            // ══════════════════════════════════════════════════════════════════════════════
            // WEIGHTS - Category weights (must sum to 1.0)
            // ══════════════════════════════════════════════════════════════════════════════
            ("Weights", "CreditHistory", "Credit History Weight", "Weight for credit history scoring category", ParameterDataType.Percentage, 0.25m, 0m, 1m, 1),
            ("Weights", "FinancialHealth", "Financial Health Weight", "Weight for financial health scoring category", ParameterDataType.Percentage, 0.25m, 0m, 1m, 2),
            ("Weights", "CashflowStability", "Cashflow Stability Weight", "Weight for cashflow stability scoring category", ParameterDataType.Percentage, 0.15m, 0m, 1m, 3),
            ("Weights", "DebtServiceCapacity", "Debt Service Capacity Weight", "Weight for debt service capacity scoring category", ParameterDataType.Percentage, 0.20m, 0m, 1m, 4),
            ("Weights", "CollateralCoverage", "Collateral Coverage Weight", "Weight for collateral coverage scoring category", ParameterDataType.Percentage, 0.15m, 0m, 1m, 5),

            // ══════════════════════════════════════════════════════════════════════════════
            // CREDIT HISTORY - Bureau data scoring
            // ══════════════════════════════════════════════════════════════════════════════
            ("CreditHistory", "BaseScore", "Base Score", "Starting score before adjustments", ParameterDataType.Score, 70m, 0m, 100m, 1),
            // Credit score thresholds
            ("CreditHistory", "ExcellentCreditScoreThreshold", "Excellent Credit Score Threshold", "Minimum score to qualify as excellent credit", ParameterDataType.Integer, 700m, 300m, 850m, 2),
            ("CreditHistory", "GoodCreditScoreThreshold", "Good Credit Score Threshold", "Minimum score to qualify as good credit", ParameterDataType.Integer, 650m, 300m, 850m, 3),
            ("CreditHistory", "PoorCreditScoreThreshold", "Poor Credit Score Threshold", "Scores below this are considered poor", ParameterDataType.Integer, 600m, 300m, 850m, 4),
            // Score adjustments (bonuses)
            ("CreditHistory", "ExcellentCreditScoreBonus", "Excellent Credit Score Bonus", "Points added for excellent credit score (>=700)", ParameterDataType.Score, 20m, 0m, 50m, 5),
            ("CreditHistory", "GoodCreditScoreBonus", "Good Credit Score Bonus", "Points added for good credit score (>=650)", ParameterDataType.Score, 10m, 0m, 50m, 6),
            ("CreditHistory", "PerformingLoansBonus", "Performing Loans Bonus", "Points added for 3+ performing loans on record", ParameterDataType.Score, 5m, 0m, 20m, 7),
            ("CreditHistory", "MinPerformingLoansForBonus", "Min Performing Loans for Bonus", "Number of performing loans required for bonus", ParameterDataType.Integer, 3m, 1m, 10m, 8),
            // Score adjustments (penalties)
            ("CreditHistory", "PoorCreditScorePenalty", "Poor Credit Score Penalty", "Points deducted for poor credit score (<600)", ParameterDataType.Score, 20m, 0m, 50m, 9),
            ("CreditHistory", "DefaultPenalty", "Default Penalty", "Points deducted for any defaulted loans on record", ParameterDataType.Score, 30m, 0m, 50m, 10),
            ("CreditHistory", "DelinquencyPenalty", "Delinquency Penalty", "Points deducted for any delinquent loans on record", ParameterDataType.Score, 15m, 0m, 50m, 11),
            ("CreditHistory", "LegalActionsPenalty", "Legal Actions Penalty", "Points deducted for legal actions on record", ParameterDataType.Score, 20m, 0m, 50m, 12),
            // Delinquency thresholds
            ("CreditHistory", "SevereDelinquencyDaysThreshold", "Severe Delinquency Days Threshold", "Days overdue to trigger severe delinquency penalty", ParameterDataType.Integer, 90m, 30m, 180m, 13),
            ("CreditHistory", "SevereDelinquencyPenalty", "Severe Delinquency Penalty", "Points deducted for severe delinquency (>=90 days)", ParameterDataType.Score, 15m, 0m, 50m, 14),
            ("CreditHistory", "WatchListDaysThreshold", "Watch List Days Threshold", "Days overdue to trigger watch list penalty", ParameterDataType.Integer, 30m, 7m, 90m, 15),
            ("CreditHistory", "WatchListPenalty", "Watch List Penalty", "Points deducted for watch list status (>=30 days)", ParameterDataType.Score, 8m, 0m, 30m, 16),
            // Fraud risk thresholds
            ("CreditHistory", "HighFraudRiskScoreThreshold", "High Fraud Risk Score Threshold", "Bureau fraud score threshold for high risk", ParameterDataType.Integer, 70m, 50m, 100m, 17),
            ("CreditHistory", "HighFraudRiskPenalty", "High Fraud Risk Penalty", "Points deducted for high fraud risk (>=70)", ParameterDataType.Score, 25m, 0m, 50m, 18),
            ("CreditHistory", "ElevatedFraudRiskScoreThreshold", "Elevated Fraud Risk Score Threshold", "Bureau fraud score threshold for elevated risk", ParameterDataType.Integer, 50m, 30m, 70m, 19),
            ("CreditHistory", "ElevatedFraudRiskPenalty", "Elevated Fraud Risk Penalty", "Points deducted for elevated fraud risk (>=50)", ParameterDataType.Score, 10m, 0m, 30m, 20),
            // Missing data
            ("CreditHistory", "MissingBureauDataPenaltyPerParty", "Missing Bureau Data Penalty (Per Party)", "Points deducted per party with missing bureau data", ParameterDataType.Score, 5m, 0m, 20m, 21),

            // ══════════════════════════════════════════════════════════════════════════════
            // FINANCIAL HEALTH - Balance sheet and profitability
            // ══════════════════════════════════════════════════════════════════════════════
            ("FinancialHealth", "BaseScore", "Base Score", "Starting score before adjustments", ParameterDataType.Score, 60m, 0m, 100m, 1),
            // Liquidity thresholds
            ("FinancialHealth", "StrongCurrentRatio", "Strong Current Ratio", "Current ratio threshold for strong liquidity bonus", ParameterDataType.Decimal, 2.0m, 1m, 5m, 2),
            ("FinancialHealth", "WeakCurrentRatio", "Weak Current Ratio", "Current ratio below this triggers liquidity penalty", ParameterDataType.Decimal, 1.0m, 0.5m, 2m, 3),
            ("FinancialHealth", "StrongCurrentRatioBonus", "Strong Current Ratio Bonus", "Points added for strong liquidity (CR>=2.0)", ParameterDataType.Score, 10m, 0m, 30m, 4),
            ("FinancialHealth", "WeakCurrentRatioPenalty", "Weak Current Ratio Penalty", "Points deducted for weak liquidity (CR<1.0)", ParameterDataType.Score, 15m, 0m, 30m, 5),
            // Leverage thresholds
            ("FinancialHealth", "ConservativeDebtToEquity", "Conservative Debt-to-Equity", "D/E ratio threshold for conservative leverage bonus", ParameterDataType.Decimal, 1.0m, 0.5m, 2m, 6),
            ("FinancialHealth", "HighDebtToEquity", "High Debt-to-Equity", "D/E ratio above this triggers high leverage penalty", ParameterDataType.Decimal, 3.0m, 2m, 5m, 7),
            ("FinancialHealth", "ConservativeLeverageBonus", "Conservative Leverage Bonus", "Points added for conservative leverage (D/E<=1.0)", ParameterDataType.Score, 10m, 0m, 30m, 8),
            ("FinancialHealth", "HighLeveragePenalty", "High Leverage Penalty", "Points deducted for high leverage (D/E>3.0)", ParameterDataType.Score, 20m, 0m, 40m, 9),
            // Profitability thresholds
            ("FinancialHealth", "StrongNetMarginPercent", "Strong Net Margin %", "Net profit margin threshold for strong profitability bonus", ParameterDataType.Percentage, 10m, 5m, 30m, 10),
            ("FinancialHealth", "StrongNetMarginBonus", "Strong Net Margin Bonus", "Points added for strong profitability (NPM>=10%)", ParameterDataType.Score, 15m, 0m, 30m, 11),
            ("FinancialHealth", "LossMakingPenalty", "Loss-Making Penalty", "Points deducted if company is loss-making", ParameterDataType.Score, 25m, 0m, 50m, 12),
            // ROE
            ("FinancialHealth", "StrongROE", "Strong ROE %", "Return on Equity threshold for bonus", ParameterDataType.Percentage, 15m, 5m, 50m, 13),
            ("FinancialHealth", "StrongROEBonus", "Strong ROE Bonus", "Points added for strong ROE (>=15%)", ParameterDataType.Score, 10m, 0m, 30m, 14),

            // ══════════════════════════════════════════════════════════════════════════════
            // CASHFLOW - Bank statement analysis
            // ══════════════════════════════════════════════════════════════════════════════
            ("Cashflow", "BaseScore", "Base Score", "Starting score before adjustments", ParameterDataType.Score, 60m, 0m, 100m, 1),
            // Statement source adjustments
            ("Cashflow", "InternalStatementBonus", "Internal Statement Bonus", "Points added when own-bank statement is provided", ParameterDataType.Score, 10m, 0m, 30m, 2),
            ("Cashflow", "MissingInternalPenalty", "Missing Internal Statement Penalty", "Points deducted when own-bank statement is missing", ParameterDataType.Score, 15m, 0m, 30m, 3),
            ("Cashflow", "VerifiedExternalBonus", "Verified External Statement Bonus", "Points added for verified external bank statements", ParameterDataType.Score, 5m, 0m, 20m, 4),
            // Cashflow metrics
            ("Cashflow", "PositiveCashflowBonus", "Positive Cashflow Bonus", "Points added for positive net monthly cashflow", ParameterDataType.Score, 15m, 0m, 30m, 5),
            ("Cashflow", "NegativeCashflowPenalty", "Negative Cashflow Penalty", "Points deducted for negative net monthly cashflow", ParameterDataType.Score, 20m, 0m, 40m, 6),
            // Volatility thresholds
            ("Cashflow", "LowVolatilityThreshold", "Low Volatility Threshold", "Cashflow volatility below this is considered stable", ParameterDataType.Percentage, 30m, 10m, 50m, 7),
            ("Cashflow", "HighVolatilityThreshold", "High Volatility Threshold", "Cashflow volatility above this is considered unstable", ParameterDataType.Percentage, 50m, 30m, 80m, 8),
            ("Cashflow", "LowVolatilityBonus", "Low Volatility Bonus", "Points added for stable cashflow (volatility<30%)", ParameterDataType.Score, 10m, 0m, 30m, 9),
            ("Cashflow", "HighVolatilityPenalty", "High Volatility Penalty", "Points deducted for unstable cashflow (volatility>50%)", ParameterDataType.Score, 10m, 0m, 30m, 10),
            // Risk indicators
            ("Cashflow", "GamblingPenalty", "Gambling Transactions Penalty", "Points deducted for gambling activity detected", ParameterDataType.Score, 15m, 0m, 40m, 11),
            ("Cashflow", "BouncedTransactionPenalty", "Bounced Transaction Penalty", "Points deducted for bounced/failed transactions", ParameterDataType.Score, 20m, 0m, 40m, 12),
            ("Cashflow", "HighNegativeBalanceDaysThreshold", "High Negative Balance Days Threshold", "Days with negative balance to trigger high penalty", ParameterDataType.Integer, 10m, 5m, 30m, 13),
            ("Cashflow", "ModerateNegativeBalanceDaysThreshold", "Moderate Negative Balance Days Threshold", "Days with negative balance to trigger moderate penalty", ParameterDataType.Integer, 5m, 1m, 15m, 14),
            ("Cashflow", "HighNegativeBalancePenalty", "High Negative Balance Penalty", "Points deducted for frequent negative balance (>10 days)", ParameterDataType.Score, 15m, 0m, 30m, 15),
            ("Cashflow", "ModerateNegativeBalancePenalty", "Moderate Negative Balance Penalty", "Points deducted for some negative balance (>5 days)", ParameterDataType.Score, 5m, 0m, 20m, 16),
            // Period coverage
            ("Cashflow", "MinimumMonthsRequired", "Minimum Months Required", "Minimum months of statement history required", ParameterDataType.Integer, 6m, 3m, 12m, 17),
            ("Cashflow", "IdealMonthsCoverage", "Ideal Months Coverage", "Ideal months of statement history for bonus", ParameterDataType.Integer, 12m, 6m, 24m, 18),
            ("Cashflow", "InsufficientCoveragePenalty", "Insufficient Coverage Penalty", "Points deducted for less than minimum months", ParameterDataType.Score, 10m, 0m, 30m, 19),
            ("Cashflow", "IdealCoverageBonus", "Ideal Coverage Bonus", "Points added for ideal statement coverage", ParameterDataType.Score, 5m, 0m, 20m, 20),

            // ══════════════════════════════════════════════════════════════════════════════
            // DSCR - Debt Service Coverage Ratio
            // ══════════════════════════════════════════════════════════════════════════════
            ("DSCR", "ExcellentDSCR", "Excellent DSCR", "DSCR threshold for excellent rating", ParameterDataType.Decimal, 2.0m, 1.5m, 5m, 1),
            ("DSCR", "GoodDSCR", "Good DSCR", "DSCR threshold for good rating", ParameterDataType.Decimal, 1.5m, 1.25m, 3m, 2),
            ("DSCR", "AdequateDSCR", "Adequate DSCR", "DSCR threshold for adequate rating", ParameterDataType.Decimal, 1.25m, 1m, 2m, 3),
            ("DSCR", "MinimumDSCR", "Minimum DSCR", "Minimum acceptable DSCR for approval", ParameterDataType.Decimal, 1.0m, 0.8m, 1.5m, 4),
            // DSCR scores
            ("DSCR", "ExcellentDSCRScore", "Excellent DSCR Score", "Score awarded for excellent DSCR (>=2.0)", ParameterDataType.Score, 90m, 70m, 100m, 5),
            ("DSCR", "GoodDSCRScore", "Good DSCR Score", "Score awarded for good DSCR (>=1.5)", ParameterDataType.Score, 75m, 60m, 90m, 6),
            ("DSCR", "AdequateDSCRScore", "Adequate DSCR Score", "Score awarded for adequate DSCR (>=1.25)", ParameterDataType.Score, 60m, 40m, 80m, 7),
            ("DSCR", "MinimumDSCRScore", "Minimum DSCR Score", "Score awarded for minimum DSCR (>=1.0)", ParameterDataType.Score, 45m, 30m, 60m, 8),
            ("DSCR", "BelowMinimumDSCRScore", "Below Minimum DSCR Score", "Score awarded for DSCR below minimum (<1.0)", ParameterDataType.Score, 25m, 0m, 40m, 9),
            // Interest coverage
            ("DSCR", "StrongInterestCoverage", "Strong Interest Coverage", "Interest coverage ratio for strong rating", ParameterDataType.Decimal, 5.0m, 3m, 10m, 10),
            ("DSCR", "WeakInterestCoverage", "Weak Interest Coverage", "Interest coverage ratio below this is weak", ParameterDataType.Decimal, 2.0m, 1m, 3m, 11),
            ("DSCR", "StrongInterestCoverageBonus", "Strong Interest Coverage Bonus", "Points added for strong interest coverage (>=5x)", ParameterDataType.Score, 5m, 0m, 20m, 12),
            ("DSCR", "WeakInterestCoveragePenalty", "Weak Interest Coverage Penalty", "Points deducted for weak interest coverage (<2x)", ParameterDataType.Score, 10m, 0m, 30m, 13),

            // ══════════════════════════════════════════════════════════════════════════════
            // COLLATERAL - Loan-to-Value and lien status
            // ══════════════════════════════════════════════════════════════════════════════
            ("Collateral", "ExcellentLTV", "Excellent LTV %", "LTV threshold for excellent collateral coverage", ParameterDataType.Percentage, 50m, 30m, 70m, 1),
            ("Collateral", "GoodLTV", "Good LTV %", "LTV threshold for good collateral coverage", ParameterDataType.Percentage, 70m, 50m, 90m, 2),
            ("Collateral", "AdequateLTV", "Adequate LTV %", "LTV threshold for adequate collateral coverage", ParameterDataType.Percentage, 100m, 80m, 120m, 3),
            // LTV scores
            ("Collateral", "ExcellentLTVScore", "Excellent LTV Score", "Score awarded for excellent LTV (<=50%)", ParameterDataType.Score, 90m, 70m, 100m, 4),
            ("Collateral", "GoodLTVScore", "Good LTV Score", "Score awarded for good LTV (<=70%)", ParameterDataType.Score, 75m, 60m, 90m, 5),
            ("Collateral", "AdequateLTVScore", "Adequate LTV Score", "Score awarded for adequate LTV (<=100%)", ParameterDataType.Score, 55m, 40m, 70m, 6),
            ("Collateral", "UnderCollateralizedScore", "Under-Collateralized Score", "Score awarded when LTV exceeds 100%", ParameterDataType.Score, 35m, 0m, 50m, 7),
            // Lien status
            ("Collateral", "PerfectedLienBonus", "Perfected Lien Bonus", "Points added when all liens are perfected", ParameterDataType.Score, 5m, 0m, 20m, 8),
            ("Collateral", "UnperfectedLienPenalty", "Unperfected Lien Penalty", "Points deducted when liens are not perfected", ParameterDataType.Score, 10m, 0m, 30m, 9),

            // ══════════════════════════════════════════════════════════════════════════════
            // RECOMMENDATIONS - Decision thresholds
            // ══════════════════════════════════════════════════════════════════════════════
            ("Recommendations", "StrongApproveMinScore", "Strong Approve Min Score", "Minimum score for strong approval recommendation", ParameterDataType.Score, 75m, 60m, 90m, 1),
            ("Recommendations", "StrongApproveMaxRedFlags", "Strong Approve Max Red Flags", "Maximum red flags allowed for strong approval", ParameterDataType.Integer, 0m, 0m, 2m, 2),
            ("Recommendations", "ApproveMinScore", "Approve Min Score", "Minimum score for approval recommendation", ParameterDataType.Score, 65m, 50m, 80m, 3),
            ("Recommendations", "ApproveMaxRedFlags", "Approve Max Red Flags", "Maximum red flags allowed for approval", ParameterDataType.Integer, 1m, 0m, 3m, 4),
            ("Recommendations", "ApproveWithConditionsMinScore", "Approve With Conditions Min Score", "Minimum score for conditional approval", ParameterDataType.Score, 50m, 40m, 70m, 5),
            ("Recommendations", "ReferMinScore", "Refer Min Score", "Minimum score for refer recommendation (below this = decline)", ParameterDataType.Score, 35m, 20m, 50m, 6),
            ("Recommendations", "CriticalRedFlagsThreshold", "Critical Red Flags Threshold", "Number of red flags that triggers automatic decline", ParameterDataType.Integer, 3m, 2m, 5m, 7),

            // ══════════════════════════════════════════════════════════════════════════════
            // LOAN ADJUSTMENTS - Amount and rate modifications
            // ══════════════════════════════════════════════════════════════════════════════
            ("LoanAdjustments", "Score80PlusMultiplier", "Score 80+ Amount Multiplier", "Loan amount multiplier for scores >= 80", ParameterDataType.Decimal, 1.0m, 0.8m, 1.2m, 1),
            ("LoanAdjustments", "Score70PlusMultiplier", "Score 70+ Amount Multiplier", "Loan amount multiplier for scores >= 70", ParameterDataType.Decimal, 0.9m, 0.7m, 1.1m, 2),
            ("LoanAdjustments", "Score60PlusMultiplier", "Score 60+ Amount Multiplier", "Loan amount multiplier for scores >= 60", ParameterDataType.Decimal, 0.75m, 0.5m, 1.0m, 3),
            ("LoanAdjustments", "Score50PlusMultiplier", "Score 50+ Amount Multiplier", "Loan amount multiplier for scores >= 50", ParameterDataType.Decimal, 0.6m, 0.4m, 0.9m, 4),
            ("LoanAdjustments", "BelowScore50Multiplier", "Below Score 50 Amount Multiplier", "Loan amount multiplier for scores < 50", ParameterDataType.Decimal, 0.5m, 0.3m, 0.7m, 5),
            // Interest rate adjustments
            ("LoanAdjustments", "BaseInterestRate", "Base Interest Rate %", "Base annual interest rate before adjustments", ParameterDataType.Percentage, 18.0m, 10m, 30m, 6),
            ("LoanAdjustments", "Score80PlusRateAdjustment", "Score 80+ Rate Adjustment", "Interest rate adjustment for scores >= 80 (negative = discount)", ParameterDataType.Decimal, -2.0m, -5m, 0m, 7),
            ("LoanAdjustments", "Score70PlusRateAdjustment", "Score 70+ Rate Adjustment", "Interest rate adjustment for scores >= 70", ParameterDataType.Decimal, -1.0m, -3m, 0m, 8),
            ("LoanAdjustments", "Score60PlusRateAdjustment", "Score 60+ Rate Adjustment", "Interest rate adjustment for scores >= 60", ParameterDataType.Decimal, 0m, -2m, 2m, 9),
            ("LoanAdjustments", "Score50PlusRateAdjustment", "Score 50+ Rate Adjustment", "Interest rate adjustment for scores >= 50", ParameterDataType.Decimal, 2.0m, 0m, 5m, 10),
            ("LoanAdjustments", "BelowScore50RateAdjustment", "Below Score 50 Rate Adjustment", "Interest rate adjustment for scores < 50", ParameterDataType.Decimal, 4.0m, 2m, 8m, 11),
            // Tenor restrictions
            ("LoanAdjustments", "MaxTenorForLowScores", "Max Tenor for Low Scores (months)", "Maximum loan tenor allowed for low-scoring applications", ParameterDataType.Integer, 36m, 12m, 60m, 12),
            ("LoanAdjustments", "LowScoreThresholdForTenorRestriction", "Low Score Threshold for Tenor Restriction", "Score below which tenor restriction applies", ParameterDataType.Score, 70m, 50m, 80m, 13),

            // ══════════════════════════════════════════════════════════════════════════════
            // STATEMENT TRUST - Trust weights for different statement sources
            // ══════════════════════════════════════════════════════════════════════════════
            ("StatementTrust", "CoreBanking", "Core Banking Trust Weight", "Trust weight for statements from own core banking system", ParameterDataType.Decimal, 1.0m, 0.8m, 1m, 1),
            ("StatementTrust", "OpenBanking", "Open Banking Trust Weight", "Trust weight for statements via Open Banking API", ParameterDataType.Decimal, 0.95m, 0.7m, 1m, 2),
            ("StatementTrust", "MonoConnect", "Mono Connect Trust Weight", "Trust weight for statements from Mono Connect", ParameterDataType.Decimal, 0.90m, 0.7m, 1m, 3),
            ("StatementTrust", "ManualUploadVerified", "Manual Upload (Verified) Trust Weight", "Trust weight for manually uploaded and verified statements", ParameterDataType.Decimal, 0.85m, 0.6m, 1m, 4),
            ("StatementTrust", "ManualUploadPending", "Manual Upload (Pending) Trust Weight", "Trust weight for manually uploaded statements pending verification", ParameterDataType.Decimal, 0.70m, 0.4m, 0.9m, 5),
        };

        int paramCount = 0;
        foreach (var (category, key, displayName, description, dataType, value, min, max, sortOrder) in allParameters)
        {
            var param = ScoringParameter.Create(category, key, displayName, description, dataType, value, createdByUserId, min, max, sortOrder);
            if (param.IsSuccess)
            {
                await context.ScoringParameters.AddAsync(param.Value);
                
                // Add ScoringParameterHistory for creation
                var history = ScoringParameterHistory.RecordCreation(
                    param.Value.Id, category, key, value, createdByUserId);
                await context.ScoringParameterHistory.AddAsync(history);
                paramCount++;
            }
            else
            {
                logger.LogWarning("Failed to create parameter {Category}.{Key}: {Error}", category, key, param.Error);
            }
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Scoring parameters seeded successfully ({Count} parameters across 9 categories)", paramCount);
    }

    private static async Task SeedLoanApplicationsAsync(
        CRMSDbContext context, ILogger logger,
        (ApplicationUser SystemAdmin, ApplicationUser LoanOfficer, ApplicationUser BranchApprover, 
         ApplicationUser CreditOfficer, ApplicationUser HOReviewer, ApplicationUser LegalOfficer, ApplicationUser HeadOfLegal,
         ApplicationUser CommitteeMember1, ApplicationUser CommitteeMember2, ApplicationUser CommitteeMember3,
         ApplicationUser FinalApprover, ApplicationUser Operations, ApplicationUser GMFinance, ApplicationUser RiskManager, ApplicationUser Auditor) users,
        List<LoanProduct> products,
        WorkflowDefinition? workflowDef)
    {
        if (await context.LoanApplications.AnyAsync())
        {
            logger.LogInformation("Loan applications already exist, skipping...");
            return;
        }

        logger.LogInformation("Seeding loan applications at various stages...");
        var corporateProduct = products.FirstOrDefault(p => p.Code == "CORP-TERM-001") ?? products.First();

        // Create applications at ALL workflow stages for comprehensive coverage
        var scenarios = new[]
        {
            // Initial stages
            (NigerianCompanies[0], 500_000_000m, LoanApplicationStatus.Draft, "Draft - Initial application being prepared"),
            (NigerianCompanies[1], 250_000_000m, LoanApplicationStatus.Submitted, "Submitted - Awaiting data gathering"),
            (NigerianCompanies[2], 180_000_000m, LoanApplicationStatus.DataGathering, "Data Gathering - Collecting documents"),
            
            // Branch level
            (NigerianCompanies[3], 1_000_000_000m, LoanApplicationStatus.BranchReview, "Branch Review - Under assessment"),
            (NigerianCompanies[4], 750_000_000m, LoanApplicationStatus.BranchApproved, "Branch Approved - Pending credit analysis"),
            (NigerianCompanies[5], 150_000_000m, LoanApplicationStatus.BranchReturned, "Branch Returned - Needs more information"),
            (NigerianCompanies[6], 100_000_000m, LoanApplicationStatus.BranchRejected, "Branch Rejected - Did not meet criteria"),
            
            // Credit and HO level
            (NigerianCompanies[7], 300_000_000m, LoanApplicationStatus.CreditAnalysis, "Credit Analysis - Bureau checks and scoring"),
            (NigerianCompanies[8], 2_000_000_000m, LoanApplicationStatus.HOReview, "HO Review - Regional review in progress"),
            (NigerianCompanies[9], 1_200_000_000m, LoanApplicationStatus.LegalReview, "Legal Review - Legal officer preparing opinion"),
            (NigerianCompanies[10], 950_000_000m, LoanApplicationStatus.LegalApproval, "Legal Approval - Awaiting Head of Legal countersignature"),

            // Committee level
            (NigerianCompanies[11], 1_500_000_000m, LoanApplicationStatus.CommitteeCirculation, "Committee Circulation - Voting in progress"),
            (NigerianCompanies[12], 800_000_000m, LoanApplicationStatus.CommitteeApproved, "Committee Approved - Awaiting final approval"),
            (NigerianCompanies[13], 120_000_000m, LoanApplicationStatus.CommitteeRejected, "Committee Rejected - Insufficient collateral"),
            
            // Final stages
            (NigerianCompanies[0], 450_000_000m, LoanApplicationStatus.FinalApproval, "Final Approval - MD sign-off pending"),
            (NigerianCompanies[1], 400_000_000m, LoanApplicationStatus.Approved, "Approved - Ready for offer generation"),
            (NigerianCompanies[2], 350_000_000m, LoanApplicationStatus.OfferGenerated, "Offer Generated - Awaiting acceptance"),
            (NigerianCompanies[3], 320_000_000m, LoanApplicationStatus.OfferAccepted, "Offer Accepted - Pending security perfection"),
            (NigerianCompanies[4], 280_000_000m, LoanApplicationStatus.SecurityPerfection, "Security Perfection - Legal preparing instruments"),
            (NigerianCompanies[5], 500_000_000m, LoanApplicationStatus.SecurityApproval, "Security Approval - Awaiting Head of Legal countersignature"),
            (NigerianCompanies[6], 150_000_000m, LoanApplicationStatus.DisbursementPending, "Disbursement Pending - Operations preparing memo"),
            (NigerianCompanies[7], 420_000_000m, LoanApplicationStatus.DisbursementBranchApproval, "Disbursement Branch Auth - Awaiting Branch Manager"),
            (NigerianCompanies[8], 370_000_000m, LoanApplicationStatus.DisbursementHQApproval, "Disbursement HQ Auth - Awaiting GM Finance"),
            (NigerianCompanies[9], 600_000_000m, LoanApplicationStatus.Disbursed, "Disbursed - Loan active"),
            (NigerianCompanies[10], 280_000_000m, LoanApplicationStatus.Closed, "Closed - Loan fully repaid"),
            (NigerianCompanies[11], 200_000_000m, LoanApplicationStatus.Rejected, "Rejected - Final rejection"),
            (NigerianCompanies[12], 90_000_000m, LoanApplicationStatus.Cancelled, "Cancelled - Customer withdrew application")
        };

        var appIndex = 0;
        foreach (var (companyName, amount, targetStatus, description) in scenarios)
        {
            logger.LogInformation("Creating application: {Company} - {Status}", companyName, targetStatus);
            
            var app = await CreateLoanApplicationAsync(
                context, users, corporateProduct, workflowDef,
                companyName, amount, targetStatus, appIndex);
            
            appIndex++;
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Loan applications seeded successfully");
    }

    private static async Task<LoanApplication> CreateLoanApplicationAsync(
        CRMSDbContext context,
        (ApplicationUser SystemAdmin, ApplicationUser LoanOfficer, ApplicationUser BranchApprover, 
         ApplicationUser CreditOfficer, ApplicationUser HOReviewer, ApplicationUser LegalOfficer, ApplicationUser HeadOfLegal,
         ApplicationUser CommitteeMember1, ApplicationUser CommitteeMember2, ApplicationUser CommitteeMember3,
         ApplicationUser FinalApprover, ApplicationUser Operations, ApplicationUser GMFinance, ApplicationUser RiskManager, ApplicationUser Auditor) users,
        LoanProduct product,
        WorkflowDefinition? workflowDef,
        string companyName,
        decimal amount,
        LoanApplicationStatus targetStatus,
        int index)
    {
        var accountNumber = $"001{_random.Next(1000000, 9999999)}";
        var customerId = $"CUST{DateTime.UtcNow:yyyyMMdd}{index:D4}";

        var appResult = LoanApplication.CreateCorporate(
            product.Id, product.Code, accountNumber, customerId, companyName,
            Money.Create(amount, "NGN"), 36, 16.5m, InterestRateType.Reducing,
            users.LoanOfficer.Id, users.LoanOfficer.LocationId,
            $"Business expansion and working capital for {companyName}");

        if (appResult.IsFailure)
        {
            throw new Exception($"Failed to create loan application: {appResult.Error}");
        }

        var app = appResult.Value;
        await context.LoanApplications.AddAsync(app);

        // Add parties (directors and signatories)
        for (int i = 0; i < 3; i++)
        {
            var name = NigerianNames[(_random.Next(NigerianNames.Length) + i) % NigerianNames.Length];
            var bvn = BVNs[(_random.Next(BVNs.Length) + i) % BVNs.Length];
            app.AddParty(PartyType.Director, name, bvn, $"{name.ToLower().Replace(" ", ".")}@email.com", 
                $"+234801{_random.Next(1000000, 9999999)}", "Director", 33.3m);
        }

        for (int i = 0; i < 2; i++)
        {
            var name = NigerianNames[(_random.Next(NigerianNames.Length) + i + 3) % NigerianNames.Length];
            var bvn = BVNs[(_random.Next(BVNs.Length) + i + 3) % BVNs.Length];
            app.AddParty(PartyType.Signatory, name, bvn, $"{name.ToLower().Replace(" ", ".")}@email.com", 
                $"+234802{_random.Next(1000000, 9999999)}", "Account Signatory", null);
        }

        // Add documents
        app.AddDocument(DocumentCategory.BankStatement, "internal_statement_6months.pdf", 
            "/storage/statements/internal_" + app.Id + ".pdf", 1024 * 500, "application/pdf", users.LoanOfficer.Id);
        app.AddDocument(DocumentCategory.AuditedFinancials, "audited_financials_2025.pdf",
            "/storage/financials/audited_" + app.Id + ".pdf", 1024 * 800, "application/pdf", users.LoanOfficer.Id);
        app.AddDocument(DocumentCategory.CompanyRegistration, "cac_certificate.pdf",
            "/storage/registration/cac_" + app.Id + ".pdf", 1024 * 200, "application/pdf", users.LoanOfficer.Id);

        // Progress through workflow stages based on target status
        await ProgressApplicationToStatusAsync(context, app, targetStatus, users, workflowDef);

        return app;
    }

    private static async Task ProgressApplicationToStatusAsync(
        CRMSDbContext context,
        LoanApplication app,
        LoanApplicationStatus targetStatus,
        (ApplicationUser SystemAdmin, ApplicationUser LoanOfficer, ApplicationUser BranchApprover, 
         ApplicationUser CreditOfficer, ApplicationUser HOReviewer, ApplicationUser LegalOfficer, ApplicationUser HeadOfLegal,
         ApplicationUser CommitteeMember1, ApplicationUser CommitteeMember2, ApplicationUser CommitteeMember3,
         ApplicationUser FinalApprover, ApplicationUser Operations, ApplicationUser GMFinance, ApplicationUser RiskManager, ApplicationUser Auditor) users,
        WorkflowDefinition? workflowDef)
    {
        if (targetStatus == LoanApplicationStatus.Draft) return;

        // Submit
        app.Submit(users.LoanOfficer.Id);
        if (targetStatus == LoanApplicationStatus.Submitted) return;

        // Branch Review
        app.SubmitForBranchReview(users.LoanOfficer.Id);
        if (targetStatus == LoanApplicationStatus.BranchReview) return;

        // Branch Approved
        if (targetStatus == LoanApplicationStatus.Rejected || targetStatus == LoanApplicationStatus.BranchRejected)
        {
            app.RejectBranch(users.BranchApprover.Id, "Application does not meet minimum requirements");
            return;
        }
        app.ApproveBranch(users.BranchApprover.Id, "Application meets branch requirements");
        if (targetStatus == LoanApplicationStatus.BranchApproved) return;

        // Credit Analysis - add consent, bureau reports, statements, financials, collateral, guarantors
        await AddCreditAnalysisDataAsync(context, app, users);
        app.StartCreditAnalysis(app.Parties.Count, users.CreditOfficer.Id);
        
        // Record credit checks completed
        foreach (var _ in app.Parties)
        {
            app.RecordCreditCheckCompleted(users.CreditOfficer.Id);
        }
        if (targetStatus == LoanApplicationStatus.CreditAnalysis) return;

        // HO Review
        app.MoveToHOReview(users.CreditOfficer.Id);
        
        // Add AI Advisory
        await AddAdvisoryAsync(context, app, users);
        
        if (targetStatus == LoanApplicationStatus.HOReview) return;

        // Legal Review (LegalOfficer maker)
        app.MoveToLegalReview(users.HOReviewer.Id);
        if (targetStatus == LoanApplicationStatus.LegalReview) return;

        // Legal Approval (HeadOfLegal checker)
        app.SubmitLegalOpinion(users.LegalOfficer.Id, "Legal opinion prepared — title documents verified, no encumbrances found");
        if (targetStatus == LoanApplicationStatus.LegalApproval) return;

        app.ApproveLegalReview(users.HeadOfLegal.Id, "Legal opinion countersigned");

        // Committee
        app.MoveToCommittee(users.HeadOfLegal.Id);
        
        // Create committee review
        var committeeReview = await CreateCommitteeReviewAsync(context, app, users);
        
        if (targetStatus == LoanApplicationStatus.CommitteeCirculation) return;

        // Committee Approved
        if (committeeReview != null)
        {
            committeeReview.StartVoting();
            committeeReview.CastVote(users.CommitteeMember1.Id, CommitteeVote.Approve, "Strong financials");
            committeeReview.CastVote(users.CommitteeMember2.Id, CommitteeVote.Approve, "Good collateral coverage");
            committeeReview.CastVote(users.CommitteeMember3.Id, CommitteeVote.Approve, "Low credit risk");
            committeeReview.RecordDecision(users.CommitteeMember1.Id, CommitteeDecision.Approved,
                "Unanimous approval with standard conditions",
                app.RequestedAmount.Amount * 0.95m, app.RequestedTenorMonths, app.InterestRatePerAnnum + 0.5m,
                "Quarterly financial reporting required");
        }
        app.ApproveCommittee(users.CommitteeMember1.Id,
            Money.Create(app.RequestedAmount.Amount * 0.95m, "NGN"),
            app.RequestedTenorMonths, app.InterestRatePerAnnum + 0.5m);
        if (targetStatus == LoanApplicationStatus.CommitteeApproved) return;

        // Move to FinalApproval — MD/CEO sign-off stage
        app.MoveToFinalApproval(users.SystemAdmin.Id);
        if (targetStatus == LoanApplicationStatus.FinalApproval) return;

        // Final Approved
        app.FinalApprove(users.FinalApprover.Id, "All conditions met");
        
        // Generate loan pack
        await GenerateLoanPackAsync(context, app, users);
        
        if (targetStatus == LoanApplicationStatus.Approved) return;

        // Offer Generated → Offer Accepted
        app.IssueOfferLetter(users.LoanOfficer.Id);
        if (targetStatus == LoanApplicationStatus.OfferGenerated) return;

        app.AcceptOffer(users.LoanOfficer.Id, DateTime.UtcNow.AddDays(-1), Domain.Enums.OfferAcceptanceMethod.InBranchSigning, true);
        if (targetStatus == LoanApplicationStatus.OfferAccepted) return;

        // Security Perfection (LegalOfficer maker)
        app.MoveToSecurityPerfection(users.SystemAdmin.Id);
        if (targetStatus == LoanApplicationStatus.SecurityPerfection) return;

        app.SubmitSecurityDocuments(users.LegalOfficer.Id, "Deed of mortgage and charge documents verified and submitted");
        if (targetStatus == LoanApplicationStatus.SecurityApproval) return;

        // Security Approval (HeadOfLegal checker)
        app.ApproveSecurityPerfection(users.HeadOfLegal.Id, "Security perfection confirmed — all instruments properly executed");
        if (targetStatus == LoanApplicationStatus.DisbursementPending) return;

        // Disbursement Pending (Operations maker)
        app.PrepareDisbursementMemo(users.Operations.Id, "Disbursement memo prepared and core banking entry initiated");
        if (targetStatus == LoanApplicationStatus.DisbursementBranchApproval) return;

        // Disbursement Branch Authorisation (BranchApprover checker 1)
        app.ApproveDisbursementBranch(users.BranchApprover.Id, "Branch Manager authorisation granted");
        if (targetStatus == LoanApplicationStatus.DisbursementHQApproval) return;

        // Disbursement HQ Authorisation (GMFinance checker 2) → Disbursed
        var coreBankingLoanId = $"LN{DateTime.UtcNow:yyyyMMddHHmmss}{_random.Next(1000, 9999)}";
        app.RecordDisbursement(coreBankingLoanId, users.GMFinance.Id);

        // Create workflow instance if definition exists
        if (workflowDef != null)
        {
            var stage = workflowDef.GetStage(app.Status);
            if (stage != null)
            {
                var workflowInstance = WorkflowInstance.Create(
                    app.Id, workflowDef.Id, app.Status, stage.DisplayName, stage.AssignedRole, stage.SLAHours, users.LoanOfficer.Id);
                if (workflowInstance.IsSuccess)
                {
                    await context.WorkflowInstances.AddAsync(workflowInstance.Value);
                }
            }
        }
    }

    private static async Task AddCreditAnalysisDataAsync(
        CRMSDbContext context,
        LoanApplication app,
        (ApplicationUser SystemAdmin, ApplicationUser LoanOfficer, ApplicationUser BranchApprover, 
         ApplicationUser CreditOfficer, ApplicationUser HOReviewer, ApplicationUser LegalOfficer, ApplicationUser HeadOfLegal,
         ApplicationUser CommitteeMember1, ApplicationUser CommitteeMember2, ApplicationUser CommitteeMember3,
         ApplicationUser FinalApprover, ApplicationUser Operations, ApplicationUser GMFinance, ApplicationUser RiskManager, ApplicationUser Auditor) users)
    {
        // 1. Consent Records
        foreach (var party in app.Parties.Where(p => p.PartyType == PartyType.Director || p.PartyType == PartyType.Signatory))
        {
            var consentResult = ConsentRecord.Create(
                party.FullName, party.BVN, ConsentType.CreditBureauCheck,
                "Credit assessment for loan application",
                "I hereby authorize the bank to obtain my credit report from any licensed credit bureau in Nigeria.",
                "1.0", ConsentCaptureMethod.Digital, users.LoanOfficer.Id, users.LoanOfficer.FullName,
                app.Id, null, party.Email, party.PhoneNumber, null, "192.168.1.100", "Mozilla/5.0");
            
            if (consentResult.IsSuccess)
            {
                await context.ConsentRecords.AddAsync(consentResult.Value);
                
                // 2. Bureau Reports - Use SmartComply (current provider after migration)
                var provider = CreditBureauProvider.SmartComply;
                
                var bureauReportResult = BureauReport.Create(
                    provider, SubjectType.Individual, party.FullName,
                    party.BVN, users.CreditOfficer.Id, app.Id,
                    taxId: null, partyId: party.Id, partyType: party.PartyType.ToString());
                
                if (bureauReportResult.IsSuccess)
                {
                    var report = bureauReportResult.Value;
                    var creditScore = _random.Next(450, 850);
                    var grade = creditScore >= 700 ? "A" : creditScore >= 600 ? "B" : creditScore >= 500 ? "C" : "D";
                    
                    var totalAccounts = _random.Next(3, 10);
                    var activeLoans = _random.Next(2, totalAccounts);
                    var performingAccounts = _random.Next(1, activeLoans + 1);
                    
                    report.CompleteWithData(
                        $"SC{_random.Next(100000, 999999)}", // SmartComply Registry ID
                        creditScore, grade, DateTime.UtcNow.AddDays(-1),
                        $"{{\"provider\":\"SmartComply\",\"status\":\"success\"}}", null,
                        totalAccounts, activeLoans, performingAccounts, _random.Next(0, 3), _random.Next(0, 3),
                        _random.Next(10000000, 50000000), _random.Next(0, 5000000), _random.Next(50000000, 100000000),
                        _random.Next(0, 60), _random.Next(10) == 0);
                    
                    // Add multiple bureau accounts with different statuses
                    var accountConfigs = new[]
                    {
                        (AccountStatus.Performing, DelinquencyLevel.Current, LegalStatus.None, "Term Loan"),
                        (AccountStatus.Performing, DelinquencyLevel.Days1To30, LegalStatus.None, "Overdraft"),
                        (AccountStatus.NonPerforming, DelinquencyLevel.Days91To120, LegalStatus.Litigation, "Credit Card"),
                        (AccountStatus.Closed, DelinquencyLevel.Current, LegalStatus.None, "Personal Loan")
                    };
                    
                    foreach (var (status, delinquency, legal, accType) in accountConfigs.Take(2 + _random.Next(2)))
                    {
                        var account = BureauAccount.Create(report.Id, $"001{_random.Next(1000000, 9999999)}", 
                            "Commercial Bank", accType, status, delinquency,
                            _random.Next(50000000, 100000000), _random.Next(10000000, 50000000), _random.Next(500000, 2000000),
                            DateTime.UtcNow.AddYears(-2), status == AccountStatus.Closed ? DateTime.UtcNow.AddMonths(-6) : null,
                            DateTime.UtcNow.AddDays(-30), _random.Next(500000, 2000000),
                            "000000000000", legal, legal != LegalStatus.None ? DateTime.UtcNow.AddMonths(-3) : null, "NGN", DateTime.UtcNow);
                        report.AddAccount(account);
                    }
                    
                    // Add multiple score factors
                    var factors = new[]
                    {
                        ("PAYMENT_HISTORY", "Consistent payment history", "Positive", 1),
                        ("CREDIT_UTILIZATION", "Credit utilization at 45%", "Neutral", 2),
                        ("ACCOUNT_AGE", "Average account age 5+ years", "Positive", 3),
                        ("RECENT_INQUIRIES", "Multiple recent credit inquiries", "Negative", 4)
                    };
                    foreach (var (code, desc, impact, order) in factors)
                    {
                        var factor = BureauScoreFactor.Create(report.Id, code, desc, impact, order);
                        report.AddScoreFactor(factor);
                    }
                    
                    // Add fraud check results (SmartComply integration)
                    var fraudScore = _random.Next(15, 85);
                    var fraudRecommendation = fraudScore < 30 ? "Low Risk - Approve" 
                        : fraudScore < 60 ? "Medium Risk - Review Required" 
                        : "High Risk - Manual Review";
                    report.RecordFraudCheckResults(fraudScore, fraudRecommendation, 
                        $"{{\"fraudRiskScore\":{fraudScore},\"recommendation\":\"{fraudRecommendation}\"}}");
                    
                    await context.BureauReports.AddAsync(report);
                }
            }
        }

        // 2b. Business Consent Record (for corporate entity credit check using RC number)
        // RC number is stored in NIN field (repurposed for business identifier)
        if (!string.IsNullOrEmpty(app.RegistrationNumber))
        {
            var businessConsentResult = ConsentRecord.Create(
                app.CustomerName, null, ConsentType.CreditBureauCheck,
                "Corporate credit assessment for loan application",
                "The company hereby authorizes the bank to obtain its credit report from any licensed credit bureau in Nigeria.",
                "1.0", ConsentCaptureMethod.Digital, users.LoanOfficer.Id, users.LoanOfficer.FullName,
                app.Id, nin: app.RegistrationNumber, null, null, null, "192.168.1.100", "Mozilla/5.0");

            if (businessConsentResult.IsSuccess)
            {
                await context.ConsentRecords.AddAsync(businessConsentResult.Value);

                // Business Bureau Report
                var provider = CreditBureauProvider.SmartComply;
                var businessReportResult = BureauReport.Create(
                    provider, SubjectType.Business, app.CustomerName,
                    null, users.CreditOfficer.Id, app.Id,
                    taxId: app.RegistrationNumber, partyId: null, partyType: "Business");

                if (businessReportResult.IsSuccess)
                {
                    var report = businessReportResult.Value;
                    var totalAccounts = _random.Next(5, 15);
                    var activeLoans = _random.Next(3, totalAccounts);
                    var performingAccounts = _random.Next(2, activeLoans + 1);

                    report.CompleteWithData(
                        $"SC{_random.Next(100000, 999999)}",
                        null, null, DateTime.UtcNow.AddDays(-1),
                        $"{{\"provider\":\"{provider}\",\"status\":\"success\",\"businessName\":\"{app.CustomerName}\"}}",
                        null, totalAccounts, activeLoans, performingAccounts,
                        _random.Next(0, 2), _random.Next(0, 2),
                        _random.Next(100000000, 500000000), _random.Next(0, 10000000),
                        _random.Next(200000000, 800000000), _random.Next(0, 30), false);

                    await context.BureauReports.AddAsync(report);
                }
            }
        }

        // 3. Bank Statements
        var statementResult = BankStatement.Create(
            app.AccountNumber, app.CustomerName, "Access Bank PLC",
            DateTime.UtcNow.AddMonths(-6), DateTime.UtcNow,
            _random.Next(50000000, 100000000), _random.Next(60000000, 150000000),
            StatementFormat.PDF, StatementSource.CoreBanking, users.LoanOfficer.Id,
            "internal_statement.pdf", "/storage/statements/internal_" + app.Id + ".pdf", app.Id);
        
        if (statementResult.IsSuccess)
        {
            var statement = statementResult.Value;
            
            // Add transactions
            for (int month = 0; month < 6; month++)
            {
                var txDate = DateTime.UtcNow.AddMonths(-6 + month);
                
                // Salary credits
                statement.AddTransaction(txDate.AddDays(25), "Salary Credit", 
                    _random.Next(5000000, 15000000), StatementTransactionType.Credit, 
                    _random.Next(50000000, 100000000), $"SAL{_random.Next(10000, 99999)}");
                
                // Business inflows
                for (int j = 0; j < 5; j++)
                {
                    statement.AddTransaction(txDate.AddDays(_random.Next(1, 28)), "Business Receipt",
                        _random.Next(1000000, 10000000), StatementTransactionType.Credit,
                        _random.Next(50000000, 100000000), $"TRF{_random.Next(10000, 99999)}");
                }
                
                // Outflows
                for (int j = 0; j < 8; j++)
                {
                    statement.AddTransaction(txDate.AddDays(_random.Next(1, 28)), "Business Payment",
                        _random.Next(500000, 5000000), StatementTransactionType.Debit,
                        _random.Next(30000000, 80000000), $"PAY{_random.Next(10000, 99999)}");
                }
            }

            // Complete analysis
            var summary = new CashflowSummary(
                6, DateTime.UtcNow.AddMonths(-6), DateTime.UtcNow,
                _random.Next(50000000, 100000000), _random.Next(30000000, 60000000),
                30, 45, _random.Next(20000000, 50000000),
                _random.Next(8000000, 15000000), true, 25, "Employer Ltd",
                _random.Next(2000000, 5000000), _random.Next(1000000, 3000000), _random.Next(500000, 1500000), _random.Next(100000, 500000),
                0, 0, 0, 0,
                _random.Next(5000000, 15000000), _random.Next(50000000, 100000000),
                0.15m, 0.12m);
            statement.CompleteAnalysis(summary);
            
            await context.BankStatements.AddAsync(statement);
        }

        // 4. Financial Statements
        for (int year = 2023; year <= 2025; year++)
        {
            var fsResult = FinancialStatement.Create(
                app.Id, year, new DateTime(year, 12, 31), FinancialYearType.Audited,
                InputMethod.ManualEntry, users.LoanOfficer.Id, "NGN",
                "KPMG Professional Services", "KPMG Nigeria", new DateTime(year + 1, 3, 15), "Unqualified");
            
            if (fsResult.IsSuccess)
            {
                var fs = fsResult.Value;
                
                // Balance Sheet
                var totalAssets = _random.Next(500000000, 2000000000);
                var currentAssets = totalAssets * 0.4m;
                var nonCurrentAssets = totalAssets * 0.6m;
                var totalLiabilities = totalAssets * 0.6m;
                var currentLiabilities = totalLiabilities * 0.3m;
                var nonCurrentLiabilities = totalLiabilities * 0.7m;
                var equity = totalAssets - totalLiabilities;
                
                var bs = BalanceSheet.Create(fs.Id,
                    currentAssets * 0.3m, currentAssets * 0.25m, currentAssets * 0.25m, currentAssets * 0.1m, currentAssets * 0.1m,
                    nonCurrentAssets * 0.6m, nonCurrentAssets * 0.1m, nonCurrentAssets * 0.2m, nonCurrentAssets * 0.05m, nonCurrentAssets * 0.05m,
                    currentLiabilities * 0.3m, currentLiabilities * 0.2m, currentLiabilities * 0.2m, currentLiabilities * 0.15m, currentLiabilities * 0.1m, currentLiabilities * 0.05m,
                    nonCurrentLiabilities * 0.7m, nonCurrentLiabilities * 0.1m, nonCurrentLiabilities * 0.1m, nonCurrentLiabilities * 0.1m,
                    equity * 0.3m, equity * 0.1m, equity * 0.5m, equity * 0.1m);
                fs.SetBalanceSheet(bs);
                
                // Income Statement
                var revenue = _random.Next(200000000, 800000000);
                var income = IncomeStatement.Create(fs.Id, revenue, revenue * 0.02m, revenue * 0.6m,
                    revenue * 0.05m, revenue * 0.1m, revenue * 0.03m, revenue * 0.02m,
                    revenue * 0.005m, revenue * 0.04m, revenue * 0.005m, revenue * 0.05m, 0);
                fs.SetIncomeStatement(income);
                
                // Cash Flow Statement
                var cf = CashFlowStatement.Create(fs.Id,
                    income.ProfitBeforeTax, revenue * 0.03m, revenue * 0.04m, -(currentAssets * 0.1m), revenue * 0.045m, 0,
                    nonCurrentAssets * 0.05m, nonCurrentAssets * 0.01m, nonCurrentAssets * 0.02m, nonCurrentAssets * 0.005m, revenue * 0.005m, 0, 0,
                    nonCurrentLiabilities * 0.1m, nonCurrentLiabilities * 0.05m, revenue * 0.04m, 0, 0, 0,
                    _random.Next(10000000, 50000000));
                fs.SetCashFlowStatement(cf);
                
                fs.Submit();
                fs.Verify(users.CreditOfficer.Id, "Verified against source documents");
                
                await context.FinancialStatements.AddAsync(fs);
            }
        }

        // 5. Collateral with Valuations and Documents - Cover multiple types
        var collateralConfigs = new[]
        {
            (CollateralType.RealEstate, 0.5m, LienType.FirstCharge, "PLOT-2345-LAGOS", "Victoria Island, Lagos"),
            (CollateralType.FixedDeposit, 0.25m, LienType.Pledge, "FD-001234567", "Access Bank Branch"),
            (CollateralType.Equipment, 0.15m, LienType.Hypothecation, "EQ-CAT-2024", "Factory Premises"),
            (CollateralType.Vehicle, 0.1m, LienType.Hypothecation, "LAG-123-ABC", "Company Fleet"),
            (CollateralType.Inventory, 0.2m, LienType.FloatingCharge, "INV-BATCH-001", "Warehouse A"),
            (CollateralType.Stocks, 0.08m, LienType.Pledge, "GTB-1000-UNITS", "CSCS Account")
        };
        
        foreach (var (colType, valuePct, lienType, assetId, location) in collateralConfigs.Take(3))
        {
            var colValue = app.RequestedAmount.Amount * valuePct;
            var colResult = Collateral.Create(app.Id, colType, 
                $"{colType} collateral for {app.CustomerName}", users.LoanOfficer.Id,
                assetId, location, app.CustomerName, "Corporate");
            
            if (colResult.IsSuccess)
            {
                var col = colResult.Value;
                col.SetValuation(Money.Create(colValue, "NGN"), Money.Create(colValue * 0.7m, "NGN"));
                col.Approve(users.CreditOfficer.Id);
                col.RecordPerfection(lienType, $"LIEN-{_random.Next(100000, 999999)}", "CAC Nigeria", DateTime.UtcNow.AddDays(-5));
                
                // Add CollateralValuation with different types
                var valTypes = new[] { ValuationType.Initial, ValuationType.MarketValue };
                foreach (var valType in valTypes)
                {
                    var valuation = CollateralValuation.Create(
                        col.Id, valType, DateTime.UtcNow.AddDays(-10),
                        Money.Create(colValue, "NGN"), Money.Create(colValue * 0.7m, "NGN"),
                        "John Adeyemi", "Knight Frank Nigeria", $"KF-{valType}-2024",
                        $"VAL-{_random.Next(100000, 999999)}", users.CreditOfficer.Id,
                        $"{valType} valuation completed");
                    col.AddValuation(valuation);
                }
                
                // Add CollateralDocument
                var doc = CollateralDocument.Create(
                    col.Id, colType == CollateralType.RealEstate ? "Title Deed" : "Certificate",
                    $"{colType}_doc_{col.Id}.pdf", $"/storage/collateral/{col.Id}/",
                    _random.Next(100000, 500000), "application/pdf", users.LoanOfficer.Id,
                    $"{colType} supporting document");
                doc.Verify(users.CreditOfficer.Id);
                col.AddDocument(doc);
                
                await context.Collaterals.AddAsync(col);
            }
        }

        // 6. Guarantors with Documents - Cover different guarantee types
        var guaranteeConfigs = new[]
        {
            (GuaranteeType.Unlimited, "Director", (decimal?)null),
            (GuaranteeType.Limited, "Business Partner", app.RequestedAmount.Amount * 0.5m),
            (GuaranteeType.JointAndSeveral, "Shareholder", app.RequestedAmount.Amount * 0.3m)
        };
        
        for (int i = 0; i < guaranteeConfigs.Length && i < 3; i++)
        {
            var (guaranteeType, relationship, limitAmount) = guaranteeConfigs[i];
            var gName = NigerianNames[(_random.Next(NigerianNames.Length) + i + 5) % NigerianNames.Length];
            var gBvn = BVNs[(_random.Next(BVNs.Length) + i + 5) % BVNs.Length];
            
            var gResult = Guarantor.CreateIndividual(
                app.Id, gName, gBvn, guaranteeType,
                users.LoanOfficer.Id, relationship,
                $"{gName.ToLower().Replace(" ", ".")}@email.com", $"+234803{_random.Next(1000000, 9999999)}",
                "Lagos, Nigeria", limitAmount.HasValue ? Money.Create(limitAmount.Value, "NGN") : null);
            
            if (gResult.IsSuccess)
            {
                var g = gResult.Value;
                g.SetFinancialDetails(
                    Money.Create(_random.Next(100000000, 500000000), "NGN"),
                    "Business Owner", "Self-Employed", Money.Create(_random.Next(5000000, 20000000), "NGN"));
                
                // Record credit check if consent exists
                var consent = context.ConsentRecords.Local.FirstOrDefault(c => c.BVN == gBvn);
                if (consent != null)
                {
                    g.RecordCreditCheck(Guid.NewGuid(), _random.Next(550, 750), "B", false, null, 1,
                        Money.Create(_random.Next(10000000, 50000000), "NGN"));
                }
                
                g.Approve(users.CreditOfficer.Id, Money.Create(_random.Next(100000000, 500000000), "NGN"));
                g.RecordAgreementSigned($"/storage/guarantees/{g.Id}.pdf");
                g.Activate();
                
                // Add multiple GuarantorDocuments
                var docTypes = new[] { "ID Card", "Address Proof", "Net Worth Statement" };
                foreach (var docType in docTypes.Take(2))
                {
                    var gDoc = GuarantorDocument.Create(
                        g.Id, docType, $"guarantor_{docType.ToLower().Replace(" ", "_")}_{g.Id}.pdf", 
                        $"/storage/guarantors/{g.Id}/",
                        _random.Next(50000, 200000), "application/pdf", users.LoanOfficer.Id,
                        $"Guarantor {docType}");
                    gDoc.Verify(users.CreditOfficer.Id);
                    g.AddDocument(gDoc);
                }
                
                await context.Guarantors.AddAsync(g);
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task AddAdvisoryAsync(
        CRMSDbContext context,
        LoanApplication app,
        (ApplicationUser SystemAdmin, ApplicationUser LoanOfficer, ApplicationUser BranchApprover, 
         ApplicationUser CreditOfficer, ApplicationUser HOReviewer, ApplicationUser LegalOfficer, ApplicationUser HeadOfLegal,
         ApplicationUser CommitteeMember1, ApplicationUser CommitteeMember2, ApplicationUser CommitteeMember3,
         ApplicationUser FinalApprover, ApplicationUser Operations, ApplicationUser GMFinance, ApplicationUser RiskManager, ApplicationUser Auditor) users)
    {
        var advisoryResult = CreditAdvisory.Create(app.Id, users.CreditOfficer.Id, "MOCK-AI-v1.0");
        if (advisoryResult.IsFailure) return;

        var advisory = advisoryResult.Value;
        advisory.StartProcessing();

        // Add risk scores for all categories
        var categories = new[]
        {
            (RiskCategory.CreditHistory, 0.20m, "Strong credit history with consistent payments"),
            (RiskCategory.FinancialHealth, 0.15m, "Solid balance sheet and profitability"),
            (RiskCategory.CashflowStability, 0.15m, "Stable monthly cashflows"),
            (RiskCategory.DebtServiceCapacity, 0.20m, "DSCR of 1.8x exceeds minimum"),
            (RiskCategory.CollateralCoverage, 0.15m, "Adequate collateral coverage at 120%"),
            (RiskCategory.ManagementRisk, 0.05m, "Experienced management team"),
            (RiskCategory.IndustryRisk, 0.05m, "Stable industry outlook"),
            (RiskCategory.ConcentrationRisk, 0.05m, "Diversified customer base")
        };

        foreach (var (category, weight, rationale) in categories)
        {
            var score = RiskScore.Create(category, _random.Next(60, 90), weight, rationale,
                category == RiskCategory.ConcentrationRisk ? new List<string> { "Top customer >20% revenue" } : null,
                new List<string> { $"Strong {category} performance" });
            advisory.AddRiskScore(score);
        }

        advisory.SetRecommendation(AdvisoryRecommendation.Approve, 
            app.RequestedAmount.Amount * 0.95m, app.RequestedTenorMonths, 
            app.InterestRatePerAnnum + 0.5m, app.RequestedAmount.Amount);

        advisory.AddCondition("Quarterly financial reporting");
        advisory.AddCondition("Maintain minimum DSCR of 1.25x");
        advisory.AddCovenant("No additional long-term debt without prior approval");
        advisory.AddCovenant("Insurance coverage maintained on all collateral");

        advisory.SetAnalysisContent(
            $"Executive Summary: {app.CustomerName} presents a moderate-risk credit profile with strong fundamentals.",
            "Strengths: Stable revenue growth, experienced management, diversified operations, strong banking relationship.",
            "Weaknesses: Industry cyclicality, moderate leverage, concentration in top customers.",
            "Mitigating Factors: Strong collateral coverage, multiple guarantors, proven track record.",
            "Key Risks: Economic downturn impact, exchange rate volatility, regulatory changes.");

        advisory.Complete();
        await context.CreditAdvisories.AddAsync(advisory);
    }

    private static async Task<CommitteeReview?> CreateCommitteeReviewAsync(
        CRMSDbContext context,
        LoanApplication app,
        (ApplicationUser SystemAdmin, ApplicationUser LoanOfficer, ApplicationUser BranchApprover, 
         ApplicationUser CreditOfficer, ApplicationUser HOReviewer, ApplicationUser LegalOfficer, ApplicationUser HeadOfLegal,
         ApplicationUser CommitteeMember1, ApplicationUser CommitteeMember2, ApplicationUser CommitteeMember3,
         ApplicationUser FinalApprover, ApplicationUser Operations, ApplicationUser GMFinance, ApplicationUser RiskManager, ApplicationUser Auditor) users)
    {
        var reviewResult = CommitteeReview.Create(
            app.Id, app.ApplicationNumber, CommitteeType.HeadOfficeCredit,
            users.CreditOfficer.Id, 3, 2, 72);
        
        if (reviewResult.IsFailure) return null;

        var review = reviewResult.Value;
        review.AddMember(users.CommitteeMember1.Id, users.CommitteeMember1.FullName, Roles.CommitteeMember, true);
        review.AddMember(users.CommitteeMember2.Id, users.CommitteeMember2.FullName, Roles.CommitteeMember);
        review.AddMember(users.CommitteeMember3.Id, users.CommitteeMember3.FullName, Roles.CommitteeMember);

        // Add comments - from committee members (only members can add Committee-visible comments)
        review.AddComment(users.CommitteeMember1.Id, 
            "This application has been thoroughly analyzed. Key metrics are within acceptable ranges. The company has shown strong growth trajectory.", CommentVisibility.Committee);
        review.AddComment(users.CommitteeMember2.Id, 
            "Risk assessment reviewed. Collateral coverage is adequate at 1.2x. Recommend conditional approval.", CommentVisibility.Committee);
        review.AddComment(users.CommitteeMember3.Id, 
            "Financial statements verified. Revenue trends are positive. No concerns with liquidity ratios.", CommentVisibility.Committee);
        // Internal comment from non-member
        review.AddComment(users.CreditOfficer.Id, 
            "Credit analysis complete. All documentation verified and filed.", CommentVisibility.Internal);

        // Attach document
        review.AttachDocument(users.CreditOfficer.Id, "credit_memo.pdf", 
            $"/storage/committee/{review.Id}/credit_memo.pdf", "Credit Memorandum", DocumentVisibility.Committee);

        await context.CommitteeReviews.AddAsync(review);
        return review;
    }

    private static async Task GenerateLoanPackAsync(
        CRMSDbContext context,
        LoanApplication app,
        (ApplicationUser SystemAdmin, ApplicationUser LoanOfficer, ApplicationUser BranchApprover, 
         ApplicationUser CreditOfficer, ApplicationUser HOReviewer, ApplicationUser LegalOfficer, ApplicationUser HeadOfLegal,
         ApplicationUser CommitteeMember1, ApplicationUser CommitteeMember2, ApplicationUser CommitteeMember3,
         ApplicationUser FinalApprover, ApplicationUser Operations, ApplicationUser GMFinance, ApplicationUser RiskManager, ApplicationUser Auditor) users)
    {
        var packResult = LoanPack.Create(app.Id, app.ApplicationNumber, users.CreditOfficer.Id,
            users.CreditOfficer.FullName, app.CustomerName, app.ProductCode, app.RequestedAmount.Amount);
        
        if (packResult.IsFailure) return;

        var pack = packResult.Value;
        pack.SetDocument($"LoanPack_{app.ApplicationNumber}_v1.pdf", 
            $"/storage/loanpacks/{app.Id}/LoanPack_v1.pdf", 1024 * 1500, 
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(app.Id.ToString()))));
        
        pack.SetContentSummary(app.ApprovedAmount?.Amount, 75, "Low", 3, 5, 3, 2);
        pack.SetIncludedSections(true, true, true, true, true, true, true, true, true);

        await context.LoanPacks.AddAsync(pack);
    }

    /// <summary>
    /// Seeds global consent records for the mock SmartComply/CBS BVNs so that credit bureau checks
    /// succeed on any UI-created application that uses the mock external service accounts.
    /// These are not tied to a specific loan application (loanApplicationId = null), so
    /// GetValidConsentAsync finds them for any application using these BVNs.
    /// </summary>
    private static async Task SeedMockConsentRecordsAsync(
        CRMSDbContext context, ILogger logger, ApplicationUser loanOfficer)
    {
        // Mock SmartComply BVNs — covers all parties on CBS account 1234567890:
        //   directors: 22234567890, 22234567891, 22234567892
        //   signatory: 22234567893 (Fatima Bello)
        //   additional mock individual: 22212345678
        var mockIndividuals = new[]
        {
            ("John Adebayo", "22234567890"),
            ("Amina Ibrahim", "22234567891"),
            ("Chukwuma Okonkwo", "22234567892"),
            ("Fatima Bello", "22234567893"),
            ("Oluwaseun Bakare", "22212345678"),
        };

        foreach (var (name, bvn) in mockIndividuals)
        {
            var alreadyExists = await context.ConsentRecords
                .AnyAsync(c => c.BVN == bvn && c.ConsentType == ConsentType.CreditBureauCheck);

            if (alreadyExists)
                continue;

            var result = ConsentRecord.Create(
                name, bvn, ConsentType.CreditBureauCheck,
                "Credit assessment for loan application",
                "I hereby authorize the bank to obtain my credit report from any licensed credit bureau in Nigeria.",
                "1.0", ConsentCaptureMethod.Digital,
                loanOfficer.Id, loanOfficer.FullName,
                loanApplicationId: null, nin: null,
                email: null, phoneNumber: null,
                signatureData: null, ipAddress: "127.0.0.1", userAgent: "SeederBot/1.0",
                validityDays: 3650); // 10-year validity for test data

            if (result.IsSuccess)
                await context.ConsentRecords.AddAsync(result.Value);
        }

        // Mock RC numbers for business checks
        var mockBusinesses = new[]
        {
            ("Mock Business RC123456", "RC123456"),
            ("Mock Business RC654321", "RC654321"),
        };

        foreach (var (name, rcNumber) in mockBusinesses)
        {
            var alreadyExists = await context.ConsentRecords
                .AnyAsync(c => c.NIN == rcNumber && c.ConsentType == ConsentType.CreditBureauCheck);

            if (alreadyExists)
                continue;

            var result = ConsentRecord.Create(
                name, bvn: null, ConsentType.CreditBureauCheck,
                "Corporate credit assessment for loan application",
                "The company hereby authorizes the bank to obtain its credit report from any licensed credit bureau in Nigeria.",
                "1.0", ConsentCaptureMethod.Digital,
                loanOfficer.Id, loanOfficer.FullName,
                loanApplicationId: null, nin: rcNumber,
                email: null, phoneNumber: null,
                signatureData: null, ipAddress: "127.0.0.1", userAgent: "SeederBot/1.0",
                validityDays: 3650);

            if (result.IsSuccess)
                await context.ConsentRecords.AddAsync(result.Value);
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Mock consent records seeded for {Count} BVNs and {BizCount} RC numbers",
            mockIndividuals.Length, mockBusinesses.Length);
    }

    private static async Task SeedAuditLogsAsync(
        CRMSDbContext context, ILogger logger,
        (ApplicationUser SystemAdmin, ApplicationUser LoanOfficer, ApplicationUser BranchApprover, 
         ApplicationUser CreditOfficer, ApplicationUser HOReviewer, ApplicationUser LegalOfficer, ApplicationUser HeadOfLegal,
         ApplicationUser CommitteeMember1, ApplicationUser CommitteeMember2, ApplicationUser CommitteeMember3,
         ApplicationUser FinalApprover, ApplicationUser Operations, ApplicationUser GMFinance, ApplicationUser RiskManager, ApplicationUser Auditor) users)
    {
        if (await context.AuditLogs.AnyAsync())
        {
            logger.LogInformation("Audit logs already exist, skipping...");
            return;
        }

        logger.LogInformation("Seeding audit logs...");

        // Add various audit log entries
        var auditEntries = new[]
        {
            (AuditAction.Login, AuditCategory.Authentication, "User logged in", users.LoanOfficer),
            (AuditAction.Create, AuditCategory.LoanApplication, "Loan application created", users.LoanOfficer),
            (AuditAction.Submit, AuditCategory.LoanApplication, "Loan application submitted", users.LoanOfficer),
            (AuditAction.Approve, AuditCategory.LoanApplication, "Branch approval granted", users.BranchApprover),
            (AuditAction.BureauRequest, AuditCategory.CreditBureau, "Credit bureau check requested", users.CreditOfficer),
            (AuditAction.BureauResponse, AuditCategory.CreditBureau, "Credit bureau response received", users.CreditOfficer),
            (AuditAction.AdvisoryGenerated, AuditCategory.Advisory, "AI advisory generated", users.CreditOfficer),
            (AuditAction.Vote, AuditCategory.Committee, "Committee vote cast", users.CommitteeMember1),
            (AuditAction.Decision, AuditCategory.Committee, "Committee decision recorded", users.CommitteeMember1),
            (AuditAction.Approve, AuditCategory.LoanApplication, "Final approval granted", users.FinalApprover),
            (AuditAction.ConfigChange, AuditCategory.Configuration, "Scoring parameter updated", users.SystemAdmin),
            (AuditAction.Export, AuditCategory.DataAccess, "Loan pack exported", users.Operations),
            (AuditAction.Read, AuditCategory.DataAccess, "Bureau report viewed", users.Auditor)
        };

        foreach (var (action, category, description, user) in auditEntries)
        {
            var log = AuditLog.Create(action, category, description, "LoanApplication",
                null, null, user.Id, user.FullName, null, "192.168.1.100", "Mozilla/5.0");
            await context.AuditLogs.AddAsync(log);
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Audit logs seeded successfully");
    }

    private static async Task SeedPermissionsAndRolePermissionsAsync(CRMSDbContext context, ILogger logger)
    {
        if (await context.Permissions.AnyAsync())
        {
            logger.LogInformation("Permissions already exist, skipping...");
            return;
        }

        logger.LogInformation("Seeding permissions and role permissions...");

        // Create permissions
        var permissions = new[]
        {
            Permission.Create("loan.create", "Create Loan Applications", "LoanApplication"),
            Permission.Create("loan.view", "View Loan Applications", "LoanApplication"),
            Permission.Create("loan.edit", "Edit Loan Applications", "LoanApplication"),
            Permission.Create("loan.approve", "Approve Loan Applications", "LoanApplication"),
            Permission.Create("loan.reject", "Reject Loan Applications", "LoanApplication"),
            Permission.Create("bureau.request", "Request Credit Bureau Reports", "CreditBureau"),
            Permission.Create("bureau.view", "View Credit Bureau Reports", "CreditBureau"),
            Permission.Create("committee.vote", "Cast Committee Votes", "Committee"),
            Permission.Create("committee.chair", "Chair Committee Reviews", "Committee"),
            Permission.Create("config.manage", "Manage System Configuration", "Configuration"),
            Permission.Create("user.manage", "Manage Users", "Administration"),
            Permission.Create("audit.view", "View Audit Logs", "Audit"),
            Permission.Create("report.generate", "Generate Reports", "Reporting")
        };

        await context.Permissions.AddRangeAsync(permissions);
        await context.SaveChangesAsync();

        // Assign permissions to roles
        var roles = await context.Roles.ToListAsync();
        var adminRole = roles.FirstOrDefault(r => r.Name == Roles.SystemAdmin);
        var loanOfficerRole = roles.FirstOrDefault(r => r.Name == Roles.LoanOfficer);
        var creditOfficerRole = roles.FirstOrDefault(r => r.Name == Roles.CreditOfficer);
        var committeeRole = roles.FirstOrDefault(r => r.Name == Roles.CommitteeMember);
        var auditorRole = roles.FirstOrDefault(r => r.Name == Roles.Auditor);

        if (adminRole != null)
        {
            foreach (var perm in permissions)
            {
                await context.RolePermissions.AddAsync(new ApplicationRolePermission(adminRole.Id, perm.Id));
            }
        }

        if (loanOfficerRole != null)
        {
            var loanPerms = permissions.Where(p => p.Code.StartsWith("loan.") && p.Code != "loan.approve" && p.Code != "loan.reject");
            foreach (var perm in loanPerms)
            {
                await context.RolePermissions.AddAsync(new ApplicationRolePermission(loanOfficerRole.Id, perm.Id));
            }
        }

        if (creditOfficerRole != null)
        {
            var creditPerms = permissions.Where(p => p.Code.StartsWith("bureau.") || p.Code == "loan.view");
            foreach (var perm in creditPerms)
            {
                await context.RolePermissions.AddAsync(new ApplicationRolePermission(creditOfficerRole.Id, perm.Id));
            }
        }

        if (committeeRole != null)
        {
            var committeePerms = permissions.Where(p => p.Code.StartsWith("committee.") || p.Code == "loan.view");
            foreach (var perm in committeePerms)
            {
                await context.RolePermissions.AddAsync(new ApplicationRolePermission(committeeRole.Id, perm.Id));
            }
        }

        if (auditorRole != null)
        {
            var auditPerms = permissions.Where(p => p.Code == "audit.view" || p.Code == "loan.view" || p.Code == "bureau.view");
            foreach (var perm in auditPerms)
                await context.RolePermissions.AddAsync(new ApplicationRolePermission(auditorRole.Id, perm.Id));
        }

        var branchApproverRole = roles.FirstOrDefault(r => r.Name == Roles.BranchApprover);
        if (branchApproverRole != null)
        {
            // Branch-level approval: view applications, approve or reject at branch stage
            var perms = permissions.Where(p => p.Code is "loan.view" or "loan.approve" or "loan.reject");
            foreach (var perm in perms)
                await context.RolePermissions.AddAsync(new ApplicationRolePermission(branchApproverRole.Id, perm.Id));
        }

        var hoReviewerRole = roles.FirstOrDefault(r => r.Name == Roles.HOReviewer);
        if (hoReviewerRole != null)
        {
            // HO review: view applications and bureau reports, approve/reject/return at HO stage
            var perms = permissions.Where(p => p.Code is "loan.view" or "loan.approve" or "loan.reject" or "bureau.view");
            foreach (var perm in perms)
                await context.RolePermissions.AddAsync(new ApplicationRolePermission(hoReviewerRole.Id, perm.Id));
        }

        var finalApproverRole = roles.FirstOrDefault(r => r.Name == Roles.FinalApprover);
        if (finalApproverRole != null)
        {
            // Final sign-off authority: full loan visibility, bureau access, report generation
            var perms = permissions.Where(p => p.Code is "loan.view" or "loan.approve" or "loan.reject" or "bureau.view" or "report.generate");
            foreach (var perm in perms)
                await context.RolePermissions.AddAsync(new ApplicationRolePermission(finalApproverRole.Id, perm.Id));
        }

        var operationsRole = roles.FirstOrDefault(r => r.Name == Roles.Operations);
        if (operationsRole != null)
        {
            // Disbursement operations: view and complete approved loans, generate reports
            var perms = permissions.Where(p => p.Code is "loan.view" or "loan.approve" or "report.generate");
            foreach (var perm in perms)
                await context.RolePermissions.AddAsync(new ApplicationRolePermission(operationsRole.Id, perm.Id));
        }

        var riskManagerRole = roles.FirstOrDefault(r => r.Name == Roles.RiskManager);
        if (riskManagerRole != null)
        {
            // Risk oversight: full read access across loans, bureau, audit; override approve/reject authority
            var perms = permissions.Where(p => p.Code is "loan.view" or "loan.approve" or "loan.reject"
                or "bureau.view" or "audit.view" or "report.generate");
            foreach (var perm in perms)
                await context.RolePermissions.AddAsync(new ApplicationRolePermission(riskManagerRole.Id, perm.Id));
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Permissions and role permissions seeded successfully");
    }

    private static async Task SeedProductRulesAndRequirementsAsync(CRMSDbContext context, ILogger logger, List<LoanProduct> products)
    {
        if (await context.EligibilityRules.AnyAsync())
        {
            logger.LogInformation("Product rules already exist, skipping...");
            return;
        }

        logger.LogInformation("Seeding eligibility rules and document requirements...");

        // Get product IDs only - don't track the products themselves
        var productIds = products.Take(3).Select(p => p.Id).ToList();
        
        // Clear change tracker to avoid conflicts
        context.ChangeTracker.Clear();
        
        // Check if rules already exist for these products
        var existingRules = await context.Set<EligibilityRule>().Where(r => productIds.Contains(r.LoanProductId)).AnyAsync();
        var existingDocs = await context.Set<DocumentRequirement>().Where(d => productIds.Contains(d.LoanProductId)).AnyAsync();
        
        if (existingRules || existingDocs)
        {
            logger.LogInformation("Eligibility rules or document requirements already exist, skipping...");
            return;
        }

        foreach (var productId in productIds)
        {
            // Eligibility Rules - create directly via context.Set<>()
            var rules = new[]
            {
                CreateEligibilityRule(productId, EligibilityRuleType.BusinessAge, "CompanyAge", 
                    ComparisonOperator.GreaterOrEqual, "3", true, "Company must be at least 3 years old"),
                CreateEligibilityRule(productId, EligibilityRuleType.MinIncome, "AnnualTurnover",
                    ComparisonOperator.GreaterOrEqual, "100000000", true, "Minimum annual turnover of NGN 100M required"),
                CreateEligibilityRule(productId, EligibilityRuleType.CreditScore, "CreditScore",
                    ComparisonOperator.GreaterOrEqual, "500", true, "Minimum credit score of 500 required"),
                CreateEligibilityRule(productId, EligibilityRuleType.DebtToIncome, "DebtToEquityRatio",
                    ComparisonOperator.LessOrEqual, "3.0", false, "Debt to equity ratio should be below 3.0")
            };
            
            foreach (var rule in rules)
            {
                context.Entry(rule).State = EntityState.Added;
            }

            // Document Requirements - create directly via context.Set<>()
            var docs = new[]
            {
                CreateDocumentRequirement(productId, DocumentType.BusinessRegistration, "Certificate of Incorporation", true, 10, ".pdf"),
                CreateDocumentRequirement(productId, DocumentType.MemorandumOfAssociation, "Memorandum of Association", true, 10, ".pdf"),
                CreateDocumentRequirement(productId, DocumentType.AuditedFinancials, "Audited Financial Statements (3 years)", true, 20, ".pdf"),
                CreateDocumentRequirement(productId, DocumentType.BankStatement, "Bank Statements (12 months)", true, 50, ".pdf,.xlsx"),
                CreateDocumentRequirement(productId, DocumentType.IdentityDocument, "Directors' ID Cards", true, 5, ".pdf,.jpg,.png"),
                CreateDocumentRequirement(productId, DocumentType.BoardResolution, "Board Resolution", true, 10, ".pdf"),
                CreateDocumentRequirement(productId, DocumentType.TaxClearance, "Tax Clearance Certificate", false, 10, ".pdf")
            };
            
            foreach (var doc in docs)
            {
                context.Entry(doc).State = EntityState.Added;
            }
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Eligibility rules and document requirements seeded successfully");
    }

    private static EligibilityRule CreateEligibilityRule(
        Guid productId, EligibilityRuleType ruleType, string fieldName,
        ComparisonOperator op, string value, bool isHardRule, string failureMessage)
    {
        // Use reflection to create instance since Create is internal
        var rule = (EligibilityRule)Activator.CreateInstance(typeof(EligibilityRule), true)!;
        
        typeof(EligibilityRule).GetProperty(nameof(EligibilityRule.LoanProductId))!
            .GetSetMethod(true)!.Invoke(rule, [productId]);
        typeof(Entity).GetProperty(nameof(Entity.Id))!
            .GetSetMethod(true)!.Invoke(rule, [Guid.NewGuid()]);
        
        var type = typeof(EligibilityRule);
        type.GetProperty(nameof(EligibilityRule.RuleType))!.GetSetMethod(true)!.Invoke(rule, [ruleType]);
        type.GetProperty(nameof(EligibilityRule.FieldName))!.GetSetMethod(true)!.Invoke(rule, [fieldName]);
        type.GetProperty(nameof(EligibilityRule.Operator))!.GetSetMethod(true)!.Invoke(rule, [op]);
        type.GetProperty(nameof(EligibilityRule.Value))!.GetSetMethod(true)!.Invoke(rule, [value]);
        type.GetProperty(nameof(EligibilityRule.IsHardRule))!.GetSetMethod(true)!.Invoke(rule, [isHardRule]);
        type.GetProperty(nameof(EligibilityRule.FailureMessage))!.GetSetMethod(true)!.Invoke(rule, [failureMessage]);
        
        return rule;
    }

    private static DocumentRequirement CreateDocumentRequirement(
        Guid productId, DocumentType docType, string name, bool isMandatory, int maxSize, string extensions)
    {
        var doc = (DocumentRequirement)Activator.CreateInstance(typeof(DocumentRequirement), true)!;
        
        typeof(DocumentRequirement).GetProperty(nameof(DocumentRequirement.LoanProductId))!
            .GetSetMethod(true)!.Invoke(doc, [productId]);
        typeof(Entity).GetProperty(nameof(Entity.Id))!
            .GetSetMethod(true)!.Invoke(doc, [Guid.NewGuid()]);
        
        var type = typeof(DocumentRequirement);
        type.GetProperty(nameof(DocumentRequirement.DocumentType))!.GetSetMethod(true)!.Invoke(doc, [docType]);
        type.GetProperty(nameof(DocumentRequirement.Name))!.GetSetMethod(true)!.Invoke(doc, [name]);
        type.GetProperty(nameof(DocumentRequirement.IsMandatory))!.GetSetMethod(true)!.Invoke(doc, [isMandatory]);
        type.GetProperty(nameof(DocumentRequirement.MaxFileSizeMB))!.GetSetMethod(true)!.Invoke(doc, [maxSize]);
        type.GetProperty(nameof(DocumentRequirement.AllowedExtensions))!.GetSetMethod(true)!.Invoke(doc, [extensions]);
        
        return doc;
    }

    private static async Task SeedDataAccessLogsAsync(
        CRMSDbContext context, ILogger logger,
        (ApplicationUser SystemAdmin, ApplicationUser LoanOfficer, ApplicationUser BranchApprover, 
         ApplicationUser CreditOfficer, ApplicationUser HOReviewer, ApplicationUser LegalOfficer, ApplicationUser HeadOfLegal,
         ApplicationUser CommitteeMember1, ApplicationUser CommitteeMember2, ApplicationUser CommitteeMember3,
         ApplicationUser FinalApprover, ApplicationUser Operations, ApplicationUser GMFinance, ApplicationUser RiskManager, ApplicationUser Auditor) users)
    {
        if (await context.DataAccessLogs.CountAsync() > 1)
        {
            logger.LogInformation("Data access logs already exist, skipping...");
            return;
        }

        logger.LogInformation("Seeding data access logs...");

        var accessLogs = new[]
        {
            DataAccessLog.Create(users.CreditOfficer.Id, users.CreditOfficer.FullName, Roles.CreditOfficer,
                SensitiveDataType.CreditReport, "BureauReport", Guid.NewGuid(), DataAccessType.View,
                null, null, null, "Credit report viewed for risk assessment", "192.168.1.100"),
            DataAccessLog.Create(users.Auditor.Id, users.Auditor.FullName, Roles.Auditor,
                SensitiveDataType.PersonalInformation, "LoanApplicationParty", Guid.NewGuid(), DataAccessType.View,
                null, null, null, "Party information reviewed during audit", "192.168.1.101"),
            DataAccessLog.Create(users.Operations.Id, users.Operations.FullName, Roles.Operations,
                SensitiveDataType.BankStatement, "BankStatement", Guid.NewGuid(), DataAccessType.Export,
                null, null, null, "Bank statement exported for review", "192.168.1.102"),
            DataAccessLog.Create(users.LoanOfficer.Id, users.LoanOfficer.FullName, Roles.LoanOfficer,
                SensitiveDataType.PersonalInformation, "Guarantor", Guid.NewGuid(), DataAccessType.View,
                null, null, null, "Guarantor details viewed", "192.168.1.103"),
            DataAccessLog.Create(users.RiskManager.Id, users.RiskManager.FullName, Roles.RiskManager,
                SensitiveDataType.CreditReport, "CreditAdvisory", Guid.NewGuid(), DataAccessType.View,
                null, null, null, "AI advisory reviewed for risk assessment", "192.168.1.104")
        };

        await context.DataAccessLogs.AddRangeAsync(accessLogs);
        await context.SaveChangesAsync();
        logger.LogInformation("Data access logs seeded successfully");
    }

    private static async Task SeedNotificationsAsync(
        CRMSDbContext context, ILogger logger,
        (ApplicationUser SystemAdmin, ApplicationUser LoanOfficer, ApplicationUser BranchApprover, 
         ApplicationUser CreditOfficer, ApplicationUser HOReviewer, ApplicationUser LegalOfficer, ApplicationUser HeadOfLegal,
         ApplicationUser CommitteeMember1, ApplicationUser CommitteeMember2, ApplicationUser CommitteeMember3,
         ApplicationUser FinalApprover, ApplicationUser Operations, ApplicationUser GMFinance, ApplicationUser RiskManager, ApplicationUser Auditor) users)
    {
        if (await context.Notifications.AnyAsync())
        {
            logger.LogInformation("Notifications already exist, skipping...");
            return;
        }

        logger.LogInformation("Seeding notifications...");

        var notificationData = new[]
        {
            (NotificationType.ApplicationSubmitted, NotificationChannel.Email, NotificationPriority.Normal, users.LoanOfficer,
                "APPLICATION_SUBMITTED", "Loan Application Submitted", "Your loan application LA-2026-001 has been submitted successfully."),
            (NotificationType.WorkflowAssigned, NotificationChannel.Email, NotificationPriority.High, users.BranchApprover,
                "PENDING_APPROVAL", "Application Pending Your Approval", "Loan application LA-2026-001 is pending your review."),
            (NotificationType.CreditCheckCompleted, NotificationChannel.InApp, NotificationPriority.Normal, users.CreditOfficer,
                "CREDIT_CHECK_COMPLETE", "Credit Check Complete", "Credit bureau checks have been completed for LA-2026-002."),
            (NotificationType.CommitteeVoteRequired, NotificationChannel.Email, NotificationPriority.High, users.CommitteeMember1,
                "COMMITTEE_VOTE_REQUIRED", "Committee Vote Required", "Your vote is required for loan application LA-2026-003."),
            (NotificationType.ApplicationDisbursed, NotificationChannel.SMS, NotificationPriority.High, users.Operations,
                "DISBURSEMENT_READY", "Disbursement Ready", "Loan LA-2026-004 is ready for disbursement."),
            (NotificationType.WorkflowSLAWarning, NotificationChannel.Email, NotificationPriority.Urgent, users.LoanOfficer,
                "SLA_WARNING", "SLA Warning", "Application LA-2026-005 is approaching SLA deadline.")
        };

        foreach (var (type, channel, priority, user, code, subject, body) in notificationData)
        {
            var notificationResult = Notification.Create(type, channel, priority,
                user.FullName, channel == NotificationChannel.SMS ? "+2348012345678" : user.Email, code,
                subject, body, null, user.Id, null, null, null, null);
            
            if (notificationResult.IsSuccess)
            {
                var notification = notificationResult.Value;
                // Mark some as sent/delivered
                if (_random.Next(2) == 0)
                {
                    notification.MarkAsSending();
                    notification.MarkAsSent("mock-message-id-" + Guid.NewGuid().ToString("N")[..8], "MockProvider", "OK");
                    if (_random.Next(2) == 0)
                    {
                        notification.MarkAsDelivered();
                    }
                }
                await context.Notifications.AddAsync(notification);
            }
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Notifications seeded successfully");
    }

    /// <summary>
    /// Inserts a WorkflowTransition row only if one with the same
    /// (WorkflowDefinitionId, FromStatus, ToStatus, Action) does not already exist.
    /// Uses a WHERE NOT EXISTS sub-select so the operation is fully idempotent and
    /// never causes DbUpdateConcurrencyException regardless of how many times the
    /// seeder runs.
    /// </summary>
    private static async Task InsertTransitionIfMissingAsync(
        CRMSDbContext context,
        Guid workflowDefinitionId,
        string fromStatus,
        string toStatus,
        string action,
        string requiredRole,
        DateTime now)
    {
        await context.Database.ExecuteSqlRawAsync(
            @"INSERT INTO WorkflowTransitions
                  (Id, WorkflowDefinitionId, FromStatus, ToStatus, Action, RequiredRole,
                   RequiresComment, ConditionExpression, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy)
              SELECT @p0, @p1, @p2, @p3, @p4, @p5,
                     0, NULL, @p6, '', NULL, NULL
              WHERE NOT EXISTS (
                  SELECT 1 FROM WorkflowTransitions
                  WHERE WorkflowDefinitionId = @p7
                    AND FromStatus = @p8
                    AND ToStatus   = @p9
                    AND Action     = @p10
              )",
            Guid.NewGuid(), workflowDefinitionId, fromStatus, toStatus, action, requiredRole, now,
            workflowDefinitionId, fromStatus, toStatus, action);
    }
}

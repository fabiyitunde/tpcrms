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
        "Transcorp Hotels PLC", "Oando PLC", "FBN Holdings PLC"
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

    private static async Task<(ApplicationUser SystemAdmin, ApplicationUser LoanOfficer, ApplicationUser BranchApprover, 
        ApplicationUser CreditOfficer, ApplicationUser HOReviewer, ApplicationUser CommitteeMember1, 
        ApplicationUser CommitteeMember2, ApplicationUser CommitteeMember3, ApplicationUser FinalApprover, 
        ApplicationUser Operations, ApplicationUser RiskManager, ApplicationUser Auditor)> 
        SeedUsersAsync(CRMSDbContext context, ILogger logger)
    {
        if (await context.Users.AnyAsync())
        {
            logger.LogInformation("Users already exist, checking password hashes...");
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
                    {
                        user.SetPasswordHash(validHash);
                    }
                    await context.SaveChangesAsync();
                    logger.LogInformation("Password hashes updated successfully. Password for all users: Password1$$$");
                }
            }
            
            return (
                existingUsers.FirstOrDefault(u => u.UserName == "admin") ?? existingUsers.First(),
                existingUsers.FirstOrDefault(u => u.UserName == "loanofficer") ?? existingUsers.First(),
                existingUsers.FirstOrDefault(u => u.UserName == "branchapprover") ?? existingUsers.First(),
                existingUsers.FirstOrDefault(u => u.UserName == "creditofficer") ?? existingUsers.First(),
                existingUsers.FirstOrDefault(u => u.UserName == "horeviewer") ?? existingUsers.First(),
                existingUsers.FirstOrDefault(u => u.UserName == "committee1") ?? existingUsers.First(),
                existingUsers.FirstOrDefault(u => u.UserName == "committee2") ?? existingUsers.First(),
                existingUsers.FirstOrDefault(u => u.UserName == "committee3") ?? existingUsers.First(),
                existingUsers.FirstOrDefault(u => u.UserName == "finalapprover") ?? existingUsers.First(),
                existingUsers.FirstOrDefault(u => u.UserName == "operations") ?? existingUsers.First(),
                existingUsers.FirstOrDefault(u => u.UserName == "riskmanager") ?? existingUsers.First(),
                existingUsers.FirstOrDefault(u => u.UserName == "auditor") ?? existingUsers.First()
            );
        }

        logger.LogInformation("Seeding users...");
        var roles = await context.Roles.ToListAsync();
        var branchId = Guid.NewGuid();

        var userDefinitions = new[]
        {
            ("admin", "System", "Administrator", Roles.SystemAdmin, "admin@crms.ng"),
            ("loanofficer", "Adewale", "Johnson", Roles.LoanOfficer, "adewale.johnson@crms.ng"),
            ("loanofficer2", "Chioma", "Okonkwo", Roles.LoanOfficer, "chioma.okonkwo@crms.ng"),
            ("branchapprover", "Oluwaseun", "Adeyemi", Roles.BranchApprover, "oluwaseun.adeyemi@crms.ng"),
            ("creditofficer", "Uche", "Eze", Roles.CreditOfficer, "uche.eze@crms.ng"),
            ("horeviewer", "Fatima", "Ibrahim", Roles.HOReviewer, "fatima.ibrahim@crms.ng"),
            ("committee1", "Emeka", "Nnamdi", Roles.CommitteeMember, "emeka.nnamdi@crms.ng"),
            ("committee2", "Blessing", "Okafor", Roles.CommitteeMember, "blessing.okafor@crms.ng"),
            ("committee3", "Tunde", "Bakare", Roles.CommitteeMember, "tunde.bakare@crms.ng"),
            ("finalapprover", "Yusuf", "Mohammed", Roles.FinalApprover, "yusuf.mohammed@crms.ng"),
            ("operations", "Folake", "Balogun", Roles.Operations, "folake.balogun@crms.ng"),
            ("riskmanager", "Chukwuemeka", "Obi", Roles.RiskManager, "chukwuemeka.obi@crms.ng"),
            ("auditor", "Amina", "Suleiman", Roles.Auditor, "amina.suleiman@crms.ng")
        };

        var createdUsers = new List<ApplicationUser>();
        foreach (var (userName, firstName, lastName, roleName, email) in userDefinitions)
        {
            var userResult = ApplicationUser.Create(email, userName, firstName, lastName, UserType.Staff, "+234801234" + _random.Next(1000, 9999), branchId);
            if (userResult.IsSuccess)
            {
                // Use real password hash if hasher is available, otherwise use mock
                var passwordHash = _passwordHasher?.HashPassword("Password1$$$") 
                    ?? "AQAAAAIAAYagAAAAEMocked" + Guid.NewGuid().ToString("N");
                userResult.Value.SetPasswordHash(passwordHash);
                var role = roles.FirstOrDefault(r => r.Name == roleName);
                if (role != null) userResult.Value.AddRole(role);
                await context.Users.AddAsync(userResult.Value);
                createdUsers.Add(userResult.Value);
            }
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Users seeded successfully ({Count} users)", createdUsers.Count);

        return (
            createdUsers[0], createdUsers[1], createdUsers[3], createdUsers[4], createdUsers[5],
            createdUsers[6], createdUsers[7], createdUsers[8], createdUsers[9], createdUsers[10],
            createdUsers[11], createdUsers[12]
        );
    }

    private static async Task<WorkflowDefinition?> SeedWorkflowDefinitionAsync(CRMSDbContext context, ILogger logger)
    {
        if (await context.WorkflowDefinitions.AnyAsync())
        {
            logger.LogInformation("Workflow definition already exists, fetching...");
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
            (LoanApplicationStatus.CommitteeCirculation, "Committee", "Committee review", Roles.CommitteeMember, 72, 7, false, false),
            (LoanApplicationStatus.CommitteeApproved, "Committee Approved", "Approved by committee", Roles.FinalApprover, 24, 8, false, false),
            (LoanApplicationStatus.Approved, "Approved", "Final approval", Roles.Operations, 24, 9, false, false),
            (LoanApplicationStatus.Disbursed, "Disbursed", "Loan disbursed", Roles.Operations, 0, 10, false, true),
            (LoanApplicationStatus.Rejected, "Rejected", "Application rejected", Roles.LoanOfficer, 0, 11, false, true)
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
            (LoanApplicationStatus.BranchApproved, LoanApplicationStatus.CreditAnalysis, WorkflowAction.MoveToNextStage, Roles.CreditOfficer),
            (LoanApplicationStatus.CreditAnalysis, LoanApplicationStatus.HOReview, WorkflowAction.MoveToNextStage, Roles.CreditOfficer),
            (LoanApplicationStatus.HOReview, LoanApplicationStatus.CommitteeCirculation, WorkflowAction.Approve, Roles.HOReviewer),
            (LoanApplicationStatus.HOReview, LoanApplicationStatus.Rejected, WorkflowAction.Reject, Roles.HOReviewer),
            (LoanApplicationStatus.CommitteeCirculation, LoanApplicationStatus.CommitteeApproved, WorkflowAction.Approve, Roles.CommitteeMember),
            (LoanApplicationStatus.CommitteeCirculation, LoanApplicationStatus.Rejected, WorkflowAction.Reject, Roles.CommitteeMember),
            (LoanApplicationStatus.CommitteeApproved, LoanApplicationStatus.Approved, WorkflowAction.Approve, Roles.FinalApprover),
            (LoanApplicationStatus.Approved, LoanApplicationStatus.Disbursed, WorkflowAction.Complete, Roles.Operations)
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

        logger.LogInformation("Seeding scoring parameters...");
        var parameters = new[]
        {
            ("CREDIT_SCORE_WEIGHT", "Credit Score Weight", RiskCategory.CreditHistory, 0.20m, ParameterDataType.Percentage),
            ("FINANCIAL_HEALTH_WEIGHT", "Financial Health Weight", RiskCategory.FinancialHealth, 0.15m, ParameterDataType.Percentage),
            ("CASHFLOW_WEIGHT", "Cashflow Stability Weight", RiskCategory.CashflowStability, 0.15m, ParameterDataType.Percentage),
            ("DSCR_WEIGHT", "Debt Service Capacity Weight", RiskCategory.DebtServiceCapacity, 0.20m, ParameterDataType.Percentage),
            ("COLLATERAL_WEIGHT", "Collateral Coverage Weight", RiskCategory.CollateralCoverage, 0.15m, ParameterDataType.Percentage),
            ("MANAGEMENT_WEIGHT", "Management Risk Weight", RiskCategory.ManagementRisk, 0.05m, ParameterDataType.Percentage),
            ("INDUSTRY_WEIGHT", "Industry Risk Weight", RiskCategory.IndustryRisk, 0.05m, ParameterDataType.Percentage),
            ("CONCENTRATION_WEIGHT", "Concentration Risk Weight", RiskCategory.ConcentrationRisk, 0.05m, ParameterDataType.Percentage),
            ("MIN_CREDIT_SCORE", "Minimum Credit Score", RiskCategory.CreditHistory, 500m, ParameterDataType.Score),
            ("MIN_DSCR", "Minimum DSCR", RiskCategory.DebtServiceCapacity, 1.25m, ParameterDataType.Decimal),
            ("MAX_LTV", "Maximum LTV Ratio", RiskCategory.CollateralCoverage, 70m, ParameterDataType.Percentage),
            ("MAX_DTI", "Maximum DTI Ratio", RiskCategory.DebtServiceCapacity, 40m, ParameterDataType.Percentage)
        };

        foreach (var (code, name, category, value, dataType) in parameters)
        {
            var param = ScoringParameter.Create(category.ToString(), code, name, $"Scoring parameter: {name}", dataType, value, createdByUserId);
            if (param.IsSuccess)
            {
                await context.ScoringParameters.AddAsync(param.Value);
                
                // Add ScoringParameterHistory for creation
                var history = ScoringParameterHistory.RecordCreation(
                    param.Value.Id, category.ToString(), code, value, createdByUserId);
                await context.ScoringParameterHistory.AddAsync(history);
            }
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Scoring parameters and history seeded successfully");
    }

    private static async Task SeedLoanApplicationsAsync(
        CRMSDbContext context, ILogger logger,
        (ApplicationUser SystemAdmin, ApplicationUser LoanOfficer, ApplicationUser BranchApprover, 
         ApplicationUser CreditOfficer, ApplicationUser HOReviewer, ApplicationUser CommitteeMember1, 
         ApplicationUser CommitteeMember2, ApplicationUser CommitteeMember3, ApplicationUser FinalApprover, 
         ApplicationUser Operations, ApplicationUser RiskManager, ApplicationUser Auditor) users,
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
            
            // Committee level
            (NigerianCompanies[9], 1_500_000_000m, LoanApplicationStatus.CommitteeCirculation, "Committee Circulation - Voting in progress"),
            (NigerianCompanies[10], 800_000_000m, LoanApplicationStatus.CommitteeApproved, "Committee Approved - Awaiting final approval"),
            (NigerianCompanies[11], 120_000_000m, LoanApplicationStatus.CommitteeRejected, "Committee Rejected - Insufficient collateral"),
            
            // Final stages
            (NigerianCompanies[0], 450_000_000m, LoanApplicationStatus.FinalApproval, "Final Approval - MD sign-off pending"),
            (NigerianCompanies[1], 400_000_000m, LoanApplicationStatus.Approved, "Approved - Ready for offer generation"),
            (NigerianCompanies[2], 350_000_000m, LoanApplicationStatus.OfferGenerated, "Offer Generated - Awaiting acceptance"),
            (NigerianCompanies[3], 320_000_000m, LoanApplicationStatus.OfferAccepted, "Offer Accepted - Pending disbursement"),
            (NigerianCompanies[4], 600_000_000m, LoanApplicationStatus.Disbursed, "Disbursed - Loan active"),
            (NigerianCompanies[5], 280_000_000m, LoanApplicationStatus.Closed, "Closed - Loan fully repaid"),
            (NigerianCompanies[6], 200_000_000m, LoanApplicationStatus.Rejected, "Rejected - Final rejection"),
            (NigerianCompanies[7], 90_000_000m, LoanApplicationStatus.Cancelled, "Cancelled - Customer withdrew application")
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
         ApplicationUser CreditOfficer, ApplicationUser HOReviewer, ApplicationUser CommitteeMember1, 
         ApplicationUser CommitteeMember2, ApplicationUser CommitteeMember3, ApplicationUser FinalApprover, 
         ApplicationUser Operations, ApplicationUser RiskManager, ApplicationUser Auditor) users,
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
            users.LoanOfficer.Id, users.LoanOfficer.BranchId,
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
         ApplicationUser CreditOfficer, ApplicationUser HOReviewer, ApplicationUser CommitteeMember1, 
         ApplicationUser CommitteeMember2, ApplicationUser CommitteeMember3, ApplicationUser FinalApprover, 
         ApplicationUser Operations, ApplicationUser RiskManager, ApplicationUser Auditor) users,
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

        // Committee
        app.MoveToCommittee(users.HOReviewer.Id);
        
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

        // Final Approved
        app.FinalApprove(users.FinalApprover.Id, "All conditions met");
        
        // Generate loan pack
        await GenerateLoanPackAsync(context, app, users);
        
        if (targetStatus == LoanApplicationStatus.Approved) return;

        // Disbursed
        var coreBankingLoanId = $"LN{DateTime.UtcNow:yyyyMMddHHmmss}{_random.Next(1000, 9999)}";
        app.RecordDisbursement(coreBankingLoanId, users.Operations.Id);

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
         ApplicationUser CreditOfficer, ApplicationUser HOReviewer, ApplicationUser CommitteeMember1, 
         ApplicationUser CommitteeMember2, ApplicationUser CommitteeMember3, ApplicationUser FinalApprover, 
         ApplicationUser Operations, ApplicationUser RiskManager, ApplicationUser Auditor) users)
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
                
                // 2. Bureau Reports - Cover all providers
                var providers = new[] { CreditBureauProvider.CreditRegistry, CreditBureauProvider.FirstCentral, CreditBureauProvider.CRC };
                var providerIndex = _random.Next(providers.Length);
                var provider = providers[providerIndex];
                
                var bureauReportResult = BureauReport.Create(
                    provider, SubjectType.Individual, party.FullName,
                    party.BVN, users.CreditOfficer.Id, consentResult.Value.Id, app.Id);
                
                if (bureauReportResult.IsSuccess)
                {
                    var report = bureauReportResult.Value;
                    var creditScore = _random.Next(450, 850);
                    var grade = creditScore >= 700 ? "A" : creditScore >= 600 ? "B" : creditScore >= 500 ? "C" : "D";
                    
                    report.CompleteWithData(
                        $"{provider.ToString()[..2].ToUpper()}{_random.Next(100000, 999999)}", // Registry ID
                        creditScore, grade, DateTime.UtcNow.AddDays(-1),
                        $"{{\"provider\":\"{provider}\",\"status\":\"success\"}}", null,
                        _random.Next(3, 10), _random.Next(2, 8), _random.Next(0, 3), _random.Next(0, 3),
                        _random.Next(10000000, 50000000), _random.Next(50000000, 100000000),
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
         ApplicationUser CreditOfficer, ApplicationUser HOReviewer, ApplicationUser CommitteeMember1, 
         ApplicationUser CommitteeMember2, ApplicationUser CommitteeMember3, ApplicationUser FinalApprover, 
         ApplicationUser Operations, ApplicationUser RiskManager, ApplicationUser Auditor) users)
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
         ApplicationUser CreditOfficer, ApplicationUser HOReviewer, ApplicationUser CommitteeMember1, 
         ApplicationUser CommitteeMember2, ApplicationUser CommitteeMember3, ApplicationUser FinalApprover, 
         ApplicationUser Operations, ApplicationUser RiskManager, ApplicationUser Auditor) users)
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
         ApplicationUser CreditOfficer, ApplicationUser HOReviewer, ApplicationUser CommitteeMember1, 
         ApplicationUser CommitteeMember2, ApplicationUser CommitteeMember3, ApplicationUser FinalApprover, 
         ApplicationUser Operations, ApplicationUser RiskManager, ApplicationUser Auditor) users)
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

    private static async Task SeedAuditLogsAsync(
        CRMSDbContext context, ILogger logger,
        (ApplicationUser SystemAdmin, ApplicationUser LoanOfficer, ApplicationUser BranchApprover, 
         ApplicationUser CreditOfficer, ApplicationUser HOReviewer, ApplicationUser CommitteeMember1, 
         ApplicationUser CommitteeMember2, ApplicationUser CommitteeMember3, ApplicationUser FinalApprover, 
         ApplicationUser Operations, ApplicationUser RiskManager, ApplicationUser Auditor) users)
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
            {
                await context.RolePermissions.AddAsync(new ApplicationRolePermission(auditorRole.Id, perm.Id));
            }
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
         ApplicationUser CreditOfficer, ApplicationUser HOReviewer, ApplicationUser CommitteeMember1, 
         ApplicationUser CommitteeMember2, ApplicationUser CommitteeMember3, ApplicationUser FinalApprover, 
         ApplicationUser Operations, ApplicationUser RiskManager, ApplicationUser Auditor) users)
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
         ApplicationUser CreditOfficer, ApplicationUser HOReviewer, ApplicationUser CommitteeMember1, 
         ApplicationUser CommitteeMember2, ApplicationUser CommitteeMember3, ApplicationUser FinalApprover, 
         ApplicationUser Operations, ApplicationUser RiskManager, ApplicationUser Auditor) users)
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
}

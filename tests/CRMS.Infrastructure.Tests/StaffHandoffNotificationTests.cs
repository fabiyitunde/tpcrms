using CRMS.Application.Notification.Interfaces;
using CRMS.Application.Notification.Services;
using CRMS.Domain.Aggregates.LoanApplication;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Aggregates.Workflow;
using CRMS.Domain.Constants;
using CRMS.Domain.Entities.Identity;
using CRMS.Domain.Enums;
using CRMS.Domain.ValueObjects;
using CRMS.Infrastructure.Events.Handlers;
using CRMS.Infrastructure.Persistence;
using CRMS.Infrastructure.Persistence.Repositories;
using CRMS.Infrastructure.Persistence.Repositories.Rhshf;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace CRMS.Infrastructure.Tests;

/// <summary>
/// The corporate and RH-SHF handoff notices, and the bell: a stage change produces the right email
/// (rendered from the seeded templates) for the right role, scoped to the case's branch, and every
/// staff email leaves an in-app copy for the same person with a link into the case. Same in-memory,
/// real-orchestrator setup as NampNotificationTests.
/// </summary>
public class StaffHandoffNotificationTests
{
    private sealed class CapturingEmailSender : INotificationSender
    {
        public List<NotificationMessage> Sent { get; } = [];
        public NotificationChannel Channel => NotificationChannel.Email;
        public Task<NotificationSendResult> SendAsync(NotificationMessage message, CancellationToken ct = default)
        {
            Sent.Add(message);
            return Task.FromResult(new NotificationSendResult(true, Guid.NewGuid().ToString(), "CapturingTestSender"));
        }
    }

    private sealed class Harness : IDisposable
    {
        public CRMSDbContext Context { get; }
        public NotificationOrchestrator Orchestrator { get; }
        public LoanApplicationStatusChangedNotificationHandler CorporateHandler { get; }
        public RhshfStageChangedNotificationHandler RhshfHandler { get; }
        public RhshfCaseDecidedStaffNotificationHandler RhshfDecidedHandler { get; }
        public LoanApplicationRepository LoanRepo { get; }

        public Harness()
        {
            var options = new DbContextOptionsBuilder<CRMSDbContext>()
                .UseInMemoryDatabase("StaffNotif_" + Guid.NewGuid())
                .Options;
            Context = new CRMSDbContext(options);
            SeedData.SeedAsync(Context, NullLogger.Instance).GetAwaiter().GetResult();

            var notifRepo = new NotificationRepository(Context);
            var templateRepo = new NotificationTemplateRepository(Context);
            var userRepo = new UserRepository(Context);
            LoanRepo = new LoanApplicationRepository(Context);
            var workflowRepo = new WorkflowDefinitionRepository(Context);
            var profileRepo = new RhshfCreditProfileRepository(Context);

            Orchestrator = new NotificationOrchestrator(notifRepo, templateRepo, [new CapturingEmailSender()], Context);
            var resolver = new NampNotificationRecipientResolver(userRepo);
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["WebsiteUrl"] = "https://crms.test" })
                .Build();

            CorporateHandler = new LoanApplicationStatusChangedNotificationHandler(
                LoanRepo, workflowRepo, userRepo, resolver, Orchestrator, config,
                NullLogger<LoanApplicationStatusChangedNotificationHandler>.Instance);
            RhshfHandler = new RhshfStageChangedNotificationHandler(
                profileRepo, resolver, Orchestrator, config, NullLogger<RhshfStageChangedNotificationHandler>.Instance);
            RhshfDecidedHandler = new RhshfCaseDecidedStaffNotificationHandler(
                profileRepo, resolver, Orchestrator, config, NullLogger<RhshfCaseDecidedStaffNotificationHandler>.Instance);
        }

        public async Task<ApplicationUser> AddUserAsync(string email, string role, Guid? locationId)
        {
            var user = ApplicationUser.Create(email, email.Split('@')[0], "Test", email.Split('@')[0],
                UserType.Staff, locationId: locationId).Value;
            var roleEntity = await Context.Roles.FirstAsync(r => r.Name == role);
            user.AddRole(roleEntity);
            await Context.Users.AddAsync(user);
            await Context.SaveChangesAsync();
            return user;
        }

        /// <summary>The streamlined corporate flow's first hops, as the seeder defines them.</summary>
        public async Task SeedCorporateWorkflowAsync()
        {
            var wf = WorkflowDefinition.Create("Corporate", "Corporate loan", LoanApplicationType.Corporate).Value;
            wf.AddStage(LoanApplicationStatus.Draft, "Draft", "", Roles.LoanOfficer, 0, 1);
            wf.AddStage(LoanApplicationStatus.CreditReview, "Credit Review", "", Roles.CreditOfficer, 72, 2);
            wf.AddStage(LoanApplicationStatus.CommitteeCirculation, "Committee Circulation", "", Roles.SystemAdmin, 48, 3);
            wf.AddStage(LoanApplicationStatus.Rejected, "Rejected", "", "", 0, 4, isTerminal: true);
            wf.AddTransition(LoanApplicationStatus.Draft, LoanApplicationStatus.CreditReview, WorkflowAction.Submit, Roles.LoanOfficer);
            wf.AddTransition(LoanApplicationStatus.CreditReview, LoanApplicationStatus.CommitteeCirculation, WorkflowAction.Approve, Roles.CreditOfficer);
            wf.AddTransition(LoanApplicationStatus.CreditReview, LoanApplicationStatus.Draft, WorkflowAction.Return, Roles.LegalOfficer);
            wf.AddTransition(LoanApplicationStatus.CreditReview, LoanApplicationStatus.Rejected, WorkflowAction.Reject, Roles.CreditOfficer);
            wf.AddTransition(LoanApplicationStatus.CommitteeCirculation, LoanApplicationStatus.Rejected, WorkflowAction.Reject, Roles.SystemAdmin);
            await Context.WorkflowDefinitions.AddAsync(wf);
            await Context.SaveChangesAsync();
        }

        public async Task<LoanApplication> AddCorporateApplicationAsync(Guid branchId, Guid loanOfficerId)
        {
            var app = LoanApplication.CreateCorporate(
                Guid.NewGuid(), "CORP-TERM", "0123456789", "CUST-1", "Alliedsoft Limited",
                Money.Create(25_000_000m), 24, 18m, InterestRateType.Reducing, loanOfficerId, branchId).Value;
            await LoanRepo.AddAsync(app);
            await Context.SaveChangesAsync();
            return app;
        }

        public async Task<RhshfCreditProfile> AddRhshfProfileAsync(Guid branchId)
        {
            var profile = RhshfCreditProfile.Create(
                submissionId: Guid.NewGuid(), programmeCode: "RH-SHF-DRY-2026", programmeName: "Renewed Hope",
                sessionCode: "2026-DRY", sessionName: "Dry Season 2026", facId: Guid.NewGuid(),
                companyName: "Alliedsoft Limited", rcNumber: "RC123456", tin: "01234567-0001",
                boaAccountNumber: "0123456789", contactEmail: "fac@company.com", contactPhone: "+2348012345678",
                state: "Kano", lga: "Nassarawa", totalEopValue: 51_500_000m, currency: "NGN", farmerCount: 1200,
                callbackUrl: "https://portal.example.gov.ng/api/integrations/crms/webhook",
                certifiedByAdmin: "admin@boa.gov.ng", certifiedAt: DateTime.UtcNow, rawSubmissionPayload: "{}",
                eopLines: null, resolvedBranchId: branchId, resolvedOfficeId: null).Value;
            await Context.RhshfCreditProfiles.AddAsync(profile);
            await Context.SaveChangesAsync();
            return profile;
        }

        public void Dispose() => Context.Dispose();
    }

    [Fact]
    public async Task SeedData_AppliesStaffHandoffTemplates()
    {
        using var h = new Harness();
        foreach (var code in new[] { "CORPORATE_ACTION_REQUIRED", "CORPORATE_CASE_UPDATE", "RHSHF_ACTION_REQUIRED", "RHSHF_CASE_UPDATE" })
            Assert.True(
                await h.Context.NotificationTemplates.AnyAsync(t => t.Code == code && t.Channel == NotificationChannel.Email),
                $"Template '{code}' should be seeded by SeedData");
    }

    [Fact]
    public async Task Corporate_CreditReview_TellsTheRolesWithAWayOut_InTheBranch_ButNotTheActor()
    {
        using var h = new Harness();
        await h.SeedCorporateWorkflowAsync();
        var branchId = Guid.NewGuid();
        var lo = await h.AddUserAsync("lo@boa.ng", Roles.LoanOfficer, branchId);
        var credit = await h.AddUserAsync("credit@boa.ng", Roles.CreditOfficer, branchId);
        var legal = await h.AddUserAsync("legal@boa.ng", Roles.LegalOfficer, branchId);
        await h.AddUserAsync("credit-other@boa.ng", Roles.CreditOfficer, Guid.NewGuid()); // another branch
        var app = await h.AddCorporateApplicationAsync(branchId, lo.Id);

        await h.CorporateHandler.HandleAsync(new LoanApplicationStatusChangedEvent(app.Id, app.ApplicationNumber, LoanApplicationStatus.CreditReview, lo.Id, null));

        var emails = await h.Context.Notifications.Where(n => n.TemplateCode == "CORPORATE_ACTION_REQUIRED").ToListAsync();
        Assert.Equal(2, emails.Count);
        Assert.Contains(emails, n => n.RecipientAddress == "credit@boa.ng");
        Assert.Contains(emails, n => n.RecipientAddress == "legal@boa.ng");
        Assert.All(emails, n => Assert.Contains("Credit Review", n.Subject));
        Assert.All(emails, n => Assert.Contains($"https://crms.test/applications/{app.Id}", n.Body));

        // And the bell: one in-app copy per person, linking straight into the case.
        var bell = await h.Context.Notifications.Where(n => n.Channel == NotificationChannel.InApp).ToListAsync();
        Assert.Equal(2, bell.Count);
        Assert.All(bell, n => Assert.Equal($"/applications/{app.Id}", n.ActionUrl));
        Assert.All(bell, n => Assert.Null(n.ReadAt));
        Assert.Contains(bell, n => n.RecipientUserId == credit.Id && n.Body.Contains("Alliedsoft Limited is at Credit Review"));
        Assert.DoesNotContain(bell, n => n.RecipientUserId == legal.Id && n.ReadAt != null);
    }

    [Fact]
    public async Task Corporate_CommitteeCirculation_TellsTheCreditOfficer_ToSetUpTheCommittee()
    {
        using var h = new Harness();
        await h.SeedCorporateWorkflowAsync();
        var branchId = Guid.NewGuid();
        var lo = await h.AddUserAsync("lo@boa.ng", Roles.LoanOfficer, branchId);
        var credit = await h.AddUserAsync("credit@boa.ng", Roles.CreditOfficer, branchId);
        var app = await h.AddCorporateApplicationAsync(branchId, lo.Id);

        // Only the system holds a transition out of Committee Circulation, so the handler names the Credit Officer itself.
        await h.CorporateHandler.HandleAsync(new LoanApplicationStatusChangedEvent(app.Id, app.ApplicationNumber, LoanApplicationStatus.CommitteeCirculation, Guid.NewGuid(), null));

        var emails = await h.Context.Notifications.Where(n => n.TemplateCode == "CORPORATE_ACTION_REQUIRED").ToListAsync();
        Assert.Single(emails);
        Assert.Equal(credit.Id, emails[0].RecipientUserId);
    }

    [Fact]
    public async Task Corporate_ReturnedToDraft_TellsTheLoanOfficerWhoCreatedIt_WithTheReason()
    {
        using var h = new Harness();
        await h.SeedCorporateWorkflowAsync();
        var branchId = Guid.NewGuid();
        var lo = await h.AddUserAsync("lo@boa.ng", Roles.LoanOfficer, branchId);
        await h.AddUserAsync("lo2@boa.ng", Roles.LoanOfficer, branchId); // same branch, not the initiator
        var app = await h.AddCorporateApplicationAsync(branchId, lo.Id);

        await h.CorporateHandler.HandleAsync(new LoanApplicationStatusChangedEvent(app.Id, app.ApplicationNumber, LoanApplicationStatus.Draft, Guid.NewGuid(), "Bank statements are missing"));

        var emails = await h.Context.Notifications.Where(n => n.TemplateCode == "CORPORATE_CASE_UPDATE").ToListAsync();
        Assert.Single(emails);
        Assert.Equal(lo.Id, emails[0].RecipientUserId);
        Assert.Contains("returned to you for rework", emails[0].Subject);
        Assert.Contains("Bank statements are missing", emails[0].Body);
    }

    [Fact]
    public async Task Corporate_CreationAndSilentStatuses_TellNobody()
    {
        using var h = new Harness();
        await h.SeedCorporateWorkflowAsync();
        var branchId = Guid.NewGuid();
        var lo = await h.AddUserAsync("lo@boa.ng", Roles.LoanOfficer, branchId);
        await h.AddUserAsync("credit@boa.ng", Roles.CreditOfficer, branchId);

        // Creating a Draft raises no status event at all.
        var app = await h.AddCorporateApplicationAsync(branchId, lo.Id);
        Assert.DoesNotContain(app.DomainEvents, e => e is LoanApplicationStatusChangedEvent);

        await h.CorporateHandler.HandleAsync(new LoanApplicationStatusChangedEvent(app.Id, app.ApplicationNumber, LoanApplicationStatus.CommitteeApproved, lo.Id, null));
        Assert.False(await h.Context.Notifications.AnyAsync());
    }

    [Fact]
    public async Task Rhshf_Stage_TellsTheRoleThatActsThere_ScopedToTheBranch()
    {
        using var h = new Harness();
        var branchId = Guid.NewGuid();
        var legal = await h.AddUserAsync("legal@boa.ng", Roles.LegalOfficer, branchId);
        await h.AddUserAsync("legal-other@boa.ng", Roles.LegalOfficer, Guid.NewGuid());
        var credit = await h.AddUserAsync("credit@boa.ng", Roles.CreditOfficer, branchId);
        var profile = await h.AddRhshfProfileAsync(branchId);

        await h.RhshfHandler.HandleAsync(new RhshfInternalStageChangedEvent(profile.Id, RhshfInternalStage.LegalClearance, credit.Id, null));

        var emails = await h.Context.Notifications.Where(n => n.TemplateCode == "RHSHF_ACTION_REQUIRED").ToListAsync();
        Assert.Single(emails);
        Assert.Equal(legal.Id, emails[0].RecipientUserId);
        Assert.Contains("Legal Clearance", emails[0].Subject);
        Assert.Equal(profile.Reference, emails[0].LoanApplicationNumber);

        var bell = await h.Context.Notifications.SingleAsync(n => n.Channel == NotificationChannel.InApp);
        Assert.Equal($"/rhshf/detail/{profile.Reference}", bell.ActionUrl);
        Assert.Equal(legal.Id, bell.RecipientUserId);
    }

    [Fact]
    public async Task Rhshf_StagesThatWaitOnTheFac_TellNoStaff()
    {
        using var h = new Harness();
        var branchId = Guid.NewGuid();
        await h.AddUserAsync("credit@boa.ng", Roles.CreditOfficer, branchId);
        var profile = await h.AddRhshfProfileAsync(branchId);

        await h.RhshfHandler.HandleAsync(new RhshfInternalStageChangedEvent(profile.Id, RhshfInternalStage.AwaitingOfferAcceptance, null, null));

        Assert.False(await h.Context.Notifications.AnyAsync());
    }

    [Fact]
    public async Task Rhshf_Declined_TellsTheBranchCreditOfficers()
    {
        using var h = new Harness();
        var branchId = Guid.NewGuid();
        var credit = await h.AddUserAsync("credit@boa.ng", Roles.CreditOfficer, branchId);
        var profile = await h.AddRhshfProfileAsync(branchId);
        foreach (var stage in new[]
        {
            RhshfProfilingStage.CompanyVerification, RhshfProfilingStage.CreditBureauCheck,
            RhshfProfilingStage.EopReview, RhshfProfilingStage.SupportingDocuments, RhshfProfilingStage.ReviewAndSubmit,
        })
        {
            if (stage == RhshfProfilingStage.EopReview)
                profile.AddFarmPlanDuringProfiling("Maize", 100m, 3_000m, profile.TotalEopValue * 3m / 300_000m);
            var advanced = profile.AdvanceStage(stage);
            Assert.True(advanced.IsSuccess, advanced.Error);
        }
        var declined = profile.Appraise(credit.Id, RhshfAppraisalOutcome.Decline, "Weak farmer base");
        Assert.True(declined.IsSuccess, declined.Error);
        await h.Context.SaveChangesAsync();

        await h.RhshfDecidedHandler.HandleAsync(new RhshfCaseDecidedEvent(profile.Id));

        var emails = await h.Context.Notifications.Where(n => n.TemplateCode == "RHSHF_CASE_UPDATE").ToListAsync();
        Assert.Single(emails);
        Assert.Equal(credit.Id, emails[0].RecipientUserId);
        Assert.Contains("declined", emails[0].Subject);
    }

    [Fact]
    public async Task Bell_MirrorsStaffEmailsOnly_NeverCredentialEmails()
    {
        using var h = new Harness();
        var user = await h.AddUserAsync("someone@boa.ng", Roles.LoanOfficer, null);

        // A staff email with a user id gets a copy on the bell.
        await h.Orchestrator.SendEmailAsync(NotificationType.WorkflowAssigned, user.Email, user.FullName, "NAMP_ACTION_REQUIRED",
            new Dictionary<string, string>
            {
                ["RecipientName"] = user.FullName, ["StageName"] = "Ratification", ["ApplicationNumber"] = "NAMP-1",
                ["ApplicantName"] = "Musa", ["Sla"] = "2 days", ["ActionUrl"] = "https://crms.test/namp/abc",
            }, user.Id);

        // A password reset never does, even though it carries the user id.
        await h.Orchestrator.SendEmailAsync(NotificationType.Custom, user.Email, user.FullName, "PASSWORD_RESET",
            new Dictionary<string, string> { ["RecipientName"] = user.FullName, ["ResetUrl"] = "https://crms.test/reset?token=x", ["ExpiryMinutes"] = "30" }, user.Id);

        // An email to someone outside the bank (no user id) never does either.
        await h.Orchestrator.SendEmailAsync(NotificationType.WorkflowAssigned, "fac@company.com", "FAC", "NAMP_ACTION_REQUIRED",
            new Dictionary<string, string>
            {
                ["RecipientName"] = "FAC", ["StageName"] = "x", ["ApplicationNumber"] = "x", ["ApplicantName"] = "x", ["Sla"] = "x", ["ActionUrl"] = "/x",
            });

        var bell = await h.Context.Notifications.Where(n => n.Channel == NotificationChannel.InApp).ToListAsync();
        Assert.Single(bell);
        Assert.Equal(user.Id, bell[0].RecipientUserId);
        Assert.Equal("/namp/abc", bell[0].ActionUrl);          // absolute link stored as a path
        Assert.Equal(NotificationStatus.Delivered, bell[0].Status); // nothing to send, nothing for the worker
        Assert.Contains("Ratification", bell[0].Subject);
    }
}

using CRMS.Application.Notification.Interfaces;
using CRMS.Application.Notification.Services;
using CRMS.Domain.Aggregates.Committee;
using CRMS.Domain.Aggregates.Namp;
using CRMS.Domain.Constants;
using CRMS.Domain.Entities.Identity;
using CRMS.Domain.Enums;
using CRMS.Infrastructure.Events.Handlers;
using CRMS.Infrastructure.Persistence;
using CRMS.Infrastructure.Persistence.Repositories;
using CRMS.Infrastructure.Persistence.Repositories.Namp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace CRMS.Infrastructure.Tests;

/// <summary>
/// Confirms the NAMP workflow notification wiring: a NampStatusChangedEvent produces the right
/// email (rendered from the seeded templates) for the right recipient. Uses an in-memory database
/// with the REAL notification orchestrator, REAL seeder (so templates apply exactly as in
/// production) and a capturing sender — no network, CI-safe.
/// </summary>
public class NampNotificationTests
{
    private readonly ITestOutputHelper _output;

    public NampNotificationTests(ITestOutputHelper output) => _output = output;

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
        public CapturingEmailSender Sender { get; }
        public NampStatusChangedNotificationHandler Handler { get; }
        public NampStagingReceivedNotificationHandler StagingHandler { get; }
        public NampApplicationRepository NampRepo { get; }
        public NampWorkflowConfigRepository ConfigRepo { get; }
        public CommitteeReviewRepository CommitteeRepo { get; }
        public NampStagingRepository StagingRepo { get; }
        public UserRepository UserRepo { get; }

        public Harness()
        {
            var options = new DbContextOptionsBuilder<CRMSDbContext>()
                .UseInMemoryDatabase("NampNotif_" + Guid.NewGuid())
                .Options;
            Context = new CRMSDbContext(options);

            // Seed roles + email templates (incl. the NAMP templates) exactly as production does.
            SeedData.SeedAsync(Context, NullLogger.Instance).GetAwaiter().GetResult();

            var notifRepo = new NotificationRepository(Context);
            var templateRepo = new NotificationTemplateRepository(Context);
            NampRepo = new NampApplicationRepository(Context);
            ConfigRepo = new NampWorkflowConfigRepository(Context);
            CommitteeRepo = new CommitteeReviewRepository(Context);
            StagingRepo = new NampStagingRepository(Context);
            UserRepo = new UserRepository(Context);
            Sender = new CapturingEmailSender();

            Orchestrator = new NotificationOrchestrator(notifRepo, templateRepo, [Sender], Context);

            var resolver = new NampNotificationRecipientResolver(UserRepo);
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["WebsiteUrl"] = "https://crms.test" })
                .Build();

            Handler = new NampStatusChangedNotificationHandler(
                NampRepo, ConfigRepo, CommitteeRepo, UserRepo, resolver, Orchestrator, config,
                NullLogger<NampStatusChangedNotificationHandler>.Instance);

            StagingHandler = new NampStagingReceivedNotificationHandler(
                StagingRepo, resolver, Orchestrator, config,
                NullLogger<NampStagingReceivedNotificationHandler>.Instance);
        }

        public async Task<NampStagingRecord> AddStagingRecordAsync(Guid branchId)
        {
            var record = NampStagingRecord.Create(
                Guid.NewGuid().ToString("N"), "{}", "Aisha Bello", "0000000029",
                NampApplicantCategory.YouthAgripreneur, "Tractor", 3_500_000m);
            record.ResolveBranch(branchId, null, null);
            await StagingRepo.AddAsync(record);
            await Context.SaveChangesAsync();
            return record;
        }

        /// <summary>Creates an active user with the given role at the given location.</summary>
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

        public async Task<NampApplication> AddApplicationAsync(
            Guid branchId, Guid recalledByUserId, NampCommitteeTier tier = NampCommitteeTier.Branch)
        {
            var app = NampApplication.Create(
                "NAMP-2026-TEST0001", Guid.NewGuid(), Guid.NewGuid().ToString("N"),
                "Aisha Bello", "0000000029", NampApplicantCategory.YouthAgripreneur,
                "Tractor", 3_500_000m, branchId, tier, recalledByUserId).Value;
            await NampRepo.AddAsync(app);
            await Context.SaveChangesAsync();
            return app;
        }

        public async Task SeedConfigAsync(NampApplicationStatus status, string displayName, string role, int slaHours, bool terminal)
        {
            await ConfigRepo.AddAsync(NampWorkflowConfig.Create(status, displayName, displayName, role, slaHours, 0, terminal));
            await Context.SaveChangesAsync();
        }

        public void Dispose() => Context.Dispose();
    }

    [Fact]
    public async Task SeedData_AppliesNampWorkflowTemplates()
    {
        using var h = new Harness();
        foreach (var code in new[] { "NAMP_NEW_IN_QUEUE", "NAMP_ACTION_REQUIRED", "NAMP_COMMITTEE_VOTE", "NAMP_DECLINED" })
            Assert.True(
                await h.Context.NotificationTemplates.AnyAsync(t => t.Code == code && t.Channel == NotificationChannel.Email),
                $"Template '{code}' should be seeded by SeedData");
    }

    [Fact]
    public async Task StagingRecordReceived_QueuesNewInQueue_ToBranchLoanOfficers()
    {
        using var h = new Harness();
        var branchId = Guid.NewGuid();
        var lo = await h.AddUserAsync("lo@boa.ng", Roles.LoanOfficer, branchId);
        // A loan officer at a different branch must NOT be notified (scoping).
        await h.AddUserAsync("lo-other@boa.ng", Roles.LoanOfficer, Guid.NewGuid());

        var record = await h.AddStagingRecordAsync(branchId);

        await h.StagingHandler.HandleAsync(new NampStagingRecordReceivedEvent(record.Id));

        var notifs = await h.Context.Notifications.Where(n => n.TemplateCode == "NAMP_NEW_IN_QUEUE").ToListAsync();
        Assert.Single(notifs);
        Assert.Equal("lo@boa.ng", notifs[0].RecipientAddress);
        Assert.Contains(record.CrmsApplicationNumber, notifs[0].Body);
        Assert.Contains("https://crms.test/namp", notifs[0].BodyHtml);

        await h.Orchestrator.ProcessPendingAsync();
        Assert.Contains(h.Sender.Sent, m => m.RecipientAddress == "lo@boa.ng");
        _output.WriteLine($"New-in-queue queued to {notifs[0].RecipientAddress}: {notifs[0].Subject}");
    }

    [Fact]
    public async Task StagingRecordReceived_WhenAlreadyRecalled_QueuesNothing()
    {
        using var h = new Harness();
        var branchId = Guid.NewGuid();
        await h.AddUserAsync("lo@boa.ng", Roles.LoanOfficer, branchId);
        var record = await h.AddStagingRecordAsync(branchId);
        record.MarkRecalled(Guid.NewGuid());
        h.StagingRepo.Update(record);
        await h.Context.SaveChangesAsync();

        await h.StagingHandler.HandleAsync(new NampStagingRecordReceivedEvent(record.Id));

        Assert.False(await h.Context.Notifications.AnyAsync(n => n.TemplateCode == "NAMP_NEW_IN_QUEUE"));
    }

    [Fact]
    public async Task Submitted_QueuesActionRequired_ToBranchScopedCreditOfficer()
    {
        using var h = new Harness();
        var branchId = Guid.NewGuid();
        var lo = await h.AddUserAsync("lo@boa.ng", Roles.LoanOfficer, branchId);
        var creditOfficer = await h.AddUserAsync("credit@boa.ng", Roles.CreditOfficer, branchId);
        // A credit officer at another branch must NOT be notified (scoping).
        await h.AddUserAsync("credit-other@boa.ng", Roles.CreditOfficer, Guid.NewGuid());

        var app = await h.AddApplicationAsync(branchId, lo.Id);
        await h.SeedConfigAsync(NampApplicationStatus.Submitted, "Financial Appraisal", Roles.CreditOfficer, 168, terminal: false);

        await h.Handler.HandleAsync(new NampStatusChangedEvent(app.Id, NampApplicationStatus.Submitted, lo.Id, null));

        var notifs = await h.Context.Notifications.Where(n => n.TemplateCode == "NAMP_ACTION_REQUIRED").ToListAsync();
        Assert.Single(notifs);
        Assert.Equal("credit@boa.ng", notifs[0].RecipientAddress);
        Assert.Contains(app.ApplicationNumber, notifs[0].Body);
        Assert.Contains("Financial Appraisal", notifs[0].Body);
        Assert.Contains("https://crms.test/namp/" + app.Id, notifs[0].BodyHtml);

        await h.Orchestrator.ProcessPendingAsync();
        Assert.Contains(h.Sender.Sent, m => m.RecipientAddress == "credit@boa.ng");
        _output.WriteLine($"Action-required queued to {notifs[0].RecipientAddress}: {notifs[0].Subject}");
    }

    [Fact]
    public async Task Ratification_RoutesToTierManager_NotGenericFinalApprover()
    {
        using var h = new Harness();
        var branchId = Guid.NewGuid();
        var lo = await h.AddUserAsync("lo@boa.ng", Roles.LoanOfficer, branchId);
        var zonalManager = await h.AddUserAsync("zonal@boa.ng", Roles.ZonalManager, branchId);
        // A generic FinalApprover must NOT be notified for a tier-routed ratification.
        await h.AddUserAsync("final@boa.ng", Roles.FinalApprover, branchId);

        var app = await h.AddApplicationAsync(branchId, lo.Id, NampCommitteeTier.Zonal);
        await h.SeedConfigAsync(NampApplicationStatus.Ratification, "Ratification", Roles.FinalApprover, 48, terminal: false);

        await h.Handler.HandleAsync(new NampStatusChangedEvent(app.Id, NampApplicationStatus.Ratification, lo.Id, null));

        var notifs = await h.Context.Notifications.Where(n => n.TemplateCode == "NAMP_ACTION_REQUIRED").ToListAsync();
        Assert.Single(notifs);
        Assert.Equal("zonal@boa.ng", notifs[0].RecipientAddress);
        _output.WriteLine($"Ratification routed to tier manager: {notifs[0].RecipientAddress}");
    }

    [Fact]
    public async Task CommitteeCirculation_QueuesVoteRequest_ToEachMember()
    {
        using var h = new Harness();
        var branchId = Guid.NewGuid();
        var lo = await h.AddUserAsync("lo@boa.ng", Roles.LoanOfficer, branchId);
        var member1 = await h.AddUserAsync("m1@boa.ng", Roles.CreditOfficer, branchId);
        var member2 = await h.AddUserAsync("m2@boa.ng", Roles.CreditOfficer, branchId);

        var app = await h.AddApplicationAsync(branchId, lo.Id);

        var review = CommitteeReview.Create(app.Id, app.ApplicationNumber, CommitteeType.BranchCredit, lo.Id, 2, 2).Value;
        review.AddMember(member1.Id, member1.FullName, "Member");
        review.AddMember(member2.Id, member2.FullName, "Member");
        await h.CommitteeRepo.AddAsync(review);
        // Link the review onto the application (set by CirculateToCommittee in the real flow).
        typeof(NampApplication).GetProperty(nameof(NampApplication.CurrentCommitteeReviewId))!
            .SetValue(app, review.Id);
        h.NampRepo.Update(app);
        await h.Context.SaveChangesAsync();

        await h.Handler.HandleAsync(new NampStatusChangedEvent(
            app.Id, NampApplicationStatus.BranchCommitteeCirculation, lo.Id, null));

        var notifs = await h.Context.Notifications.Where(n => n.TemplateCode == "NAMP_COMMITTEE_VOTE").ToListAsync();
        Assert.Equal(2, notifs.Count);
        Assert.Contains(notifs, n => n.RecipientAddress == "m1@boa.ng");
        Assert.Contains(notifs, n => n.RecipientAddress == "m2@boa.ng");
        _output.WriteLine($"Committee vote requests queued to {notifs.Count} members");
    }

    [Fact]
    public async Task FinancialDeclined_QueuesDecline_ToOriginatingLoanOfficer_WithReason()
    {
        using var h = new Harness();
        var branchId = Guid.NewGuid();
        var lo = await h.AddUserAsync("lo@boa.ng", Roles.LoanOfficer, branchId);
        var app = await h.AddApplicationAsync(branchId, lo.Id);
        await h.SeedConfigAsync(NampApplicationStatus.FinancialDeclined, "Financial Appraisal", Roles.CreditOfficer, 0, terminal: true);

        await h.Handler.HandleAsync(new NampStatusChangedEvent(
            app.Id, NampApplicationStatus.FinancialDeclined, lo.Id, "Insufficient repayment capacity"));

        var notif = await h.Context.Notifications.FirstOrDefaultAsync(n => n.TemplateCode == "NAMP_DECLINED");
        Assert.NotNull(notif);
        Assert.Equal("lo@boa.ng", notif!.RecipientAddress);
        Assert.Contains("Insufficient repayment capacity", notif.Body);
        Assert.Contains(app.ApplicationNumber, notif.Body);
        _output.WriteLine($"Decline queued to {notif.RecipientAddress}: {notif.Subject}");
    }

    [Fact]
    public async Task NonNotifiableStatus_QueuesNothing()
    {
        using var h = new Harness();
        var branchId = Guid.NewGuid();
        var lo = await h.AddUserAsync("lo@boa.ng", Roles.LoanOfficer, branchId);
        var app = await h.AddApplicationAsync(branchId, lo.Id);

        await h.Handler.HandleAsync(new NampStatusChangedEvent(app.Id, NampApplicationStatus.Draft, lo.Id, null));
        await h.Handler.HandleAsync(new NampStatusChangedEvent(app.Id, NampApplicationStatus.FinancialAppraisal, lo.Id, null));

        Assert.False(await h.Context.Notifications.AnyAsync());
    }
}

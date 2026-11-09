using CRMS.Application.Billing;
using CRMS.Application.Billing.Alerts;
using CRMS.Application.Notification.Commands;
using CRMS.Application.Notification.Interfaces;
using CRMS.Domain.Aggregates.Billing;
using CRMS.Domain.Enums;
using CRMS.Infrastructure.Identity;
using CRMS.Infrastructure.Persistence;
using CRMS.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CRMS.Infrastructure.Tests;

/// <summary>The billing checks and how their findings become, refresh and resolve alerts.</summary>
public class BillingAlertTests
{
    private sealed class Harness : IDisposable
    {
        public CRMSDbContext Db { get; }
        public BillingUsageRepository Usage { get; }
        public BillingRateRepository Rates { get; }
        public BillingStatementRepository Statements { get; }
        public BillingAlertRepository Alerts { get; }
        public UserRepository Users { get; }
        public BillingSettings Settings { get; } = new();
        public Mock<INotificationService> Notifications { get; } = new();
        public DateTime Now { get; set; } = new(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);
        public Guid ManagerId { get; }

        public Harness(string name)
        {
            Db = new CRMSDbContext(new DbContextOptionsBuilder<CRMSDbContext>().UseInMemoryDatabase("BillingAlerts_" + name + Guid.NewGuid()).Options);
            SeedData.SeedAsync(Db, NullLogger.Instance).GetAwaiter().GetResult();
            Usage = new BillingUsageRepository(Db);
            Rates = new BillingRateRepository(Db);
            Statements = new BillingStatementRepository(Db);
            Alerts = new BillingAlertRepository(Db);
            Users = new UserRepository(Db);
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Billing:Manager:Email"] = "billing@vendor.test" }).Build();
            BillingSeeder.SeedManagerAsync(Db, config, new PasswordHasher(), NullLogger.Instance).GetAwaiter().GetResult();
            ManagerId = Db.Users.Single(u => u.Email == "billing@vendor.test").Id;
        }

        public BillingAlertChecks Checks => new(Usage, Rates, Statements, Settings, () => Now);

        public RunBillingAlertChecksHandler Runner
        {
            get
            {
                var sender = new Mock<IEmailSenderInfo>();
                sender.Setup(s => s.Get()).Returns(new EmailSenderInfoDto("Mock", true, "noreply@test", "CRMS", "eu-west-1", "https://crms.test/"));
                return new RunBillingAlertChecksHandler(Checks, Alerts, Users, Db, Notifications.Object, sender.Object, Settings, NullLogger<RunBillingAlertChecksHandler>.Instance);
            }
        }

        public async Task Calls(string item, DateTime at, int n, bool ok = true, BillingFlow flow = BillingFlow.Namp)
        {
            for (var i = 0; i < n; i++)
                Db.BillingUsages.Add(BillingUsage.Create(item, flow, Guid.NewGuid(), "R", null, null, ok, ok ? null : "boom", "h", null, at, BillingUsageSource.Live, null, null).Value);
            await Db.SaveChangesAsync();
        }

        public async Task Rate(string item, DateTime from, decimal price)
        {
            Db.BillingRates.Add(BillingRate.Create(item, null, from, price, 0m, null, ManagerId).Value);
            await Db.SaveChangesAsync();
        }

        public void Dispose() => Db.Dispose();
    }

    private static DateTime Utc(int y, int m, int d, int h = 12) => new(y, m, d, h, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task A_service_well_above_its_norm_is_a_spike_and_a_quiet_one_is_not()
    {
        using var h = new Harness(nameof(A_service_well_above_its_norm_is_a_spike_and_a_quiet_one_is_not));
        await h.Rate(BillingItems.CrcFull, Utc(2026, 1, 1), 100m);
        await h.Rate(BillingItems.Bvn, Utc(2026, 1, 1), 10m);
        // Norm: 30 CRC full reports a month over the last three months.
        foreach (var m in new[] { 8, 9, 10 }) await h.Calls(BillingItems.CrcFull, Utc(2026, m, 5), 30);
        // Half-way through November: 60 already, on course for ~120, four times the norm.
        await h.Calls(BillingItems.CrcFull, Utc(2026, 11, 3), 60);
        // BVN: 5 this month, below the floor of 10 calls, so it can never spike however it scales.
        foreach (var m in new[] { 8, 9, 10 }) await h.Calls(BillingItems.Bvn, Utc(2026, m, 5), 10);
        await h.Calls(BillingItems.Bvn, Utc(2026, 11, 3), 5);

        var findings = await h.Checks.RunAsync();

        var spike = Assert.Single(findings, f => f.Kind == BillingAlertKind.SpendSpike);
        Assert.Equal(BillingAlertSeverity.Critical, spike.Severity);
        Assert.Contains("CRC full report", spike.Title);
        Assert.Equal("spike:bureau:crc-full:2026-11", spike.Key);
        Assert.DoesNotContain(findings, f => f.Kind == BillingAlertKind.UnpricedActivity);
        // November is open and no statement exists for October, which had rows: that one note is expected.
        var unclosed = Assert.Single(findings, f => f.Kind == BillingAlertKind.MonthNotClosed);
        Assert.Equal(BillingAlertSeverity.Warning, unclosed.Severity);     // the 15th is past the grace days
    }

    [Fact]
    public async Task Failing_unattributed_and_unpriced_calls_each_raise_their_own_finding()
    {
        using var h = new Harness(nameof(Failing_unattributed_and_unpriced_calls_each_raise_their_own_finding));
        await h.Rate(BillingItems.CrcFull, Utc(2026, 1, 1), 100m);
        await h.Calls(BillingItems.CrcFull, h.Now.AddHours(-10), 2, ok: true);
        await h.Calls(BillingItems.CrcFull, h.Now.AddHours(-9), 6, ok: false);        // 75% failed in the window
        await h.Calls(BillingItems.CrcFull, h.Now.AddDays(-5), 20, ok: false);        // outside the 2-day window, ignored
        await h.Calls(BillingItems.Tin, h.Now.AddHours(-1), 3, flow: BillingFlow.Unattributed);   // no price, no case

        var findings = await h.Checks.RunAsync();

        var failing = Assert.Single(findings, f => f.Kind == BillingAlertKind.HighFailureRate);
        Assert.Contains("75%", failing.Title);
        Assert.Equal("/admin/diagnostics", failing.Link);
        var unattributed = Assert.Single(findings, f => f.Kind == BillingAlertKind.UnattributedCalls);
        Assert.Contains("3 call(s)", unattributed.Title);
        var unpriced = Assert.Single(findings, f => f.Kind == BillingAlertKind.UnpricedActivity);
        Assert.Contains("TIN verification", unpriced.Title);
        Assert.Equal("/billing/rates", unpriced.Link);
        Assert.DoesNotContain(findings, f => f.Kind == BillingAlertKind.SpendSpike);    // no history, nothing to compare
    }

    [Fact]
    public async Task An_issued_statement_past_its_due_date_is_overdue_and_a_closed_month_raises_nothing()
    {
        using var h = new Harness(nameof(An_issued_statement_past_its_due_date_is_overdue_and_a_closed_month_raises_nothing));
        var statement = BillingStatement.Create("BILL-202609-01", Utc(2026, 9, 1), "NGN", 7.5m, h.ManagerId, "Vera", null).Value;
        statement.AddLine(Utc(2026, 9, 1), BillingItems.CrcFull, "CRC full report", BillingCategory.CreditBureau, BillingFlow.Namp, 10, 100m, 0m);
        h.Db.BillingStatements.Add(statement);
        var october = BillingStatement.Create("BILL-202610-01", Utc(2026, 10, 1), "NGN", 7.5m, h.ManagerId, "Vera", null).Value;
        october.MarkPaid(DateTime.UtcNow, h.ManagerId, "TRF-OCT", null);
        h.Db.BillingStatements.Add(october);
        await h.Db.SaveChangesAsync();
        await h.Calls(BillingItems.CrcFull, Utc(2026, 10, 5), 3);                      // October rows, billed on the October statement
        await h.Usage.AssignUnbilledAsync(Utc(2026, 10, 1).Date, october.Id);

        h.Now = statement.IssuedAt.AddDays(45);                                        // 15 days past 30-day terms
        var findings = await h.Checks.RunAsync();

        var overdue = Assert.Single(findings, f => f.Kind == BillingAlertKind.OverdueStatement);
        Assert.Contains("BILL-202609-01", overdue.Title);
        Assert.Equal(BillingAlertSeverity.Warning, overdue.Severity);
        Assert.DoesNotContain(findings, f => f.Kind == BillingAlertKind.MonthNotClosed);

        Assert.True(statement.MarkPaid(DateTime.UtcNow, h.ManagerId, "TRF", null).IsSuccess);
        await h.Db.SaveChangesAsync();
        Assert.DoesNotContain(await h.Checks.RunAsync(), f => f.Kind == BillingAlertKind.OverdueStatement);
    }

    [Fact]
    public async Task Findings_become_alerts_once_refresh_in_place_email_managers_only_when_new_and_resolve_when_gone()
    {
        using var h = new Harness(nameof(Findings_become_alerts_once_refresh_in_place_email_managers_only_when_new_and_resolve_when_gone));
        await h.Calls(BillingItems.Nin, h.Now.AddHours(-2), 4, flow: BillingFlow.Unattributed);   // unattributed + unpriced; October had no rows, so no unclosed month

        var first = (await h.Runner.Handle(new RunBillingAlertChecksCommand(null))).Data!;
        Assert.Equal(2, first.Findings);
        Assert.Equal(2, first.Added);
        Assert.Equal(1, first.Emailed);
        h.Notifications.Verify(n => n.SendEmailAsync(NotificationType.SystemAlert, "billing@vendor.test", It.IsAny<string>(), BillingAlertNames.TemplateCode,
            It.Is<Dictionary<string, string>>(v => v["Count"] == "2" && v["AlertListHtml"].Contains("NIN verification") && v["DashboardLink"] == "https://crms.test/billing"),
            It.IsAny<Guid?>(), null, null, NotificationPriority.Normal, It.IsAny<CancellationToken>()), Times.Once);

        var second = (await h.Runner.Handle(new RunBillingAlertChecksCommand(null))).Data!;
        Assert.Equal(0, second.Added);
        Assert.Equal(2, second.Updated);
        Assert.Equal(0, second.Emailed);                                               // nothing new, no second email
        var alerts = await h.Alerts.GetActiveAsync();
        Assert.Equal(2, alerts.Count);
        Assert.All(alerts, a => Assert.Equal(2, a.TimesSeen));
        Assert.All(alerts, a => Assert.NotNull(a.NotifiedAt));

        // The manager dismisses the unattributed one; it stays dismissed while the condition persists.
        var act = new ActOnBillingAlertHandler(h.Alerts, h.Users, h.Db, h.Settings);
        var unattributed = alerts.Single(a => a.Kind == BillingAlertKind.UnattributedCalls);
        Assert.True((await act.Handle(new ActOnBillingAlertCommand(unattributed.Id, "dismiss", h.ManagerId))).IsSuccess);
        await h.Runner.Handle(new RunBillingAlertChecksCommand(null));
        Assert.Equal(BillingAlertStatus.Dismissed, (await h.Alerts.GetByIdAsync(unattributed.Id))!.Status);
        var visible = (await new GetBillingAlertsHandler(h.Alerts).Handle(new GetBillingAlertsQuery(IncludeHistory: false))).Data!;
        Assert.Single(visible);                                                        // dashboard hides the dismissed one

        // Price the item: the unpriced alert resolves on the next run, the others remain.
        await h.Rate(BillingItems.Nin, Utc(2026, 1, 1), 50m);
        var third = (await h.Runner.Handle(new RunBillingAlertChecksCommand(null))).Data!;
        Assert.Equal(1, third.Resolved);
        var resolved = (await h.Alerts.GetRecentAsync(10)).Single(a => a.Kind == BillingAlertKind.UnpricedActivity);
        Assert.Equal(BillingAlertStatus.Resolved, resolved.Status);
        Assert.NotNull(resolved.ResolvedAt);

        // A bank administrator may not snooze or run the checks by hand.
        var admin = Db_AdminId(h);
        Assert.False((await act.Handle(new ActOnBillingAlertCommand(unattributed.Id, "snooze", admin))).IsSuccess);
        Assert.False((await h.Runner.Handle(new RunBillingAlertChecksCommand(admin))).IsSuccess);
    }

    private static Guid Db_AdminId(Harness h)
    {
        var admin = CRMS.Domain.Entities.Identity.ApplicationUser.Create("admin@bank.test", "admin", "Ada", "Admin", CRMS.Domain.Entities.Identity.UserType.Staff, null, null).Value;
        admin.SetPasswordHash(new PasswordHasher().HashPassword("Password1$$$"));
        admin.AddRole(h.Db.Roles.Single(r => r.Name == CRMS.Domain.Constants.Roles.SystemAdmin));
        h.Db.Users.Add(admin);
        h.Db.SaveChanges();
        return admin.Id;
    }
}

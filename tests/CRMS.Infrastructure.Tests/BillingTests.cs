using CRMS.Application.Billing;
using CRMS.Application.Billing.Commands;
using CRMS.Application.Billing.Queries;
using CRMS.Application.Common;
using CRMS.Application.Identity.Commands;
using CRMS.Application.Identity.Interfaces;
using CRMS.Application.Notification.Interfaces;
using CRMS.Domain.Aggregates.Billing;
using CRMS.Domain.Common;
using CRMS.Domain.Constants;
using CRMS.Domain.Entities.Identity;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using CRMS.Infrastructure.ExternalServices.SmartComply;
using CRMS.Infrastructure.Identity;
using CRMS.Infrastructure.Persistence;
using CRMS.Infrastructure.Persistence.Repositories;
using CRMS.Infrastructure.Services.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CRMS.Infrastructure.Tests;

/// <summary>
/// Billing: what the bank owes the vendor. The ledger, the dated price list, the loan outcome
/// classification, the vendor-only role and the dashboard arithmetic.
/// </summary>
public class BillingTests
{
    // ───────────────────────────── Fixtures ─────────────────────────────

    private sealed class RecordingLedger : IBillingLedger
    {
        public List<(BillingUsageDraft Draft, BillingScope? Scope)> Rows { get; } = [];
        public Task RecordAsync(BillingUsageDraft draft, CancellationToken ct = default)
        {
            Rows.Add((draft, draft.Scope ?? BillingContext.Current));
            return Task.CompletedTask;
        }
    }

    private sealed class Harness : IDisposable
    {
        public CRMSDbContext Db { get; }
        public UserRepository Users { get; }
        public RoleRepository Roles { get; }
        public BillingUsageRepository Usage { get; }
        public BillingRateRepository Rates { get; }
        public PasswordHasher Hasher { get; } = new();

        public Harness(string name)
        {
            Db = new CRMSDbContext(new DbContextOptionsBuilder<CRMSDbContext>().UseInMemoryDatabase("Billing_" + name + Guid.NewGuid()).Options);
            SeedData.SeedAsync(Db, NullLogger.Instance).GetAwaiter().GetResult();
            Users = new UserRepository(Db);
            Roles = new RoleRepository(Db);
            Usage = new BillingUsageRepository(Db);
            Rates = new BillingRateRepository(Db);
        }

        public async Task<ApplicationUser> SeedManagerAsync(string email = "billing@vendor.test")
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Billing:Manager:Email"] = email, ["Billing:Manager:FirstName"] = "Vera", ["Billing:Manager:LastName"] = "Vendor",
            }).Build();
            await BillingSeeder.SeedManagerAsync(Db, config, Hasher, NullLogger.Instance);
            return (await Users.GetByEmailAsync(email))!;
        }

        public async Task<ApplicationUser> SeedStaffAsync(string email, string role)
        {
            var user = ApplicationUser.Create(email, email.Split('@')[0], "Staff", "Member", UserType.Staff, null, null).Value;
            user.SetPasswordHash(Hasher.HashPassword("Password1$$$"));
            user.AddRole((await Roles.GetByNameAsync(role))!);
            await Users.AddAsync(user);
            await Db.SaveChangesAsync();
            return user;
        }

        public async Task AddUsageAsync(string item, BillingFlow flow, DateTime at, bool ok = true, Guid? caseId = null, string? reference = null, BillingUsageSource source = BillingUsageSource.Live, string? key = null)
        {
            var row = BillingUsage.Create(item, flow, caseId, reference, null, null, ok, ok ? null : "boom", "host", null, at, source, key, null).Value;
            Db.BillingUsages.Add(row);
            await Db.SaveChangesAsync();
        }

        public void Dispose() => Db.Dispose();
    }

    private static DateTime Utc(int y, int m, int d = 1) => new(y, m, d, 12, 0, 0, DateTimeKind.Utc);

    // ───────────────────────────── Price book ─────────────────────────────

    [Fact]
    public void The_price_in_force_is_the_latest_dated_rate_not_after_the_month_flow_specific_first()
    {
        var who = Guid.NewGuid();
        var rates = new[]
        {
            BillingRate.Create(BillingItems.CrcFull, null, Utc(2026, 1), 500m, 300m, null, who).Value,
            BillingRate.Create(BillingItems.CrcFull, null, Utc(2026, 6), 650m, 320m, "renegotiated", who).Value,
            BillingRate.Create(BillingItems.CrcFull, BillingFlow.Namp, Utc(2026, 3), 700m, 300m, null, who).Value,
            BillingRate.Create(BillingItems.LoanBooked, BillingFlow.Rhshf, Utc(2026, 1), 2000m, 0m, null, who).Value,
        };
        var book = new BillingPriceBook(rates);

        Assert.Equal(0m, book.Resolve(BillingItems.CrcFull, BillingFlow.Corporate, Utc(2025, 12)).UnitPrice);   // before any rate
        Assert.Equal(500m, book.Resolve(BillingItems.CrcFull, BillingFlow.Corporate, Utc(2026, 4)).UnitPrice);
        Assert.Equal(650m, book.Resolve(BillingItems.CrcFull, BillingFlow.Corporate, Utc(2026, 9)).UnitPrice);
        Assert.Equal(700m, book.Resolve(BillingItems.CrcFull, BillingFlow.Namp, Utc(2026, 9)).UnitPrice);        // NAMP override beats the newer all-flows rate
        Assert.Equal(500m, book.Resolve(BillingItems.CrcFull, BillingFlow.Namp, Utc(2026, 2)).UnitPrice);        // before the override existed
        Assert.Equal(2000m, book.Resolve(BillingItems.LoanBooked, BillingFlow.Rhshf, Utc(2026, 7)).UnitPrice);
        Assert.Equal(0m, book.Resolve(BillingItems.LoanBooked, BillingFlow.Corporate, Utc(2026, 7)).UnitPrice);  // a platform fee never leaks across flows
        Assert.False(book.Resolve(BillingItems.Bvn, BillingFlow.Corporate, Utc(2026, 7)).IsSet);
    }

    [Fact]
    public void Rates_are_whole_months_two_decimals_and_never_negative()
    {
        var who = Guid.NewGuid();
        var rate = BillingRate.Create(BillingItems.Bvn, null, new DateTime(2026, 11, 17, 8, 30, 0, DateTimeKind.Utc), 50m, 20m, null, who).Value;
        Assert.Equal(Utc(2026, 11).Date, rate.EffectiveFrom);
        Assert.True(BillingRate.Create(BillingItems.Bvn, null, Utc(2026, 1), -1m, 0m, null, who).IsFailure);
        Assert.True(BillingRate.Create(BillingItems.Bvn, null, Utc(2026, 1), 1.005m, 0m, null, who).IsFailure);
        Assert.True(BillingRate.Create("not:an-item", null, Utc(2026, 1), 1m, 0m, null, who).IsFailure);
    }

    // ───────────────────────────── Loan classification ─────────────────────────────

    [Fact]
    public void Each_track_maps_to_booked_declined_cancelled_or_processing()
    {
        Assert.Equal(BillingLoanOutcome.Booked, BillingLoanClassifier.Corporate(LoanApplicationStatus.Disbursed, false));
        Assert.Equal(BillingLoanOutcome.Booked, BillingLoanClassifier.Corporate(LoanApplicationStatus.Disbursement, true));
        Assert.Equal(BillingLoanOutcome.Declined, BillingLoanClassifier.Corporate(LoanApplicationStatus.RatificationDeclined, false));
        Assert.Equal(BillingLoanOutcome.Cancelled, BillingLoanClassifier.Corporate(LoanApplicationStatus.Cancelled, false));
        Assert.Equal(BillingLoanOutcome.Processing, BillingLoanClassifier.Corporate(LoanApplicationStatus.CreditReview, false));

        Assert.Equal(BillingLoanOutcome.Booked, BillingLoanClassifier.Namp(NampApplicationStatus.Active, true));
        Assert.Equal(BillingLoanOutcome.Declined, BillingLoanClassifier.Namp(NampApplicationStatus.ZonalCommitteeDeclined, false));
        Assert.Equal(BillingLoanOutcome.Cancelled, BillingLoanClassifier.Namp(NampApplicationStatus.OfferLapsed, false));
        Assert.Equal(BillingLoanOutcome.Processing, BillingLoanClassifier.Namp(NampApplicationStatus.Ratification, false));

        Assert.Equal(BillingLoanOutcome.Booked, BillingLoanClassifier.Rhshf(RhshfCaseStatus.Approved, true));
        Assert.Equal(BillingLoanOutcome.Processing, BillingLoanClassifier.Rhshf(RhshfCaseStatus.Approved, false));
        Assert.Equal(BillingLoanOutcome.Declined, BillingLoanClassifier.Rhshf(RhshfCaseStatus.Declined, false));
        Assert.Equal(BillingLoanOutcome.Cancelled, BillingLoanClassifier.Rhshf(RhshfCaseStatus.Expired, false));
    }

    // ───────────────────────────── The SmartComply wrapper ─────────────────────────────

    [Fact]
    public async Task Every_call_leaves_a_row_with_the_ambient_scope_success_or_failure_and_the_access_check_is_free()
    {
        var inner = new Mock<ISmartComplyProvider>();
        inner.Setup(p => p.GetCRCFullAsync("12345678901", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new SmartComplyIndividualCreditReport(null, null, null, null, null, null, null, null, null!, [], [], [], [], null)));
        inner.Setup(p => p.VerifyBvnAsync("22222222222", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<SmartComplyBvnResult>("No record found"));
        inner.Setup(p => p.GetBusinessCreditRawJsonAsync("first_central", "RC123", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("gateway down"));
        inner.Setup(p => p.CheckAccessAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SmartComplyAccessCheck(true, "ok", "h", "u", "k"));
        var ledger = new RecordingLedger();
        var billed = new BilledSmartComplyProvider(inner.Object, ledger);
        var caseId = Guid.NewGuid();

        using (BillingContext.Begin(BillingFlow.Namp, caseId, "NAMP-0001", "Applicant"))
        {
            await billed.GetCRCFullAsync("12345678901");
            using (BillingContext.Subject("Director"))
                await billed.VerifyBvnAsync("22222222222");
            await Assert.ThrowsAsync<HttpRequestException>(() => billed.GetBusinessCreditRawJsonAsync("first_central", "RC123"));
            await billed.CheckAccessAsync();
        }
        await billed.GetCRCFullAsync("12345678901");   // outside any scope

        Assert.Equal(4, ledger.Rows.Count);
        var (full, fullScope) = ledger.Rows[0];
        Assert.Equal(BillingItems.CrcFull, full.Item);
        Assert.True(full.Succeeded);
        Assert.Equal("…8901", full.SubjectHint);
        Assert.Equal(BillingFlow.Namp, fullScope!.Flow);
        Assert.Equal(caseId, fullScope.CaseId);
        Assert.Equal("Applicant", fullScope.SubjectType);

        var (bvn, bvnScope) = ledger.Rows[1];
        Assert.Equal(BillingItems.Bvn, bvn.Item);
        Assert.False(bvn.Succeeded);                       // billed all the same
        Assert.Equal("No record found", bvn.Detail);
        Assert.Equal("Director", bvnScope!.SubjectType);
        Assert.Equal("NAMP-0001", bvnScope.CaseReference);  // the case survives the narrower subject scope

        var (raw, _) = ledger.Rows[2];
        Assert.Equal(BillingItems.BusinessFirstCentral, raw.Item);
        Assert.False(raw.Succeeded);
        Assert.Contains("gateway down", raw.Detail);

        Assert.Null(ledger.Rows[3].Scope);                  // the ledger will file it as Unattributed
    }

    [Fact]
    public async Task The_ledger_files_an_unscoped_call_as_unattributed_and_a_scoped_one_under_its_case()
    {
        using var h = new Harness(nameof(The_ledger_files_an_unscoped_call_as_unattributed_and_a_scoped_one_under_its_case));
        var scopes = new Mock<IServiceScopeFactory>();
        var scope = new Mock<IServiceScope>();
        var sp = new Mock<IServiceProvider>();
        sp.Setup(s => s.GetService(typeof(CRMSDbContext))).Returns(h.Db);
        scope.Setup(s => s.ServiceProvider).Returns(sp.Object);
        scopes.Setup(s => s.CreateScope()).Returns(scope.Object);
        var ledger = new BillingLedger(scopes.Object, NullLogger<BillingLedger>.Instance);
        var caseId = Guid.NewGuid();

        await ledger.RecordAsync(new BillingUsageDraft(BillingItems.Tin, true));
        using (BillingContext.Begin(BillingFlow.Rhshf, caseId, "RS-0007", "Company"))
            await ledger.RecordAsync(new BillingUsageDraft(BillingItems.CacAdvanced, false, "Authorization Failed", "RC9"));

        var rows = await h.Db.BillingUsages.OrderBy(u => u.Item).ToListAsync();
        Assert.Equal(2, rows.Count);
        var cac = rows.Single(r => r.Item == BillingItems.CacAdvanced);
        Assert.Equal(BillingFlow.Rhshf, cac.Flow);
        Assert.Equal(caseId, cac.CaseId);
        Assert.Equal("RS-0007", cac.CaseReference);
        Assert.Equal(BillingCategory.Identity, cac.Category);
        Assert.Equal(BillingUsage.MonthOf(DateTime.UtcNow), cac.Month);
        Assert.Equal(BillingUsageSource.Live, cac.Source);
        Assert.Equal(BillingFlow.Unattributed, rows.Single(r => r.Item == BillingItems.Tin).Flow);
    }

    // ───────────────────────────── The vendor role ─────────────────────────────

    [Fact]
    public async Task The_first_manager_comes_from_configuration_once_and_only_once()
    {
        using var h = new Harness(nameof(The_first_manager_comes_from_configuration_once_and_only_once));
        var first = await h.SeedManagerAsync("billing@vendor.test");
        Assert.True(BillingAccess.IsManager(first));
        Assert.Equal(UserStatus.Active, first.Status);
        Assert.False(h.Hasher.VerifyPassword("", first.PasswordHash));

        await h.SeedManagerAsync("someone-else@vendor.test");    // a later setting change adds nobody
        Assert.Null(await h.Users.GetByEmailAsync("someone-else@vendor.test"));
        Assert.Single(await h.Users.GetByRoleAsync(Roles.BillingManager));
    }

    [Fact]
    public async Task Bank_administrators_cannot_grant_edit_or_deactivate_the_vendor_role()
    {
        using var h = new Harness(nameof(Bank_administrators_cannot_grant_edit_or_deactivate_the_vendor_role));
        var manager = await h.SeedManagerAsync();
        var perms = new PermissionRepository(h.Db);
        var notifications = new Mock<INotificationService>();

        var register = new RegisterUserHandler(h.Users, h.Roles, perms, h.Hasher, h.Db, notifications.Object, NullLogger<RegisterUserHandler>.Instance);
        var granted = await register.Handle(new RegisterUserCommand("sneaky@bank.test", "sneaky", "Welcome@1234", "S", "A", UserType.Staff, null, null, [Roles.SystemAdmin, Roles.BillingManager]));
        Assert.False(granted.IsSuccess);
        Assert.Contains("Billing > Access", granted.Error);
        Assert.Null(await h.Users.GetByEmailAsync("sneaky@bank.test"));

        var admin = await h.SeedStaffAsync("admin@bank.test", Roles.SystemAdmin);
        var update = new UpdateUserHandler(h.Users, h.Roles, perms, h.Db);
        var selfGrant = await update.Handle(new UpdateUserCommand(admin.Id, "S", "A", null, null, [Roles.SystemAdmin, Roles.BillingManager]));
        Assert.False(selfGrant.IsSuccess);
        var editVendor = await update.Handle(new UpdateUserCommand(manager.Id, "Changed", "Name", null, null, [Roles.LoanOfficer]));
        Assert.False(editVendor.IsSuccess);
        Assert.Contains("Billing Manager account", editVendor.Error);

        var toggle = new ToggleUserStatusHandler(h.Users, h.Db);
        var deactivate = await toggle.Handle(new ToggleUserStatusCommand(manager.Id, Deactivate: true));
        Assert.False(deactivate.IsSuccess);

        var again = (await h.Users.GetByIdAsync(manager.Id))!;
        Assert.Equal("Vera", again.FirstName);
        Assert.Equal(UserStatus.Active, again.Status);
        Assert.True(BillingAccess.IsManager(again));
    }

    [Fact]
    public async Task A_manager_can_add_and_remove_managers_but_never_the_last_one_and_admins_cannot()
    {
        using var h = new Harness(nameof(A_manager_can_add_and_remove_managers_but_never_the_last_one_and_admins_cannot));
        var manager = await h.SeedManagerAsync();
        var admin = await h.SeedStaffAsync("admin@bank.test", Roles.SystemAdmin);
        var auth = new Mock<IAuthService>();
        auth.Setup(a => a.RequestPasswordResetAsync("second@vendor.test", "https://crms.test/reset-password", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApplicationResult.Success());
        var add = new AddBillingManagerHandler(h.Users, h.Roles, h.Hasher, auth.Object, h.Db, NullLogger<AddBillingManagerHandler>.Instance);
        var remove = new RemoveBillingManagerHandler(h.Users, h.Roles, h.Db);

        var byAdmin = await add.Handle(new AddBillingManagerCommand("second@vendor.test", "Sam", "Second", "https://crms.test/reset-password", admin.Id));
        Assert.False(byAdmin.IsSuccess);
        Assert.Contains("Only a Billing Manager", byAdmin.Error);

        var byManager = await add.Handle(new AddBillingManagerCommand("Second@Vendor.test", "Sam", "Second", "https://crms.test/reset-password", manager.Id));
        Assert.True(byManager.IsSuccess, byManager.Error);
        Assert.Contains("set their password", byManager.Data);
        auth.Verify(a => a.RequestPasswordResetAsync("second@vendor.test", "https://crms.test/reset-password", It.IsAny<CancellationToken>()), Times.Once);
        var second = (await h.Users.GetByEmailAsync("second@vendor.test"))!;
        Assert.True(BillingAccess.IsManager(second));

        // Grant to an existing bank account: it keeps its roles and gains this one, no email.
        var existing = await add.Handle(new AddBillingManagerCommand("admin@bank.test", "", "", "https://crms.test/reset-password", manager.Id));
        Assert.True(existing.IsSuccess, existing.Error);
        Assert.Contains("existing password", existing.Data);
        Assert.Equal(3, (await h.Users.GetByRoleAsync(Roles.BillingManager)).Count);

        Assert.True((await remove.Handle(new RemoveBillingManagerCommand(admin.Id, manager.Id))).IsSuccess);
        Assert.True((await remove.Handle(new RemoveBillingManagerCommand(second.Id, manager.Id))).IsSuccess);
        Assert.Equal(UserStatus.Inactive, (await h.Users.GetByIdAsync(second.Id))!.Status);          // no other role: deactivated
        Assert.Equal(UserStatus.Active, (await h.Users.GetByIdAsync(admin.Id))!.Status);             // still bank staff

        var last = await remove.Handle(new RemoveBillingManagerCommand(manager.Id, manager.Id));
        Assert.False(last.IsSuccess);
        Assert.Contains("at least one active Billing Manager", last.Error);
    }

    // ───────────────────────────── Rates ─────────────────────────────

    [Fact]
    public async Task Setting_a_rate_needs_a_manager_and_replaces_the_same_months_row_instead_of_adding_one()
    {
        using var h = new Harness(nameof(Setting_a_rate_needs_a_manager_and_replaces_the_same_months_row_instead_of_adding_one));
        var manager = await h.SeedManagerAsync();
        var admin = await h.SeedStaffAsync("admin@bank.test", Roles.SystemAdmin);
        var handler = new SetBillingRateHandler(h.Rates, h.Users, h.Db);

        Assert.False((await handler.Handle(new SetBillingRateCommand(BillingItems.CrcFull, null, Utc(2026, 11), 500m, 300m, null, admin.Id))).IsSuccess);
        Assert.True((await handler.Handle(new SetBillingRateCommand(BillingItems.CrcFull, null, Utc(2026, 11), 500m, 300m, null, manager.Id))).IsSuccess);
        Assert.True((await handler.Handle(new SetBillingRateCommand(BillingItems.CrcFull, null, Utc(2026, 11, 20), 550m, 300m, "typo fixed", manager.Id))).IsSuccess);
        Assert.True((await handler.Handle(new SetBillingRateCommand(BillingItems.CrcFull, null, Utc(2026, 12), 600m, 300m, null, manager.Id))).IsSuccess);
        var wrongShape = await handler.Handle(new SetBillingRateCommand(BillingItems.LoanBooked, null, Utc(2026, 11), 1000m, 0m, null, manager.Id));
        Assert.False(wrongShape.IsSuccess);
        Assert.Contains("one loan flow at a time", wrongShape.Error);
        Assert.False((await handler.Handle(new SetBillingRateCommand(BillingItems.CrcFull, BillingFlow.Namp, Utc(2026, 11), 1m, 0m, null, manager.Id))).IsSuccess);

        var rows = await h.Rates.GetAllAsync();
        Assert.Equal(2, rows.Count);
        Assert.Equal(550m, rows.Single(r => r.EffectiveFrom == Utc(2026, 11).Date).UnitPrice);
        Assert.Equal("typo fixed", rows.Single(r => r.EffectiveFrom == Utc(2026, 11).Date).Note);

        var sheet = (await new GetBillingRatesHandler(h.Rates).Handle(new GetBillingRatesQuery(IncludeCost: false))).Data!;
        var crc = sheet.Services.Single(s => s.Item == BillingItems.CrcFull);
        Assert.Null(crc.UnitCost);                                    // SystemAdmin never sees cost
        Assert.Equal(2, crc.History.Count);
        Assert.Equal(BillingItems.LoanFlows.Count * BillingItems.LoanEventItems.Count, sheet.PlatformFees.Count);
    }

    // ───────────────────────────── Loan milestones ─────────────────────────────

    private sealed class FakeLoans(List<BillingLoanSnapshot> loans) : IBillingLoanSource
    {
        public List<BillingLoanSnapshot> Loans { get; } = loans;
        public Task<IReadOnlyList<BillingLoanSnapshot>> AllLoansAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<BillingLoanSnapshot>>(Loans);
    }

    [Fact]
    public async Task Loan_sync_writes_each_milestone_once_and_follows_a_loan_to_its_outcome()
    {
        using var h = new Harness(nameof(Loan_sync_writes_each_milestone_once_and_follows_a_loan_to_its_outcome));
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var loans = new FakeLoans(
        [
            new(BillingFlow.Corporate, a, "LA-1", "Acme", Utc(2026, 9, 3), BillingLoanOutcome.Processing, null, 5_000_000m),
            new(BillingFlow.Namp, b, "NAMP-1", "Bolu", Utc(2026, 9, 10), BillingLoanOutcome.Declined, Utc(2026, 10, 2), 800_000m),
        ]);
        var sync = new SyncBillingLoanEventsHandler(loans, h.Usage, h.Db);

        var first = (await sync.Handle(new SyncBillingLoanEventsCommand())).Data!;
        var second = (await sync.Handle(new SyncBillingLoanEventsCommand())).Data!;
        Assert.Equal(3, first.RowsAdded);          // two received, one declined
        Assert.Equal(0, second.RowsAdded);

        loans.Loans[0] = loans.Loans[0] with { Outcome = BillingLoanOutcome.Booked, OutcomeAt = Utc(2026, 11, 5) };
        var third = (await sync.Handle(new SyncBillingLoanEventsCommand())).Data!;
        Assert.Equal(1, third.RowsAdded);

        var rows = await h.Db.BillingUsages.ToListAsync();
        Assert.All(rows, r => Assert.Equal(BillingUsageSource.Derived, r.Source));
        var booked = rows.Single(r => r.Item == BillingItems.LoanBooked);
        Assert.Equal(a, booked.CaseId);
        Assert.Equal(Utc(2026, 11).Date, booked.Month);
        Assert.Equal(BillingCategory.Platform, booked.Category);
    }

    // ───────────────────────────── Dashboard ─────────────────────────────

    [Fact]
    public async Task The_dashboard_prices_each_month_at_its_own_rate_and_counts_loans_by_outcome()
    {
        using var h = new Harness(nameof(The_dashboard_prices_each_month_at_its_own_rate_and_counts_loans_by_outcome));
        var who = Guid.NewGuid();
        h.Db.BillingRates.AddRange(
            BillingRate.Create(BillingItems.CrcFull, null, Utc(2026, 1), 500m, 300m, null, who).Value,
            BillingRate.Create(BillingItems.CrcFull, null, Utc(2026, 11), 600m, 300m, null, who).Value,
            BillingRate.Create(BillingItems.LoanBooked, BillingFlow.Namp, Utc(2026, 1), 10_000m, 0m, null, who).Value);
        await h.Db.SaveChangesAsync();

        var c1 = Guid.NewGuid(); var c2 = Guid.NewGuid(); var c3 = Guid.NewGuid();
        await h.AddUsageAsync(BillingItems.CrcFull, BillingFlow.Namp, Utc(2026, 10, 5), caseId: c1, reference: "N-1");
        await h.AddUsageAsync(BillingItems.CrcFull, BillingFlow.Namp, Utc(2026, 11, 5), caseId: c1, reference: "N-1");
        await h.AddUsageAsync(BillingItems.CrcFull, BillingFlow.Namp, Utc(2026, 11, 6), ok: false, caseId: c2, reference: "N-2");
        await h.AddUsageAsync(BillingItems.Bvn, BillingFlow.Unattributed, Utc(2026, 11, 7));
        await h.AddUsageAsync(BillingItems.LoanReceived, BillingFlow.Namp, Utc(2026, 10, 1), caseId: c1, reference: "N-1", source: BillingUsageSource.Derived, key: "k1");
        await h.AddUsageAsync(BillingItems.LoanReceived, BillingFlow.Namp, Utc(2026, 11, 1), caseId: c2, reference: "N-2", source: BillingUsageSource.Derived, key: "k2");
        await h.AddUsageAsync(BillingItems.LoanReceived, BillingFlow.Namp, Utc(2026, 11, 2), caseId: c3, reference: "N-3", source: BillingUsageSource.Derived, key: "k3");
        await h.AddUsageAsync(BillingItems.LoanBooked, BillingFlow.Namp, Utc(2026, 11, 9), caseId: c1, reference: "N-1", source: BillingUsageSource.Derived, key: "k4");
        await h.AddUsageAsync(BillingItems.CrcFull, BillingFlow.Corporate, Utc(2026, 3, 5), source: BillingUsageSource.Reconstructed, key: "old-1");

        var handler = new GetBillingDashboardHandler(h.Usage, h.Rates);
        var nov = (await handler.Handle(new GetBillingDashboardQuery(Utc(2026, 11, 15), null, IncludeCost: true))).Data!;

        Assert.Equal(600m * 2 + 10_000m, nov.DueThisMonth);            // the failed call is billed; the unpriced BVN is worth zero
        Assert.Equal(1_200m, nov.ThirdPartyDue);
        Assert.Equal(10_000m, nov.PlatformDue);
        Assert.Equal(600m, nov.OwnCost);
        Assert.Equal(3, nov.CallsThisMonth);
        Assert.Equal(1, nov.FailedCallsThisMonth);
        Assert.Equal(1, nov.UnattributedThisMonth);
        var namp = nov.Loans.Single(l => l.Flow == BillingFlow.Namp);
        Assert.Equal(2, namp.Received);
        Assert.Equal(1, namp.Booked);
        Assert.Equal(2, namp.Processing);                                // three received ever, one booked
        Assert.Equal(12, nov.ByMonth.Count);
        Assert.Equal(500m, nov.ByMonth.Single(m => m.Month == Utc(2026, 10).Date).Total);   // October at October's price
        Assert.Equal(1, nov.ReconstructedRows);
        Assert.False(nov.MonthIsReconstructed);
        Assert.True(nov.AnyRateSet);

        var asAdmin = (await handler.Handle(new GetBillingDashboardQuery(Utc(2026, 11, 15), BillingFlow.Namp, IncludeCost: false))).Data!;
        Assert.Null(asAdmin.OwnCost);
        Assert.Equal(0, asAdmin.UnattributedThisMonth);                 // filtered to NAMP
        Assert.Equal(11_200m, asAdmin.DueThisMonth);

        var march = (await handler.Handle(new GetBillingDashboardQuery(Utc(2026, 3, 15), null, false))).Data!;
        Assert.True(march.MonthIsReconstructed);                        // before the first live row

        var charges = (await new SearchBillingUsageHandler(h.Usage, h.Rates).Handle(new SearchBillingUsageQuery(new BillingUsageSearch(Utc(2026, 11), null, BillingCategory.CreditBureau, Succeeded: false), true))).Data!;
        var failed = Assert.Single(charges.Items);
        Assert.Equal("N-2", failed.CaseReference);
        Assert.Equal("/namp/" + c2, failed.CaseLink);
        Assert.Equal(600m, failed.UnitPrice);
        Assert.Equal(300m, failed.UnitCost);
    }

    // ───────────────────────────── Backfill ─────────────────────────────

    private sealed class FakeBackfill(List<BillingUsageDraft> drafts) : IBillingBackfillSource
    {
        public DateTime? Before { get; private set; }
        public Task<IReadOnlyList<BillingUsageDraft>> ReconstructAsync(DateTime before, CancellationToken ct = default)
        {
            Before = before;
            return Task.FromResult<IReadOnlyList<BillingUsageDraft>>(drafts);
        }
    }

    [Fact]
    public async Task Backfill_previews_without_writing_then_adds_only_what_is_missing_and_stops_at_the_first_live_row()
    {
        using var h = new Harness(nameof(Backfill_previews_without_writing_then_adds_only_what_is_missing_and_stops_at_the_first_live_row));
        var manager = await h.SeedManagerAsync();
        await h.AddUsageAsync(BillingItems.Bvn, BillingFlow.Corporate, Utc(2026, 11, 1));          // billing went live here
        var caseId = Guid.NewGuid();
        var scope = new BillingScope(BillingFlow.Corporate, caseId, "LA-9", "Applicant");
        var source = new FakeBackfill(
        [
            new(BillingItems.CrcFull, true, null, "…1234", null, scope, Utc(2026, 5, 2), BillingUsageSource.Reconstructed, "br:1:crc"),
            new(BillingItems.CrcScore, true, null, "…1234", null, scope, Utc(2026, 5, 2), BillingUsageSource.Reconstructed, "br:1:score"),
            new(BillingItems.PartnerCheck, false, "timeout", null, null, new BillingScope(BillingFlow.PartnerApi, null, "Acme · r1"), Utc(2026, 7, 2), BillingUsageSource.Reconstructed, "cclog:1:check"),
        ]);
        await h.AddUsageAsync(BillingItems.CrcScore, BillingFlow.Corporate, Utc(2026, 5, 2), caseId: caseId, source: BillingUsageSource.Reconstructed, key: "br:1:score");
        var handler = new RunBillingBackfillHandler(source, h.Usage, h.Users, h.Db);

        var preview = (await handler.Handle(new RunBillingBackfillCommand(DryRun: true, manager.Id))).Data!;
        Assert.Equal(Utc(2026, 11, 1), source.Before);
        Assert.Equal(3, preview.Candidates);
        Assert.Equal(1, preview.AlreadyPresent);
        Assert.Equal(0, preview.Added);
        Assert.Equal(2, preview.ByItem.Sum(i => i.Count));
        Assert.Equal(2, await h.Db.BillingUsages.CountAsync());        // nothing written

        var written = (await handler.Handle(new RunBillingBackfillCommand(DryRun: false, manager.Id))).Data!;
        Assert.Equal(2, written.Added);
        Assert.Equal(4, await h.Db.BillingUsages.CountAsync());
        var partner = await h.Db.BillingUsages.SingleAsync(u => u.Item == BillingItems.PartnerCheck);
        Assert.Equal(BillingUsageSource.Reconstructed, partner.Source);
        Assert.False(partner.Succeeded);
        Assert.Equal(Utc(2026, 7).Date, partner.Month);

        var again = (await handler.Handle(new RunBillingBackfillCommand(DryRun: false, manager.Id))).Data!;
        Assert.Equal(0, again.Added);
    }
}

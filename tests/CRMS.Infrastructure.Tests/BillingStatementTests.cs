using CRMS.Application.Billing;
using CRMS.Application.Billing.Statements;
using CRMS.Application.Identity.Interfaces;
using CRMS.Domain.Aggregates.Billing;
using CRMS.Domain.Constants;
using CRMS.Domain.Entities.Identity;
using CRMS.Domain.Interfaces;
using CRMS.Domain.Services;
using CRMS.Infrastructure.Documents;
using CRMS.Infrastructure.Identity;
using CRMS.Infrastructure.Persistence;
using CRMS.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CRMS.Infrastructure.Tests;

/// <summary>Closing a billing month: preview, issue, late rows, paid, void, and the bank's PDF and Excel copies.</summary>
public class BillingStatementTests
{
    private sealed class Harness : IDisposable
    {
        public CRMSDbContext Db { get; }
        public BillingUsageRepository Usage { get; }
        public BillingRateRepository Rates { get; }
        public BillingStatementRepository Statements { get; }
        public UserRepository Users { get; }
        public BillingSettings Settings { get; } = new() { VendorName = "Freewave Consult Ltd", PaymentInstructions = "Pay to 0123456789, Vendor Bank" };
        public AuditService Audit { get; }
        public ApplicationUser Manager { get; }
        public ApplicationUser Admin { get; }

        public Harness(string name)
        {
            Db = new CRMSDbContext(new DbContextOptionsBuilder<CRMSDbContext>().UseInMemoryDatabase("BillingStatements_" + name + Guid.NewGuid()).Options);
            SeedData.SeedAsync(Db, NullLogger.Instance).GetAwaiter().GetResult();
            Usage = new BillingUsageRepository(Db);
            Rates = new BillingRateRepository(Db);
            Statements = new BillingStatementRepository(Db);
            Users = new UserRepository(Db);
            Audit = new AuditService(new AuditLogRepository(Db), new DataAccessLogRepository(Db), Db);
            var hasher = new PasswordHasher();
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Billing:Manager:Email"] = "billing@vendor.test" }).Build();
            BillingSeeder.SeedManagerAsync(Db, config, hasher, NullLogger.Instance).GetAwaiter().GetResult();
            Manager = Users.GetByEmailAsync("billing@vendor.test").GetAwaiter().GetResult()!;
            Admin = ApplicationUser.Create("admin@bank.test", "admin", "Ada", "Admin", UserType.Staff, null, null).Value;
            Admin.SetPasswordHash(hasher.HashPassword("Password1$$$"));
            Admin.AddRole(Db.Roles.Single(r => r.Name == Roles.SystemAdmin));
            Db.Users.Add(Admin);
            Db.SaveChanges();
        }

        public async Task Usage_(string item, BillingFlow flow, DateTime at, int n = 1)
        {
            for (var i = 0; i < n; i++)
                Db.BillingUsages.Add(BillingUsage.Create(item, flow, Guid.NewGuid(), "REF", null, null, true, null, "h", null, at, BillingUsageSource.Live, null, null).Value);
            await Db.SaveChangesAsync();
        }

        public async Task Rate(string item, BillingFlow? flow, DateTime from, decimal price, decimal cost = 0)
        {
            Db.BillingRates.Add(BillingRate.Create(item, flow, from, price, cost, null, Manager.Id).Value);
            await Db.SaveChangesAsync();
        }

        public PreviewBillingStatementHandler Preview => new(Usage, Rates, Statements, Settings);
        public IssueBillingStatementHandler Issue => new(Usage, Rates, Statements, Users, Db, Audit, Settings);
        public MarkBillingStatementPaidHandler Pay => new(Statements, Users, Db, Audit);
        public VoidBillingStatementHandler Void => new(Statements, Usage, Users, Db, Audit);
        public GetBillingStatementsHandler List => new(Statements, Settings);
        public ExportBillingStatementHandler Export => new(Statements, new BillingStatementExporter(), Settings);

        public void Dispose() => Db.Dispose();
    }

    private static DateTime Utc(int y, int m, int d = 1) => new(y, m, d, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Issuing_freezes_the_month_sweeps_in_unbilled_earlier_rows_and_leaves_later_rows_for_next_time()
    {
        using var h = new Harness(nameof(Issuing_freezes_the_month_sweeps_in_unbilled_earlier_rows_and_leaves_later_rows_for_next_time));
        await h.Rate(BillingItems.CrcFull, null, Utc(2026, 1), 1000m, 600m);
        await h.Rate(BillingItems.LoanBooked, BillingFlow.Namp, Utc(2026, 1), 5000m);
        await h.Usage_(BillingItems.CrcFull, BillingFlow.Namp, Utc(2026, 9, 3), 2);     // earlier month, never billed
        await h.Usage_(BillingItems.CrcFull, BillingFlow.Namp, Utc(2026, 10, 3), 3);
        await h.Usage_(BillingItems.LoanBooked, BillingFlow.Namp, Utc(2026, 10, 9));
        await h.Usage_(BillingItems.Bvn, BillingFlow.Corporate, Utc(2026, 10, 9));       // unpriced
        await h.Usage_(BillingItems.CrcFull, BillingFlow.Namp, Utc(2026, 11, 2));        // next month, stays unbilled

        var preview = (await h.Preview.Handle(new PreviewBillingStatementQuery(Utc(2026, 10, 20), IncludeCost: true))).Data!;
        Assert.True(preview.CanIssue, preview.BlockedReason);
        Assert.Equal(7, preview.UsageCount);
        Assert.Equal(2000m + 3000m + 5000m, preview.Subtotal);
        Assert.Equal(750m, preview.VatAmount);                                   // 7.5% default
        Assert.Equal(10_750m, preview.Total);
        Assert.Equal(3000m, preview.CostTotal);
        Assert.Contains(preview.Warnings, w => w.Contains("earlier months"));
        Assert.Contains(preview.Warnings, w => w.Contains("BVN verification"));
        Assert.Equal(Utc(2026, 9).Date, preview.Lines.First().Month);           // earlier rows come first

        var denied = await h.Issue.Handle(new IssueBillingStatementCommand(Utc(2026, 10), null, h.Admin.Id, "Ada"));
        Assert.False(denied.IsSuccess);

        var issued = (await h.Issue.Handle(new IssueBillingStatementCommand(Utc(2026, 10), "October usage", h.Manager.Id, "Vera Vendor"))).Data!;
        Assert.Equal("BILL-202610-01", issued.Number);
        Assert.Equal(BillingStatementStatus.Issued, issued.Status);
        Assert.Equal(10_750m, issued.Total);
        Assert.Equal(7, issued.UsageCount);
        Assert.Equal(4, issued.Lines.Count);
        Assert.Equal(3000m, issued.CostTotal);

        var stored = await h.Db.BillingStatements.Include(s => s.Lines).SingleAsync();
        Assert.Equal(4, stored.Lines.Count);                                     // lines really persisted, not lost to the Modified trap
        Assert.Equal(7, await h.Db.BillingUsages.CountAsync(u => u.StatementId == issued.Id));
        Assert.Equal(1, await h.Db.BillingUsages.CountAsync(u => u.StatementId == null));   // November stays open

        var again = await h.Preview.Handle(new PreviewBillingStatementQuery(Utc(2026, 10), false));
        Assert.False(again.Data!.CanIssue);
        Assert.Contains("already covers", again.Data.BlockedReason);
        Assert.Contains(await h.Db.AuditLogs.ToListAsync(), a => a.EntityType == "BillingStatement" && a.Description.Contains("BILL-202610-01"));
    }

    [Fact]
    public async Task Paid_and_void_follow_the_rules_and_void_releases_the_rows_under_a_fresh_number()
    {
        using var h = new Harness(nameof(Paid_and_void_follow_the_rules_and_void_releases_the_rows_under_a_fresh_number));
        await h.Rate(BillingItems.PartnerCheck, null, Utc(2026, 1), 2500m);
        await h.Usage_(BillingItems.PartnerCheck, BillingFlow.PartnerApi, Utc(2026, 10, 5), 4);
        var first = (await h.Issue.Handle(new IssueBillingStatementCommand(Utc(2026, 10), null, h.Manager.Id, "Vera"))).Data!;

        var voidByAdmin = await h.Void.Handle(new VoidBillingStatementCommand(first.Id, "oops", h.Admin.Id, "Ada"));
        Assert.False(voidByAdmin.IsSuccess);
        var noReason = await h.Void.Handle(new VoidBillingStatementCommand(first.Id, "  ", h.Manager.Id, "Vera"));
        Assert.False(noReason.IsSuccess);

        var voided = await h.Void.Handle(new VoidBillingStatementCommand(first.Id, "Price agreed late", h.Manager.Id, "Vera"));
        Assert.True(voided.IsSuccess, voided.Error);
        Assert.Contains("4 rows", voided.Data);
        Assert.Equal(0, await h.Db.BillingUsages.CountAsync(u => u.StatementId != null));
        Assert.Equal(BillingStatementStatus.Voided, (await h.Statements.GetByIdAsync(first.Id))!.Status);

        var cannotPayVoided = await h.Pay.Handle(new MarkBillingStatementPaidCommand(first.Id, Utc(2026, 11, 1), "TRF1", null, h.Manager.Id, "Vera"));
        Assert.False(cannotPayVoided.IsSuccess);

        await h.Rate(BillingItems.PartnerCheck, null, Utc(2026, 10), 3000m);     // the late agreement
        var second = (await h.Issue.Handle(new IssueBillingStatementCommand(Utc(2026, 10), null, h.Manager.Id, "Vera"))).Data!;
        Assert.Equal("BILL-202610-02", second.Number);                            // voided number not reused
        Assert.Equal(12_000m, second.Subtotal);

        var paid = await h.Pay.Handle(new MarkBillingStatementPaidCommand(second.Id, Utc(2026, 11, 8), "TRF-778", "Received in full", h.Manager.Id, "Vera"));
        Assert.True(paid.IsSuccess, paid.Error);
        var reloaded = (await h.Statements.GetByIdAsync(second.Id))!;
        Assert.Equal(BillingStatementStatus.Paid, reloaded.Status);
        Assert.Equal("TRF-778", reloaded.PaymentReference);
        Assert.Equal(1, reloaded.Lines.Count);                                    // marking paid did not duplicate lines
        Assert.False((await h.Void.Handle(new VoidBillingStatementCommand(second.Id, "no", h.Manager.Id, "Vera"))).IsSuccess);

        var list = (await h.List.Handle(new GetBillingStatementsQuery())).Data!;
        Assert.Equal(2, list.Count);
        Assert.Equal("BILL-202610-02", list[0].Number);
        Assert.Equal(DateTime.UtcNow.Date <= second.IssuedAt.AddDays(30).Date ? false : true, list[0].Overdue);
    }

    [Fact]
    public async Task The_banks_pdf_and_excel_copies_carry_the_totals_and_never_the_vendors_cost()
    {
        using var h = new Harness(nameof(The_banks_pdf_and_excel_copies_carry_the_totals_and_never_the_vendors_cost));
        await h.Rate(BillingItems.CrcFull, null, Utc(2026, 1), 1000m, 600m);
        await h.Usage_(BillingItems.CrcFull, BillingFlow.Rhshf, Utc(2026, 10, 3), 5);
        var issued = (await h.Issue.Handle(new IssueBillingStatementCommand(Utc(2026, 10), "Thank you", h.Manager.Id, "Vera"))).Data!;

        var pdf = (await h.Export.Handle(new ExportBillingStatementQuery(issued.Id, "pdf"))).Data!;
        Assert.Equal("BILL-202610-01-2026-10.pdf", pdf.FileName);
        Assert.Equal("application/pdf", pdf.ContentType);
        Assert.True(pdf.Content.Length > 1500);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf.Content, 0, 4));

        var xlsx = (await h.Export.Handle(new ExportBillingStatementQuery(issued.Id, "xlsx"))).Data!;
        Assert.Equal("BILL-202610-01-2026-10.xlsx", xlsx.FileName);
        using var wb = new ClosedXML.Excel.XLWorkbook(new MemoryStream(xlsx.Content));
        var ws = wb.Worksheet(1);
        var text = string.Join(" ", ws.CellsUsed().Select(c => c.GetFormattedString()));
        Assert.Contains("Freewave Consult Ltd", text);
        Assert.Contains("BILL-202610-01", text);
        Assert.Contains("CRC full report", text);
        Assert.Contains("Pay to 0123456789", text);
        Assert.DoesNotContain("600", text);                                       // no cost column
        Assert.Equal(5000m, (decimal)ws.Cell(11, 7).GetDouble());

        Assert.False((await h.Export.Handle(new ExportBillingStatementQuery(issued.Id, "docx"))).IsSuccess);
    }
}

using CRMS.Application.Advisory.Interfaces;
using CRMS.Application.CustomerCheck;
using CRMS.Domain.Interfaces;
using Xunit;

namespace CRMS.Application.Tests.CustomerCheck;

/// <summary>How the advisory's input is drawn from the other sections, and how its answer is
/// shaped for the partner.</summary>
public class CustomerCheckAdvisoryRulesTests
{
    private static readonly DateTime Now = new(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);
    private static readonly CustomerCheckSettings Settings = new();

    private static StatementTransaction Tx(DateTime date, decimal amount, TransactionType type, decimal balance = 1000m, string description = "TRANSFER")
        => new(Guid.NewGuid().ToString(), date, description, amount, type, balance, null);

    [Fact]
    public void Cashflow_input_averages_over_the_whole_window_including_quiet_months()
    {
        var txns = new List<StatementTransaction>
        {
            Tx(new DateTime(2026, 11, 2), 120_000m, TransactionType.Credit),
            Tx(new DateTime(2026, 11, 5), 20_000m, TransactionType.Debit),
            Tx(new DateTime(2026, 10, 1), 120_000m, TransactionType.Credit),
            Tx(new DateTime(2026, 10, 9), 50_000m, TransactionType.Debit, -200m, "POS FAILED"),
            Tx(new DateTime(2025, 1, 1), 999_999m, TransactionType.Credit),  // outside the window
        };

        var c = CustomerCheckRules.CashflowInput([txns], 12, 300_000m, Now)!;

        Assert.Equal(12, c.WindowMonths);
        Assert.Equal(1, c.AccountsRead);
        Assert.Equal(4, c.TransactionsAnalyzed);
        Assert.Equal(2, c.MonthsWithActivity);
        Assert.Equal(20_000m, c.AverageMonthlyInflow);
        Assert.Equal(Math.Round(70_000m / 12m, 2), c.AverageMonthlyOutflow);
        Assert.Equal(Math.Round(20_000m - Math.Round(70_000m / 12m, 2), 2), c.NetMonthlyCashflow);
        Assert.True(c.InflowVolatility > 1m);
        Assert.Equal(1, c.BouncedTransactionCount);
        Assert.Equal(1, c.DaysWithNegativeBalance);
        Assert.Equal(300_000m, c.TotalBalance);
        Assert.Null(CustomerCheckRules.CashflowInput([], 12, 0m, Now));
    }

    [Fact]
    public void Advisory_input_is_drawn_from_the_sections_without_identifiers()
    {
        var query = new CustomerCheckQuery("req", "22212345678", [], CustomerCheckSections.All, 12, true, false, 500_000m, 12);
        var identity = new IdentitySection(SectionStatuses.Ok, null, "src", "ADA OKORO", new DateOnly(1985, 3, 14), "F", "0803****123", null, null, null, false);
        var bureau = new CreditBureauSection(SectionStatuses.Ok, null, "FIRST_CENTRAL", [], true, Now, new BureauScore(612, "Fair", "FIRST_CENTRAL"),
            new BureauSummary(5, 3, 2, 3, 1, 1, 2_500_000m, 6_100_000m, 1_840_000m, 215_000m, 94, "NGN"),
            [new BureauLoan("X", "2210", "Personal", 300_000m, 215_000m, 215_000m, CheckLoanStatuses.WrittenOff, null, null, null, null, null, null)],
            [new BureauEnquiry("Carbon", "Loan", null), new BureauEnquiry("Fair", "Loan", null)]);
        var fraud = new FraudCheckSection(SectionStatuses.Ok, null, 38, "MEDIUM", "Caution", false, null);
        var accounts = new BankAccountsSection(SectionStatuses.Ok, null, BvnMatchResults.Match, "1001", "Active", "2", new DateOnly(2019, 2, 11), []);
        var loans = new BankLoansSection(SectionStatuses.Ok, null,
        [
            new BankLoan("9981", "P", 2_500_000m, 1_625_000m, 0m, 0, CheckLoanStatuses.Running, "Active", null, null, null, 112_500m),
            new BankLoan("9982", "P", 100_000m, 40_000m, 10_000m, 31, CheckLoanStatuses.Overdue, "Active", null, null, null, 5_000m),
            new BankLoan("9983", "P", 100_000m, 0m, 0m, 0, CheckLoanStatuses.Paid, "Closed", null, null, null, null),
        ]);
        var crms = new CrmsFacilitiesSection(SectionStatuses.Ok, null, "MATCH", new CrmsFacilitySummary(2, 1, 1_625_000m, true), []);

        var r = CustomerCheckRules.AdvisoryInput(Guid.NewGuid(), query, Settings, identity, bureau, fraud, accounts, loans, crms, null, Now);

        Assert.True(r.ProposalSupplied);
        Assert.Equal(500_000m, r.ProposedAmount);
        Assert.NotNull(r.Bureau);
        Assert.Equal(612, r.Bureau!.CreditScore);
        Assert.True(r.Bureau.Reused);
        Assert.Equal(1, r.Bureau.WrittenOffLoans);
        Assert.Equal(2, r.Bureau.RecentEnquiries);
        Assert.Equal(94, r.Bureau.MaxDaysOverdue);
        Assert.True(r.Exposure.BankDataAvailable);
        Assert.Equal(1, r.Exposure.BankRunningLoans);
        Assert.Equal(1, r.Exposure.BankOverdueLoans);
        Assert.Equal(1, r.Exposure.BankPaidLoans);
        Assert.Equal(1_665_000m, r.Exposure.BankOutstanding);
        Assert.Equal(10_000m, r.Exposure.BankOverdue);
        Assert.Equal(31, r.Exposure.BankMaxDaysPastDue);
        Assert.Equal(117_500m, r.Exposure.BankMonthlyInstalments);
        Assert.Equal(2, r.Exposure.CrmsDirectFacilities);
        Assert.True(r.Identity.BvnVerified);
        Assert.False(r.Identity.WatchListed);
        Assert.Equal(38, r.Identity.FraudRiskScore);
        Assert.True(r.Identity.BankCustomerFound);
        Assert.Equal("2", r.Identity.KycLevel);
        Assert.Equal(92, r.Identity.MonthsAsCustomer);
        Assert.True(CustomerCheckRules.AdvisoryHasBasis(r));

        var json = System.Text.Json.JsonSerializer.Serialize(r);
        Assert.DoesNotContain("22212345678", json);
        Assert.DoesNotContain("OKORO", json);
        Assert.DoesNotContain("1985", json);
    }

    [Fact]
    public void Advisory_input_with_nothing_run_has_no_basis_and_uses_the_stand_in_proposal()
    {
        var query = new CustomerCheckQuery("req", "22212345678", [], CustomerCheckSections.All, 12, true, false, null, null);
        var r = CustomerCheckRules.AdvisoryInput(Guid.NewGuid(), query, Settings,
            IdentitySection.Empty(SectionStatuses.Unavailable), CreditBureauSection.Empty(SectionStatuses.Unavailable),
            FraudCheckSection.Empty(SectionStatuses.NotRequested), BankAccountsSection.Empty(SectionStatuses.Unavailable),
            BankLoansSection.Empty(SectionStatuses.Unavailable), CrmsFacilitiesSection.Empty(SectionStatuses.Unavailable), null, Now);

        Assert.False(r.ProposalSupplied);
        Assert.Equal(Settings.FraudDefaultLoanAmount, r.ProposedAmount);
        Assert.Null(r.Bureau);
        Assert.False(r.Exposure.BankDataAvailable);
        Assert.Null(r.Identity.BvnVerified);
        Assert.False(CustomerCheckRules.AdvisoryHasBasis(r));

        var noRecord = CustomerCheckRules.AdvisoryInput(Guid.NewGuid(), query, Settings,
            IdentitySection.Empty(SectionStatuses.NotRequested), CreditBureauSection.Empty(SectionStatuses.NoRecord),
            FraudCheckSection.Empty(SectionStatuses.NotRequested), BankAccountsSection.Empty(SectionStatuses.NoRecord),
            BankLoansSection.Empty(SectionStatuses.NoRecord), CrmsFacilitiesSection.Empty(SectionStatuses.Ok), null, Now);
        Assert.True(noRecord.Exposure.BankDataAvailable);
        Assert.False(noRecord.Identity.BankCustomerFound);
        Assert.True(CustomerCheckRules.AdvisoryHasBasis(noRecord));
    }

    [Fact]
    public void The_advisory_section_lists_assessed_and_unassessed_categories_and_its_basis()
    {
        var request = new CustomerAdvisoryRequest(Guid.NewGuid(), 500_000m, 12, true,
            new CustomerBureauInput("FIRST_CENTRAL", false, 612, 5, 2, 3, 1, 1, 0, 1_840_000m, 215_000m, 94, 1),
            new CustomerCashflowInput(12, 1, 87, 12, 420_000m, 390_000m, 30_000m, 0.2m, 0, 0, 142_301m),
            new CustomerExposureInput(true, 1, 0, 2, 0, 1_625_000m, 0, 0, 112_500m, 2, 1, 1_625_000m),
            new CustomerIdentityInput(true, false, 38, "MEDIUM", true, "2", 92));
        var response = new CustomerAdvisoryResponse(true, null,
            [new RiskScoreOutput("CreditHistory", 55.04m, 0.4m, "Medium", "why", ["concern"], ["positive"])],
            [new CustomerAdvisoryGap("RepaymentCapacity", 0.25m, "No statement was read.")],
            61.44m, "Low", "ApproveWithConditions", 375_000m, 12, ["Clear overdue."], ["flag"], "summary", "s", "w", "m", "k", true, "hybrid-v1.0-rules+llm");

        var section = CustomerCheckRules.MapAdvisory(response, request, Now);

        Assert.Equal(SectionStatuses.Ok, section.Status);
        Assert.Equal(61.4m, section.OverallScore);
        Assert.Equal("ApproveWithConditions", section.Recommendation);
        Assert.Equal(2, section.Categories.Count);
        Assert.True(section.Categories[0].Assessed);
        Assert.Equal(55.0m, section.Categories[0].Score);
        Assert.False(section.Categories[1].Assessed);
        Assert.Null(section.Categories[1].Score);
        Assert.Equal("No statement was read.", section.Categories[1].Rationale);
        Assert.Equal("LLM", section.NarrativeSource);
        Assert.Equal(375_000m, section.Proposal!.RecommendedAmount);
        Assert.Contains("FIRST_CENTRAL report", section.Basis);
        Assert.Contains("statements on 1 account over 12 months", section.Basis);
        Assert.Contains("fraud check", section.Basis);
        Assert.Equal("summary", section.Narrative!.Summary);
    }
}

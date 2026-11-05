using CRMS.Application.CustomerCheck;
using CRMS.Domain.Aggregates.CustomerCheck;
using CRMS.Domain.Interfaces;
using Xunit;

namespace CRMS.Application.Tests.CustomerCheck;

public class CustomerCheckRulesTests
{
    private static readonly CustomerCheckSettings Settings = new();
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    // ── Validation ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_defaults_to_every_section_and_the_default_window()
    {
        var (query, errors) = CustomerCheckRules.Validate(new CustomerCheckRequest("req-1", "222 1234-5678", null, null, null), Settings);

        Assert.Empty(errors);
        Assert.NotNull(query);
        Assert.Equal("22212345678", query!.Bvn);
        Assert.Equal(CustomerCheckSections.All, query.Sections);
        Assert.Equal(Settings.DefaultActivityWindowMonths, query.ActivityWindowMonths);
        Assert.True(query.IncludeScore);
        Assert.False(query.RunAmlCheck);
        Assert.False(string.IsNullOrWhiteSpace(query.RequestId));
    }

    [Theory]
    [InlineData(null, "bvn is required.")]
    [InlineData("1234567890", "bvn must be 11 digits.")]
    [InlineData("2221234567X", "bvn must be 11 digits.")]
    public void Validate_refuses_a_bad_bvn_without_repeating_it(string? bvn, string expected)
    {
        var (query, errors) = CustomerCheckRules.Validate(new CustomerCheckRequest("req-1", bvn, null, null, null), Settings);

        Assert.Null(query);
        Assert.Contains(expected, errors);
        if (bvn is not null) Assert.DoesNotContain(errors, e => e.Contains(bvn));
    }

    [Fact]
    public void Validate_checks_nubans_sections_and_options()
    {
        var request = new CustomerCheckRequest("r1", "22212345678",
            ["0123456789", "12345", "0123456789"],
            ["creditBureau", "nothing"],
            new CustomerCheckOptions(48, null, null, -5, 0));

        var (query, errors) = CustomerCheckRules.Validate(request, Settings);

        Assert.Null(query);
        Assert.Contains("nubans[1] must be 10 digits.", errors);
        Assert.Contains(errors, e => e.StartsWith("sections[1] is not a known section"));
        Assert.Contains(errors, e => e.StartsWith("options.activityWindowMonths must be between 1 and"));
        Assert.Contains("options.loanAmount must be greater than zero.", errors);
        Assert.Contains("options.tenorMonths must be between 1 and 120.", errors);
    }

    [Fact]
    public void Validate_canonicalises_sections_and_dedupes_nubans()
    {
        var request = new CustomerCheckRequest("r1", "22212345678", ["0123456789", "0123-456-789"], ["CREDITBUREAU", "bankLoans"], null);

        var (query, errors) = CustomerCheckRules.Validate(request, Settings);

        Assert.Empty(errors);
        Assert.Equal(["0123456789"], query!.Nubans);
        Assert.Equal([CustomerCheckSections.CreditBureau, CustomerCheckSections.BankLoans], query.Sections);
    }

    [Fact]
    public void Validate_caps_the_number_of_nubans()
    {
        var request = new CustomerCheckRequest("req-1", "22212345678", Enumerable.Range(0, 6).Select(i => $"012345678{i}").ToList(), null, null);

        var (query, errors) = CustomerCheckRules.Validate(request, Settings);

        Assert.Null(query);
        Assert.Contains("nubans holds at most 5 entries.", errors);
    }

    // ── Masking ──────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("0123456789", "****6789")]
    [InlineData("12", "****")]
    [InlineData(null, "****")]
    public void MaskLast4_keeps_only_the_last_four_digits(string? value, string expected)
        => Assert.Equal(expected, CustomerCheckRules.MaskLast4(value));

    [Theory]
    [InlineData("08031234567", "0803****567")]
    [InlineData("1234567", "*******")]
    [InlineData(null, null)]
    public void MaskPhone_keeps_the_ends(string? phone, string? expected)
        => Assert.Equal(expected, CustomerCheckRules.MaskPhone(phone));

    // ── Status words ─────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Open", "Performing", 0, 500, CheckLoanStatuses.Running)]
    [InlineData("Open", "Performing", 100, 500, CheckLoanStatuses.Overdue)]
    [InlineData("Closed", null, 0, 0, CheckLoanStatuses.Paid)]
    [InlineData("Open", "Delinquent", 0, 500, CheckLoanStatuses.Overdue)]
    [InlineData("Written Off", "Lost", 0, 500, CheckLoanStatuses.WrittenOff)]
    [InlineData(null, null, 0, 500, CheckLoanStatuses.Running)]
    [InlineData(null, null, 0, 0, CheckLoanStatuses.Unknown)]
    [InlineData("Settled", null, 0, 0, CheckLoanStatuses.Paid)]
    public void BureauLoanStatus_maps_bureau_words_and_amounts(string? status, string? performance, decimal overdue, decimal outstanding, string expected)
        => Assert.Equal(expected, CustomerCheckRules.BureauLoanStatus(status, performance, overdue, outstanding));

    [Theory]
    [InlineData(100, null, CheckLoanStatuses.InProcess)]
    [InlineData(200, null, CheckLoanStatuses.InProcess)]
    [InlineData(300, 0, CheckLoanStatuses.Running)]
    [InlineData(300, 250, CheckLoanStatuses.Overdue)]
    [InlineData(600, 0, CheckLoanStatuses.Paid)]
    [InlineData(601, 0, CheckLoanStatuses.WrittenOff)]
    [InlineData(700, 0, CheckLoanStatuses.Paid)]
    [InlineData(999, 0, CheckLoanStatuses.Unknown)]
    public void FineractLoanStatus_maps_status_codes(int code, int? overdue, string expected)
        => Assert.Equal(expected, CustomerCheckRules.FineractLoanStatus(code, overdue));

    [Theory]
    [InlineData(400, false)]
    [InlineData(500, false)]
    [InlineData(300, true)]
    [InlineData(600, true)]
    public void IsBookedLoan_leaves_out_withdrawn_and_rejected(int code, bool expected)
        => Assert.Equal(expected, CustomerCheckRules.IsBookedLoan(code));

    [Theory]
    [InlineData("Active", CheckAccountStatuses.Active)]
    [InlineData("Dormant", CheckAccountStatuses.Dormant)]
    [InlineData("Closed", CheckAccountStatuses.Closed)]
    [InlineData("Submitted and pending approval", CheckAccountStatuses.Inactive)]
    [InlineData("Inactive", CheckAccountStatuses.Inactive)]
    [InlineData("Matured", CheckAccountStatuses.Inactive)]
    [InlineData("", CheckAccountStatuses.Unknown)]
    [InlineData(null, CheckAccountStatuses.Unknown)]
    public void SavingsStatus_maps_core_banking_words(string? raw, string expected)
        => Assert.Equal(expected, CustomerCheckRules.SavingsStatus(raw));

    [Theory]
    [InlineData(0, "LOW")]
    [InlineData(29, "LOW")]
    [InlineData(30, "MEDIUM")]
    [InlineData(69, "MEDIUM")]
    [InlineData(70, "HIGH")]
    [InlineData(100, "HIGH")]
    public void RiskBand_follows_the_documented_cut_offs(int score, string expected)
        => Assert.Equal(expected, CustomerCheckRules.RiskBand(score));

    [Theory]
    [InlineData("Subject not found in credit bureau", true)]
    [InlineData("No credit data available", true)]
    [InlineData("Connection error: timeout", false)]
    [InlineData(null, false)]
    public void IsNoRecord_recognises_the_providers_no_record_answers(string? error, bool expected)
        => Assert.Equal(expected, CustomerCheckRules.IsNoRecord(error));

    // ── Schedule and activity ────────────────────────────────────────────────────────────────────

    [Fact]
    public void ScheduleState_counts_overdue_from_the_oldest_unpaid_instalment()
    {
        var periods = new[]
        {
            (Now.AddDays(-40), 0m, true),
            (Now.AddDays(-10), 500m, false),
            (Now.AddDays(-3), 500m, false),
            (Now.AddDays(20), 500m, false),
        };

        var (overdue, dpd, next, nextAmount) = CustomerCheckRules.ScheduleState(periods, Now);

        Assert.Equal(1000m, overdue);
        Assert.Equal(10, dpd);
        Assert.Equal(Now.AddDays(20), next);
        Assert.Equal(500m, nextAmount);
    }

    [Fact]
    public void ScheduleState_with_nothing_due_reports_zero_and_no_next_instalment()
    {
        var (overdue, dpd, next, nextAmount) = CustomerCheckRules.ScheduleState([(Now.AddDays(-40), 0m, true)], Now);

        Assert.Equal(0m, overdue);
        Assert.Equal(0, dpd);
        Assert.Null(next);
        Assert.Null(nextAmount);
    }

    [Fact]
    public void Activity_summarises_the_window()
    {
        var tx = new List<StatementTransaction>
        {
            new("1", Now.AddMonths(-5), "salary", 1000m, TransactionType.Credit, 1000m, null),
            new("2", Now.AddMonths(-2), "atm", 300m, TransactionType.Debit, 700m, null),
            new("3", Now.AddDays(-1), "transfer", 200m, TransactionType.Credit, 900m, null),
        };

        var activity = CustomerCheckRules.Activity(tx, 12, Now.AddYears(-3), Now);

        Assert.Equal(12, activity.WindowMonths);
        Assert.Equal(DateOnly.FromDateTime(Now.AddMonths(-5)), activity.FirstTransaction);
        Assert.Equal(DateOnly.FromDateTime(Now.AddDays(-1)), activity.LastTransaction);
        Assert.Equal(3, activity.TransactionsInWindow);
        Assert.Equal(1200m, activity.CreditsInWindow);
        Assert.Equal(300m, activity.DebitsInWindow);
        Assert.Equal(36, activity.MonthsSinceOpened);
    }

    [Fact]
    public void Activity_with_no_transactions_has_no_dates()
    {
        var activity = CustomerCheckRules.Activity([], 6, null, Now);

        Assert.Null(activity.FirstTransaction);
        Assert.Null(activity.LastTransaction);
        Assert.Equal(0, activity.TransactionsInWindow);
        Assert.Null(activity.MonthsSinceOpened);
    }

    [Theory]
    [InlineData("2026-11-04", 2026, 11, 4)]
    [InlineData("04-11-2026", 2026, 11, 4)]
    [InlineData("04/11/2026", 2026, 11, 4)]
    [InlineData("1985-03-14T00:00:00", 1985, 3, 14)]
    public void ParseDate_accepts_the_formats_providers_use(string value, int y, int m, int d)
        => Assert.Equal(new DateOnly(y, m, d), CustomerCheckRules.ParseDate(value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a date")]
    public void ParseDate_returns_null_for_nonsense(string? value)
        => Assert.Null(CustomerCheckRules.ParseDate(value));

    // ── Bureau mapping ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void MapBureau_merges_history_and_performance_and_masks_account_numbers()
    {
        var report = Report(
            history:
            [
                new SmartComplyLoanHistory("FairMoney", null, "1234562210", "Personal", 300_000m, 100_000m, 215_000m,
                    new DateTime(2026, 6, 30), "3 months", new DateTime(2026, 5, 2), new DateTime(2026, 8, 2),
                    "Delinquent", "Open", 215_000m, null, 0, null, null),
            ],
            performance:
            [
                new SmartComplyLoanPerformance("FairMoney", "1234562210", 300_000m, 215_000m, "Open", "Delinquent", 215_000m,
                    "Personal", "3 months", "Monthly", null, null, null, null),
                new SmartComplyLoanPerformance("Access Bank", "9999990042", 800_000m, 0m, "Closed", "Performing", 0m,
                    "Salary Advance", "12 months", "Monthly", null, null, new DateTime(2023, 1, 15), null),
            ]);

        var section = CustomerCheckRules.MapBureau(report, Bureaus.FirstCentral, false, Now, new SmartComplyCreditScore(612, "Fair", "FC", null), [Bureaus.Crc]);

        Assert.Equal(SectionStatuses.Ok, section.Status);
        Assert.Equal("FIRST_CENTRAL", section.Bureau);
        Assert.Equal(["CRC"], section.BureausSkipped);
        Assert.Equal(2, section.Loans.Count);

        var fairMoney = section.Loans[0];
        Assert.Equal("2210", fairMoney.AccountNumberLast4);
        Assert.Equal(CheckLoanStatuses.Overdue, fairMoney.Status);
        Assert.Equal("Monthly", fairMoney.RepaymentFrequency);
        Assert.Equal(new DateOnly(2026, 5, 2), fairMoney.DisbursedDate);

        var access = section.Loans[1];
        Assert.Equal("0042", access.AccountNumberLast4);
        Assert.Equal(CheckLoanStatuses.Paid, access.Status);

        Assert.Equal(612, section.Score!.Value);
        Assert.Equal("FIRST_CENTRAL", section.Score.Source);
        Assert.Equal(5, section.Summary!.TotalLoans);
    }

    [Fact]
    public void MapFraud_marks_the_financial_analysis_as_not_assessed_without_amount_and_tenor()
    {
        var result = new SmartComplyLoanFraudResult(1, "A B", "x", true, false, 38, "Proceed with caution", "ok",
            new SmartComplyFinancialAnalysis(new SmartComplyRiskItem(10, "ok"), null, null, null, null), null, null);

        var without = CustomerCheckRules.MapFraud(result, amountAndTenorSupplied: false, amlChecked: false);
        var with = CustomerCheckRules.MapFraud(result, amountAndTenorSupplied: true, amlChecked: true);

        Assert.Equal("MEDIUM", without.RiskBand);
        Assert.False(without.FinancialAnalysis!.Assessed);
        Assert.Null(without.FinancialAnalysis.Items);
        Assert.True(with.FinancialAnalysis!.Assessed);
        Assert.True(with.AmlChecked);
        Assert.Equal(10, with.FinancialAnalysis.Items!["incomeStability"].RiskScore);
    }

    // ── Consumer entitlements ────────────────────────────────────────────────────────────────────

    [Fact]
    public void ApiConsumer_allows_only_its_sections_and_validates_limits()
    {
        var consumer = ApiConsumer.Create("AgriPay", ApiKeys.Hash("k"), "crms_abc", [CustomerCheckSections.CreditBureau, " fraudCheck "],
            30, true, 30, null, Guid.NewGuid()).Value;

        Assert.True(consumer.Allows(CustomerCheckSections.CreditBureau));
        Assert.True(consumer.Allows("FRAUDCHECK"));
        Assert.False(consumer.Allows(CustomerCheckSections.BankLoans));

        Assert.True(ApiConsumer.Create("x", "h", "p", [], 0, false, 30, null, Guid.NewGuid()).IsFailure);
        Assert.True(ApiConsumer.Create("x", "h", "p", [], 10, true, 0, null, Guid.NewGuid()).IsFailure);
        Assert.True(ApiConsumer.Create("", "h", "p", [], 10, true, 30, null, Guid.NewGuid()).IsFailure);
    }

    [Fact]
    public void ApiKeys_generate_distinct_prefixed_keys_whose_hash_is_stable()
    {
        var (k1, h1, p1) = ApiKeys.Generate();
        var (k2, _, _) = ApiKeys.Generate();

        Assert.StartsWith(ApiKeys.Prefix, k1);
        Assert.NotEqual(k1, k2);
        Assert.Equal(h1, ApiKeys.Hash(k1));
        Assert.Equal(h1, ApiKeys.Hash("  " + k1 + " "));
        Assert.Equal(64, h1.Length);
        Assert.StartsWith(ApiKeys.Prefix, p1);
        Assert.True(p1.Length < k1.Length);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────────

    internal static SmartComplyIndividualCreditReport Report(
        List<SmartComplyLoanHistory>? history = null, List<SmartComplyLoanPerformance>? performance = null,
        string? dateOfBirth = "1985-03-14", string? name = "ADEBAYO MUSA OKORO")
        => new("id", "22212345678", name, "08031234567", "Male", dateOfBirth, "Kaduna", "a@b.c",
            new SmartComplyCreditSummary(5, 3, 2, 3, 1, 1, 2_500_000m, 100_000m, 6_100_000m, 1_840_000m, 215_000m, 94, "ORD-1"),
            [], [new SmartComplyCreditEnquiry("Carbon MFB", "Loan application", new DateTime(2026, 10, 19), null)],
            performance ?? [], history ?? [], Now);
}

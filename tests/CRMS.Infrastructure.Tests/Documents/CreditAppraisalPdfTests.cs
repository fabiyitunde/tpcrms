using System.Text;
using CRMS.Application.CreditAppraisal;
using CRMS.Application.Namp.DTOs;
using CRMS.Application.Rhshf.Queries;
using CRMS.Domain.Enums;
using CRMS.Infrastructure.Documents;
using Xunit;

namespace CRMS.Infrastructure.Tests.Documents;

/// <summary>
/// The stand-alone credit appraisal PDF on each loan track. As with the other documents, the cases
/// that matter are the sparse ones: an officer can save an appraisal with most figures blank, and
/// the document must still print. Set APPRAISAL_PDF_DIR to keep the files and look at them.
/// </summary>
public class CreditAppraisalPdfTests
{
    private static readonly CreditAppraisalPdfGenerator Generator = new();
    private static readonly DateTime PrintedAt = new(2026, 11, 24, 15, 0, 0, DateTimeKind.Utc);

    private static void AssertPdf(byte[] bytes, string name)
    {
        Assert.True(bytes.Length > 1000);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));
        if (Environment.GetEnvironmentVariable("APPRAISAL_PDF_DIR") is { Length: > 0 } dir)
        {
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, name + ".pdf"), bytes);
        }
    }

    private static AppraisalCaseInfo Case(string track, string number) =>
        new(track, number, "Iveren Agro Ltd", "Working Capital Loan", "Amount requested", 25_000_000m, "NGN", 12, "Credit Review");

    // ── Corporate ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Corporate_with_every_figure()
    {
        var doc = new CorporateAppraisalDocument(Case("Corporate Loan", "LA20261124001"),
            1.42m, 0.85m, 1.6m, 62.5m, "Adequate", "ConditionalApprove",
            "Cash flow from the last three years covers the proposed repayment. Approval subject to a\nperfected legal mortgage on the Makurdi warehouse.",
            "appraisal-memo.pdf", "Ada Obi", new DateTimeOffset(2026, 11, 20, 10, 30, 0, TimeSpan.Zero));

        AssertPdf(Generator.Corporate(doc, "Musa Bello", PrintedAt), "corporate-full");
    }

    [Fact]
    public void Corporate_with_only_the_required_fields()
    {
        var doc = new CorporateAppraisalDocument(Case("Corporate Loan", "LA20261124002") with { Product = null, TenorMonths = null },
            null, null, null, null, "Marginal", "Decline", null, null, null, DateTimeOffset.UtcNow);

        AssertPdf(Generator.Corporate(doc, "CRMS user", PrintedAt), "corporate-sparse");
    }

    // ── NAMP ─────────────────────────────────────────────────────────────────────────────────────

    private static NampFinancialAppraisalReportDto NampReport() => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new DateTime(2026, 11, 18, 9, 0, 0, DateTimeKind.Utc),
        450_000m, 1.35m, 70m, "Adequate", "Equity of 30% paid from savings.", "No delinquent facilities.",
        "Approve", "Viable on projected hire demand.", "RentalHireRevenue",
        1_200_000m, 65m, "Letters from two cooperatives.",
        80m, 15_000m, 250_000m, 60_000m, 90_000m,
        3_400_000m, 1.21m, 0.24m, 1.18m);

    [Fact]
    public void Namp_with_the_calculator_and_a_decision()
    {
        var doc = new NampAppraisalDocument(Case("NAMP Equipment Loan", "NAMP-2026-000123"), NampReport(),
            "Viable. Recommend to committee.", "Ada Obi", new DateTime(2026, 11, 19, 14, 0, 0, DateTimeKind.Utc));

        AssertPdf(Generator.Namp(doc, "Musa Bello", PrintedAt), "namp-full");
    }

    [Fact]
    public void Namp_with_no_dscr_or_calculator_inputs()
    {
        // A missing DSCR once printed a bare "×"; it must print as blank instead, without failing.
        var report = NampReport() with
        {
            DebtServiceCoverageRatio = null, HectaresPerMonth = null, RatePerHectare = null, MonthlyFuelCost = null,
            MonthlyMaintenanceCost = null, MonthlyOperatorWage = null, NetPresentValue = null, BenefitCostRatio = null,
            InternalRateOfReturn = null, ProfitabilityIndex = null,
        };
        var doc = new NampAppraisalDocument(Case("NAMP Equipment Loan", "NAMP-2026-000124"), report, null, null, null);

        AssertPdf(Generator.Namp(doc, "Musa Bello", PrintedAt), "namp-sparse");
    }

    // ── RH-SHF ───────────────────────────────────────────────────────────────────────────────────

    private static RhshfFinancialAppraisalDto RhshfAppraisal(bool sparse = false) => new(
        sparse ? [] :
        [
            new RhshfFarmPlanDto(Guid.NewGuid(), "Rice", 120m, 4_500m, 650m, 540_000m, 351_000_000m, Guid.NewGuid(), "Ada Obi", DateTime.UtcNow),
            new RhshfFarmPlanDto(Guid.NewGuid(), "Maize", 60m, 3_800m, 420m, 228_000m, 95_760_000m, Guid.NewGuid(), "Ada Obi", DateTime.UtcNow),
        ],
        new RhshfAppraisalReportDto(Guid.NewGuid(), 1, Guid.NewGuid(), "Ada Obi", new DateTime(2026, 11, 21, 11, 0, 0, DateTimeKind.Utc),
            40_000_000m, 25_000_000m, 6, 9m, sparse ? null : "Costs from the FAC's last two dry seasons.",
            446_760_000m, 180_000_000m, 65_000_000m, 16_200_000m, 261_200_000m, 196_200_000m, 381_760_000m,
            185_560_000m, 41.5m, 1.95m, 185_560_000m,
            sparse ? null : 0.18m, 120_000_000m,
            180m, 4_266m, 582m,
            sparse ? null : 2_400m, sparse ? null : 310m,
            sparse ? null : 43.7m, sparse ? null : 46.7m,
            true, true, !sparse, !sparse, !sparse, !sparse, sparse ? 2 : 5,
            RhshfRepaymentCapacityRating.Strong, RhshfCreditRecommendation.Pass,
            sparse ? null : "Main risk is late rains; offtaker contract covers price.",
            sparse ? "Tests failed for lack of data; recommended on the FAC's track record." : null),
        new RhshfAppraisalThresholdsDto(1.25m, 20m, 15m, 20m, 20m),
        446_760_000m, "NGN");

    [Fact]
    public void Rhshf_with_the_farm_plan_tests_cashflow_and_break_even()
    {
        var doc = new RhshfAppraisalDocument(Case("RH-SHF Dry-Season Inputs", "RHSHF-2026-142546"), RhshfAppraisal());

        AssertPdf(Generator.Rhshf(doc, "Musa Bello", PrintedAt), "rhshf-full");
    }

    [Fact]
    public void Rhshf_with_no_farm_plan_irr_or_break_even()
    {
        var doc = new RhshfAppraisalDocument(Case("RH-SHF Dry-Season Inputs", "RHSHF-2026-142547"), RhshfAppraisal(sparse: true));

        AssertPdf(Generator.Rhshf(doc, "Musa Bello", PrintedAt), "rhshf-sparse");
    }
}

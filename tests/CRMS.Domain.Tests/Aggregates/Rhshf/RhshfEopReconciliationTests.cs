using CRMS.Domain.Common;
using CRMS.Domain.Aggregates.Rhshf;

namespace CRMS.Domain.Tests.Aggregates.Rhshf;

/// <summary>
/// The portal's reconciliation rules 1 to 3, re-checked on receipt at its request.
///
/// Rule 1: input lines + packaging excess + charges = approved EOP total.
/// Rule 2: the rows marked as financed sum to the loan amount.
/// Rule 3: the loan amount is the figure CRMS profiles.
///
/// The portal refuses to send a case that fails these, so this is a second pair of eyes — which is
/// the point. The approved EOP, the offer letter and the core banking booking have to describe one
/// financing structure, with nothing financed also deducted as a charge, and the letter's
/// implementation plan is printed from these same rows.
///
/// Figures throughout are the portal's own worked example: one EOP, ten farmers, everything financed.
/// </summary>
public class RhshfEopReconciliationTests
{
    private const decimal Npk = 112_000.00m;
    private const decimal Urea = 108_000.00m;
    private const decimal Herbicide = 3_200_000.00m;
    private const decimal Pesticide = 800_000.00m;
    private const decimal Seeds = 40_000_000.00m;
    private const decimal InputsTotal = 44_220_000.00m;

    private const decimal Insurance = 1_768_800.00m;
    private const decimal WorkingCapital = 2_211_000.00m;
    private const decimal TechServicesFee = 40_000.00m;
    private const decimal ChargesTotal = 4_019_800.00m;

    private const decimal LoanAmount = 48_239_800.00m;

    [Fact]
    public void AcceptsThePortalsWorkedExample()
    {
        var result = Create(LoanAmount, Financing());

        Assert.True(result.IsSuccess);
        var profile = result.Value;
        Assert.True(profile.HasItemisedFinancing);
        Assert.Equal(6, profile.FinancingLines.Count);
        Assert.Equal(3, profile.FinancingCharges.Count);
        Assert.Equal(InputsTotal, profile.FinancingLines.Sum(l => l.Amount));
        Assert.Equal(ChargesTotal, profile.FinancingCharges.Sum(c => c.Amount));
        Assert.Equal(0m, profile.ExcludedFromLoan);
    }

    [Fact]
    public void StoresTheRowsAsSent_WithoutRecomputingThem()
    {
        // The portal's spec is explicit: unit prices of supplier-pack lines are rounded down to the
        // kobo, so amount is NOT quantity x unitPrice x farmers. The stored amount must be the sent
        // one — this line's arithmetic deliberately does not multiply out.
        var result = Create(LoanAmount, Financing());

        var npk = result.Value.FinancingLines.First(l => l.Item == "NPK Fertilizer");
        Assert.Equal(Npk, npk.Amount);
        Assert.Equal("NPK 20:10:10", npk.SpecificInput);
        Assert.Equal("Example Supplier Ltd", npk.Supplier);
        Assert.Equal("10 Kg", npk.QuantityDisplay);
        Assert.Equal("NPK 20:10:10", npk.Description);
        Assert.NotEqual(npk.Amount, (npk.QuantityPerFarmer ?? 0m) * (npk.UnitPrice ?? 0m));
    }

    [Fact]
    public void KeepsThePortalsOwnLabelAndRate_SoTheLetterStatesTheChargeTheProgrammesWay()
    {
        var result = Create(LoanAmount, Financing());

        var insurance = result.Value.FinancingCharges.First(c => c.Code == "insurance");
        Assert.Equal("Insurance", insurance.Label);
        Assert.Equal(4m, insurance.Rate);
        Assert.Equal(Insurance, insurance.Amount);

        var techFee = result.Value.FinancingCharges.First(c => c.Code == "techServicesFee");
        Assert.Equal(4_000m, techFee.Rate); // naira per farmer, not a percentage
        Assert.Equal(TechServicesFee, techFee.Amount);
    }

    [Fact]
    public void AllowsANullRate_WhenTheCasesEopsUsedDifferentOnes()
    {
        // Printing one rate across a case whose EOPs were approved at different rates would state
        // something untrue, so the portal sends null and the letter shows the amount alone.
        var charges = new List<RhshfFinancingChargeInput>
        {
            new("insurance", "Insurance", null, "Varies by EOP", Insurance, true),
            new("workingCapital", "Working capital", 5m, "…", WorkingCapital, true),
            new("techServicesFee", "Technology services fee", 4_000m, "…", TechServicesFee, true),
        };

        var result = Create(LoanAmount, Financing(charges: charges));

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.FinancingCharges.First(c => c.Code == "insurance").Rate);
    }

    // ── Rule 1 ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Rule1_RefusesWhenTheBreakdownDoesNotMakeTheApprovedTotal()
    {
        // The double-charge shape: the charges are already inside the total, then counted again.
        var result = Create(LoanAmount, Financing(approvedEopTotal: InputsTotal));

        Assert.True(result.IsFailure);
        Assert.Contains("rule 1", result.Error);
        Assert.Contains("4,019,800.00", result.Error);
    }

    [Fact]
    public void Rule1_CountsPackagingExcess()
    {
        // Whole supplier packs bought above the per-farmer requirement are financed too. Omitting
        // them from the check would let a case through whose plan is short by exactly that amount.
        var withExcess = Lines().Select((l, i) => i == 0 ? l with { PackagingExcess = 5_000m } : l).ToList();

        Assert.True(Create(LoanAmount, Financing(lines: withExcess)).IsFailure);
        Assert.True(Create(LoanAmount + 5_000m, Financing(
            lines: withExcess, approvedEopTotal: LoanAmount + 5_000m, loanAmount: LoanAmount + 5_000m)).IsSuccess);
    }

    // ── Rule 2 ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Rule2_RefusesWhenTheFinancedRowsDoNotMakeTheLoanAmount()
    {
        // A row excluded from the loan while the loan amount still claims the full total.
        var lines = Lines().Select((l, i) => i == 4 ? l with { InLoan = false } : l).ToList();

        var result = Create(LoanAmount, Financing(lines: lines));

        Assert.True(result.IsFailure);
        Assert.Contains("rule 2", result.Error);
    }

    [Fact]
    public void Rule2_AcceptsAnExcludedRow_WhenTheLoanAmountReflectsIt()
    {
        // excludedFromLoan is zero today, but the field exists — so a case that finances less than
        // it approves must reconcile, not be rejected on an assumption that the two are equal.
        var lines = Lines().Select((l, i) => i == 4 ? l with { InLoan = false } : l).ToList();
        var financed = LoanAmount - Seeds;

        var result = Create(financed, Financing(
            lines: lines, approvedEopTotal: LoanAmount, loanAmount: financed, excludedFromLoan: Seeds));

        Assert.True(result.IsSuccess);
        Assert.Equal(Seeds, result.Value.ExcludedFromLoan);
        Assert.Equal(financed, result.Value.FinancedLoanAmount);
    }

    // ── Rule 3 ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Rule3_RefusesWhenTheLoanAmountIsNotTheProfiledAmount()
    {
        // Otherwise the case is approved for one figure and the letter printed from another.
        var result = Create(LoanAmount + 1_000m, Financing(approvedEopTotal: LoanAmount, loanAmount: LoanAmount));

        Assert.True(result.IsFailure);
        Assert.Contains("rule 3", result.Error);
    }

    // ── Tolerance and absence ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void ToleratesAKoboOfRounding_ButNotANaira()
    {
        Assert.True(Create(LoanAmount, Financing(approvedEopTotal: LoanAmount + 0.01m)).IsSuccess);
        Assert.True(Create(LoanAmount, Financing(approvedEopTotal: LoanAmount + 1.00m)).IsFailure);
    }

    [Fact]
    public void SkipsTheCheckEntirely_WhenNoFinancingWasSent()
    {
        // The portal is rolling this out separately; a submission without it must behave exactly as
        // before. The letter falls back to the crop-level lines and one balancing row.
        var result = Create(LoanAmount, financing: null);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.HasItemisedFinancing);
    }

    // ── Per-EOP summary, financed farmers and late breakdowns (CRMSQA#2) ─────────────────────────

    private static readonly Guid EopId = Guid.Parse("9f982067-fff9-47f4-9b20-6e9b2c717606");

    private static RhshfFinancingInput OneEop(int farmers = 7, decimal? summaryInputs = null) => new(
        [new RhshfFinancingLineInput(EopId, "Maize", "NPK Fertilizer", "Fertilizer", null, "Supplier", 2m, "Kg", 1_000m,
            "perFarmer", farmers, 14_000m, 0m, true)],
        [new RhshfFinancingChargeInput("insurance", "Insurance", 4m, null, 560m, true)],
        InputsTotal: 14_000m, PackagingExcessTotal: 0m, ChargesTotal: 560m,
        ApprovedEopTotal: 14_560m, LoanAmount: 14_560m, ExcludedFromLoan: 0m,
        Eops: [new RhshfFinancingEopInput(EopId, "Maize", "Kano", farmers, summaryInputs ?? 14_000m, 0m, 560m, 0m, 0m, 14_560m, 14_560m)]);

    [Fact]
    public void FinancedFarmers_ComeFromTheEops_NotTheFacsRegisteredCount()
    {
        // The portal sends farmerCount as everyone registered under the FAC (10 here); the EOP says 7.
        var profile = Create(14_560m, OneEop(farmers: 7)).Value;

        Assert.Equal(10, profile.FarmerCount);
        Assert.Equal(7, profile.FinancedFarmerCount);
    }

    [Fact]
    public void Rule4_RejectsAnEopSummaryThatDisagreesWithItsOwnLines()
    {
        var result = Create(14_560m, OneEop(summaryInputs: 15_000m));

        Assert.True(result.IsFailure);
        Assert.Contains("rule 4", result.Error);
    }

    [Fact]
    public void LateBreakdown_IsTakenOnACaseWithoutOne_AndNeverReplacesOne()
    {
        var profile = Create(14_560m, financing: null).Value;
        Assert.True(profile.CanTakeLateFinancing);

        var attached = profile.AttachLateFinancing(OneEop());

        Assert.True(attached.IsSuccess);
        Assert.True(profile.HasItemisedFinancing);
        Assert.Single(profile.FinancingEops);
        Assert.False(profile.CanTakeLateFinancing);
        Assert.True(profile.AttachLateFinancing(OneEop()).IsFailure);
        Assert.Single(profile.FinancingLines);
    }

    [Fact]
    public void LateBreakdown_ThatDoesNotReconcile_LeavesTheCaseExactlyAsItWas()
    {
        var profile = Create(14_560m, financing: null).Value;

        var attached = profile.AttachLateFinancing(OneEop(summaryInputs: 15_000m));

        Assert.True(attached.IsFailure);
        Assert.False(profile.HasItemisedFinancing);
        Assert.Empty(profile.FinancingEops);
        Assert.Null(profile.ApprovedEopTotal);
    }

    [Fact]
    public void Backfill_AddsTheEopSummary_OnlyWhereNoneIsHeld()
    {
        var withoutEops = OneEop() with { Eops = null };
        var profile = Create(14_560m, withoutEops).Value;
        Assert.Empty(profile.FinancingEops);

        var eops = OneEop().Eops!;
        Assert.True(profile.BackfillFinancingEops(eops).IsSuccess);
        Assert.Equal(7, profile.FinancedFarmerCount);
        Assert.True(profile.BackfillFinancingEops(eops).IsFailure);
        Assert.Single(profile.FinancingEops);
    }

    // ── Fixtures ──────────────────────────────────────────────────────────────────────────────────

    private static List<RhshfFinancingLineInput> Lines() =>
    [
        new(EopId: Guid.NewGuid(), Crop: "Maize", Item: "NPK Fertilizer", Category: "Fertilizer",
            SpecificInput: "NPK 20:10:10", Supplier: "Example Supplier Ltd", QuantityPerFarmer: 10m,
            Unit: "Kg", UnitPrice: 1_120.00m, Basis: "perFarmer", Farmers: 10,
            Amount: Npk, PackagingExcess: 0m, InLoan: true),
        Line("Urea Fertilizer", Urea),
        Line("Herbicide", Herbicide),
        Line("Pesticide", Pesticide),
        Line("Improved Seeds", Seeds),
        new(EopId: null, Crop: "Maize", Item: "Extension Services", Category: "Services",
            SpecificInput: null, Supplier: null, QuantityPerFarmer: null, Unit: "Per Farmer",
            UnitPrice: null, Basis: "perFarmer", Farmers: 10,
            Amount: 0m, PackagingExcess: 0m, InLoan: true),
    ];

    private static RhshfFinancingLineInput Line(string item, decimal amount) =>
        new(Guid.NewGuid(), "Maize", item, null, null, "Example Supplier Ltd", 1m, "Kg", amount,
            "perFarmer", 10, amount, 0m, true);

    private static List<RhshfFinancingChargeInput> DefaultCharges() =>
    [
        new("insurance", "Insurance", 4m, "4% of the per-farmer input subtotal", Insurance, true),
        new("workingCapital", "Working capital", 5m, "5% of the per-farmer input subtotal", WorkingCapital, true),
        new("techServicesFee", "Technology services fee", 4_000m, "NGN 4,000 per farmer", TechServicesFee, true),
    ];

    private static RhshfFinancingInput Financing(
        List<RhshfFinancingLineInput>? lines = null,
        List<RhshfFinancingChargeInput>? charges = null,
        decimal? approvedEopTotal = null,
        decimal? loanAmount = null,
        decimal? excludedFromLoan = null)
    {
        var resolvedLines = lines ?? Lines();
        var resolvedCharges = charges ?? DefaultCharges();
        return new RhshfFinancingInput(
            resolvedLines, resolvedCharges,
            InputsTotal: resolvedLines.Sum(l => l.Amount),
            PackagingExcessTotal: resolvedLines.Sum(l => l.PackagingExcess),
            ChargesTotal: resolvedCharges.Sum(c => c.Amount),
            ApprovedEopTotal: approvedEopTotal ?? LoanAmount,
            LoanAmount: loanAmount ?? LoanAmount,
            ExcludedFromLoan: excludedFromLoan ?? 0m);
    }

    private static Result<RhshfCreditProfile> Create(decimal totalEopValue, RhshfFinancingInput? financing) =>
        RhshfCreditProfile.Create(
            submissionId: Guid.NewGuid(), programmeCode: "RH-SHF-DRY-2026", programmeName: "Renewed Hope",
            sessionCode: "2026-DRY", sessionName: "Dry Season 2026", facId: Guid.NewGuid(),
            companyName: "Iveren Agro Limited", rcNumber: "RC771204", tin: null,
            boaAccountNumber: "0123456789", contactEmail: "fac@company.com", contactPhone: "+2348012345678",
            state: "Yobe", lga: "Damaturu", totalEopValue: totalEopValue, currency: "NGN", farmerCount: 10,
            callbackUrl: "https://portal.example.gov.ng/api/integrations/crms/webhook",
            certifiedByAdmin: "admin@boa.gov.ng", certifiedAt: DateTime.UtcNow, rawSubmissionPayload: "{}",
            eopLines: null, resolvedBranchId: null, resolvedOfficeId: null, financing: financing);
}

using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Application.Tests.Rhshf;

/// <summary>
/// Appraise(Proceed) now requires a saved financial appraisal behind it (the guard NAMP declares
/// but never wires up). Tests whose subject is a *later* stage shouldn't each have to restate that
/// setup, so this seeds a viable farm plan + appraisal and then appraises.
///
/// Tests that are specifically about the appraisal gate call Appraise directly instead.
/// </summary>
internal static class RhshfAppraisalTestExtensions
{
    // One sizing rule for every fixture plan: revenue ≈ 3× the financed inputs, which clears
    // every viability gate with room to spare regardless of the case's EOP value.
    private const string SeedCrop = "Maize";
    private const decimal SeedHectares = 100m;
    private const decimal SeedYieldPerHectare = 3_000m;

    internal static decimal ViablePricePerKg(RhshfCreditProfile profile) =>
        profile.TotalEopValue * 3m / (SeedHectares * SeedYieldPerHectare);

    internal static RhshfAppraisalThresholds DefaultThresholds() =>
        RhshfAppraisalThresholds.Create(
            minDscr: 1.25m, minGrossMarginPercent: 15m, hurdleRatePercent: 15m,
            minYieldHeadroomPercent: 10m, minPriceHeadroomPercent: 10m).Value;

    /// <summary>Seeds a comfortably viable plan sized off the case's own EOP value, so the gates
    /// pass without the caller having to reason about the numbers.</summary>
    internal static void SeedViableAppraisal(this RhshfCreditProfile profile, Guid creditOfficerId)
    {
        if (profile.GetCurrentCycleFinancialAppraisal() is not null) return;

        if (profile.GetCurrentCycleFarmPlans().Count == 0)
        {
            var plan = RhshfFarmPlan.Create(
                profile.Id, profile.CurrentCycleNumber, SeedCrop,
                SeedHectares, SeedYieldPerHectare, ViablePricePerKg(profile), creditOfficerId).Value;
            profile.AddFarmPlan(plan);
        }

        profile.SaveFinancialAppraisal(
            preparedByUserId: creditOfficerId,
            ownProductionCost: profile.TotalEopValue * 0.1m,
            harvestAndLogisticsCost: profile.TotalEopValue * 0.05m,
            cycleMonths: 6,
            interestRatePercent: 12m,
            assumptionBasisNote: "Test fixture",
            thresholds: DefaultThresholds(),
            recommendation: RhshfCreditRecommendation.Pass,
            summaryNotes: null,
            overrideJustification: null);
    }

    /// <summary>
    /// Drop-in for AdvanceStage in profiling walk-throughs. Submission now requires a farm plan
    /// (the agronomic basis the appraisal depends on), so this adds one while passing through the
    /// EOP review stage where the FAC would enter it.
    ///
    /// The plan is sized off the case's own EOP value so that it is comfortably viable — tests
    /// about later stages inherit it via SeedViableAppraisal and must not trip the gates.
    ///
    /// Tests whose subject is the submission gate itself call AdvanceStage directly.
    /// </summary>
    internal static Result AdvanceStageForTest(this RhshfCreditProfile profile, RhshfProfilingStage stage)
    {
        if (stage == RhshfProfilingStage.EopReview && profile.GetTargetCycleFarmPlans().Count == 0)
            profile.AddFarmPlanDuringProfiling(
                SeedCrop, SeedHectares, SeedYieldPerHectare, ViablePricePerKg(profile));

        return profile.AdvanceStage(stage);
    }

    /// <summary>Drops the fixture-seeded plan so a test can state its own crop economics from
    /// scratch. Uses the officer-side RemoveFarmPlan — no test-only domain surface.</summary>
    internal static void ClearFarmPlansForTest(this RhshfCreditProfile profile)
    {
        foreach (var plan in profile.GetCurrentCycleFarmPlans().ToList())
            profile.RemoveFarmPlan(plan.Id);
    }

    /// <summary>Drop-in for Appraise in tests about later stages — seeds the prerequisites first
    /// when the outcome is Proceed.</summary>
    internal static Result AppraiseWithFinancials(
        this RhshfCreditProfile profile, Guid creditOfficerId, RhshfAppraisalOutcome outcome,
        string? notes, RhshfProfilingStage? returnToStage = null)
    {
        if (outcome == RhshfAppraisalOutcome.Proceed)
            profile.SeedViableAppraisal(creditOfficerId);

        return profile.Appraise(creditOfficerId, outcome, notes, returnToStage);
    }
}

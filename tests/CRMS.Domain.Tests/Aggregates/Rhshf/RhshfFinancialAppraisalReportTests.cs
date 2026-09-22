using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Tests.Aggregates.Rhshf;

public class RhshfFinancialAppraisalReportTests
{
    private const decimal TotalEopValue = 5_000_000m;

    private static RhshfCreditProfile ProfileAtAppraisal()
    {
        var profile = RhshfCreditProfile.Create(
            submissionId: Guid.NewGuid(), programmeCode: "RH-SHF-DRY-2026", programmeName: "Renewed Hope",
            sessionCode: "2026-DRY", sessionName: "Dry Season 2026", facId: Guid.NewGuid(),
            companyName: "Alliedsoft Limited", rcNumber: "RC123456", tin: "01234567-0001",
            boaAccountNumber: "0123456789", contactEmail: "fac@company.com", contactPhone: "+2348012345678",
            state: "Kano", lga: "Nassarawa", totalEopValue: TotalEopValue, currency: "NGN", farmerCount: 1200,
            callbackUrl: "https://portal.example.gov.ng/api/integrations/crms/webhook",
            certifiedByAdmin: "admin@boa.gov.ng", certifiedAt: DateTime.UtcNow, rawSubmissionPayload: "{}",
            eopLines: null, resolvedBranchId: null, resolvedOfficeId: null).Value;

        foreach (var stage in new[]
        {
            RhshfProfilingStage.CompanyVerification, RhshfProfilingStage.CreditBureauCheck,
            RhshfProfilingStage.EopReview, RhshfProfilingStage.SupportingDocuments, RhshfProfilingStage.ReviewAndSubmit,
        })
        {
            profile.AdvanceStageForTest(stage);
        }

        // Every test here states its own crop economics — drop the fixture's placeholder plan.
        profile.ClearFarmPlansForTest();
        return profile;
    }

    private static void AddPlan(RhshfCreditProfile profile, decimal hectares, decimal yieldPerHa, decimal pricePerKg, string crop = "Maize")
        => profile.AddFarmPlan(RhshfFarmPlan.Create(
            profile.Id, profile.CurrentCycleNumber, crop, hectares, yieldPerHa, pricePerKg, Guid.NewGuid()).Value);

    private static Result Save(
        RhshfCreditProfile profile, RhshfCreditRecommendation recommendation = RhshfCreditRecommendation.Pass,
        string? overrideJustification = null, decimal ownCost = 300_000m, decimal harvestCost = 200_000m)
        => profile.SaveFinancialAppraisal(
            preparedByUserId: Guid.NewGuid(), ownProductionCost: ownCost, harvestAndLogisticsCost: harvestCost,
            cycleMonths: 6, interestRatePercent: 12m, assumptionBasisNote: "Off-take agreement with XYZ Foods",
            thresholds: RhshfAppraisalTestExtensions.DefaultThresholds(),
            recommendation: recommendation, summaryNotes: null, overrideJustification: overrideJustification);

    [Fact]
    public void Save_ViablePlan_ComputesMetricsAndPassesAllGates()
    {
        var profile = ProfileAtAppraisal();
        AddPlan(profile, hectares: 50m, yieldPerHa: 3_000m, pricePerKg: 100m); // revenue 15,000,000

        var result = Save(profile);

        Assert.True(result.IsSuccess);
        var report = profile.GetCurrentCycleFinancialAppraisal()!;
        Assert.Equal(15_000_000m, report.GrossRevenue);
        Assert.Equal(TotalEopValue, report.FinancedInputCost);
        Assert.True(report.AllGatesPass);
        Assert.Equal(5, report.GatesPassed);
        Assert.Equal(RhshfRepaymentCapacityRating.Strong, report.RepaymentCapacityRating);
    }

    [Fact]
    public void Save_ComputedFiguresAreDerived_NotAcceptedFromCaller()
    {
        // The whole point of moving the calculator into the domain: the officer supplies
        // assumptions, the system supplies the arithmetic.
        var profile = ProfileAtAppraisal();
        AddPlan(profile, 50m, 3_000m, 100m);

        Save(profile);
        var report = profile.GetCurrentCycleFinancialAppraisal()!;

        var expected = RhshfViabilityCalculator.Compute(
            grossRevenue: 15_000_000m, financedInputCost: TotalEopValue,
            ownProductionCost: 300_000m, harvestAndLogisticsCost: 200_000m,
            interestRatePercent: 12m, cycleMonths: 6);

        Assert.Equal(expected.Dscr, report.Dscr);
        Assert.Equal(expected.GrossMargin, report.GrossMargin);
        Assert.Equal(expected.AmountDueAtHarvest, report.AmountDueAtHarvest);
    }

    [Fact]
    public void Save_PassRecommendationWithFailingGates_IsRejectedWithoutJustification()
    {
        // NAMP accepts Pass with every metric failing. Here it is refused.
        var profile = ProfileAtAppraisal();
        AddPlan(profile, hectares: 10m, yieldPerHa: 500m, pricePerKg: 100m); // revenue 500,000 — far under water

        var result = Save(profile, RhshfCreditRecommendation.Pass, overrideJustification: null);

        Assert.True(result.IsFailure);
        Assert.Contains("override justification", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Null(profile.GetCurrentCycleFinancialAppraisal());
    }

    [Fact]
    public void Save_PassWithFailingGates_IsAllowedWhenJustified_AndTheJustificationIsRetained()
    {
        var profile = ProfileAtAppraisal();
        AddPlan(profile, 10m, 500m, 100m);

        var result = Save(profile, RhshfCreditRecommendation.Pass,
            overrideJustification: "NIRSAL CRG covers 75% of exposure; committee briefed.");

        Assert.True(result.IsSuccess);
        var report = profile.GetCurrentCycleFinancialAppraisal()!;
        Assert.False(report.AllGatesPass);
        Assert.Contains("NIRSAL", report.OverrideJustification);
    }

    [Fact]
    public void Save_FailRecommendationWithFailingGates_NeedsNoJustification()
    {
        var profile = ProfileAtAppraisal();
        AddPlan(profile, 10m, 500m, 100m);

        var result = Save(profile, RhshfCreditRecommendation.Fail, overrideJustification: null);

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfCreditRecommendation.Fail, profile.GetCurrentCycleFinancialAppraisal()!.CreditOfficerRecommendation);
    }

    [Fact]
    public void Save_JustificationIsClearedWhenAllGatesPass()
    {
        // A stale override note left on a now-passing appraisal would misrepresent the decision.
        var profile = ProfileAtAppraisal();
        AddPlan(profile, 50m, 3_000m, 100m);

        Save(profile, RhshfCreditRecommendation.Pass, overrideJustification: "not needed");

        Assert.Null(profile.GetCurrentCycleFinancialAppraisal()!.OverrideJustification);
    }

    [Fact]
    public void Save_WithoutFarmPlans_IsRejected()
    {
        var profile = ProfileAtAppraisal();

        var result = Save(profile);

        Assert.True(result.IsFailure);
        Assert.Contains("farm plan", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Save_Twice_UpdatesInPlace_RatherThanStackingReportsForTheCycle()
    {
        var profile = ProfileAtAppraisal();
        AddPlan(profile, 50m, 3_000m, 100m);
        Save(profile);

        Save(profile, ownCost: 900_000m);

        Assert.Single(profile.FinancialAppraisals);
        Assert.Equal(900_000m, profile.GetCurrentCycleFinancialAppraisal()!.OwnProductionCost);
    }

    [Fact]
    public void Save_SnapshotsThresholdsInForceAtTheTime()
    {
        var profile = ProfileAtAppraisal();
        AddPlan(profile, 50m, 3_000m, 100m);

        Save(profile);

        var report = profile.GetCurrentCycleFinancialAppraisal()!;
        Assert.Equal(1.25m, report.ThresholdMinDscr);
        Assert.Equal(15m, report.ThresholdHurdleRatePercent);
    }

    [Fact]
    public void Save_MultipleCrops_BlendsYieldAndPriceAcrossTheWholeFarmPlan()
    {
        var profile = ProfileAtAppraisal();
        AddPlan(profile, hectares: 50m, yieldPerHa: 3_000m, pricePerKg: 100m, crop: "Maize"); // 150,000kg / 15,000,000
        AddPlan(profile, hectares: 50m, yieldPerHa: 1_000m, pricePerKg: 300m, crop: "Rice");  //  50,000kg / 15,000,000

        Save(profile);
        var report = profile.GetCurrentCycleFinancialAppraisal()!;

        Assert.Equal(100m, report.TotalHectares);
        Assert.Equal(30_000_000m, report.GrossRevenue);
        Assert.Equal(2_000m, report.BlendedYieldKgPerHectare);          // 200,000kg / 100ha
        Assert.Equal(150m, report.BlendedPricePerKg);                   // 30,000,000 / 200,000kg
    }

    [Fact]
    public void AddFarmPlan_DuplicateCropInSameCycle_IsRejected()
    {
        var profile = ProfileAtAppraisal();
        AddPlan(profile, 50m, 3_000m, 100m, "Maize");

        var duplicate = RhshfFarmPlan.Create(profile.Id, profile.CurrentCycleNumber, "maize", 10m, 1_000m, 50m, Guid.NewGuid()).Value;
        var result = profile.AddFarmPlan(duplicate);

        Assert.True(result.IsFailure);
        Assert.Single(profile.GetCurrentCycleFarmPlans());
    }

    [Fact]
    public void SaveFinancialAppraisal_WhenNotAtAppraisalStage_IsRejected()
    {
        var profile = ProfileAtAppraisal();
        AddPlan(profile, 50m, 3_000m, 100m);
        Save(profile);
        profile.Appraise(Guid.NewGuid(), RhshfAppraisalOutcome.Proceed, null); // now at RiskReview

        var result = Save(profile);

        Assert.True(result.IsFailure);
    }
}

public class RhshfFarmPlanTests
{
    private static readonly Guid ProfileId = Guid.NewGuid();

    [Fact]
    public void Create_ValidInput_DerivesRevenueAndOutput()
    {
        var plan = RhshfFarmPlan.Create(ProfileId, 1, "Maize", 20m, 2_500m, 120m, Guid.NewGuid()).Value;

        Assert.Equal(50_000m, plan.ExpectedOutputKg);       // 20 × 2,500
        Assert.Equal(6_000_000m, plan.ExpectedRevenue);     // 50,000 × 120
    }

    [Theory]
    [InlineData(0, 2_500, 120)]
    [InlineData(20, 0, 120)]
    [InlineData(20, 2_500, 0)]
    [InlineData(-5, 2_500, 120)]
    public void Create_NonPositiveInputs_Fail(decimal hectares, decimal yieldPerHa, decimal pricePerKg)
        => Assert.True(RhshfFarmPlan.Create(ProfileId, 1, "Maize", hectares, yieldPerHa, pricePerKg, Guid.NewGuid()).IsFailure);

    [Fact]
    public void Create_MissingCrop_Fails()
        => Assert.True(RhshfFarmPlan.Create(ProfileId, 1, "  ", 20m, 2_500m, 120m, Guid.NewGuid()).IsFailure);
}

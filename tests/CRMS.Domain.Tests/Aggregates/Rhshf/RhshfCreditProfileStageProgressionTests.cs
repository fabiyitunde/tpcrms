using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Tests.Aggregates.Rhshf;

public class RhshfCreditProfileStageProgressionTests
{
    private static RhshfCreditProfile CreateValidProfile()
    {
        var result = RhshfCreditProfile.Create(
            submissionId: Guid.NewGuid(), programmeCode: "RH-SHF-DRY-2026", programmeName: "Renewed Hope",
            sessionCode: "2026-DRY", sessionName: "Dry Season 2026", facId: Guid.NewGuid(),
            companyName: "Alliedsoft Limited", rcNumber: "RC123456", tin: "01234567-0001",
            boaAccountNumber: "0123456789", contactEmail: "fac@company.com", contactPhone: "+2348012345678",
            state: "Kano", lga: "Nassarawa", totalEopValue: 51_500_000.00m, currency: "NGN", farmerCount: 1200,
            callbackUrl: "https://portal.example.gov.ng/api/integrations/crms/webhook",
            certifiedByAdmin: "admin@boa.gov.ng", certifiedAt: DateTime.UtcNow, rawSubmissionPayload: "{}",
            eopLines: null, resolvedBranchId: null, resolvedOfficeId: null);
        return result.Value;
    }

    [Fact]
    public void AdvanceStage_WalksThroughAllFiveStages_ThenCompletesProfiling()
    {
        var profile = CreateValidProfile();
        var stages = new[]
        {
            RhshfProfilingStage.CompanyVerification, RhshfProfilingStage.CreditBureauCheck,
            RhshfProfilingStage.EopReview, RhshfProfilingStage.SupportingDocuments, RhshfProfilingStage.ReviewAndSubmit,
        };

        foreach (var stage in stages)
        {
            Assert.Equal(stage, profile.CurrentStage);
            var result = profile.AdvanceStageForTest(stage);
            Assert.True(result.IsSuccess);
        }

        Assert.Null(profile.CurrentStage);
        Assert.Equal(RhshfCaseStatus.UnderReview, profile.Status);
    }

    [Fact]
    public void AdvanceStage_FirstCall_FlipsStatusFromProfilingPendingToInProgress()
    {
        var profile = CreateValidProfile();
        Assert.Equal(RhshfCaseStatus.ProfilingPending, profile.Status);

        profile.AdvanceStage(RhshfProfilingStage.CompanyVerification);

        Assert.Equal(RhshfCaseStatus.ProfilingInProgress, profile.Status);
    }

    [Fact]
    public void AdvanceStage_SkippingAStage_Fails()
    {
        var profile = CreateValidProfile();

        var result = profile.AdvanceStage(RhshfProfilingStage.EopReview); // skipping stages 1-2

        Assert.True(result.IsFailure);
        Assert.Equal(RhshfProfilingStage.CompanyVerification, profile.CurrentStage);
    }

    [Fact]
    public void AdvanceStage_ReplayingACompletedStage_Fails()
    {
        var profile = CreateValidProfile();
        profile.AdvanceStage(RhshfProfilingStage.CompanyVerification);

        var replay = profile.AdvanceStage(RhshfProfilingStage.CompanyVerification);

        Assert.True(replay.IsFailure);
    }

    [Fact]
    public void RecordBureauCheck_OnWrongStage_Fails()
    {
        var profile = CreateValidProfile(); // still on CompanyVerification

        var result = profile.RecordBureauCheck(RhshfBureauOutcome.Cleared, 0, 0, 0, 0, 0, null);

        Assert.True(result.IsFailure);
        Assert.Equal(RhshfBureauOutcome.NotRun, profile.BureauCheckOutcome);
    }

    [Fact]
    public void RecordBureauCheck_OnCorrectStage_Succeeds()
    {
        var profile = CreateValidProfile();
        profile.AdvanceStage(RhshfProfilingStage.CompanyVerification); // now on CreditBureauCheck

        var result = profile.RecordBureauCheck(RhshfBureauOutcome.Flagged, 5, 2, 1, 1_000_000m, 50_000m, "{}");

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfBureauOutcome.Flagged, profile.BureauCheckOutcome);
        Assert.Equal(1, profile.BureauDelinquentFacilities);
    }

    [Fact]
    public void AddSupportingDocument_BeforeReachingThatStage_Fails()
    {
        var profile = CreateValidProfile(); // on CompanyVerification

        var result = profile.AddSupportingDocument(RhshfDocumentCategory.Other, "cac.pdf", "application/pdf", "path/cac.pdf", 1024);

        Assert.True(result.IsFailure);
        Assert.Empty(profile.SupportingDocuments);
    }

    [Fact]
    public void AddSupportingDocument_OnCorrectStage_AddsMultipleFilesWithoutForcingAdvance()
    {
        var profile = CreateValidProfile();
        profile.AdvanceStage(RhshfProfilingStage.CompanyVerification);
        profile.AdvanceStage(RhshfProfilingStage.CreditBureauCheck);
        profile.AdvanceStage(RhshfProfilingStage.EopReview); // now on SupportingDocuments

        var first = profile.AddSupportingDocument(RhshfDocumentCategory.Other, "cac.pdf", "application/pdf", "path/cac.pdf", 1024);
        var second = profile.AddSupportingDocument(RhshfDocumentCategory.Other, "bank-statement.pdf", "application/pdf", "path/bs.pdf", 2048);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(2, profile.SupportingDocuments.Count);
        Assert.Equal(RhshfProfilingStage.SupportingDocuments, profile.CurrentStage); // adding a doc doesn't advance
    }

    // ── Phase D gates: profiling is now attested and gated, not just traversed ────────────

    [Fact]
    public void AdvanceStage_FromReviewAndSubmit_WithoutAFarmPlan_IsBlocked()
    {
        // The Credit Officer's appraisal is built on the crop economics. A case submitted without
        // them arrives unmodellable, which is how the old form let cases through.
        var profile = CreateValidProfile();
        foreach (var stage in new[]
        {
            RhshfProfilingStage.CompanyVerification, RhshfProfilingStage.CreditBureauCheck,
            RhshfProfilingStage.EopReview, RhshfProfilingStage.SupportingDocuments,
        })
        {
            profile.AdvanceStage(stage);
        }

        var result = profile.AdvanceStage(RhshfProfilingStage.ReviewAndSubmit);

        Assert.True(result.IsFailure);
        Assert.Contains("farm plan", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(RhshfProfilingStage.ReviewAndSubmit, profile.CurrentStage);
        Assert.Equal(RhshfCaseStatus.ProfilingInProgress, profile.Status);
    }

    [Fact]
    public void AddFarmPlanDuringProfiling_FilesUnderTheCycleTheCaseIsAboutToEnter()
    {
        // CurrentCycleNumber is still 0 during profiling and only increments on submit, so a plan
        // filed under it would be invisible to the appraisal that reads the current cycle.
        var profile = CreateValidProfile();
        profile.AdvanceStage(RhshfProfilingStage.CompanyVerification);
        profile.AdvanceStage(RhshfProfilingStage.CreditBureauCheck); // now on EopReview

        var added = profile.AddFarmPlanDuringProfiling("Maize", 100m, 3_000m, 515m);

        Assert.True(added.IsSuccess);
        Assert.Equal(0, profile.CurrentCycleNumber);
        Assert.Equal(1, profile.FarmPlans.Single().CycleNumber);

        profile.AdvanceStage(RhshfProfilingStage.EopReview);
        profile.AdvanceStage(RhshfProfilingStage.SupportingDocuments);
        profile.AdvanceStage(RhshfProfilingStage.ReviewAndSubmit);

        Assert.Equal(1, profile.CurrentCycleNumber);
        Assert.Single(profile.GetCurrentCycleFarmPlans());
    }

    [Fact]
    public void AddFarmPlanDuringProfiling_OutsideTheEopReviewStage_Fails()
    {
        var profile = CreateValidProfile(); // still on CompanyVerification

        var result = profile.AddFarmPlanDuringProfiling("Maize", 100m, 3_000m, 515m);

        Assert.True(result.IsFailure);
        Assert.Empty(profile.FarmPlans);
    }

    [Fact]
    public void AdvanceStage_FromSupportingDocuments_WithAMandatoryCategoryMissing_IsBlocked()
    {
        var profile = ProfileOnSupportingDocuments();
        profile.AddSupportingDocument(RhshfDocumentCategory.CacCertificate, "cac.pdf", "application/pdf", "p/cac.pdf", 1024);

        var result = profile.AdvanceStage(RhshfProfilingStage.SupportingDocuments, MandatoryCacAndOffTake());

        Assert.True(result.IsFailure);
        Assert.Contains(nameof(RhshfDocumentCategory.OffTakeAgreement), result.Error);
        Assert.Equal(RhshfProfilingStage.SupportingDocuments, profile.CurrentStage);
    }

    [Fact]
    public void AdvanceStage_FromSupportingDocuments_WithEveryMandatoryCategorySatisfied_Succeeds()
    {
        var profile = ProfileOnSupportingDocuments();
        profile.AddSupportingDocument(RhshfDocumentCategory.CacCertificate, "cac.pdf", "application/pdf", "p/cac.pdf", 1024);
        profile.AddSupportingDocument(RhshfDocumentCategory.OffTakeAgreement, "offtake.pdf", "application/pdf", "p/ot.pdf", 2048);

        var result = profile.AdvanceStage(RhshfProfilingStage.SupportingDocuments, MandatoryCacAndOffTake());

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfProfilingStage.ReviewAndSubmit, profile.CurrentStage);
    }

    [Fact]
    public void AdvanceStage_InactiveOrOptionalRequirements_DoNotBlockSubmission()
    {
        var profile = ProfileOnSupportingDocuments();
        var optional = RhshfDocumentRequirement.Create(
            RhshfDocumentCategory.BankStatement, "Bank statement", null, isMandatory: false, sortOrder: 1).Value;
        var retired = RhshfDocumentRequirement.Create(
            RhshfDocumentCategory.LandDocumentation, "Land documentation", null, isMandatory: true, sortOrder: 2).Value;
        retired.Deactivate();

        var result = profile.AdvanceStage(RhshfProfilingStage.SupportingDocuments, [optional, retired]);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void AdvanceStage_RecordsAStageConfirmation_WithTheRequestContext()
    {
        var profile = CreateValidProfile();

        profile.AdvanceStage(RhshfProfilingStage.CompanyVerification, null, "102.89.41.7", "Mozilla/5.0");

        var confirmation = profile.StageConfirmations.Single();
        Assert.Equal(RhshfProfilingStage.CompanyVerification, confirmation.Stage);
        Assert.Equal(profile.FacId, confirmation.ConfirmedByFacId);
        Assert.Equal("102.89.41.7", confirmation.IpAddress);
        Assert.Equal(1, confirmation.CycleNumber); // the cycle being assembled, not the completed one
    }

    [Fact]
    public void AdvanceStage_BlockedByAGate_RecordsNoConfirmation()
    {
        var profile = ProfileOnSupportingDocuments();

        profile.AdvanceStage(RhshfProfilingStage.SupportingDocuments, MandatoryCacAndOffTake());

        Assert.DoesNotContain(
            profile.StageConfirmations, c => c.Stage == RhshfProfilingStage.SupportingDocuments);
    }

    // ── Back/forward navigation: the FAC can revisit a reached stage to correct a record ─────

    [Fact]
    public void GoToStage_BackToAnEarlierReachedStage_Succeeds()
    {
        var profile = CreateValidProfile();
        profile.AdvanceStage(RhshfProfilingStage.CompanyVerification);
        profile.AdvanceStage(RhshfProfilingStage.CreditBureauCheck); // now on EopReview

        var result = profile.GoToStage(RhshfProfilingStage.CompanyVerification);

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfProfilingStage.CompanyVerification, profile.CurrentStage);
        Assert.Equal(RhshfCaseStatus.ProfilingInProgress, profile.Status);
    }

    [Fact]
    public void GoToStage_BeyondTheHighWaterMark_Fails()
    {
        var profile = CreateValidProfile();
        profile.AdvanceStage(RhshfProfilingStage.CompanyVerification); // reached CreditBureauCheck only

        var result = profile.GoToStage(RhshfProfilingStage.ReviewAndSubmit);

        Assert.True(result.IsFailure);
        Assert.Equal(RhshfProfilingStage.CreditBureauCheck, profile.CurrentStage);
    }

    [Fact]
    public void FurthestProfilingStageReached_HoldsTheHighWaterMark_AfterNavigatingBack()
    {
        var profile = CreateValidProfile();
        profile.AdvanceStage(RhshfProfilingStage.CompanyVerification);
        profile.AdvanceStage(RhshfProfilingStage.CreditBureauCheck);
        profile.AdvanceStage(RhshfProfilingStage.EopReview); // furthest = SupportingDocuments

        profile.GoToStage(RhshfProfilingStage.CompanyVerification);

        Assert.Equal(RhshfProfilingStage.SupportingDocuments, profile.FurthestProfilingStageReached());
        // and can jump forward again to anything within the frontier without re-confirming each step
        var forward = profile.GoToStage(RhshfProfilingStage.SupportingDocuments);
        Assert.True(forward.IsSuccess);
        Assert.Equal(RhshfProfilingStage.SupportingDocuments, profile.CurrentStage);
    }

    [Fact]
    public void AdvanceStage_ReConfirmingAfterNavigatingBack_DoesNotDuplicateTheConfirmation()
    {
        var profile = CreateValidProfile();
        profile.AdvanceStage(RhshfProfilingStage.CompanyVerification);
        profile.GoToStage(RhshfProfilingStage.CompanyVerification);

        var reconfirm = profile.AdvanceStage(RhshfProfilingStage.CompanyVerification);

        Assert.True(reconfirm.IsSuccess);
        Assert.Equal(RhshfProfilingStage.CreditBureauCheck, profile.CurrentStage);
        Assert.Single(profile.StageConfirmations, c => c.Stage == RhshfProfilingStage.CompanyVerification);
    }

    [Fact]
    public void AdvanceStage_AtSubmit_ReChecksMandatoryDocuments_InCaseOneWasRemovedAfterNavigatingBack()
    {
        // Reach ReviewAndSubmit having left the documents stage without the gate running (null
        // requirements) — the shape of a doc removed on a back-visit then a forward jump to submit.
        var profile = ProfileOnSupportingDocuments();
        profile.AddSupportingDocument(RhshfDocumentCategory.CacCertificate, "cac.pdf", "application/pdf", "p/cac.pdf", 1024);
        profile.AdvanceStage(RhshfProfilingStage.SupportingDocuments); // null reqs — no gate
        Assert.Equal(RhshfProfilingStage.ReviewAndSubmit, profile.CurrentStage);

        var result = profile.AdvanceStage(RhshfProfilingStage.ReviewAndSubmit, MandatoryCacAndOffTake());

        Assert.True(result.IsFailure);
        Assert.Contains(nameof(RhshfDocumentCategory.OffTakeAgreement), result.Error);
        Assert.Equal(RhshfProfilingStage.ReviewAndSubmit, profile.CurrentStage);
        Assert.Equal(RhshfCaseStatus.ProfilingInProgress, profile.Status);
    }

    [Fact]
    public void AdvanceStage_AtSubmit_WithADirectorMissingBvn_IsBlocked_ThenProceedsOnceAdded()
    {
        var profile = CreateValidProfile();
        profile.AdvanceStage(RhshfProfilingStage.CompanyVerification); // on CreditBureauCheck
        var director = RhshfDirector.CreateManual(
            profile.Id, "Jane Doe", bvn: null, shareholdingPercent: null, isChairman: false, email: null, phoneNumber: null).Value;
        profile.AddDirector(director);
        profile.AdvanceStage(RhshfProfilingStage.CreditBureauCheck); // on EopReview
        profile.AddFarmPlanDuringProfiling("Maize", 100m, 3_000m, 515m);
        profile.AdvanceStage(RhshfProfilingStage.EopReview);
        profile.AdvanceStage(RhshfProfilingStage.SupportingDocuments);

        var blocked = profile.AdvanceStage(RhshfProfilingStage.ReviewAndSubmit);

        Assert.True(blocked.IsFailure);
        Assert.Contains("BVN", blocked.Error);
        Assert.Equal(RhshfProfilingStage.ReviewAndSubmit, profile.CurrentStage);
        Assert.Equal(RhshfCaseStatus.ProfilingInProgress, profile.Status);

        director.UpdateBvn("12345678901");
        var ok = profile.AdvanceStage(RhshfProfilingStage.ReviewAndSubmit);

        Assert.True(ok.IsSuccess);
        Assert.Equal(RhshfCaseStatus.UnderReview, profile.Status);
    }

    private static RhshfCreditProfile ProfileOnSupportingDocuments()
    {
        var profile = CreateValidProfile();
        profile.AdvanceStage(RhshfProfilingStage.CompanyVerification);
        profile.AdvanceStage(RhshfProfilingStage.CreditBureauCheck);
        profile.AddFarmPlanDuringProfiling("Maize", 100m, 3_000m, 515m);
        profile.AdvanceStage(RhshfProfilingStage.EopReview);
        return profile;
    }

    private static RhshfDocumentRequirement[] MandatoryCacAndOffTake() =>
    [
        RhshfDocumentRequirement.Create(
            RhshfDocumentCategory.CacCertificate, "CAC certificate", null, isMandatory: true, sortOrder: 1).Value,
        RhshfDocumentRequirement.Create(
            RhshfDocumentCategory.OffTakeAgreement, "Off-take agreement", null, isMandatory: true, sortOrder: 2).Value,
    ];
}

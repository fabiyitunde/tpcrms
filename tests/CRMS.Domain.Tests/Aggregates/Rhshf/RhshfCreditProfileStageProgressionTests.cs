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
            var result = profile.AdvanceStage(stage);
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

        var result = profile.AddSupportingDocument("cac.pdf", "application/pdf", "path/cac.pdf", 1024);

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

        var first = profile.AddSupportingDocument("cac.pdf", "application/pdf", "path/cac.pdf", 1024);
        var second = profile.AddSupportingDocument("bank-statement.pdf", "application/pdf", "path/bs.pdf", 2048);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(2, profile.SupportingDocuments.Count);
        Assert.Equal(RhshfProfilingStage.SupportingDocuments, profile.CurrentStage); // adding a doc doesn't advance
    }
}

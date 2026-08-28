using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Tests.Aggregates.Rhshf;

public class RhshfCreditProfileAppraisalTests
{
    private static RhshfCreditProfile CreateProfileUnderReview()
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
        var profile = result.Value;

        foreach (var stage in new[]
        {
            RhshfProfilingStage.CompanyVerification, RhshfProfilingStage.CreditBureauCheck,
            RhshfProfilingStage.EopReview, RhshfProfilingStage.SupportingDocuments, RhshfProfilingStage.ReviewAndSubmit,
        })
        {
            profile.AdvanceStage(stage);
        }

        return profile; // now UnderReview, CurrentCycleNumber == 1, InternalStage == Appraisal
    }

    [Fact]
    public void AdvanceStage_IntoUnderReview_SetsCycleOneAndAppraisalStage()
    {
        var profile = CreateProfileUnderReview();

        Assert.Equal(RhshfCaseStatus.UnderReview, profile.Status);
        Assert.Equal(1, profile.CurrentCycleNumber);
        Assert.Equal(RhshfInternalStage.Appraisal, profile.InternalStage);
    }

    [Fact]
    public void Appraise_Proceed_AdvancesToRiskReview()
    {
        var profile = CreateProfileUnderReview();
        var creditOfficerId = Guid.NewGuid();

        var result = profile.Appraise(creditOfficerId, RhshfAppraisalOutcome.Proceed, "Looks fine");

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfInternalStage.RiskReview, profile.InternalStage);
        Assert.Single(profile.Appraisals);
        Assert.Equal(1, profile.Appraisals.Single().CycleNumber);
    }

    [Fact]
    public void Appraise_CalledTwiceForSameCycle_SecondCallFails()
    {
        var profile = CreateProfileUnderReview();
        var creditOfficerId = Guid.NewGuid();
        profile.Appraise(creditOfficerId, RhshfAppraisalOutcome.Proceed, null);

        var second = profile.Appraise(Guid.NewGuid(), RhshfAppraisalOutcome.Proceed, null);

        Assert.True(second.IsFailure);
    }

    [Fact]
    public void Appraise_ReturnToFac_ResetsStatusAndReopensFacForm()
    {
        var profile = CreateProfileUnderReview();

        var result = profile.Appraise(Guid.NewGuid(), RhshfAppraisalOutcome.ReturnToFac, "Need clearer EOP breakdown",
            returnToStage: RhshfProfilingStage.EopReview);

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfCaseStatus.ProfilingInProgress, profile.Status);
        Assert.Equal(RhshfProfilingStage.EopReview, profile.CurrentStage);
        Assert.Null(profile.InternalStage);
    }

    [Fact]
    public void Appraise_Decline_IsTerminalAndRaisesDomainEvent()
    {
        var profile = CreateProfileUnderReview();

        var result = profile.Appraise(Guid.NewGuid(), RhshfAppraisalOutcome.Decline, "Ineligible");

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfCaseStatus.Declined, profile.Status);
        Assert.Equal(RhshfDecisionOutcome.Declined, profile.DecisionOutcome);
        Assert.Equal("CRMS Appraisal", profile.DecidedBy);
        Assert.Contains(profile.DomainEvents, e => e is RhshfCaseDecidedEvent);
    }

    [Fact]
    public void ReviewRisk_BeforeAppraisal_Fails()
    {
        var profile = CreateProfileUnderReview();

        var result = profile.ReviewRisk(Guid.NewGuid(), RhshfRiskReviewOutcome.Cleared, null);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void ReviewRisk_BySameUserAsAppraisal_Fails()
    {
        var profile = CreateProfileUnderReview();
        var sameUser = Guid.NewGuid();
        profile.Appraise(sameUser, RhshfAppraisalOutcome.Proceed, null);

        var result = profile.ReviewRisk(sameUser, RhshfRiskReviewOutcome.Cleared, null);

        Assert.True(result.IsFailure);
        Assert.Empty(profile.RiskReviews);
    }

    [Fact]
    public void ReviewRisk_ByDifferentUser_Cleared_AdvancesToCommitteeVoting()
    {
        var profile = CreateProfileUnderReview();
        profile.Appraise(Guid.NewGuid(), RhshfAppraisalOutcome.Proceed, null);

        var result = profile.ReviewRisk(Guid.NewGuid(), RhshfRiskReviewOutcome.Cleared, null);

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfInternalStage.CommitteeVoting, profile.InternalStage);
    }

    [Fact]
    public void ReviewRisk_CalledTwiceForSameCycle_SecondCallFails()
    {
        var profile = CreateProfileUnderReview();
        profile.Appraise(Guid.NewGuid(), RhshfAppraisalOutcome.Proceed, null);
        profile.ReviewRisk(Guid.NewGuid(), RhshfRiskReviewOutcome.Cleared, null);

        var second = profile.ReviewRisk(Guid.NewGuid(), RhshfRiskReviewOutcome.Cleared, null);

        Assert.True(second.IsFailure);
    }

    [Fact]
    public void ReviewRisk_Decline_IsTerminal()
    {
        var profile = CreateProfileUnderReview();
        profile.Appraise(Guid.NewGuid(), RhshfAppraisalOutcome.Proceed, null);

        var result = profile.ReviewRisk(Guid.NewGuid(), RhshfRiskReviewOutcome.Decline, "Fraud flag");

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfCaseStatus.Declined, profile.Status);
        Assert.Equal("CRMS Risk Review", profile.DecidedBy);
    }

    [Fact]
    public void AdvanceToRatification_WhenAtCommitteeVoting_Succeeds()
    {
        var profile = CreateProfileUnderReview();
        profile.Appraise(Guid.NewGuid(), RhshfAppraisalOutcome.Proceed, null);
        profile.ReviewRisk(Guid.NewGuid(), RhshfRiskReviewOutcome.Cleared, null);

        var result = profile.AdvanceToRatification();

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfInternalStage.Ratification, profile.InternalStage);
    }

    [Fact]
    public void AdvanceToRatification_WhenNotAtCommitteeVoting_Fails()
    {
        var profile = CreateProfileUnderReview(); // still at Appraisal

        var result = profile.AdvanceToRatification();

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void DeclineAtCommittee_IsTerminal()
    {
        var profile = CreateProfileUnderReview();
        profile.Appraise(Guid.NewGuid(), RhshfAppraisalOutcome.Proceed, null);
        profile.ReviewRisk(Guid.NewGuid(), RhshfRiskReviewOutcome.Cleared, null);

        var result = profile.DeclineAtCommittee("Committee rejected");

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfCaseStatus.Declined, profile.Status);
        Assert.Equal("CRMS Committee", profile.DecidedBy);
        Assert.Contains(profile.DomainEvents, e => e is RhshfCaseDecidedEvent);
    }

    [Fact]
    public void ReturnToFacFromCommittee_ResetsToProfiling()
    {
        var profile = CreateProfileUnderReview();
        profile.Appraise(Guid.NewGuid(), RhshfAppraisalOutcome.Proceed, null);
        profile.ReviewRisk(Guid.NewGuid(), RhshfRiskReviewOutcome.Cleared, null);

        var result = profile.ReturnToFacFromCommittee(RhshfProfilingStage.SupportingDocuments);

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfCaseStatus.ProfilingInProgress, profile.Status);
        Assert.Equal(RhshfProfilingStage.SupportingDocuments, profile.CurrentStage);
        Assert.Null(profile.InternalStage);
    }

    [Fact]
    public void GetCurrentCycleAppraisalAndRiskActorIds_ReturnsBothActors()
    {
        var profile = CreateProfileUnderReview();
        var creditOfficerId = Guid.NewGuid();
        var riskOfficerId = Guid.NewGuid();
        profile.Appraise(creditOfficerId, RhshfAppraisalOutcome.Proceed, null);
        profile.ReviewRisk(riskOfficerId, RhshfRiskReviewOutcome.Cleared, null);

        var ids = profile.GetCurrentCycleAppraisalAndRiskActorIds();

        Assert.Contains(creditOfficerId, ids);
        Assert.Contains(riskOfficerId, ids);
    }

    [Fact]
    public void FullCycle_ReturnToFac_ThenResubmit_OpensFreshCycle_AndAllowsSameCreditOfficerAgain()
    {
        var profile = CreateProfileUnderReview();
        var creditOfficerId = Guid.NewGuid();
        profile.Appraise(creditOfficerId, RhshfAppraisalOutcome.ReturnToFac, "Fix documents", RhshfProfilingStage.SupportingDocuments);

        // FAC resumes and resubmits
        profile.AdvanceStage(RhshfProfilingStage.SupportingDocuments);
        profile.AdvanceStage(RhshfProfilingStage.ReviewAndSubmit);

        Assert.Equal(2, profile.CurrentCycleNumber);
        Assert.Equal(RhshfInternalStage.Appraisal, profile.InternalStage);

        // Same credit officer CAN appraise cycle 2 (only checker-distinctness matters, not maker)
        var secondAppraisal = profile.Appraise(creditOfficerId, RhshfAppraisalOutcome.Proceed, null);
        Assert.True(secondAppraisal.IsSuccess);
        Assert.Equal(2, profile.Appraisals.Count);

        // But the checker for cycle 2 must still differ from cycle 2's maker
        var sameUserAsChecker = profile.ReviewRisk(creditOfficerId, RhshfRiskReviewOutcome.Cleared, null);
        Assert.True(sameUserAsChecker.IsFailure);
    }
}

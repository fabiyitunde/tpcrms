using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Tests.Aggregates.Rhshf;

/// <summary>
/// Legal clearance sits between Appraisal and Risk Review: the security is sighted, opined on and
/// perfected before the case is circulated, so the committee votes on a settled file and an approved
/// case can no longer be killed by a later legal finding.
/// </summary>
public class RhshfCreditProfileLegalClearanceTests
{
    private const decimal TotalEopValue = 51_500_000.00m;

    private static RhshfCreditProfile CreateProfileAtLegalClearance()
    {
        var result = RhshfCreditProfile.Create(
            submissionId: Guid.NewGuid(), programmeCode: "RH-SHF-DRY-2026", programmeName: "Renewed Hope",
            sessionCode: "2026-DRY", sessionName: "Dry Season 2026", facId: Guid.NewGuid(),
            companyName: "Alliedsoft Limited", rcNumber: "RC123456", tin: "01234567-0001",
            boaAccountNumber: "0123456789", contactEmail: "fac@company.com", contactPhone: "+2348012345678",
            state: "Kano", lga: "Nassarawa", totalEopValue: TotalEopValue, currency: "NGN", farmerCount: 1200,
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
            profile.AdvanceStageForTest(stage);
        }

        profile.AppraiseWithFinancials(Guid.NewGuid(), RhshfAppraisalOutcome.Proceed, null);

        return profile; // now InternalStage == LegalClearance
    }

    [Fact]
    public void Appraisal_Proceed_LandsAtLegalClearance_NotRiskReview()
    {
        var profile = CreateProfileAtLegalClearance();

        Assert.Equal(RhshfInternalStage.LegalClearance, profile.InternalStage);
        Assert.Equal(RhshfCaseStatus.UnderReview, profile.Status);
    }

    [Fact]
    public void AdvanceToRiskReviewFromLegal_HandsTheCaseToRisk()
    {
        var profile = CreateProfileAtLegalClearance();

        var result = profile.AdvanceToRiskReviewFromLegal(Guid.NewGuid(), "title verified, charge registered");

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfInternalStage.RiskReview, profile.InternalStage);
        Assert.Equal(RhshfCaseStatus.UnderReview, profile.Status);
    }

    [Fact]
    public void RiskReview_CannotRunBeforeLegalClearance()
    {
        var profile = CreateProfileAtLegalClearance();

        // The stage guard is what enforces the ordering — Risk must not be able to jump the queue and
        // circulate a case whose security has not been looked at.
        var result = profile.ReviewRisk(Guid.NewGuid(), RhshfRiskReviewOutcome.Cleared, null);

        Assert.True(result.IsFailure);
        Assert.Equal(RhshfInternalStage.LegalClearance, profile.InternalStage);
    }

    [Fact]
    public void RiskOfficer_CannotBeTheLegalOfficerWhoCleared()
    {
        var profile = CreateProfileAtLegalClearance();
        var legalOfficerId = Guid.NewGuid();
        profile.AdvanceToRiskReviewFromLegal(legalOfficerId);

        var result = profile.ReviewRisk(legalOfficerId, RhshfRiskReviewOutcome.Cleared, null, null, legalOfficerId);

        Assert.True(result.IsFailure);
        Assert.Contains("Legal Officer", result.Error);
    }

    [Fact]
    public void ReturnToFacFromLegal_SendsTheCaseBackToTheFac_AtTheRequestedStage()
    {
        var profile = CreateProfileAtLegalClearance();
        var cycleBefore = profile.CurrentCycleNumber;

        // A fixable defect in what the FAC declared goes back to the FAC, not onward — there is no
        // ratification to return to at this point in the flow any more.
        var result = profile.ReturnToFacFromLegal(RhshfProfilingStage.CreditBureauCheck, Guid.NewGuid(), "re-upload the title deed");

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfCaseStatus.ProfilingInProgress, profile.Status);
        Assert.Equal(RhshfProfilingStage.CreditBureauCheck, profile.CurrentStage);
        Assert.Null(profile.InternalStage);
        Assert.Equal(cycleBefore, profile.CurrentCycleNumber);
    }

    [Fact]
    public void DeclineAtLegalClearance_IsTerminal_FiresDomainEvent()
    {
        var profile = CreateProfileAtLegalClearance();

        var result = profile.DeclineAtLegalClearance("collateral title falsified");

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfCaseStatus.Declined, profile.Status);
        Assert.Equal(RhshfDecisionOutcome.Declined, profile.DecisionOutcome);
        Assert.Equal("CRMS Legal Clearance", profile.DecidedBy);
        Assert.Null(profile.InternalStage);
        Assert.Contains(profile.DomainEvents, e => e is RhshfCaseDecidedEvent);
    }

    [Fact]
    public void AdvanceToRiskReviewFromLegal_WhenNotAtLegalClearance_Fails()
    {
        var profile = CreateProfileAtLegalClearance();
        profile.AdvanceToRiskReviewFromLegal();

        var result = profile.AdvanceToRiskReviewFromLegal();

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void ReturnToFacFromLegal_WhenNotAtLegalClearance_Fails()
    {
        var profile = CreateProfileAtLegalClearance();
        profile.AdvanceToRiskReviewFromLegal();

        var result = profile.ReturnToFacFromLegal();

        Assert.True(result.IsFailure);
    }
}

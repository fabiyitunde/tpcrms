using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Tests.Aggregates.Rhshf;

public class RhshfCreditProfileLegalClearanceTests
{
    private const decimal TotalEopValue = 51_500_000.00m;

    private static RhshfCreditProfile CreateProfileAtLegalClearance(out Guid finalApproverId)
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
        profile.ReviewRisk(Guid.NewGuid(), RhshfRiskReviewOutcome.Cleared, null);
        profile.AdvanceToRatification();
        finalApproverId = Guid.NewGuid();
        profile.Ratify(finalApproverId, RhshfRatificationOutcome.Ratified, TotalEopValue, null, null, []);

        var offer = RhshfOffer.Create(profile.Id, profile.CurrentCycleNumber, "path/offer-cycle1.pdf").Value;
        offer.AddDocument("signed.pdf", "application/pdf", "path/signed.pdf", 1024);
        offer.Accept(null);
        profile.AdvanceToLegalClearance();

        return profile; // now InternalStage == LegalClearance
    }

    [Fact]
    public void GetCurrentCycleFinalApproverId_ReturnsRatifyingApprover()
    {
        var profile = CreateProfileAtLegalClearance(out var finalApproverId);

        Assert.Equal(finalApproverId, profile.GetCurrentCycleFinalApproverId());
    }

    [Fact]
    public void AdvanceToDisbursement_FromLegalClearance_LandsAtPreDeploymentVerification()
    {
        var profile = CreateProfileAtLegalClearance(out _);

        var result = profile.AdvanceToDisbursement();

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfInternalStage.PreDeploymentVerification, profile.InternalStage);
        Assert.Equal(RhshfCaseStatus.UnderReview, profile.Status);
    }

    [Fact]
    public void ReturnToRatificationFromLegal_Succeeds_StaysUnderReview_SameCycle()
    {
        var profile = CreateProfileAtLegalClearance(out _);
        var cycleBefore = profile.CurrentCycleNumber;

        var result = profile.ReturnToRatificationFromLegal();

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfInternalStage.Ratification, profile.InternalStage);
        Assert.Equal(RhshfCaseStatus.UnderReview, profile.Status);
        Assert.Equal(cycleBefore, profile.CurrentCycleNumber);
    }

    [Fact]
    public void ReturnToRatificationFromLegal_ThenReRatify_Succeeds_ProducesSecondRatificationRecord()
    {
        var profile = CreateProfileAtLegalClearance(out _);
        profile.ReturnToRatificationFromLegal();

        var result = profile.Ratify(Guid.NewGuid(), RhshfRatificationOutcome.Ratified, TotalEopValue, "re-ratified after legal return", null, []);

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfInternalStage.AwaitingOfferAcceptance, profile.InternalStage);
        Assert.Equal(2, profile.Ratifications.Count(r => r.CycleNumber == profile.CurrentCycleNumber));
    }

    [Fact]
    public void DeclineAtLegalClearance_IsTerminal_FiresDomainEvent()
    {
        var profile = CreateProfileAtLegalClearance(out _);

        var result = profile.DeclineAtLegalClearance("collateral title defective");

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfCaseStatus.Declined, profile.Status);
        Assert.Equal(RhshfDecisionOutcome.Declined, profile.DecisionOutcome);
        Assert.Equal("CRMS Legal Clearance", profile.DecidedBy);
        Assert.Null(profile.InternalStage);
        Assert.Contains(profile.DomainEvents, e => e is RhshfCaseDecidedEvent);
    }

    [Fact]
    public void AdvanceToDisbursement_WhenNotAtLegalClearance_Fails()
    {
        var profile = CreateProfileAtLegalClearance(out _);
        profile.AdvanceToDisbursement();

        var result = profile.AdvanceToDisbursement();

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void ReturnToRatificationFromLegal_WhenNotAtLegalClearance_Fails()
    {
        var profile = CreateProfileAtLegalClearance(out _);
        profile.AdvanceToDisbursement();

        var result = profile.ReturnToRatificationFromLegal();

        Assert.True(result.IsFailure);
    }
}

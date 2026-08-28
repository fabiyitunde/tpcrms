using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Tests.Aggregates.Rhshf;

public class RhshfCreditProfileRatificationTests
{
    private const decimal TotalEopValue = 51_500_000.00m;

    private static RhshfCreditProfile CreateProfileAtRatification()
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
            profile.AdvanceStage(stage);
        }

        profile.Appraise(Guid.NewGuid(), RhshfAppraisalOutcome.Proceed, null);
        profile.ReviewRisk(Guid.NewGuid(), RhshfRiskReviewOutcome.Cleared, null);
        profile.AdvanceToRatification();

        return profile; // now InternalStage == Ratification
    }

    [Fact]
    public void Ratify_Ratified_WithExactAmount_Succeeds_AdvancesToAwaitingOfferAcceptance()
    {
        var profile = CreateProfileAtRatification();

        var result = profile.Ratify(Guid.NewGuid(), RhshfRatificationOutcome.Ratified, TotalEopValue, null, null, []);

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfInternalStage.AwaitingOfferAcceptance, profile.InternalStage);
        Assert.Single(profile.Ratifications);
    }

    [Fact]
    public void Ratify_Ratified_WithMismatchedAmount_Fails()
    {
        var profile = CreateProfileAtRatification();

        var result = profile.Ratify(Guid.NewGuid(), RhshfRatificationOutcome.Ratified, TotalEopValue - 1, null, null, []);

        Assert.True(result.IsFailure);
        Assert.Empty(profile.Ratifications);
        Assert.Equal(RhshfInternalStage.Ratification, profile.InternalStage);
    }

    [Fact]
    public void Ratify_ByExcludedActor_Fails()
    {
        var profile = CreateProfileAtRatification();
        var committeeApproverId = Guid.NewGuid();

        var result = profile.Ratify(committeeApproverId, RhshfRatificationOutcome.Ratified, TotalEopValue, null, null, [committeeApproverId]);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Ratify_CalledTwiceForSameCycle_SecondCallFails()
    {
        var profile = CreateProfileAtRatification();
        profile.Ratify(Guid.NewGuid(), RhshfRatificationOutcome.ReturnToFac, null, "need more docs", RhshfProfilingStage.SupportingDocuments, []);

        // Cycle already closed via ReturnToFac; re-ratifying the SAME cycle before a new one opens must fail.
        var second = profile.Ratify(Guid.NewGuid(), RhshfRatificationOutcome.Ratified, TotalEopValue, null, null, []);

        Assert.True(second.IsFailure);
    }

    [Fact]
    public void Ratify_ReturnToFac_ResetsToProfiling()
    {
        var profile = CreateProfileAtRatification();

        var result = profile.Ratify(Guid.NewGuid(), RhshfRatificationOutcome.ReturnToFac, null, "clarify EOP", RhshfProfilingStage.EopReview, []);

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfCaseStatus.ProfilingInProgress, profile.Status);
        Assert.Equal(RhshfProfilingStage.EopReview, profile.CurrentStage);
        Assert.Null(profile.InternalStage);
    }

    [Fact]
    public void Ratify_Declined_IsTerminal()
    {
        var profile = CreateProfileAtRatification();

        var result = profile.Ratify(Guid.NewGuid(), RhshfRatificationOutcome.Declined, null, "does not meet policy", null, []);

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfCaseStatus.Declined, profile.Status);
        Assert.Equal("CRMS Ratification", profile.DecidedBy);
        Assert.Contains(profile.DomainEvents, e => e is RhshfCaseDecidedEvent);
    }

    [Fact]
    public void Ratify_WhenNotAtRatificationStage_Fails()
    {
        var profile = CreateProfileAtRatification();
        profile.Ratify(Guid.NewGuid(), RhshfRatificationOutcome.Ratified, TotalEopValue, null, null, []); // already past Ratification now

        var result = profile.Ratify(Guid.NewGuid(), RhshfRatificationOutcome.Ratified, TotalEopValue, null, null, []);

        Assert.True(result.IsFailure);
    }
}

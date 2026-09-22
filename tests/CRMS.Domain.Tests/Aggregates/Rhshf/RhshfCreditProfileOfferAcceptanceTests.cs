using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Tests.Aggregates.Rhshf;

public class RhshfCreditProfileOfferAcceptanceTests
{
    private const decimal TotalEopValue = 51_500_000.00m;

    private static RhshfCreditProfile CreateProfileAwaitingOfferAcceptance()
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
        profile.Ratify(Guid.NewGuid(), RhshfRatificationOutcome.Ratified, TotalEopValue, null, null, []);

        return profile; // now InternalStage == AwaitingOfferAcceptance
    }

    [Fact]
    public void AdvanceToLegalClearance_WhenAwaitingOfferAcceptance_Succeeds()
    {
        var profile = CreateProfileAwaitingOfferAcceptance();

        var result = profile.AdvanceToLegalClearance();

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfInternalStage.LegalClearance, profile.InternalStage);
        Assert.Equal(RhshfCaseStatus.UnderReview, profile.Status); // external status unchanged (§6 #9)
    }

    [Fact]
    public void AdvanceToLegalClearance_WhenNotAwaitingOfferAcceptance_Fails()
    {
        var profile = CreateProfileAwaitingOfferAcceptance();
        profile.AdvanceToLegalClearance(); // already past this stage now

        var result = profile.AdvanceToLegalClearance();

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void CancelDueToOfferRejection_SetsCancelledNotDeclined()
    {
        var profile = CreateProfileAwaitingOfferAcceptance();

        var result = profile.CancelDueToOfferRejection("Terms not acceptable");

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfCaseStatus.Cancelled, profile.Status);
        Assert.Null(profile.DecisionOutcome); // a withdrawal, not a bank decision — §6 #10
        Assert.Equal("FAC", profile.DecidedBy);
        Assert.Contains(profile.DomainEvents, e => e is RhshfCaseDecidedEvent);
    }

    [Fact]
    public void CancelDueToOfferRejection_WhenNotAwaitingOfferAcceptance_Fails()
    {
        var profile = CreateProfileAwaitingOfferAcceptance();
        profile.AdvanceToLegalClearance(); // moved on already

        var result = profile.CancelDueToOfferRejection(null);

        Assert.True(result.IsFailure);
    }
}

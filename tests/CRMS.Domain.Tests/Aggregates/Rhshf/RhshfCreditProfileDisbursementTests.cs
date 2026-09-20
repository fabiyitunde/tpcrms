using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Tests.Aggregates.Rhshf;

public class RhshfCreditProfileDisbursementTests
{
    private const decimal TotalEopValue = 51_500_000.00m;

    private static RhshfCreditProfile CreateProfileAtDisbursement()
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
        profile.Ratify(Guid.NewGuid(), RhshfRatificationOutcome.Ratified, TotalEopValue, null, null, []);

        var offer = RhshfOffer.Create(profile.Id, profile.CurrentCycleNumber, "path/offer-cycle1.pdf").Value;
        offer.AddDocument("signed.pdf", "application/pdf", "path/signed.pdf", 1024);
        offer.Accept(null);
        profile.AdvanceToLegalClearance();
        profile.AdvanceToDisbursement();

        return profile; // now InternalStage == Disbursement
    }

    [Fact]
    public void RecordDisbursementAttempt_Booked_CompletesCase_MapsToApproved_FiresDomainEvent()
    {
        var profile = CreateProfileAtDisbursement();

        var result = profile.RecordDisbursementAttempt(
            Guid.NewGuid(), TotalEopValue, "0987654321", "Agro Inputs Ltd", RhshfDisbursementStatus.Booked, 12345L, "LN-000123", null);

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfInternalStage.Completed, profile.InternalStage);
        Assert.Equal(RhshfCaseStatus.Approved, profile.Status);
        Assert.Equal(RhshfDecisionOutcome.Approved, profile.DecisionOutcome);
        Assert.Equal("CRMS Disbursement", profile.DecidedBy);
        Assert.Contains(profile.DomainEvents, e => e is RhshfCaseDecidedEvent);
        Assert.Single(profile.Disbursements);
    }

    [Fact]
    public void RecordDisbursementAttempt_MismatchedAmount_Fails_NoRecordAdded()
    {
        var profile = CreateProfileAtDisbursement();

        var result = profile.RecordDisbursementAttempt(
            Guid.NewGuid(), TotalEopValue - 1, "0987654321", "Agro Inputs Ltd", RhshfDisbursementStatus.Booked, 12345L, "LN-000123", null);

        Assert.True(result.IsFailure);
        Assert.Empty(profile.Disbursements);
        Assert.Equal(RhshfInternalStage.Disbursement, profile.InternalStage);
    }

    [Fact]
    public void RecordDisbursementAttempt_MissingSupplierAccount_Fails()
    {
        var profile = CreateProfileAtDisbursement();

        var result = profile.RecordDisbursementAttempt(
            Guid.NewGuid(), TotalEopValue, "  ", null, RhshfDisbursementStatus.Booked, 12345L, "LN-000123", null);

        Assert.True(result.IsFailure);
        Assert.Empty(profile.Disbursements);
    }

    [Fact]
    public void RecordDisbursementAttempt_Failed_IsRetryable_NoRegression()
    {
        var profile = CreateProfileAtDisbursement();

        var result = profile.RecordDisbursementAttempt(
            Guid.NewGuid(), TotalEopValue, "0987654321", "Agro Inputs Ltd", RhshfDisbursementStatus.Failed, null, null, "Fineract timeout");

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfInternalStage.Disbursement, profile.InternalStage);
        Assert.Equal(RhshfCaseStatus.UnderReview, profile.Status);
        Assert.Single(profile.Disbursements);
        // Ratify() earlier in setup already raised RhshfOfferReadyEvent (Phase 10) — what a failed
        // disbursement attempt must NOT do is add a terminal RhshfCaseDecidedEvent on top of it.
        Assert.DoesNotContain(profile.DomainEvents, e => e is RhshfCaseDecidedEvent);
    }

    [Fact]
    public void RecordDisbursementAttempt_FailedThenBooked_Succeeds_PreservesHistory()
    {
        var profile = CreateProfileAtDisbursement();
        profile.RecordDisbursementAttempt(Guid.NewGuid(), TotalEopValue, "0987654321", "Agro Inputs Ltd", RhshfDisbursementStatus.Failed, null, null, "Fineract timeout");

        var result = profile.RecordDisbursementAttempt(
            Guid.NewGuid(), TotalEopValue, "0987654321", "Agro Inputs Ltd", RhshfDisbursementStatus.Booked, 12345L, "LN-000123", null);

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfInternalStage.Completed, profile.InternalStage);
        Assert.Equal(RhshfCaseStatus.Approved, profile.Status);
        Assert.Equal(2, profile.Disbursements.Count);
    }

    [Fact]
    public void RecordDisbursementAttempt_WhenNotAtDisbursementStage_Fails()
    {
        var profile = CreateProfileAtDisbursement();
        profile.RecordDisbursementAttempt(Guid.NewGuid(), TotalEopValue, "0987654321", "Agro Inputs Ltd", RhshfDisbursementStatus.Booked, 12345L, "LN-000123", null);

        var result = profile.RecordDisbursementAttempt(Guid.NewGuid(), TotalEopValue, "0987654321", "Agro Inputs Ltd", RhshfDisbursementStatus.Booked, 12346L, "LN-000124", null);

        Assert.True(result.IsFailure);
    }
}

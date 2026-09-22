using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Tests.Aggregates.Rhshf;

/// <summary>
/// The trail's whole value is that it is complete. These tests walk real paths through the
/// lifecycle and assert an entry exists for every state the case actually visited — which is what
/// catches a future transition being added without one.
/// </summary>
public class RhshfStatusHistoryTests
{
    private const decimal Eop = 51_500_000.00m;

    private static RhshfCreditProfile CreateProfile() => RhshfCreditProfile.Create(
        submissionId: Guid.NewGuid(), programmeCode: "RH-SHF-DRY-2026", programmeName: "Renewed Hope",
        sessionCode: "2026-DRY", sessionName: "Dry Season 2026", facId: Guid.NewGuid(),
        companyName: "Alliedsoft Limited", rcNumber: "RC123456", tin: "01234567-0001",
        boaAccountNumber: "0123456789", contactEmail: "fac@company.com", contactPhone: "+2348012345678",
        state: "Kano", lga: "Nassarawa", totalEopValue: Eop, currency: "NGN", farmerCount: 1200,
        callbackUrl: "https://portal.example.gov.ng/api/integrations/crms/webhook",
        certifiedByAdmin: "admin@boa.gov.ng", certifiedAt: DateTime.UtcNow, rawSubmissionPayload: "{}",
        eopLines: null, resolvedBranchId: null, resolvedOfficeId: null).Value;

    private static RhshfCreditProfile ProfileUnderReview()
    {
        var profile = CreateProfile();
        foreach (var stage in new[]
        {
            RhshfProfilingStage.CompanyVerification, RhshfProfilingStage.CreditBureauCheck,
            RhshfProfilingStage.EopReview, RhshfProfilingStage.SupportingDocuments, RhshfProfilingStage.ReviewAndSubmit,
        })
        {
            profile.AdvanceStageForTest(stage);
        }

        return profile;
    }

    [Fact]
    public void Create_OpensTheTrailAtSubmission()
    {
        var profile = CreateProfile();

        var entry = Assert.Single(profile.StatusHistory);
        Assert.Equal(RhshfCaseStatus.ProfilingPending, entry.Status);
        Assert.Equal(RhshfProfilingStage.CompanyVerification, entry.ProfilingStage);
        Assert.Null(entry.ActorUserId);
        Assert.Equal("Portal", entry.ActorLabel);
    }

    [Fact]
    public void Profiling_RecordsEveryStageConfirmation_AttributedToTheFac()
    {
        var profile = ProfileUnderReview();

        // 1 for creation + 5 stage confirmations.
        Assert.Equal(6, profile.StatusHistory.Count);
        Assert.All(profile.StatusHistory, e => Assert.Null(e.ActorUserId));
        Assert.Equal("Submitted for credit review", profile.StatusHistory.Last().Action);
        Assert.Equal(RhshfCaseStatus.UnderReview, profile.StatusHistory.Last().Status);
        Assert.Equal(RhshfInternalStage.Appraisal, profile.StatusHistory.Last().InternalStage);
    }

    [Fact]
    public void ProfilingEntries_AreFiledUnderTheCycleTheyWillBeReviewedAs()
    {
        // CurrentCycleNumber is 0 throughout profiling. Filing the trail under it would put the
        // FAC's work in a "cycle 0" that no other record uses.
        var profile = ProfileUnderReview();

        Assert.All(profile.StatusHistory, e => Assert.Equal(1, e.CycleNumber));
    }

    [Fact]
    public void HappyPath_RecordsOneEntryPerStateVisited_EachWithItsActor()
    {
        var profile = ProfileUnderReview();
        var creditOfficer = Guid.NewGuid();
        var riskOfficer = Guid.NewGuid();
        var voter = Guid.NewGuid();
        var finalApprover = Guid.NewGuid();
        var legalOfficer = Guid.NewGuid();
        var disbursementOfficer = Guid.NewGuid();

        profile.AppraiseWithFinancials(creditOfficer, RhshfAppraisalOutcome.Proceed, "Viable");
        profile.ReviewRisk(riskOfficer, RhshfRiskReviewOutcome.Cleared, null);
        profile.AdvanceToRatification(voter, "3/3 approved.");
        profile.Ratify(finalApprover, RhshfRatificationOutcome.Ratified, Eop, null, null, []);
        profile.AdvanceToLegalClearance();
        profile.AdvanceToDisbursement(legalOfficer, "Title verified");
        profile.SeedPreDeploymentChecklist([]);
        CompleteVerification(profile, disbursementOfficer);
        profile.RecordDisbursementAttempt(
            disbursementOfficer, Eop, "0011223344", "AgroInputs Ltd",
            RhshfDisbursementStatus.Booked, 900123L, "000000900123", null);

        var staffEntries = profile.StatusHistory.Where(e => e.CycleNumber == 1 && e.InternalStage is not null).ToList();

        Assert.Equal(
            new[]
            {
                RhshfInternalStage.Appraisal,               // submission
                RhshfInternalStage.RiskReview,
                RhshfInternalStage.CommitteeVoting,
                RhshfInternalStage.Ratification,
                RhshfInternalStage.AwaitingOfferAcceptance,
                RhshfInternalStage.LegalClearance,
                RhshfInternalStage.PreDeploymentVerification,
                RhshfInternalStage.Disbursement,
                RhshfInternalStage.Active,
            },
            staffEntries.Select(e => e.InternalStage!.Value).ToArray());

        Assert.Equal(creditOfficer, Entry(profile, RhshfInternalStage.RiskReview).ActorUserId);
        Assert.Equal(riskOfficer, Entry(profile, RhshfInternalStage.CommitteeVoting).ActorUserId);
        Assert.Equal(voter, Entry(profile, RhshfInternalStage.Ratification).ActorUserId);
        Assert.Equal(finalApprover, Entry(profile, RhshfInternalStage.AwaitingOfferAcceptance).ActorUserId);
        Assert.Equal(legalOfficer, Entry(profile, RhshfInternalStage.PreDeploymentVerification).ActorUserId);
        Assert.Equal(disbursementOfficer, Entry(profile, RhshfInternalStage.Active).ActorUserId);
    }

    [Fact]
    public void OfferAcceptance_IsAttributedToTheFac_NotSilentlyToTheSystem()
    {
        var profile = RatifiedProfile(out _);

        profile.AdvanceToLegalClearance();

        var entry = Entry(profile, RhshfInternalStage.LegalClearance);
        Assert.Null(entry.ActorUserId);
        Assert.Equal("FAC", entry.ActorLabel);
    }

    [Fact]
    public void ReturnToFac_RecordsTheRoundTrip_UnderTheNewCycle()
    {
        var profile = ProfileUnderReview();
        var creditOfficer = Guid.NewGuid();

        profile.AppraiseWithFinancials(
            creditOfficer, RhshfAppraisalOutcome.ReturnToFac, "Off-take agreement missing",
            RhshfProfilingStage.SupportingDocuments);

        var entry = profile.StatusHistory.Last();
        Assert.Equal("Returned to the FAC at appraisal", entry.Action);
        Assert.Equal(creditOfficer, entry.ActorUserId);
        Assert.Equal("Off-take agreement missing", entry.Note);
        Assert.Equal(RhshfCaseStatus.ProfilingInProgress, entry.Status);
        Assert.Equal(RhshfProfilingStage.SupportingDocuments, entry.ProfilingStage);
        // The case is now assembling cycle 2 — the flat trail must say so, or the resubmission
        // reads as the same stages inexplicably repeating.
        Assert.Equal(2, entry.CycleNumber);
    }

    [Fact]
    public void Resubmission_KeepsBothCyclesInOneChronologicalTrail()
    {
        var profile = ProfileUnderReview();
        profile.AppraiseWithFinancials(Guid.NewGuid(), RhshfAppraisalOutcome.ReturnToFac, null, RhshfProfilingStage.SupportingDocuments);
        profile.AdvanceStage(RhshfProfilingStage.SupportingDocuments);
        profile.AdvanceStage(RhshfProfilingStage.ReviewAndSubmit);

        Assert.Equal(2, profile.StatusHistory.Select(e => e.CycleNumber).Distinct().Count());
        Assert.Equal(2, profile.StatusHistory.Count(e => e.Action == "Submitted for credit review"));
        // Chronological order is the whole point of the record.
        Assert.Equal(
            profile.StatusHistory.OrderBy(e => e.ChangedAt).Select(e => e.Action),
            profile.StatusHistory.Select(e => e.Action));
    }

    [Fact]
    public void LegalReturn_RecordsTheInternalReRoute_WithoutOpeningANewCycle()
    {
        var profile = RatifiedProfile(out _);
        profile.AdvanceToLegalClearance();
        var legalOfficer = Guid.NewGuid();

        profile.ReturnToRatificationFromLegal(legalOfficer, "Board resolution not executed");

        var entry = profile.StatusHistory.Last();
        Assert.Equal("Returned to ratification by Legal", entry.Action);
        Assert.Equal(legalOfficer, entry.ActorUserId);
        Assert.Equal(RhshfInternalStage.Ratification, entry.InternalStage);
        Assert.Equal(1, entry.CycleNumber); // same cycle — a legal issue is not a re-appraisal
    }

    [Fact]
    public void Decline_RecordsTheTerminalEntry_WithTheDecliningOfficer()
    {
        var profile = ProfileUnderReview();
        var creditOfficer = Guid.NewGuid();

        profile.Appraise(creditOfficer, RhshfAppraisalOutcome.Decline, "Bureau shows active default");

        var entry = profile.StatusHistory.Last();
        Assert.Equal("Declined at appraisal", entry.Action);
        Assert.Equal(creditOfficer, entry.ActorUserId);
        Assert.Equal(RhshfCaseStatus.Declined, entry.Status);
        Assert.Null(entry.InternalStage);
        Assert.Equal("Bureau shows active default", entry.Note);
    }

    [Fact]
    public void OfferRejection_IsRecordedAsAFacCancellation_NotABankDecline()
    {
        var profile = RatifiedProfile(out _);

        profile.CancelDueToOfferRejection("Terms no longer workable");

        var entry = profile.StatusHistory.Last();
        Assert.Equal(RhshfCaseStatus.Cancelled, entry.Status);
        Assert.Equal("FAC", entry.ActorLabel);
        Assert.Null(entry.ActorUserId);
    }

    [Fact]
    public void FailedDisbursement_IsRecorded_EvenThoughNoStateChanged()
    {
        var profile = AtDisbursement(out var officer);

        profile.RecordDisbursementAttempt(
            officer, Eop, "0011223344", "AgroInputs Ltd",
            RhshfDisbursementStatus.Failed, null, null, "Fineract rejected: client not found");

        var entry = profile.StatusHistory.Last();
        Assert.Contains("failed", entry.Action, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(officer, entry.ActorUserId);
        Assert.Equal("Fineract rejected: client not found", entry.Note);
        Assert.Equal(RhshfInternalStage.Disbursement, entry.InternalStage); // unchanged, as intended
    }

    [Fact]
    public void FailedTransitions_LeaveNoEntry()
    {
        var profile = ProfileUnderReview();
        var before = profile.StatusHistory.Count;

        // Wrong stage for all of these.
        profile.AdvanceToRatification(Guid.NewGuid());
        profile.AdvanceToDisbursement(Guid.NewGuid());
        profile.MarkClosed(Guid.NewGuid());
        profile.Appraise(Guid.NewGuid(), RhshfAppraisalOutcome.Proceed, null); // no financial appraisal

        Assert.Equal(before, profile.StatusHistory.Count);
    }

    [Fact]
    public void AnEntryNeverCarriesBothAUserAndALabel()
    {
        var profile = RatifiedProfile(out var finalApprover);
        profile.AdvanceToLegalClearance();

        Assert.All(profile.StatusHistory, e =>
            Assert.True(e.ActorUserId is null ^ e.ActorLabel is null,
                $"'{e.Action}' has both a user and a label, or neither."));
        Assert.Equal(finalApprover, Entry(profile, RhshfInternalStage.AwaitingOfferAcceptance).ActorUserId);
    }

    [Fact]
    public void MarkClosed_RecordsTheFinalEntry()
    {
        var profile = AtDisbursement(out var officer);
        profile.RecordDisbursementAttempt(
            officer, Eop, "0011223344", null, RhshfDisbursementStatus.Booked, 1L, "0001", null);

        profile.MarkClosed(officer);

        Assert.Equal("Loan closed — fully repaid", profile.StatusHistory.Last().Action);
        Assert.Equal(RhshfInternalStage.Closed, profile.StatusHistory.Last().InternalStage);
    }

    private static RhshfStatusHistory Entry(RhshfCreditProfile profile, RhshfInternalStage stage) =>
        profile.StatusHistory.Last(e => e.InternalStage == stage);

    private static RhshfCreditProfile RatifiedProfile(out Guid finalApprover)
    {
        var profile = ProfileUnderReview();
        profile.AppraiseWithFinancials(Guid.NewGuid(), RhshfAppraisalOutcome.Proceed, null);
        profile.ReviewRisk(Guid.NewGuid(), RhshfRiskReviewOutcome.Cleared, null);
        profile.AdvanceToRatification(Guid.NewGuid());
        finalApprover = Guid.NewGuid();
        profile.Ratify(finalApprover, RhshfRatificationOutcome.Ratified, Eop, null, null, []);
        return profile;
    }

    private static RhshfCreditProfile AtDisbursement(out Guid officer)
    {
        var profile = RatifiedProfile(out _);
        profile.AdvanceToLegalClearance();
        profile.AdvanceToDisbursement(Guid.NewGuid());
        profile.SeedPreDeploymentChecklist([]);
        officer = Guid.NewGuid();
        CompleteVerification(profile, officer);
        return profile;
    }

    /// <summary>The gate needs at least one item to exist; an empty template list leaves none, so
    /// seed a single optional item and clear the gate through the real method.</summary>
    private static void CompleteVerification(RhshfCreditProfile profile, Guid officer)
    {
        var template = RhshfPreDeploymentChecklistTemplate.Create(
            "Input supplier confirmed", null, isMandatory: false, sortOrder: 1).Value;
        profile.SeedPreDeploymentChecklist([template]);
        profile.CompletePreDeploymentVerification(officer, "All items verified");
    }
}

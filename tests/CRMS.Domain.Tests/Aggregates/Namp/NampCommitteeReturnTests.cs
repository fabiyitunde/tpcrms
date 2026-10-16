using CRMS.Domain.Aggregates.Namp;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Tests.Aggregates.Namp;

/// <summary>
/// Returning a circulating committee case for rework.
///
/// The sensitive part is not the status change but what it unwinds: votes already cast stop
/// counting, and the case has to climb the pipeline again. Two things are therefore asserted
/// throughout — that the return cannot reach a decided case, and that a returned case cannot
/// shortcut back to committee.
/// </summary>
public class NampCommitteeReturnTests
{
    [Theory]
    [InlineData(NampCommitteeReturnTarget.LoanOfficer, NampApplicationStatus.Draft)]
    [InlineData(NampCommitteeReturnTarget.CreditOfficer, NampApplicationStatus.FinancialAppraisal)]
    [InlineData(NampCommitteeReturnTarget.RiskOfficer, NampApplicationStatus.RiskReview)]
    public void ReturnsToTheChosenStage(NampCommitteeReturnTarget target, NampApplicationStatus expected)
    {
        var app = AtCommittee();

        var result = app.ReturnFromCommittee(Guid.NewGuid(), target, "Equipment valuation needs re-basing.");

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, app.Status);
    }

    [Fact]
    public void RecordsWhoReturnedItAndWhy()
    {
        // The reason is what the receiving officer acts on, so it has to be retrievable as data —
        // not only buried in a history note.
        var app = AtCommittee();
        var returnedBy = Guid.NewGuid();

        app.ReturnFromCommittee(returnedBy, NampCommitteeReturnTarget.CreditOfficer, "  DSCR assumptions optimistic.  ");

        Assert.Equal(returnedBy, app.CommitteeReturnedByUserId);
        Assert.Equal("DSCR assumptions optimistic.", app.CommitteeReturnNote);
        Assert.NotNull(app.CommitteeReturnedAt);
    }

    [Fact]
    public void RequiresAReason()
    {
        var app = AtCommittee();

        foreach (var blank in new[] { null, "", "   " })
        {
            var result = app.ReturnFromCommittee(Guid.NewGuid(), NampCommitteeReturnTarget.RiskOfficer, blank!);
            Assert.True(result.IsFailure);
        }

        Assert.Equal(NampApplicationStatus.BranchCommitteeCirculation, app.Status);
    }

    [Fact]
    public void DetachesTheSupersededReviewFromTheApplication()
    {
        // CurrentCommitteeReviewId was previously set and never cleared. Leaving it attached is what
        // would make the loan pack render the old committee's votes after re-circulation.
        var app = AtCommittee();
        Assert.NotNull(app.CurrentCommitteeReviewId);

        app.ReturnFromCommittee(Guid.NewGuid(), NampCommitteeReturnTarget.RiskOfficer, "Re-run the risk opinion.");

        Assert.Null(app.CurrentCommitteeReviewId);
        Assert.Null(app.CommitteeDecisionAt);
        Assert.Null(app.CommitteeDecisionNote);
    }

    [Fact]
    public void CannotReachACaseThatHasAlreadyBeenDecided()
    {
        // Once the last vote lands, RecordCommitteeOutcome has moved the case on. Reaching back into
        // a ratified case is the ratifier's own decline path, not this one.
        var app = AtCommittee();
        app.RecordCommitteeOutcome(Guid.NewGuid(), isApproved: true, note: null);
        Assert.Equal(NampApplicationStatus.Ratification, app.Status);

        var result = app.ReturnFromCommittee(Guid.NewGuid(), NampCommitteeReturnTarget.RiskOfficer, "Too late.");

        Assert.True(result.IsFailure);
        Assert.Contains("circulating to committee", result.Error);
        Assert.Equal(NampApplicationStatus.Ratification, app.Status);
    }

    [Fact]
    public void CannotBeUsedBeforeTheCaseReachesCommittee()
    {
        var app = AtRiskReview();

        var result = app.ReturnFromCommittee(Guid.NewGuid(), NampCommitteeReturnTarget.CreditOfficer, "Not there yet.");

        Assert.True(result.IsFailure);
        Assert.Equal(NampApplicationStatus.RiskReview, app.Status);
    }

    // ── The re-flow: no shortcuts back to committee ────────────────────────────────────────────────

    [Fact]
    public void ACaseReturnedToTheLoanOfficerMustPassCreditAndRiskAgain()
    {
        var app = AtCommittee();
        app.ReturnFromCommittee(Guid.NewGuid(), NampCommitteeReturnTarget.LoanOfficer, "Wrong equipment cart.");

        // From Draft, risk approval is not available — the only way on is resubmission.
        Assert.True(app.ApproveRiskReview(Guid.NewGuid(), null).IsFailure);

        Assert.True(app.Submit(Guid.NewGuid()).IsSuccess);
        Assert.True(app.ApproveRiskReview(Guid.NewGuid(), null).IsFailure); // still not past credit
        Assert.True(app.SubmitFinancialAppraisal(Guid.NewGuid(), isApproved: true, null).IsSuccess);
        Assert.Equal(NampApplicationStatus.RiskReview, app.Status);
        Assert.True(app.ApproveRiskReview(Guid.NewGuid(), null).IsSuccess);
        Assert.Equal(NampApplicationStatus.BranchCommitteeCirculation, app.Status);
    }

    [Fact]
    public void ACaseReturnedToTheCreditOfficerMustPassRiskAgain()
    {
        var app = AtCommittee();
        app.ReturnFromCommittee(Guid.NewGuid(), NampCommitteeReturnTarget.CreditOfficer, "Re-appraise.");

        Assert.True(app.ApproveRiskReview(Guid.NewGuid(), null).IsFailure);
        Assert.True(app.SubmitFinancialAppraisal(Guid.NewGuid(), isApproved: true, "Revised.").IsSuccess);
        Assert.Equal(NampApplicationStatus.RiskReview, app.Status);
    }

    // ── Pass numbering and the append-only history ─────────────────────────────────────────────────

    [Fact]
    public void StampsASecondPassOnTheReworkedAppraisalAndRiskRecords()
    {
        var app = AtCommittee();
        Assert.Equal(1, app.ReviewPassNumber);
        Assert.Equal(1, app.FinancialAppraisalRecords.Count);
        Assert.Equal(1, app.RiskReviewRecords.Count);

        app.ReturnFromCommittee(Guid.NewGuid(), NampCommitteeReturnTarget.CreditOfficer, "Re-appraise.");
        Assert.Equal(2, app.ReviewPassNumber);

        app.SubmitFinancialAppraisal(Guid.NewGuid(), isApproved: true, "Second look.");
        app.ApproveRiskReview(Guid.NewGuid(), "Risk re-cleared.");

        Assert.Equal(2, app.FinancialAppraisalRecords.Count);
        Assert.Equal([1, 2], app.FinancialAppraisalRecords.Select(r => r.PassNumber));
        Assert.Equal([1, 2], app.RiskReviewRecords.Select(r => r.PassNumber));
    }

    [Fact]
    public void TheFirstAppraisalSurvivesARework()
    {
        // The whole reason for the append-only records: FinancialAppraisalNote is a single field, so
        // a second appraisal used to overwrite the first with no trace of who said what.
        var app = AtCommittee();
        var firstAppraiser = app.FinancialAppraisalRecords[0].AppraisedByUserId;

        app.ReturnFromCommittee(Guid.NewGuid(), NampCommitteeReturnTarget.CreditOfficer, "Re-appraise.");
        app.SubmitFinancialAppraisal(Guid.NewGuid(), isApproved: true, "Revised figures.");

        Assert.Equal("Revised figures.", app.FinancialAppraisalNote); // latest wins on the flat field
        Assert.Equal("First pass.", app.FinancialAppraisalRecords[0].Note); // but the original stands
        Assert.Equal(firstAppraiser, app.FinancialAppraisalRecords[0].AppraisedByUserId);
    }

    [Fact]
    public void RiskRecordsDistinguishApprovalFromReturnAndDecline()
    {
        // A bool could not tell these apart, and telling them apart is why the history is read.
        var app = AtRiskReview();
        app.ReturnFromRiskReview(Guid.NewGuid(), "Needs better collateral cover.");
        app.SubmitFinancialAppraisal(Guid.NewGuid(), isApproved: true, "Reworked.");
        app.DeclineAtRiskReview(Guid.NewGuid(), "Unacceptable concentration.");

        Assert.Equal(
            [NampRiskReviewOutcome.Returned, NampRiskReviewOutcome.Declined],
            app.RiskReviewRecords.Select(r => r.Outcome));
    }

    [Fact]
    public void ClearsTheReturnBannerOnceTheCaseIsCirculatedAgain()
    {
        // The reason should stop shouting at the officer after the rework is done — but it stays in
        // the status history permanently.
        var app = AtCommittee();
        app.ReturnFromCommittee(Guid.NewGuid(), NampCommitteeReturnTarget.RiskOfficer, "Re-run the opinion.");
        Assert.NotNull(app.CommitteeReturnNote);

        app.ApproveRiskReview(Guid.NewGuid(), null);
        app.CirculateToCommittee(Guid.NewGuid(), Guid.NewGuid());

        Assert.Null(app.CommitteeReturnNote);
        Assert.Null(app.CommitteeReturnedAt);
        Assert.Contains(app.StatusHistory, h => h.Note is not null && h.Note.Contains("Re-run the opinion"));
    }

    [Fact]
    public void TheHistoryRecordsThatTheEarlierVotesWereSuperseded()
    {
        var app = AtCommittee();

        app.ReturnFromCommittee(Guid.NewGuid(), NampCommitteeReturnTarget.CreditOfficer, "Re-appraise.");

        Assert.Contains(app.StatusHistory, h => h.Note is not null && h.Note.Contains("superseded"));
    }

    // ── Fixtures ──────────────────────────────────────────────────────────────────────────────────

    private static NampApplication Draft() => NampApplication.Create(
        applicationNumber: "NAMP-2026-RET00001",
        loanProductId: Guid.NewGuid(),
        applicationReference: "PORTAL-REF-RET-1",
        applicantName: "Musa Danjuma",
        boaAccountNumber: "0123456789",
        applicantCategory: NampApplicantCategory.YouthAgripreneur,
        equipmentDescription: "Tractor",
        equipmentValue: 12_000_000m,
        branchId: Guid.NewGuid(),
        committeeTier: NampCommitteeTier.Branch,
        recalledByUserId: Guid.NewGuid()).Value;

    private static NampApplication AtRiskReview()
    {
        var app = Draft();
        app.Submit(Guid.NewGuid());
        app.SubmitFinancialAppraisal(Guid.NewGuid(), isApproved: true, "First pass.");
        return app;
    }

    private static NampApplication AtCommittee()
    {
        var app = AtRiskReview();
        app.ApproveRiskReview(Guid.NewGuid(), "Cleared to committee.");
        app.CirculateToCommittee(Guid.NewGuid(), Guid.NewGuid());
        return app;
    }
}

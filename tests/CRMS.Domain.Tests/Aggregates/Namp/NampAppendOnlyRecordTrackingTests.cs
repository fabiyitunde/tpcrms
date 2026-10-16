using CRMS.Domain.Aggregates.Namp;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Tests.Aggregates.Namp;

/// <summary>
/// Guards the two append-only record types against the EF tracking trap that broke financial
/// appraisal submission.
///
/// Entity assigns a non-default GUID in its constructor, so a child added through a collection
/// navigation on an already-persisted aggregate is tracked as Modified rather than Added. EF then
/// issues an UPDATE against a row that does not exist and the save fails with "expected to affect 1
/// row(s), but actually affected 0". CRMSDbContext.SaveChangesAsync flips those entries back to
/// Added, and every append-only type has to be listed there — adding one and forgetting the list is
/// exactly what happened here.
///
/// These assertions cannot see the DbContext, so what they pin is the precondition: that the records
/// really are append-only and carry a pre-assigned key, which is what makes the flip both necessary
/// and safe. The persistence itself was verified against the live database.
/// </summary>
public class NampAppendOnlyRecordTrackingTests
{
    [Fact]
    public void AppraisalRecordsCarryAPreAssignedKey()
    {
        // A default (empty) key would make EF treat the child as Added on its own, and no flip would
        // be needed. It does not, which is why the context has to compensate.
        var app = AtRiskReview();

        var record = Assert.Single(app.FinancialAppraisalRecords);
        Assert.NotEqual(Guid.Empty, record.Id);
        Assert.Equal(app.Id, record.NampApplicationId);
    }

    [Fact]
    public void RiskRecordsCarryAPreAssignedKey()
    {
        var app = AtRiskReview();
        app.ApproveRiskReview(Guid.NewGuid(), "Cleared.");

        var record = Assert.Single(app.RiskReviewRecords);
        Assert.NotEqual(Guid.Empty, record.Id);
        Assert.Equal(app.Id, record.NampApplicationId);
    }

    [Fact]
    public void RecordsAreOnlyEverAppended_NeverRewritten()
    {
        // The flip is safe precisely because Modified can only ever mean "new" for these types. If
        // anything ever mutated a stored record, the flip would turn that edit into a duplicate
        // INSERT — so nothing may.
        var app = AtRiskReview();
        var firstId = app.FinancialAppraisalRecords[0].Id;
        var firstNote = app.FinancialAppraisalRecords[0].Note;

        app.ApproveRiskReview(Guid.NewGuid(), "Cleared.");
        app.CirculateToCommittee(Guid.NewGuid(), Guid.NewGuid());
        app.ReturnFromCommittee(Guid.NewGuid(), NampCommitteeReturnTarget.CreditOfficer, "Re-appraise.");
        app.SubmitFinancialAppraisal(Guid.NewGuid(), isApproved: true, "Second look.");

        Assert.Equal(2, app.FinancialAppraisalRecords.Count);
        Assert.Equal(firstId, app.FinancialAppraisalRecords[0].Id);     // same row
        Assert.Equal(firstNote, app.FinancialAppraisalRecords[0].Note); // unchanged
        Assert.NotEqual(firstId, app.FinancialAppraisalRecords[1].Id);  // a new one
    }

    private static NampApplication AtRiskReview()
    {
        var app = NampApplication.Create(
            applicationNumber: "NAMP-2026-TRK00001",
            loanProductId: Guid.NewGuid(),
            applicationReference: "PORTAL-REF-TRK-1",
            applicantName: "Musa Danjuma",
            boaAccountNumber: "0123456789",
            applicantCategory: NampApplicantCategory.YouthAgripreneur,
            equipmentDescription: "Tractor",
            equipmentValue: 12_000_000m,
            branchId: Guid.NewGuid(),
            committeeTier: NampCommitteeTier.Branch,
            recalledByUserId: Guid.NewGuid()).Value;

        app.Submit(Guid.NewGuid());
        app.SubmitFinancialAppraisal(Guid.NewGuid(), isApproved: true, "First pass.");
        return app;
    }
}

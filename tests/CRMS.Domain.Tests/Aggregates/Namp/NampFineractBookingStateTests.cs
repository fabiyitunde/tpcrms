using CRMS.Domain.Aggregates.Namp;
using CRMS.Domain.Enums;
using Xunit;

namespace CRMS.Domain.Tests.Aggregates.Namp;

/// <summary>
/// FineractDisbursedOn ("Core Banking Disbursed On" on the Loan Account tab) is what separates a loan
/// core banking funded from one it merely created. The booking retry is gated on it, so these two
/// setters decide whether an officer can still finish a stalled booking.
/// </summary>
public class NampFineractBookingStateTests
{
    private static NampApplication MakeApplication() => NampApplication.Create(
        applicationNumber: "NAMP-2026-ABCD1234",
        loanProductId: Guid.NewGuid(),
        applicationReference: "PORTAL-REF-1",
        applicantName: "Musa Danjuma",
        boaAccountNumber: "0123456789",
        applicantCategory: NampApplicantCategory.YouthAgripreneur,
        equipmentDescription: "Tractor",
        equipmentValue: 12_000_000m,
        branchId: Guid.NewGuid(),
        committeeTier: NampCommitteeTier.Branch,
        recalledByUserId: Guid.NewGuid()).Value;

    [Fact]
    public void NewApplication_HasNoBookingState()
    {
        var app = MakeApplication();

        Assert.Null(app.FineractLoanId);
        Assert.Null(app.FineractLoanAccountNumber);
        Assert.Null(app.FineractDisbursedOn);
    }

    [Fact]
    public void CreatedNotDisbursed_KeepsTheLoanId_ButLeavesDisbursedOnNull()
    {
        var app = MakeApplication();

        app.SetFineractLoanCreatedNotDisbursed(901);

        Assert.Equal(901, app.FineractLoanId);
        // No account number is generated until disbursement, and no disbursement timestamp —
        // this is what keeps the booking retry open.
        Assert.Null(app.FineractLoanAccountNumber);
        Assert.Null(app.FineractDisbursedOn);
    }

    [Fact]
    public void DisbursedResult_StampsDisbursedOn_AndTheAccountNumber()
    {
        var app = MakeApplication();

        app.SetFineractLoanResult(901, "LN-000901");

        Assert.Equal(901, app.FineractLoanId);
        Assert.Equal("LN-000901", app.FineractLoanAccountNumber);
        Assert.NotNull(app.FineractDisbursedOn);
    }

    [Fact]
    public void ResumingAStalledBooking_StampsDisbursedOnOnce()
    {
        var app = MakeApplication();

        // First attempt stalled after create; the retry resumed the same loan and disbursed it.
        app.SetFineractLoanCreatedNotDisbursed(901);
        app.SetFineractLoanResult(901, "LN-000901");
        var firstStamp = app.FineractDisbursedOn;

        // A later call must not move the recorded disbursement moment.
        app.SetFineractLoanResult(901, "LN-000901");

        Assert.Equal(firstStamp, app.FineractDisbursedOn);
        Assert.Equal("LN-000901", app.FineractLoanAccountNumber);
    }
}

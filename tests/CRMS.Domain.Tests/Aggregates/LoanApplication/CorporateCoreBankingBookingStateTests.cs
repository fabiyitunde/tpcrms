using CRMS.Domain.Enums;
using CRMS.Domain.ValueObjects;
using App = CRMS.Domain.Aggregates.LoanApplication.LoanApplication;
using Xunit;

namespace CRMS.Domain.Tests.Aggregates.LoanApplication;

/// <summary>
/// Corporate equivalent of the NAMP booking-state tests. A core banking loan id with no DisbursedAt
/// means "created, not disbursed" — no money moved — and the application must stay at Disbursement so
/// Operations can resume it. Shown on the page as "Core Banking Disbursed On".
/// </summary>
public class CorporateCoreBankingBookingStateTests
{
    private static App MakeApplicationAtDisbursement()
    {
        var app = App.CreateCorporate(
            loanProductId: Guid.NewGuid(),
            productCode: "CORP-TERM",
            accountNumber: "0123456789",
            customerId: "CUST-001",
            customerName: "Alliedsoft Limited",
            requestedAmount: Money.Create(50_000_000m),
            requestedTenorMonths: 24,
            interestRatePerAnnum: 21.5m,
            interestRateType: InterestRateType.Reducing,
            initiatedByUserId: Guid.NewGuid(),
            branchId: Guid.NewGuid()).Value;

        var officer = Guid.NewGuid();
        app.SubmitForCreditReview(officer);
        app.CompleteLegalReview(officer, "Legal review complete.");
        app.SaveCreditAppraisal(officer, 1.8m, 0.4m, 1.5m, 0.6m, "Strong", "Approve", null, null, null);
        app.ApproveCreditReview(officer);
        app.ApproveCommittee(officer, Money.Create(50_000_000m), 24, 21.5m);
        app.MoveToRatification(officer);
        app.Ratify(officer, Money.Create(50_000_000m), 24, 21.5m);
        app.IssueRatifiedOfferLetter(officer);
        app.RecordCorporateOfferAcceptance(officer, DateTime.UtcNow, OfferAcceptanceMethod.InBranchSigning, kfsAcknowledged: true);
        app.MoveToSecurityPerfection(officer);
        app.CompleteSecurityPerfection(officer);

        Assert.Equal(LoanApplicationStatus.Disbursement, app.Status);
        return app;
    }

    [Fact]
    public void CreatedNotDisbursed_KeepsTheLoanId_AndNothingClaimsMoneyMoved()
    {
        var app = MakeApplicationAtDisbursement();

        var result = app.RecordCoreBankingLoanCreatedNotDisbursed(901);

        Assert.True(result.IsSuccess);
        Assert.Equal(901, app.FineractLoanId);
        // Still at Disbursement, with no disbursement date and no loan account number —
        // this is what keeps the disbursement resumable.
        Assert.Equal(LoanApplicationStatus.Disbursement, app.Status);
        Assert.Null(app.DisbursedAt);
        Assert.Null(app.CoreBankingLoanId);
    }

    [Fact]
    public void ResumingAfterAPartialBooking_CompletesTheDisbursement()
    {
        var app = MakeApplicationAtDisbursement();
        app.RecordCoreBankingLoanCreatedNotDisbursed(901);

        var result = app.CompleteDisbursement("LN-000901", Guid.NewGuid(), fineractLoanId: 901);

        Assert.True(result.IsSuccess);
        Assert.Equal(LoanApplicationStatus.Disbursed, app.Status);
        Assert.Equal(901, app.FineractLoanId);
        Assert.Equal("LN-000901", app.CoreBankingLoanId);
        Assert.NotNull(app.DisbursedAt);
    }

    [Fact]
    public void CreatedNotDisbursed_IsRejectedOutsideDisbursement()
    {
        var app = App.CreateCorporate(
            loanProductId: Guid.NewGuid(),
            productCode: "CORP-TERM",
            accountNumber: "0123456789",
            customerId: "CUST-001",
            customerName: "Alliedsoft Limited",
            requestedAmount: Money.Create(50_000_000m),
            requestedTenorMonths: 24,
            interestRatePerAnnum: 21.5m,
            interestRateType: InterestRateType.Reducing,
            initiatedByUserId: Guid.NewGuid(),
            branchId: Guid.NewGuid()).Value;

        var result = app.RecordCoreBankingLoanCreatedNotDisbursed(901);

        Assert.True(result.IsFailure);
        Assert.Null(app.FineractLoanId);
    }
}

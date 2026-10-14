using CRMS.Domain.Common;
using CRMS.Domain.Aggregates.Rhshf;

namespace CRMS.Domain.Tests.Aggregates.Rhshf;

/// <summary>
/// Reconciliation of the EOP: the input lines plus the portal's itemised charges must equal the
/// submitted total.
///
/// This is the one instruction both teams agreed on in writing — the approved EOP, the offer letter
/// and the core banking booking have to describe one financing structure, and nothing financed may
/// also be deducted as a charge. The approved amount must equal this total exactly (no partial
/// approval), and the offer letter's implementation plan is printed from these same lines and
/// charges, so a submission that does not balance would put a plan in front of a borrower that does
/// not add up to what they are signing for.
/// </summary>
public class RhshfEopReconciliationTests
{
    // The approved Sagad offer: three supplier lines, then working capital and insurance.
    private const decimal ExtensionServices = 22_500_000.00m;
    private const decimal Fertiliser = 187_200_000.00m;
    private const decimal Motar = 17_445_600.00m;
    private const decimal WorkingCapital = 10_232_280.00m;
    private const decimal Insurance = 8_185_824.00m;
    private const decimal ApprovedTotal = 245_563_704.00m;

    [Fact]
    public void Accepts_ASubmissionThatBalances()
    {
        var result = Create(ApprovedTotal, SagadLines(), new RhshfEopChargesInput(Insurance, WorkingCapital));

        Assert.True(result.IsSuccess);
        Assert.Equal(Insurance + WorkingCapital, result.Value.ItemisedChargesTotal);
        Assert.Equal(0m, result.Value.UnitemisedRemainder);
    }

    [Fact]
    public void Refuses_WhenTheChargesAreDoubleCounted()
    {
        // The exact failure the two teams set out to avoid: the portal computes the charges into the
        // total, then something adds them again. The lines plus charges overshoot the total.
        var result = Create(
            ApprovedTotal - WorkingCapital - Insurance,
            SagadLines(),
            new RhshfEopChargesInput(Insurance, WorkingCapital));

        Assert.True(result.IsFailure);
        Assert.Contains("does not reconcile", result.Error);
    }

    [Fact]
    public void Refuses_WhenTheTotalIsShortOfTheBreakdown()
    {
        var result = Create(ApprovedTotal - 1_000m, SagadLines(), new RhshfEopChargesInput(Insurance, WorkingCapital));

        Assert.True(result.IsFailure);
        Assert.Contains("does not reconcile", result.Error);
    }

    [Fact]
    public void NamesTheArithmeticInTheRefusal()
    {
        // Whoever reads this is reconciling two systems, so the message has to carry both figures
        // and the gap — "does not reconcile" alone sends them to a spreadsheet.
        var result = Create(ApprovedTotal + 2_025_000m, SagadLines(), new RhshfEopChargesInput(Insurance, WorkingCapital));

        Assert.True(result.IsFailure);
        Assert.Contains("245,563,704.00", result.Error);
        Assert.Contains("247,588,704.00", result.Error);
        Assert.Contains("2,025,000.00", result.Error);
    }

    [Fact]
    public void ToleratesRoundingOfAKoboOrLess()
    {
        // Two systems computing percentages will not always agree on the last kobo. A kobo is not a
        // reconciliation failure; a naira is.
        Assert.True(Create(ApprovedTotal + 0.01m, SagadLines(), new RhshfEopChargesInput(Insurance, WorkingCapital)).IsSuccess);
        Assert.True(Create(ApprovedTotal - 0.01m, SagadLines(), new RhshfEopChargesInput(Insurance, WorkingCapital)).IsSuccess);
        Assert.True(Create(ApprovedTotal + 1.00m, SagadLines(), new RhshfEopChargesInput(Insurance, WorkingCapital)).IsFailure);
    }

    [Fact]
    public void SkipsTheCheck_WhenNoChargesWereItemised()
    {
        // The portal is rolling the itemised charges out separately. Until they arrive there is
        // nothing to reconcile against, and a submission must not be rejected for it — the letter
        // falls back to one balancing row for the unitemised remainder.
        var result = Create(ApprovedTotal, SagadLines(), charges: null);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.ItemisedChargesTotal);
        Assert.Equal(WorkingCapital + Insurance, result.Value.UnitemisedRemainder);
    }

    [Fact]
    public void TreatsAPartialBreakdownAsItemised_AndStillRequiresItToBalance()
    {
        // Only the technology fee sent, say. It counts as itemised, so the remainder is whatever the
        // other charges come to — and the check applies, because a figure WAS supplied to check.
        var technologyFee = 40_000.00m;
        var result = Create(
            ApprovedTotal + technologyFee,
            SagadLines(),
            new RhshfEopChargesInput(TechnologyServicesFee: technologyFee));

        Assert.True(result.IsFailure);
        Assert.Contains("does not reconcile", result.Error);
    }

    [Fact]
    public void UnitemisedRemainder_IsWhatTheLetterPrintsAsItsBalancingRow()
    {
        var result = Create(ApprovedTotal, SagadLines(), new RhshfEopChargesInput(Insurance));

        Assert.True(result.IsFailure); // insurance alone does not balance
        var unitemised = Create(ApprovedTotal, SagadLines(), charges: null).Value;
        Assert.Equal(WorkingCapital + Insurance, unitemised.UnitemisedRemainder);
    }

    private static IEnumerable<RhshfEopLineInput> SagadLines() =>
    [
        new("Extension Services", 450m, 50_000m, ExtensionServices, "EL FIJIR AGRO", "Extension Services", "Items"),
        new("NPK Fertilizer", 3_600m, 52_000m, Fertiliser, "Golden Fertilizer Limited", "NPK 15-15-15", "Bags"),
        new("Motar", 450m, 38_768m, Motar, "WACOT Limited", "Motar", "Items"),
    ];

    private static Result<RhshfCreditProfile> Create(
        decimal totalEopValue, IEnumerable<RhshfEopLineInput> lines, RhshfEopChargesInput? charges) =>
        RhshfCreditProfile.Create(
            submissionId: Guid.NewGuid(), programmeCode: "RH-SHF-DRY-2026", programmeName: "Renewed Hope",
            sessionCode: "2026-DRY", sessionName: "Dry Season 2026", facId: Guid.NewGuid(),
            companyName: "Sagad Farms Limited", rcNumber: "RC771204", tin: null,
            boaAccountNumber: "0123456789", contactEmail: "fac@company.com", contactPhone: "+2348012345678",
            state: "Kano", lga: "Rano", totalEopValue: totalEopValue, currency: "NGN", farmerCount: 450,
            callbackUrl: "https://portal.example.gov.ng/api/integrations/crms/webhook",
            certifiedByAdmin: "admin@boa.gov.ng", certifiedAt: DateTime.UtcNow, rawSubmissionPayload: "{}",
            eopLines: lines, resolvedBranchId: null, resolvedOfficeId: null, charges: charges);
}

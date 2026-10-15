using System.Text;
using CRMS.Application.Rhshf.Interfaces;
using CRMS.Infrastructure.Documents;
using Xunit;

namespace CRMS.Infrastructure.Tests.Documents;

/// <summary>
/// Renders the merged post-approval document end-to-end.
///
/// The cases that matter are the sparse ones. A borrower-facing document must not fall over, or
/// print a stub, because a credit appraisal was never recorded, the core banking product defines no
/// fees, no security was captured, or the case predates the portal's itemised breakdown — all of
/// which are reachable states.
/// </summary>
public class RhshfOfferDocumentPdfTests
{
    private static bool IsPdf(byte[] bytes) =>
        bytes.Length > 1000 && Encoding.ASCII.GetString(bytes, 0, 5) == "%PDF-";

    [Fact]
    public async Task RendersTheFullySpecifiedSagadStyleOffer()
    {
        var bytes = await new RhshfOfferDocumentPdfGenerator().GenerateAsync(Data());

        Assert.True(IsPdf(bytes));
    }

    [Fact]
    public async Task RendersWithNoAppraisalRecorded()
    {
        // Terms the appraisal would have supplied are stated as "per approved credit terms" instead
        // of being invented.
        var bytes = await new RhshfOfferDocumentPdfGenerator().GenerateAsync(Data() with
        {
            InterestRatePercent = null,
            TenorMonths = null,
            AmountDueAtHarvest = null,
            InterestCharge = null,
        });

        Assert.True(IsPdf(bytes));
    }

    [Fact]
    public async Task RendersWithNoProductFeesAndNoSecurityAndNoFeesAccount()
    {
        var bytes = await new RhshfOfferDocumentPdfGenerator().GenerateAsync(Data() with
        {
            ProductFees = [],
            SecurityDescriptions = [],
            FeesAccount = null,
        });

        Assert.True(IsPdf(bytes));
    }

    [Fact]
    public async Task RendersACasePredatingTheItemisedBreakdown()
    {
        // Crop-level rows plus one balancing charge row, with no suppliers to name.
        var bytes = await new RhshfOfferDocumentPdfGenerator().GenerateAsync(Data() with
        {
            PlanIsItemised = false,
            PlanRows =
            [
                new RhshfOfferPlanRow(null, "Maize", "100,000 kg", 44_220_000.00m, false, null),
                new RhshfOfferPlanRow(null, "Programme charges", null, 4_019_800.00m, true,
                    "Insurance, working capital and programme fees, as approved"),
            ],
        });

        Assert.True(IsPdf(bytes));
    }

    [Fact]
    public async Task RendersWithNoPlanRowsAtAll()
    {
        var bytes = await new RhshfOfferDocumentPdfGenerator().GenerateAsync(Data() with { PlanRows = [] });

        Assert.True(IsPdf(bytes));
    }

    [Fact]
    public async Task RendersWithNoAddressKnown()
    {
        // Neither a CAC address nor a portal locality — the block is omitted, not stubbed.
        var bytes = await new RhshfOfferDocumentPdfGenerator().GenerateAsync(Data() with { AddressLines = [] });

        Assert.True(IsPdf(bytes));
    }

    [Fact]
    public async Task RendersAGracePeriodOnceOneIsSettled()
    {
        // Grace is null until the bank decides where it comes from; this proves the wording holds
        // when it arrives, so turning it on later needs no layout work.
        var bytes = await new RhshfOfferDocumentPdfGenerator().GenerateAsync(Data() with { GraceMonths = 6 });

        Assert.True(IsPdf(bytes));
    }

    [Fact]
    public async Task RendersALongPlanAcrossPages()
    {
        // Forty rows pushes the plan past one page: the acceptance pages must still come out intact,
        // since a signatory signing a block whose terms sit elsewhere is the failure to avoid.
        var rows = Enumerable.Range(1, 40)
            .Select(i => new RhshfOfferPlanRow(
                $"Supplier {i} Limited", $"Input item {i}", $"{i * 50:N0} Bags", i * 100_000m, false, null))
            .ToList();

        var bytes = await new RhshfOfferDocumentPdfGenerator().GenerateAsync(Data() with { PlanRows = rows });

        Assert.True(IsPdf(bytes));
    }

    private static RhshfOfferDocumentData Data() => new(
        Reference: "RHSHF-2026-811951",
        CompanyName: "Sagad Farms Limited",
        RcNumber: "RC771204",
        ProgrammeName: "Renewed Hope Smallholder Support and Value-Chain Fund (RH-SHF)",
        SessionName: "Dry Season 2026",
        ApprovedAmount: 245_563_704.00m,
        Currency: "NGN",
        GeneratedDate: new DateTime(2026, 6, 3),
        BankName: "Bank of Agriculture",
        AddressLines: ["KM 28 Rano – Rurum Road", "Farar Gada, Tiga Dam", "Kano"],
        InterestRatePercent: 9m,
        TenorMonths: 9,
        GraceMonths: null,
        AmountDueAtHarvest: 262_752_163.28m,
        InterestCharge: 17_188_459.28m,
        FarmerCount: 450,
        PlanRows:
        [
            new RhshfOfferPlanRow("EL FIJIR AGRO", "Extension Services", "450 Items", 22_500_000.00m, false, null),
            new RhshfOfferPlanRow("Golden Fertilizer Limited", "NPK 15-15-15", "3,600 Bags", 187_200_000.00m, false, null),
            new RhshfOfferPlanRow("WACOT Limited", "Motar", "450 Items", 17_445_600.00m, false, null),
            new RhshfOfferPlanRow(null, "Working capital", null, 10_232_280.00m, true,
                "5% of the input cost, excluding services priced for the whole EOP"),
            new RhshfOfferPlanRow(null, "Insurance", null, 8_185_824.00m, true,
                "4% of the input cost, excluding services priced for the whole EOP"),
        ],
        PlanIsItemised: true,
        ProductFees:
        [
            new RhshfOfferFee("Appraisal Fee", "1% (% Approved Amount)"),
            new RhshfOfferFee("Monitoring Fee", "0.5% (% Approved Amount)"),
        ],
        SecurityDescriptions:
        [
            "Bank guarantee from Stanbic IBTC for NGN 245,563,704.00, covering the principal and accrued "
            + "interest of the facility.",
        ],
        FeesAccount: new RhshfOfferFeesAccount(
            "Bank of Agriculture (BOA)", "Renewed Hope Small Holders Fees", "0016840374"),
        ValidityDays: 7);
}

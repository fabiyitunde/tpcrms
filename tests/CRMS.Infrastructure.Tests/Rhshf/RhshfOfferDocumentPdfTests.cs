using System.Text;
using CRMS.Application.Rhshf.Interfaces;
using CRMS.Infrastructure.Documents;
using Xunit;

namespace CRMS.Infrastructure.Tests.Rhshf;

/// <summary>
/// Exercises the RH-SHF offer-letter and KFS QuestPDF generators end-to-end with rich representative
/// data — proves they render without throwing and emit a valid PDF, covering the enriched layouts
/// (letter key-terms + acceptance block; KFS applied-for package + de-lumped figure breakdown) as
/// well as the sparse path where no appraisal figures are available.
/// </summary>
public class RhshfOfferDocumentPdfTests
{
    private static bool IsPdf(byte[] bytes) =>
        bytes.Length > 1000 && Encoding.ASCII.GetString(bytes, 0, 5) == "%PDF-";

    [Fact]
    public async Task OfferLetter_WithTerms_RendersValidPdf()
    {
        var data = new RhshfOfferLetterData(
            Reference: "RHSHF-2026-000123",
            CompanyName: "Green Harvest Aggregators Ltd",
            RcNumber: "RC1234567",
            ProgrammeName: "Renewed Hope",
            SessionName: "2026 Dry Season",
            ApprovedAmount: 15_000_000m,
            Currency: "NGN",
            GeneratedDate: new DateTime(2026, 10, 9),
            BankName: "Bank of Agriculture",
            InterestRatePercent: 12.5m,
            CycleMonths: 6,
            AmountDueAtHarvest: 16_875_000m);

        var bytes = await new RhshfOfferLetterPdfGenerator().GenerateAsync(data);

        Assert.True(IsPdf(bytes), "offer letter should be a valid PDF");
    }

    [Fact]
    public async Task OfferLetter_WithoutTerms_StillRenders()
    {
        var data = new RhshfOfferLetterData(
            Reference: "RHSHF-2026-000124",
            CompanyName: "Sunrise Farms Coop",
            RcNumber: "RC7654321",
            ProgrammeName: "Renewed Hope",
            SessionName: "2026 Dry Season",
            ApprovedAmount: 5_000_000m,
            Currency: "NGN",
            GeneratedDate: new DateTime(2026, 10, 9),
            BankName: "Bank of Agriculture");

        var bytes = await new RhshfOfferLetterPdfGenerator().GenerateAsync(data);

        Assert.True(IsPdf(bytes));
    }

    [Fact]
    public async Task Kfs_WithAppliedForPackageAndFigures_RendersValidPdf()
    {
        var data = new RhshfKfsData(
            Reference: "RHSHF-2026-000123",
            CompanyName: "Green Harvest Aggregators Ltd",
            RcNumber: "RC1234567",
            ProgrammeName: "Renewed Hope",
            SessionName: "2026 Dry Season",
            ApprovedAmount: 15_000_000m,
            Currency: "NGN",
            GeneratedDate: new DateTime(2026, 10, 9),
            BankName: "Bank of Agriculture",
            InterestRatePercent: 12.5m,
            CycleMonths: 6,
            AmountDueAtHarvest: 16_875_000m,
            AppliedForLines: new[]
            {
                new RhshfKfsEopLine("Maize seed", 2_000m, 1_500m, 3_000_000m),
                new RhshfKfsEopLine("NPK fertiliser", 10_000m, 1_200m, 12_000_000m),
            },
            FarmerCount: 450,
            State: "Kaduna",
            Lga: "Chikun",
            FinancedInputCost: 15_000_000m,
            InterestCharge: 1_875_000m);

        var bytes = await new RhshfKfsPdfGenerator().GenerateAsync(data);

        Assert.True(IsPdf(bytes), "KFS should be a valid PDF");
    }

    [Fact]
    public async Task Kfs_WithNoPackageOrFigures_StillRenders()
    {
        // Legacy/SQL-advanced case: no appraisal, no EOP lines, blank location.
        var data = new RhshfKfsData(
            Reference: "RHSHF-2026-000125",
            CompanyName: "Nascent Agro Ltd",
            RcNumber: "RC0001112",
            ProgrammeName: "Renewed Hope",
            SessionName: "2026 Dry Season",
            ApprovedAmount: 2_000_000m,
            Currency: "NGN",
            GeneratedDate: new DateTime(2026, 10, 9),
            BankName: "Bank of Agriculture",
            InterestRatePercent: null,
            CycleMonths: null,
            AmountDueAtHarvest: null,
            AppliedForLines: Array.Empty<RhshfKfsEopLine>(),
            FarmerCount: null,
            State: "",
            Lga: "",
            FinancedInputCost: null,
            InterestCharge: null);

        var bytes = await new RhshfKfsPdfGenerator().GenerateAsync(data);

        Assert.True(IsPdf(bytes));
    }
}

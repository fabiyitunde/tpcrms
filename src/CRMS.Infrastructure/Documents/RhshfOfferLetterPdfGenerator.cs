using CRMS.Application.Rhshf.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CRMS.Infrastructure.Documents;

/// <summary>
/// Own, independent RH-SHF offer letter generator — see IRhshfOfferLetterPdfGenerator for why this
/// doesn't reuse NampOfferLetterPdfGenerator/OfferLetterPdfGenerator's shape. Reuses BoaBrand
/// (shared branding helper, not domain code) for a consistent look with the bank's other PDFs.
/// </summary>
public class RhshfOfferLetterPdfGenerator : IRhshfOfferLetterPdfGenerator
{
    public Task<byte[]> GenerateAsync(RhshfOfferLetterData data, CancellationToken ct = default)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Element(c => ComposeHeader(c, data));
                page.Content().Element(c => ComposeContent(c, data));
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("This is a system-generated offer notice — RH-SHF Credit Profiling, reference ")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                    text.Span(data.Reference).FontSize(8).FontColor(Colors.Grey.Darken1).SemiBold();
                });
            });
        });

        return Task.FromResult(document.GeneratePdf());
    }

    private static void ComposeHeader(IContainer container, RhshfOfferLetterData data)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Element(c => BoaBrand.RenderLogo(c, 50));
                row.RelativeItem(3).AlignCenter().Column(titleCol =>
                {
                    titleCol.Item().AlignCenter().Text(data.BankName.ToUpperInvariant())
                        .Bold().FontSize(14).FontColor(Color.FromHex(BoaBrand.Primary));
                    titleCol.Item().AlignCenter().PaddingTop(2)
                        .Text("Renewed Hope – Smallholder Farmer Input Financing (RH-SHF)")
                        .FontSize(9).Italic().FontColor(Color.FromHex(BoaBrand.Accent));
                });
            });
            col.Item().PaddingTop(10).LineHorizontal(1).LineColor(Color.FromHex(BoaBrand.MediumGray));
        });
    }

    private static void ComposeContent(IContainer container, RhshfOfferLetterData data)
    {
        container.PaddingTop(20).Column(col =>
        {
            col.Item().Text($"Reference: {data.Reference}").Bold();
            col.Item().PaddingTop(2).Text($"Date: {data.GeneratedDate:dd MMMM yyyy}");

            col.Item().PaddingTop(16).Text("Dear Sir/Madam,").Bold();
            col.Item().PaddingTop(8).Text(
                $"We are pleased to confirm that the credit profiling for {data.CompanyName} " +
                $"(RC {data.RcNumber}) under the {data.ProgrammeName} programme, {data.SessionName}, " +
                "has been ratified. The facility is offered to you on the key terms set out below.");

            col.Item().PaddingTop(16).Background(Color.FromHex(BoaBrand.PanelGreen)).Padding(12).Column(box =>
            {
                box.Item().Text("Approved Amount").FontSize(9).FontColor(Colors.Grey.Darken1);
                box.Item().PaddingTop(2).Text($"{data.Currency} {data.ApprovedAmount:N2}")
                    .Bold().FontSize(16).FontColor(Color.FromHex(BoaBrand.Primary));
            });

            // Key terms summary — the full numeric breakdown is in the accompanying Key Facts Statement.
            col.Item().PaddingTop(16).Text("Key Terms").SemiBold().FontColor(Color.FromHex(BoaBrand.Primary));
            col.Item().PaddingTop(6).Table(table =>
            {
                table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(3); });

                void Term(string label, string value)
                {
                    table.Cell().Border(0.5f).BorderColor(Color.FromHex(BoaBrand.MediumGray)).Padding(6)
                        .Text(label).SemiBold().FontSize(9);
                    table.Cell().Border(0.5f).BorderColor(Color.FromHex(BoaBrand.MediumGray)).Padding(6)
                        .Text(value).FontSize(9);
                }

                Term("Facility type", "Dry-season agricultural input financing (in-kind seeds/fertiliser)");
                Term("Approved amount", $"{data.Currency} {data.ApprovedAmount:N2}");
                Term("Interest rate", data.InterestRatePercent is { } r ? $"{r:N1}% for the cycle" : "Per the facility terms");
                Term("Tenor", data.CycleMonths is { } m ? $"{m} month crop cycle (single cycle)" : "One crop cycle");
                Term("Repayment", data.AmountDueAtHarvest is { } due
                    ? $"{data.Currency} {due:N2} — single bullet, due at harvest"
                    : "Single bullet (principal plus interest), due at harvest");
            });

            // Conditions — kept short; the KFS carries the fuller facts sheet.
            col.Item().PaddingTop(16).Text("Conditions").SemiBold().FontColor(Color.FromHex(BoaBrand.Primary));
            col.Item().PaddingTop(4).Column(cond =>
            {
                void Bullet(string text) => cond.Item().PaddingTop(2).Row(r =>
                {
                    r.ConstantItem(12).Text("•").FontColor(Color.FromHex(BoaBrand.Accent));
                    r.RelativeItem().Text(text).FontSize(9);
                });

                Bullet("Disbursement is made in kind to the approved input supplier(s), not as cash to the borrower.");
                Bullet("Repayment is a single bullet of principal plus interest, due at the end of the crop cycle (harvest).");
                Bullet("Any applicable security is to be perfected as recorded on the case before disbursement.");
                Bullet("This offer is valid for 30 days from the date above; it lapses if not accepted within that period.");
                Bullet("Please read this letter together with the accompanying Key Facts Statement (KFS).");
            });

            col.Item().PaddingTop(14).Text(
                "To accept, please countersign below and return this letter together with the signed Key Facts " +
                "Statement through the RH-SHF portal. This notice does not itself constitute disbursement.")
                .FontSize(9);

            col.Item().PaddingTop(20).Text("Yours faithfully,");
            col.Item().PaddingTop(20).Text(data.BankName).Bold();

            col.Item().PaddingTop(10).LineHorizontal(0.75f).LineColor(Color.FromHex(BoaBrand.MediumGray));

            // Borrower acceptance / countersignature — the gap the FAC fills before re-uploading.
            col.Item().PaddingTop(12).Text("ACCEPTANCE").Bold().FontColor(Color.FromHex(BoaBrand.Primary));
            col.Item().PaddingTop(4).Text(
                $"We, for and on behalf of {data.CompanyName} (RC {data.RcNumber}), accept the facility on the " +
                "terms and conditions set out in this offer letter and the accompanying Key Facts Statement.")
                .FontSize(9);

            col.Item().PaddingTop(28).Row(row =>
            {
                row.RelativeItem().Column(c => { c.Item().LineHorizontal(0.75f); c.Item().PaddingTop(2).Text("Authorised signature").FontSize(8); });
                row.ConstantItem(24);
                row.RelativeItem().Column(c => { c.Item().LineHorizontal(0.75f); c.Item().PaddingTop(2).Text("Name").FontSize(8); });
            });
            col.Item().PaddingTop(24).Row(row =>
            {
                row.RelativeItem().Column(c => { c.Item().LineHorizontal(0.75f); c.Item().PaddingTop(2).Text("Designation").FontSize(8); });
                row.ConstantItem(24);
                row.RelativeItem().Column(c => { c.Item().LineHorizontal(0.75f); c.Item().PaddingTop(2).Text("Date").FontSize(8); });
            });
        });
    }
}

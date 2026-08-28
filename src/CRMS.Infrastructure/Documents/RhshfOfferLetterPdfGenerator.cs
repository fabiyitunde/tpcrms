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
                "has been ratified.");

            col.Item().PaddingTop(16).Background(Color.FromHex(BoaBrand.PanelGreen)).Padding(12).Column(box =>
            {
                box.Item().Text("Approved Amount").FontSize(9).FontColor(Colors.Grey.Darken1);
                box.Item().PaddingTop(2).Text($"{data.Currency} {data.ApprovedAmount:N2}")
                    .Bold().FontSize(16).FontColor(Color.FromHex(BoaBrand.Primary));
            });

            col.Item().PaddingTop(16).Text(
                "Please confirm your acceptance of this offer through the RH-SHF portal. This notice " +
                "does not itself constitute disbursement — please refer to the portal for next steps.");

            col.Item().PaddingTop(24).Text("Yours faithfully,");
            col.Item().PaddingTop(24).Text(data.BankName).Bold();
        });
    }
}

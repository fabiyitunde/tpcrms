using CRMS.Application.Rhshf.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CRMS.Infrastructure.Documents;

/// <summary>
/// RH-SHF Key Facts Statement — a plain-language, one-page facts table accompanying the offer letter.
/// Single-bullet product (crop cycle = tenor), so the repayment row states one amount due at harvest,
/// not a schedule. Shares BoaBrand for a consistent look with the offer letter and other bank PDFs.
/// </summary>
public class RhshfKfsPdfGenerator : IRhshfKfsPdfGenerator
{
    public Task<byte[]> GenerateAsync(RhshfKfsData data, CancellationToken ct = default)
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
                    text.Span("Key Facts Statement — RH-SHF Credit Profiling, reference ")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                    text.Span(data.Reference).FontSize(8).FontColor(Colors.Grey.Darken1).SemiBold();
                });
            });
        });

        return Task.FromResult(document.GeneratePdf());
    }

    private static void ComposeHeader(IContainer container, RhshfKfsData data)
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
                    titleCol.Item().AlignCenter().PaddingTop(2).Text("Key Facts Statement (KFS)")
                        .FontSize(10).SemiBold().FontColor(Color.FromHex(BoaBrand.Accent));
                    titleCol.Item().AlignCenter().Text("Renewed Hope – Smallholder Farmer Input Financing (RH-SHF)")
                        .FontSize(8).Italic().FontColor(Color.FromHex(BoaBrand.Accent));
                });
            });
            col.Item().PaddingTop(10).LineHorizontal(1).LineColor(Color.FromHex(BoaBrand.MediumGray));
        });
    }

    private static void ComposeContent(IContainer container, RhshfKfsData data)
    {
        container.PaddingTop(16).Column(col =>
        {
            col.Item().Text($"Reference: {data.Reference}").Bold();
            col.Item().PaddingTop(2).Text($"Date: {data.GeneratedDate:dd MMMM yyyy}");
            col.Item().PaddingTop(2).Text($"Borrower: {data.CompanyName} (RC {data.RcNumber})");
            col.Item().PaddingTop(2).Text($"Programme: {data.ProgrammeName} — {data.SessionName}");

            col.Item().PaddingTop(14).Text("This statement summarises the key facts of the facility offered to you. "
                + "Please read it with the offer letter, sign both, and return them.").FontSize(9).FontColor(Colors.Grey.Darken2);

            col.Item().PaddingTop(12).Table(table =>
            {
                table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(3); });

                void Fact(string label, string value)
                {
                    table.Cell().Border(0.5f).BorderColor(Color.FromHex(BoaBrand.MediumGray)).Padding(6)
                        .Text(label).SemiBold().FontSize(9);
                    table.Cell().Border(0.5f).BorderColor(Color.FromHex(BoaBrand.MediumGray)).Padding(6)
                        .Text(value).FontSize(9);
                }

                Fact("Facility type", "Dry-season agricultural input financing (in-kind seeds/fertiliser)");
                Fact("Approved amount", $"{data.Currency} {data.ApprovedAmount:N2}");
                Fact("Interest rate", data.InterestRatePercent is { } r ? $"{r:N1}% for the cycle" : "Per the facility terms");
                Fact("Tenor", data.CycleMonths is { } m ? $"{m} month crop cycle (single cycle)" : "One crop cycle");
                Fact("Repayment", data.AmountDueAtHarvest is { } due
                    ? $"Single bullet repayment of {data.Currency} {due:N2} due at harvest (end of the cycle)"
                    : "Single bullet repayment of principal plus interest, due at harvest (end of the cycle)");
                Fact("Disbursement", "In kind, to the approved input supplier(s) — not cash to the borrower");
                Fact("Security", "As recorded on the case (e.g. bank guarantee / NIRSAL CRG / legal mortgage), to be perfected");
                Fact("Fees", "As disclosed in the offer letter / facility terms; no hidden charges");
                Fact("Late repayment", "May attract additional charges and affect your credit record and future eligibility");
            });

            col.Item().PaddingTop(16).Text("Acknowledgement").SemiBold();
            col.Item().PaddingTop(4).Text("I/We confirm that I/we have read and understood the key facts above and accept the facility on these terms.")
                .FontSize(9);
            col.Item().PaddingTop(28).Row(row =>
            {
                row.RelativeItem().Column(c => { c.Item().LineHorizontal(0.75f); c.Item().PaddingTop(2).Text("Authorised signature").FontSize(8); });
                row.ConstantItem(30);
                row.RelativeItem().Column(c => { c.Item().LineHorizontal(0.75f); c.Item().PaddingTop(2).Text("Date").FontSize(8); });
            });
        });
    }
}

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
            // ── Applicant & application ────────────────────────────────────────────────
            col.Item().Text("Applicant & Application").SemiBold().FontColor(Color.FromHex(BoaBrand.Primary));
            col.Item().PaddingTop(6).Table(table =>
            {
                table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(3); });
                Detail(table, "Reference", data.Reference);
                Detail(table, "Date", $"{data.GeneratedDate:dd MMMM yyyy}");
                Detail(table, "Borrower", $"{data.CompanyName} (RC {data.RcNumber})");
                Detail(table, "Programme", $"{data.ProgrammeName} — {data.SessionName}");
                Detail(table, "Location", LocationText(data));
                if (data.FarmerCount is { } farmers)
                    Detail(table, "Number of farmers", farmers.ToString("N0"));
            });

            col.Item().PaddingTop(14).Text("This statement summarises what you applied for and the key facts of the "
                + "facility offered to you. Please read it with the offer letter, sign both, and return them.")
                .FontSize(9).FontColor(Colors.Grey.Darken2);

            // ── What you applied for (the EOP input package) ───────────────────────────
            col.Item().PaddingTop(14).Text("What You Applied For").SemiBold().FontColor(Color.FromHex(BoaBrand.Primary));
            if (data.AppliedForLines.Count == 0)
            {
                col.Item().PaddingTop(4).Text("No itemised input package was recorded for this application.")
                    .FontSize(9).Italic().FontColor(Colors.Grey.Darken1);
            }
            else
            {
                col.Item().PaddingTop(6).Table(table =>
                {
                    table.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(2); });

                    HeaderCell(table, "Commodity", left: true);
                    HeaderCell(table, "Quantity (kg)");
                    HeaderCell(table, "Unit price");
                    HeaderCell(table, "Line value");

                    foreach (var line in data.AppliedForLines)
                    {
                        BodyCell(table, line.Commodity, left: true);
                        BodyCell(table, line.QuantityKg.ToString("N0"));
                        BodyCell(table, $"{data.Currency} {line.UnitPricePerKg:N2}");
                        BodyCell(table, $"{data.Currency} {line.LineValue:N2}");
                    }

                    // Total applied for — equals the approved amount today (no partial approval), shown anyway.
                    var totalApplied = data.AppliedForLines.Sum(l => l.LineValue);
                    table.Cell().ColumnSpan(3).Background(Color.FromHex(BoaBrand.PanelGreen)).Padding(6)
                        .Text("Total applied for").SemiBold().FontSize(9);
                    table.Cell().Background(Color.FromHex(BoaBrand.PanelGreen)).Padding(6)
                        .Text($"{data.Currency} {totalApplied:N2}").SemiBold().FontSize(9);
                });
            }

            // ── Key facts & terms ──────────────────────────────────────────────────────
            col.Item().PaddingTop(14).Text("Key Facts & Terms").SemiBold().FontColor(Color.FromHex(BoaBrand.Primary));
            col.Item().PaddingTop(6).Table(table =>
            {
                table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(3); });

                Fact(table, "Facility type", "Dry-season agricultural input financing (in-kind seeds/fertiliser)");
                Fact(table, "Approved amount", $"{data.Currency} {data.ApprovedAmount:N2}");
                Fact(table, "Interest rate", data.InterestRatePercent is { } r ? $"{r:N1}% for the cycle" : "Per the facility terms");
                if (data.FinancedInputCost is { } principal)
                    Fact(table, "Principal financed", $"{data.Currency} {principal:N2}");
                if (data.InterestCharge is { } interest)
                    Fact(table, "Interest charge", $"{data.Currency} {interest:N2}");
                Fact(table, "Total repayable at harvest", data.AmountDueAtHarvest is { } due
                    ? $"{data.Currency} {due:N2}" : "Principal plus interest (per the facility terms)");
                Fact(table, "Tenor", data.CycleMonths is { } m ? $"{m} month crop cycle (single cycle)" : "One crop cycle");
                Fact(table, "Repayment type", "Single bullet — one payment due at harvest (end of the cycle)");
                Fact(table, "Disbursement", "In kind, to the approved input supplier(s) — not cash to the borrower");
                Fact(table, "Security", "As recorded on the case (e.g. bank guarantee / NIRSAL CRG / legal mortgage), to be perfected");
                Fact(table, "Fees", "As disclosed in the offer letter / facility terms; no hidden charges");
                Fact(table, "Late repayment", "May attract additional charges and affect your credit record and future eligibility");
            });

            // ── Acknowledgement ────────────────────────────────────────────────────────
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

    private static string LocationText(RhshfKfsData data)
    {
        var parts = new[] { data.Lga, data.State }.Where(p => !string.IsNullOrWhiteSpace(p));
        var text = string.Join(", ", parts);
        return string.IsNullOrWhiteSpace(text) ? "Not recorded" : text;
    }

    private static void Detail(TableDescriptor table, string label, string value)
    {
        table.Cell().Padding(2).Text(label).SemiBold().FontSize(9);
        table.Cell().Padding(2).Text(value).FontSize(9);
    }

    private static void Fact(TableDescriptor table, string label, string value)
    {
        table.Cell().Border(0.5f).BorderColor(Color.FromHex(BoaBrand.MediumGray)).Padding(6)
            .Text(label).SemiBold().FontSize(9);
        table.Cell().Border(0.5f).BorderColor(Color.FromHex(BoaBrand.MediumGray)).Padding(6)
            .Text(value).FontSize(9);
    }

    private static void HeaderCell(TableDescriptor table, string text, bool left = false)
    {
        IContainer cell = table.Cell().Background(Color.FromHex(BoaBrand.MediumGray)).Padding(6);
        if (!left) cell = cell.AlignRight();
        cell.Text(text).SemiBold().FontSize(9);
    }

    private static void BodyCell(TableDescriptor table, string text, bool left = false)
    {
        IContainer cell = table.Cell().Border(0.5f).BorderColor(Color.FromHex(BoaBrand.MediumGray)).Padding(6);
        if (!left) cell = cell.AlignRight();
        cell.Text(text).FontSize(9);
    }
}

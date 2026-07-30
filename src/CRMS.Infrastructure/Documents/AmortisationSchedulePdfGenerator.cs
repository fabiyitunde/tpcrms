using CRMS.Application.OfferLetter.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CRMS.Infrastructure.Documents;

public class AmortisationSchedulePdfGenerator : IAmortisationSchedulePdfGenerator
{
    private const string Primary    = BoaBrand.Primary;    // #14532d dark green
    private const string Accent     = BoaBrand.Accent;     // #1f7a3d mid green
    private const string Subtle     = BoaBrand.Subtle;     // #a7d3b5 soft green
    private const string PanelGreen = BoaBrand.PanelGreen; // #f0f4f1
    private const string LightGray  = BoaBrand.LightGray;  // #f7fafc
    private const string MedGray    = BoaBrand.MediumGray; // #e2e8f0

    public Task<byte[]> GenerateAsync(OfferLetterData data, CancellationToken ct = default)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(35);
                page.MarginVertical(30);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

                page.Header().Element(c => ComposeHeader(c, data));
                page.Content().Element(c => ComposeContent(c, data));
                page.Footer().Element(c => ComposeFooter(c, data));
            });
        });

        return Task.FromResult(document.GeneratePdf());
    }

    // ── Header ───────────────────────────────────────────────────────────────

    private static void ComposeHeader(IContainer container, OfferLetterData data)
    {
        container.Column(col =>
        {
            col.Item().Height(5).Background(Color.FromHex(Primary));
            col.Item().PaddingTop(10).Row(row =>
            {
                row.ConstantItem(65).AlignMiddle().Element(c => BoaBrand.RenderLogo(c, 55));

                row.RelativeItem().AlignCenter().AlignMiddle().Column(title =>
                {
                    title.Item().AlignCenter()
                        .Text(data.BankName.ToUpperInvariant())
                        .Bold().FontSize(13).FontColor(Color.FromHex(Primary));
                    title.Item().AlignCenter()
                        .Text(data.BranchName)
                        .FontSize(8.5f).FontColor(Colors.Grey.Darken2);
                    title.Item().AlignCenter().PaddingTop(1)
                        .Text("Corporate Loan Division")
                        .FontSize(7.5f).Italic().FontColor(Color.FromHex(Accent));
                });

                row.ConstantItem(105).AlignMiddle().Column(meta =>
                {
                    meta.Item().AlignRight()
                        .Text($"Date:  {data.GeneratedDate:dd MMM yyyy}")
                        .FontSize(8).FontColor(Colors.Grey.Darken2);
                    meta.Item().AlignRight().PaddingTop(2)
                        .Text($"Ref:  {data.ApplicationNumber}")
                        .FontSize(8).Bold().FontColor(Color.FromHex(Primary));
                    meta.Item().AlignRight().PaddingTop(1)
                        .Text($"Version: {data.Version}")
                        .FontSize(7).FontColor(Colors.Grey.Medium);
                });
            });

            col.Item().PaddingTop(8).LineHorizontal(2.5f).LineColor(Color.FromHex(Primary));
            col.Item().PaddingTop(5).AlignCenter()
                .Text("LOAN AMORTISATION SCHEDULE")
                .Bold().FontSize(15).FontColor(Color.FromHex(Primary)).LetterSpacing(0.04f);
            col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Color.FromHex(Subtle));
        });
    }

    // ── Content ──────────────────────────────────────────────────────────────

    private static void ComposeContent(IContainer container, OfferLetterData data)
    {
        container.PaddingTop(12).Column(col =>
        {
            // Summary panel
            col.Item().PaddingBottom(12)
                .Background(Color.FromHex(PanelGreen))
                .Border(1).BorderColor(Color.FromHex(MedGray))
                .Padding(10).Row(row =>
                {
                    SummaryKpi(row, "Customer", data.CustomerName);
                    SummaryKpi(row, "Product", data.ProductName);
                    SummaryKpi(row, "Approved Amount", $"{data.Currency} {data.ApprovedAmount:N2}");
                    SummaryKpi(row, "Tenor", $"{data.TenorMonths} months");
                    SummaryKpi(row, "Rate (p.a.)", $"{data.InterestRatePerAnnum:N2}%");
                    SummaryKpi(row, "Monthly Instalment", $"{data.Currency} {data.MonthlyInstallment:N2}");
                });

            // Source note
            col.Item().PaddingBottom(8)
                .Text($"Schedule source: {data.ScheduleSource}  |  Version: {data.Version}")
                .FontSize(7).Italic().FontColor(Colors.Grey.Darken1);

            // Schedule table
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.ConstantColumn(28);   // #
                    cols.RelativeColumn(1.2f); // Due Date
                    cols.RelativeColumn(1);    // Principal
                    cols.RelativeColumn(1);    // Interest
                    cols.RelativeColumn(1);    // Total Payment
                    cols.RelativeColumn(1);    // Outstanding
                });

                // Column headers — dark green
                HeaderCell(table, "#");
                HeaderCell(table, "Due Date");
                HeaderCell(table, $"Principal ({data.Currency})");
                HeaderCell(table, $"Interest ({data.Currency})");
                HeaderCell(table, $"Total Payment ({data.Currency})");
                HeaderCell(table, $"Outstanding ({data.Currency})");

                // Data rows — alternating PanelGreen
                for (var i = 0; i < data.RepaymentSchedule.Count; i++)
                {
                    var item = data.RepaymentSchedule[i];
                    var alt = i % 2 == 1;
                    DataCell(table, item.InstallmentNumber.ToString(), alt);
                    DataCell(table, item.DueDate.ToString("dd MMM yyyy"), alt);
                    DataCell(table, item.Principal.ToString("N2"), alt, true);
                    DataCell(table, item.Interest.ToString("N2"), alt, true);
                    DataCell(table, item.TotalPayment.ToString("N2"), alt, true);
                    DataCell(table, item.OutstandingBalance.ToString("N2"), alt, true);
                }

                // Totals row — accent green
                TotalCell(table, "TOTAL");
                TotalCell(table, "");
                TotalCell(table, data.TotalPrincipal.ToString("N2"), true);
                TotalCell(table, data.TotalInterest.ToString("N2"), true);
                TotalCell(table, data.TotalRepayment.ToString("N2"), true);
                TotalCell(table, "");
            });
        });
    }

    // ── Footer ───────────────────────────────────────────────────────────────

    private static void ComposeFooter(IContainer container, OfferLetterData data)
    {
        container.Column(col =>
        {
            col.Item().LineHorizontal(1).LineColor(Color.FromHex(Subtle));
            col.Item().PaddingTop(3).Row(row =>
            {
                row.RelativeItem()
                    .Text($"Ref: {data.ApplicationNumber}").FontSize(7).FontColor(Colors.Grey.Darken1);
                row.RelativeItem().AlignCenter()
                    .DefaultTextStyle(x => x.FontSize(7).FontColor(Colors.Grey.Darken1))
                    .Text(t => { t.Span("Page "); t.CurrentPageNumber(); t.Span(" of "); t.TotalPages(); });
                row.RelativeItem().AlignRight()
                    .Text(data.BankName).FontSize(7).FontColor(Colors.Grey.Darken1);
            });
            col.Item().PaddingTop(3).AlignCenter()
                .Text("CONFIDENTIAL — This schedule is for the named borrower only and may not be reproduced without authorisation.")
                .FontSize(6).Italic().FontColor(Colors.Grey.Medium);
            col.Item().PaddingTop(6).Height(4).Background(Color.FromHex(Primary));
        });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void SummaryKpi(RowDescriptor row, string label, string value)
    {
        row.RelativeItem().Column(c =>
        {
            c.Item().Text(label).FontSize(7).FontColor(Colors.Grey.Darken2);
            c.Item().PaddingTop(2).Text(value).FontSize(8.5f).Bold().FontColor(Color.FromHex(Primary));
        });
    }

    private static void HeaderCell(TableDescriptor table, string text)
    {
        table.Cell().Background(Color.FromHex(Primary)).Padding(5)
            .Text(text).Bold().FontSize(8).FontColor(Colors.White);
    }

    private static void DataCell(TableDescriptor table, string text, bool alternate, bool alignRight = false)
    {
        var bg = alternate ? Color.FromHex(PanelGreen) : Colors.White;
        var cell = table.Cell().Background(bg)
            .BorderBottom(1).BorderColor(Color.FromHex(MedGray)).Padding(4);
        if (alignRight) cell.AlignRight().Text(text).FontSize(8);
        else cell.Text(text).FontSize(8);
    }

    private static void TotalCell(TableDescriptor table, string text, bool alignRight = false)
    {
        var cell = table.Cell().Background(Color.FromHex(Accent)).Padding(5);
        if (alignRight) cell.AlignRight().Text(text).Bold().FontSize(8).FontColor(Colors.White);
        else cell.Text(text).Bold().FontSize(8).FontColor(Colors.White);
    }
}

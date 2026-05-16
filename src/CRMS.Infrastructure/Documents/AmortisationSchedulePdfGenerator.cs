using CRMS.Application.OfferLetter.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CRMS.Infrastructure.Documents;

public class AmortisationSchedulePdfGenerator : IAmortisationSchedulePdfGenerator
{
    private const string DarkBlue = "#1a365d";
    private const string LightGray = "#f7fafc";
    private const string MediumGray = "#e2e8f0";
    private const string AccentBlue = "#2b6cb0";

    public Task<byte[]> GenerateAsync(OfferLetterData data, CancellationToken ct = default)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(35);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Element(c => ComposeHeader(c, data));
                page.Content().Element(c => ComposeContent(c, data));
                page.Footer().Element(c => ComposeFooter(c, data));
            });
        });

        return Task.FromResult(document.GeneratePdf());
    }

    private void ComposeHeader(IContainer container, OfferLetterData data)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(data.BankName.ToUpperInvariant()).Bold().FontSize(13).FontColor(Color.FromHex(DarkBlue));
                    c.Item().Text(data.BranchName).FontSize(9).FontColor(Colors.Grey.Darken1);
                });
                row.RelativeItem().AlignRight().Column(c =>
                {
                    c.Item().AlignRight().Text($"Date: {data.GeneratedDate:dd-MMM-yyyy}").FontSize(9);
                    c.Item().AlignRight().Text($"Ref: {data.ApplicationNumber}").FontSize(9).Bold();
                });
            });
            col.Item().PaddingTop(6).PaddingBottom(4).AlignCenter()
                .Text("LOAN AMORTISATION SCHEDULE").Bold().FontSize(14).FontColor(Color.FromHex(DarkBlue));
            col.Item().LineHorizontal(2).LineColor(Color.FromHex(DarkBlue));
        });
    }

    private void ComposeContent(IContainer container, OfferLetterData data)
    {
        container.PaddingVertical(8).Column(col =>
        {
            // Summary info bar
            col.Item().PaddingBottom(10).Border(1).BorderColor(Color.FromHex(MediumGray))
                .Background(Color.FromHex(LightGray)).Padding(8).Row(row =>
                {
                    SummaryCell(row, "Customer", data.CustomerName);
                    SummaryCell(row, "Product", data.ProductName);
                    SummaryCell(row, "Amount", $"{data.Currency} {data.ApprovedAmount:N2}");
                    SummaryCell(row, "Tenor", $"{data.TenorMonths} months");
                    SummaryCell(row, "Rate (p.a.)", $"{data.InterestRatePerAnnum:N2}%");
                    SummaryCell(row, "Monthly Installment", $"{data.Currency} {data.MonthlyInstallment:N2}");
                });

            // Source note
            col.Item().PaddingBottom(6).Text($"Schedule computed by: {data.ScheduleSource}  |  Version: {data.Version}")
                .FontSize(7).Italic().FontColor(Colors.Grey.Darken1);

            // Schedule table
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.ConstantColumn(30);   // #
                    cols.RelativeColumn(1.2f); // Due Date
                    cols.RelativeColumn(1);    // Principal
                    cols.RelativeColumn(1);    // Interest
                    cols.RelativeColumn(1);    // Total Payment
                    cols.RelativeColumn(1);    // Outstanding
                });

                // Headers
                HeaderCell(table, "#");
                HeaderCell(table, "Due Date");
                HeaderCell(table, $"Principal ({data.Currency})");
                HeaderCell(table, $"Interest ({data.Currency})");
                HeaderCell(table, $"Total Payment ({data.Currency})");
                HeaderCell(table, $"Outstanding ({data.Currency})");

                // Data rows
                for (var i = 0; i < data.RepaymentSchedule.Count; i++)
                {
                    var item = data.RepaymentSchedule[i];
                    var alt = i % 2 == 1;
                    DataCell(table, item.InstallmentNumber.ToString(), alt);
                    DataCell(table, item.DueDate.ToString("dd-MMM-yyyy"), alt);
                    DataCell(table, item.Principal.ToString("N2"), alt, true);
                    DataCell(table, item.Interest.ToString("N2"), alt, true);
                    DataCell(table, item.TotalPayment.ToString("N2"), alt, true);
                    DataCell(table, item.OutstandingBalance.ToString("N2"), alt, true);
                }

                // Totals
                TotalCell(table, "TOTAL");
                TotalCell(table, "");
                TotalCell(table, data.TotalPrincipal.ToString("N2"), true);
                TotalCell(table, data.TotalInterest.ToString("N2"), true);
                TotalCell(table, data.TotalRepayment.ToString("N2"), true);
                TotalCell(table, "");
            });
        });
    }

    private void ComposeFooter(IContainer container, OfferLetterData data)
    {
        container.Column(col =>
        {
            col.Item().LineHorizontal(1).LineColor(Color.FromHex(MediumGray));
            col.Item().PaddingTop(3).Row(row =>
            {
                row.RelativeItem().Text($"Ref: {data.ApplicationNumber}").FontSize(7).FontColor(Colors.Grey.Darken1);
                row.RelativeItem().AlignCenter().DefaultTextStyle(x => x.FontSize(7).FontColor(Colors.Grey.Darken1)).Text(x =>
                {
                    x.Span("Page "); x.CurrentPageNumber(); x.Span(" of "); x.TotalPages();
                });
                row.RelativeItem().AlignRight().Text(data.BankName).FontSize(7).FontColor(Colors.Grey.Darken1);
            });
        });
    }

    private static void SummaryCell(RowDescriptor row, string label, string value)
    {
        row.RelativeItem().Column(c =>
        {
            c.Item().Text(label).FontSize(7).FontColor(Colors.Grey.Darken2);
            c.Item().Text(value).FontSize(8).Bold();
        });
    }

    private static void HeaderCell(TableDescriptor table, string text)
    {
        table.Cell().Background(Color.FromHex(DarkBlue)).Padding(4)
            .Text(text).Bold().FontSize(8).FontColor(Colors.White);
    }

    private static void DataCell(TableDescriptor table, string text, bool alternate, bool alignRight = false)
    {
        var cell = table.Cell()
            .Background(alternate ? Color.FromHex(LightGray) : Colors.White)
            .BorderBottom(1).BorderColor(Color.FromHex(MediumGray)).Padding(3);
        if (alignRight) cell.AlignRight().Text(text).FontSize(8);
        else cell.Text(text).FontSize(8);
    }

    private static void TotalCell(TableDescriptor table, string text, bool alignRight = false)
    {
        var cell = table.Cell().Background(Color.FromHex(MediumGray)).Padding(4);
        if (alignRight) cell.AlignRight().Text(text).Bold().FontSize(8);
        else cell.Text(text).Bold().FontSize(8);
    }
}

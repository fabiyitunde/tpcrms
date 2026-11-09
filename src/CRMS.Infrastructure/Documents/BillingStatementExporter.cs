using ClosedXML.Excel;
using CRMS.Application.Billing;
using CRMS.Application.Billing.Statements;
using CRMS.Domain.Aggregates.Billing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CRMS.Infrastructure.Documents;

/// <summary>
/// The bank's copy of a billing statement, as PDF (to send) and Excel (to reconcile). Issued by
/// the vendor to the bank, so it carries the vendor's details as issuer rather than the bank's
/// branding, and never the vendor's own cost.
/// </summary>
public sealed class BillingStatementExporter : IBillingStatementExporter
{
    private const string Primary = BoaBrand.Primary;
    private const string Accent = BoaBrand.Accent;

    public byte[] ToPdf(BillingStatementDto s, BillingSettings settings)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        string Money(decimal v) => $"{s.Currency} {v:N2}";

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(42);
                page.MarginVertical(34);
                page.DefaultTextStyle(x => x.FontSize(9.5f).FontFamily("Arial"));

                page.Header().Column(col =>
                {
                    col.Item().Height(5).Background(Color.FromHex(Primary));
                    col.Item().PaddingTop(10).Row(row =>
                    {
                        row.RelativeItem().Column(from =>
                        {
                            from.Item().Text(settings.VendorName).Bold().FontSize(15).FontColor(Color.FromHex(Primary));
                            foreach (var line in new[] { settings.VendorAddress, settings.VendorEmail, settings.VendorPhone, settings.VendorTaxId is null ? null : $"Tax ID: {settings.VendorTaxId}" })
                                if (!string.IsNullOrWhiteSpace(line))
                                    from.Item().Text(line).FontSize(8.5f).FontColor(Colors.Grey.Darken2);
                        });
                        row.ConstantItem(200).Column(meta =>
                        {
                            meta.Item().AlignRight().Text("STATEMENT").Bold().FontSize(18).FontColor(Color.FromHex(Primary)).LetterSpacing(0.05f);
                            meta.Item().AlignRight().PaddingTop(4).Text(s.Number).Bold().FontSize(10);
                            meta.Item().AlignRight().Text($"Period: {s.Month:MMMM yyyy}").FontSize(8.5f).FontColor(Colors.Grey.Darken2);
                            meta.Item().AlignRight().Text($"Issued: {s.IssuedAt:dd MMM yyyy}").FontSize(8.5f).FontColor(Colors.Grey.Darken2);
                            meta.Item().AlignRight().Text($"Due: {s.DueDate:dd MMM yyyy}").FontSize(8.5f).FontColor(Colors.Grey.Darken2);
                            if (s.Status != BillingStatementStatus.Issued)
                                meta.Item().AlignRight().PaddingTop(3).Text(s.Status == BillingStatementStatus.Paid ? $"PAID {s.PaidAt:dd MMM yyyy}" : "VOIDED")
                                    .Bold().FontSize(10).FontColor(s.Status == BillingStatementStatus.Paid ? Color.FromHex(Accent) : Colors.Red.Darken2);
                        });
                    });
                    col.Item().PaddingTop(8).LineHorizontal(2f).LineColor(Color.FromHex(Primary));
                });

                page.Content().PaddingTop(12).Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(to =>
                        {
                            to.Item().Text("BILL TO").FontSize(7.5f).Bold().FontColor(Colors.Grey.Darken1).LetterSpacing(0.08f);
                            to.Item().PaddingTop(2).Text(settings.BankName).Bold().FontSize(11);
                            if (!string.IsNullOrWhiteSpace(settings.BankAddress))
                                to.Item().Text(settings.BankAddress).FontSize(8.5f).FontColor(Colors.Grey.Darken2);
                        });
                        row.RelativeItem().AlignRight().Column(sum =>
                        {
                            sum.Item().AlignRight().Text("AMOUNT DUE").FontSize(7.5f).Bold().FontColor(Colors.Grey.Darken1).LetterSpacing(0.08f);
                            sum.Item().AlignRight().PaddingTop(2).Text(Money(s.Total)).Bold().FontSize(16).FontColor(Color.FromHex(Primary));
                            sum.Item().AlignRight().Text($"{s.UsageCount:N0} billable events, VAT {s.VatRatePercent:0.##}% included").FontSize(8).FontColor(Colors.Grey.Darken2);
                        });
                    });

                    col.Item().PaddingTop(16).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(58);
                            c.ConstantColumn(62);
                            c.RelativeColumn(3);
                            c.ConstantColumn(44);
                            c.ConstantColumn(78);
                            c.ConstantColumn(88);
                        });
                        table.Header(h =>
                        {
                            foreach (var (text, right) in new[] { ("Month", false), ("Flow", false), ("Item", false), ("Qty", true), ("Unit price", true), ("Amount", true) })
                            {
                                var cell = h.Cell().Background(Color.FromHex(Primary)).Padding(5);
                                (right ? cell.AlignRight() : cell).Text(text).Bold().FontSize(8.5f).FontColor(Colors.White);
                            }
                        });
                        var i = 0;
                        BillingCategory? lastCategory = null;
                        foreach (var line in s.Lines)
                        {
                            if (line.Category != lastCategory)
                            {
                                table.Cell().ColumnSpan(6).Background(Color.FromHex(BoaBrand.PanelGreen)).Padding(4)
                                    .Text(line.CategoryName.ToUpperInvariant()).Bold().FontSize(7.5f).FontColor(Color.FromHex(Primary)).LetterSpacing(0.06f);
                                lastCategory = line.Category;
                            }
                            var bg = i++ % 2 == 0 ? Colors.White : Color.FromHex(BoaBrand.LightGray);
                            IContainer Cell(bool right = false) { var c = table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(Color.FromHex(BoaBrand.MediumGray)).Padding(5); return right ? c.AlignRight() : c; }
                            Cell().Text(line.Month.ToString("MMM yyyy")).FontSize(8.5f);
                            Cell().Text(line.FlowName).FontSize(8.5f);
                            Cell().Text(line.ItemName).FontSize(8.5f);
                            Cell(true).Text(line.Quantity.ToString("N0")).FontSize(8.5f);
                            Cell(true).Text(line.UnitPrice.ToString("N2")).FontSize(8.5f);
                            Cell(true).Text(line.Amount.ToString("N2")).FontSize(8.5f).Bold();
                        }
                    });

                    col.Item().PaddingTop(10).AlignRight().Column(t =>
                    {
                        void Row(string label, string value, bool strong = false)
                        {
                            t.Item().Row(r =>
                            {
                                r.ConstantItem(150).AlignRight().Text(label).FontSize(strong ? 10 : 9).FontColor(strong ? Color.FromHex(Primary) : Colors.Grey.Darken2);
                                r.ConstantItem(120).AlignRight().Text(value).FontSize(strong ? 11 : 9).Bold().FontColor(strong ? Color.FromHex(Primary) : Colors.Black);
                            });
                        }
                        Row("Subtotal", Money(s.Subtotal));
                        Row($"VAT {s.VatRatePercent:0.##}%", Money(s.VatAmount));
                        t.Item().PaddingVertical(3).AlignRight().Width(270).LineHorizontal(1).LineColor(Color.FromHex(Primary));
                        Row("Total due", Money(s.Total), strong: true);
                    });

                    if (!string.IsNullOrWhiteSpace(s.Notes))
                    {
                        col.Item().PaddingTop(14).Text("NOTES").FontSize(7.5f).Bold().FontColor(Colors.Grey.Darken1).LetterSpacing(0.08f);
                        col.Item().PaddingTop(2).Text(s.Notes).FontSize(8.5f);
                    }
                    if (!string.IsNullOrWhiteSpace(settings.PaymentInstructions))
                    {
                        col.Item().PaddingTop(14).Text("PAYMENT").FontSize(7.5f).Bold().FontColor(Colors.Grey.Darken1).LetterSpacing(0.08f);
                        col.Item().PaddingTop(2).Text(settings.PaymentInstructions).FontSize(8.5f);
                    }
                    col.Item().PaddingTop(14).Text(
                        "Every third-party call is billed whether or not the provider returned a result. Lines dated before the statement period are calls from earlier months that had not yet been billed. Platform fees are charged once per application per milestone.")
                        .FontSize(7.5f).Italic().FontColor(Colors.Grey.Darken1);
                });

                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text($"{s.Number} · {settings.VendorName} to {settings.BankName}").FontSize(7.5f).FontColor(Colors.Grey.Darken1);
                    row.ConstantItem(80).AlignRight().Text(t => { t.DefaultTextStyle(x => x.FontSize(7.5f).FontColor(Colors.Grey.Darken1)); t.Span("Page "); t.CurrentPageNumber(); t.Span(" of "); t.TotalPages(); });
                });
            });
        });

        return document.GeneratePdf();
    }

    public byte[] ToExcel(BillingStatementDto s, BillingSettings settings)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Statement");

        ws.Cell(1, 1).Value = settings.VendorName;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(2, 1).Value = $"Statement {s.Number}";
        ws.Cell(3, 1).Value = "Bill to";
        ws.Cell(3, 2).Value = settings.BankName;
        ws.Cell(4, 1).Value = "Period";
        ws.Cell(4, 2).Value = s.Month.ToString("MMMM yyyy");
        ws.Cell(5, 1).Value = "Issued";
        ws.Cell(5, 2).Value = s.IssuedAt.ToString("dd MMM yyyy");
        ws.Cell(6, 1).Value = "Due";
        ws.Cell(6, 2).Value = s.DueDate.ToString("dd MMM yyyy");
        ws.Cell(7, 1).Value = "Status";
        ws.Cell(7, 2).Value = s.Status.ToString();
        ws.Cell(8, 1).Value = "Currency";
        ws.Cell(8, 2).Value = s.Currency;
        ws.Range(3, 1, 8, 1).Style.Font.Bold = true;

        var header = 10;
        var headers = new[] { "Month", "Flow", "Category", "Item", "Quantity", "Unit price", "Amount" };
        for (var c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(header, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml(Primary);
        }
        var r = header + 1;
        foreach (var line in s.Lines)
        {
            ws.Cell(r, 1).Value = line.Month.ToString("MMM yyyy");
            ws.Cell(r, 2).Value = line.FlowName;
            ws.Cell(r, 3).Value = line.CategoryName;
            ws.Cell(r, 4).Value = line.ItemName;
            ws.Cell(r, 5).Value = line.Quantity;
            ws.Cell(r, 6).Value = line.UnitPrice;
            ws.Cell(r, 7).Value = line.Amount;
            r++;
        }
        var first = header + 1;
        var last = Math.Max(first, r - 1);
        ws.Range(first, 6, last, 7).Style.NumberFormat.Format = "#,##0.00";
        ws.Range(first, 5, last, 5).Style.NumberFormat.Format = "#,##0";

        r++;
        ws.Cell(r, 6).Value = "Subtotal";
        ws.Cell(r, 7).FormulaA1 = s.Lines.Count > 0 ? $"SUM(G{first}:G{last})" : "0";
        ws.Cell(r + 1, 6).Value = $"VAT {s.VatRatePercent:0.##}%";
        ws.Cell(r + 1, 7).Value = s.VatAmount;
        ws.Cell(r + 2, 6).Value = "Total due";
        ws.Cell(r + 2, 7).FormulaA1 = $"G{r}+G{r + 1}";
        ws.Range(r, 6, r + 2, 7).Style.Font.Bold = true;
        ws.Range(r, 7, r + 2, 7).Style.NumberFormat.Format = "#,##0.00";

        if (!string.IsNullOrWhiteSpace(settings.PaymentInstructions))
        {
            ws.Cell(r + 4, 1).Value = "Payment";
            ws.Cell(r + 4, 1).Style.Font.Bold = true;
            ws.Cell(r + 4, 2).Value = settings.PaymentInstructions;
        }
        ws.Columns(1, 7).AdjustToContents();
        ws.Column(4).Width = Math.Max(ws.Column(4).Width, 34);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}

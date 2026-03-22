using CRMS.Application.OfferLetter.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CRMS.Infrastructure.Documents;

/// <summary>
/// Generates offer letter PDFs using QuestPDF.
/// </summary>
public class OfferLetterPdfGenerator : IOfferLetterPdfGenerator
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
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Element(c => ComposeHeader(c, data));
                page.Content().Element(c => ComposeContent(c, data));
                page.Footer().Element(c => ComposeFooter(c, data));
            });
        });

        var bytes = document.GeneratePdf();
        return Task.FromResult(bytes);
    }

    private void ComposeHeader(IContainer container, OfferLetterData data)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(logoCol =>
                {
                    logoCol.Item().Width(60).Height(60)
                        .Background(Color.FromHex(DarkBlue))
                        .AlignCenter().AlignMiddle()
                        .Text("BANK").FontSize(12).Bold().FontColor(Colors.White);
                });

                row.RelativeItem(3).AlignCenter().Column(titleCol =>
                {
                    titleCol.Item().AlignCenter().Text(data.BankName.ToUpperInvariant())
                        .Bold().FontSize(14).FontColor(Color.FromHex(DarkBlue));
                    titleCol.Item().AlignCenter().Text(data.BranchName)
                        .FontSize(10).FontColor(Colors.Grey.Darken1);
                });

                row.RelativeItem().AlignRight().Column(refCol =>
                {
                    refCol.Item().AlignRight().Text($"Date: {data.GeneratedDate:dd-MMM-yyyy}")
                        .FontSize(9);
                    refCol.Item().AlignRight().Text($"Ref: {data.ApplicationNumber}")
                        .FontSize(9).Bold();
                    refCol.Item().AlignRight().Text($"Version: {data.Version}")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });

            col.Item().PaddingTop(8).LineHorizontal(2).LineColor(Color.FromHex(DarkBlue));

            col.Item().PaddingTop(6).AlignCenter()
                .Text("OFFER LETTER").Bold().FontSize(16).FontColor(Color.FromHex(DarkBlue));

            col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Color.FromHex(MediumGray));
        });
    }

    private void ComposeContent(IContainer container, OfferLetterData data)
    {
        container.PaddingVertical(10).Column(col =>
        {
            // Addressee
            col.Item().Element(c => ComposeAddressee(c, data));

            // Opening Paragraph
            col.Item().Element(c => ComposeOpeningParagraph(c, data));

            // Facility Details
            col.Item().Element(c => ComposeFacilityDetails(c, data));

            // Repayment Schedule
            col.Item().Element(c => ComposeRepaymentSchedule(c, data));

            // Schedule Summary
            col.Item().Element(c => ComposeScheduleSummary(c, data));

            // Conditions
            if (data.Conditions.Any())
            {
                col.Item().Element(c => ComposeConditions(c, data));
            }

            // Acceptance Section
            col.Item().Element(c => ComposeAcceptance(c, data));
        });
    }

    private void ComposeAddressee(IContainer container, OfferLetterData data)
    {
        container.PaddingBottom(10).Column(col =>
        {
            col.Item().Text(data.CustomerName).Bold().FontSize(11);

            foreach (var line in data.CustomerAddress.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                col.Item().Text(line.Trim()).FontSize(10);
            }
        });
    }

    private void ComposeOpeningParagraph(IContainer container, OfferLetterData data)
    {
        container.PaddingBottom(15).Column(col =>
        {
            col.Item().Text($"Dear {data.CustomerName},").FontSize(10);
            col.Item().PaddingTop(8).Text(text =>
            {
                text.Span("We are pleased to inform you that your application for credit facility has been approved. " +
                    $"The details of the approved facility and the proposed repayment schedule are set out below for your review and acceptance.")
                    .FontSize(10);
            });
        });
    }

    private void ComposeFacilityDetails(IContainer container, OfferLetterData data)
    {
        container.PaddingBottom(15).Column(col =>
        {
            col.Item().Text("FACILITY DETAILS").Bold().FontSize(12).FontColor(Color.FromHex(DarkBlue));
            col.Item().PaddingTop(5);

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(1);
                    cols.RelativeColumn(2);
                });

                FacilityRow(table, "Facility Type", data.ProductName);
                FacilityRow(table, "Approved Amount", $"{data.Currency} {data.ApprovedAmount:N2}");
                FacilityRow(table, "Tenor", $"{data.TenorMonths} months");
                FacilityRow(table, "Interest Rate", $"{data.InterestRatePerAnnum:N2}% per annum");
                FacilityRow(table, "Repayment Frequency", data.RepaymentFrequency);
                FacilityRow(table, "Amortization Method", data.AmortizationMethod);
            });
        });
    }

    private void ComposeRepaymentSchedule(IContainer container, OfferLetterData data)
    {
        container.PaddingBottom(15).Column(col =>
        {
            col.Item().Text("PROPOSED REPAYMENT SCHEDULE").Bold().FontSize(12).FontColor(Color.FromHex(DarkBlue));
            col.Item().PaddingTop(2).Text($"Source: {data.ScheduleSource}").FontSize(8).Italic().FontColor(Colors.Grey.Darken1);
            col.Item().PaddingTop(5);

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.ConstantColumn(55);   // Installment #
                    cols.RelativeColumn(1);     // Due Date
                    cols.RelativeColumn(1);     // Principal
                    cols.RelativeColumn(1);     // Interest
                    cols.RelativeColumn(1);     // Total Payment
                    cols.RelativeColumn(1);     // Outstanding Balance
                });

                // Header row
                ScheduleHeader(table, "#");
                ScheduleHeader(table, "Due Date");
                ScheduleHeader(table, $"Principal ({data.Currency})");
                ScheduleHeader(table, $"Interest ({data.Currency})");
                ScheduleHeader(table, $"Total Payment ({data.Currency})");
                ScheduleHeader(table, $"Outstanding ({data.Currency})");

                // Data rows
                for (var i = 0; i < data.RepaymentSchedule.Count; i++)
                {
                    var item = data.RepaymentSchedule[i];
                    var isAlternate = i % 2 == 1;

                    ScheduleCell(table, item.InstallmentNumber.ToString(), isAlternate);
                    ScheduleCell(table, item.DueDate.ToString("dd-MMM-yyyy"), isAlternate);
                    ScheduleCell(table, item.Principal.ToString("N2"), isAlternate, alignRight: true);
                    ScheduleCell(table, item.Interest.ToString("N2"), isAlternate, alignRight: true);
                    ScheduleCell(table, item.TotalPayment.ToString("N2"), isAlternate, alignRight: true);
                    ScheduleCell(table, item.OutstandingBalance.ToString("N2"), isAlternate, alignRight: true);
                }

                // Totals row
                ScheduleTotalCell(table, "TOTAL");
                ScheduleTotalCell(table, "");
                ScheduleTotalCell(table, data.TotalPrincipal.ToString("N2"), alignRight: true);
                ScheduleTotalCell(table, data.TotalInterest.ToString("N2"), alignRight: true);
                ScheduleTotalCell(table, data.TotalRepayment.ToString("N2"), alignRight: true);
                ScheduleTotalCell(table, "");
            });
        });
    }

    private void ComposeScheduleSummary(IContainer container, OfferLetterData data)
    {
        container.PaddingBottom(15).Column(col =>
        {
            col.Item().Text("SCHEDULE SUMMARY").Bold().FontSize(12).FontColor(Color.FromHex(DarkBlue));
            col.Item().PaddingTop(5);

            col.Item().Border(1).BorderColor(Color.FromHex(MediumGray)).Column(box =>
            {
                box.Item().Row(row =>
                {
                    row.RelativeItem().Padding(10).Column(c =>
                    {
                        c.Item().Text("Total Principal").FontSize(9).FontColor(Colors.Grey.Darken1);
                        c.Item().Text($"{data.Currency} {data.TotalPrincipal:N2}").Bold().FontSize(11);
                    });

                    row.RelativeItem().Padding(10).Column(c =>
                    {
                        c.Item().Text("Total Interest").FontSize(9).FontColor(Colors.Grey.Darken1);
                        c.Item().Text($"{data.Currency} {data.TotalInterest:N2}").Bold().FontSize(11);
                    });

                    row.RelativeItem().Padding(10).Column(c =>
                    {
                        c.Item().Text("Total Repayment").FontSize(9).FontColor(Colors.Grey.Darken1);
                        c.Item().Text($"{data.Currency} {data.TotalRepayment:N2}").Bold().FontSize(11);
                    });

                    row.RelativeItem().Background(Color.FromHex(DarkBlue)).Padding(10).Column(c =>
                    {
                        c.Item().Text("Monthly Installment").FontSize(9).FontColor(Colors.White);
                        c.Item().Text($"{data.Currency} {data.MonthlyInstallment:N2}")
                            .Bold().FontSize(13).FontColor(Colors.White);
                    });
                });
            });
        });
    }

    private void ComposeConditions(IContainer container, OfferLetterData data)
    {
        container.PaddingBottom(15).Column(col =>
        {
            col.Item().Text("CONDITIONS").Bold().FontSize(12).FontColor(Color.FromHex(DarkBlue));
            col.Item().PaddingTop(5);

            col.Item().Text("This offer is subject to the following conditions:")
                .FontSize(10).Italic();
            col.Item().PaddingTop(5);

            for (var i = 0; i < data.Conditions.Count; i++)
            {
                col.Item().PaddingBottom(3).Text($"{i + 1}. {data.Conditions[i]}").FontSize(10);
            }
        });
    }

    private void ComposeAcceptance(IContainer container, OfferLetterData data)
    {
        container.PaddingTop(15).Column(col =>
        {
            col.Item().Text("ACCEPTANCE").Bold().FontSize(12).FontColor(Color.FromHex(DarkBlue));
            col.Item().PaddingTop(5);

            col.Item().Text(
                "I/We hereby accept the terms and conditions of this offer as stated above. " +
                "I/We confirm that the information provided in support of this application is true and accurate.")
                .FontSize(10);

            col.Item().PaddingTop(20).Row(row =>
            {
                // Customer Signature
                row.RelativeItem().Column(c =>
                {
                    c.Item().PaddingBottom(40).Text(""); // Space for signature
                    c.Item().LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                    c.Item().PaddingTop(3).Text("Customer Signature").FontSize(9).Bold();
                    c.Item().PaddingTop(15);
                    c.Item().LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                    c.Item().PaddingTop(3).Text("Name").FontSize(9);
                    c.Item().PaddingTop(15);
                    c.Item().LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                    c.Item().PaddingTop(3).Text("Date").FontSize(9);
                });

                row.ConstantItem(40); // Spacer

                // Witness
                row.RelativeItem().Column(c =>
                {
                    c.Item().PaddingBottom(40).Text(""); // Space for signature
                    c.Item().LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                    c.Item().PaddingTop(3).Text("Witness Signature").FontSize(9).Bold();
                    c.Item().PaddingTop(15);
                    c.Item().LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                    c.Item().PaddingTop(3).Text("Name").FontSize(9);
                    c.Item().PaddingTop(15);
                    c.Item().LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                    c.Item().PaddingTop(3).Text("Date").FontSize(9);
                });
            });
        });
    }

    private void ComposeFooter(IContainer container, OfferLetterData data)
    {
        container.Column(col =>
        {
            col.Item().LineHorizontal(1).LineColor(Color.FromHex(MediumGray));
            col.Item().PaddingTop(5).AlignCenter()
                .Text("This offer is valid for 30 days from the date of issue.")
                .FontSize(8).Bold().FontColor(Color.FromHex(AccentBlue));

            col.Item().PaddingTop(3).Row(row =>
            {
                row.RelativeItem().Text($"Ref: {data.ApplicationNumber}").FontSize(7)
                    .FontColor(Colors.Grey.Darken1);
                row.RelativeItem().AlignCenter().DefaultTextStyle(x => x.FontSize(7).FontColor(Colors.Grey.Darken1)).Text(x =>
                {
                    x.Span("Page ");
                    x.CurrentPageNumber();
                    x.Span(" of ");
                    x.TotalPages();
                });
                row.RelativeItem().AlignRight().Text($"{data.BankName}")
                    .FontSize(7).FontColor(Colors.Grey.Darken1);
            });

            col.Item().PaddingTop(2).AlignCenter()
                .Text("CONFIDENTIAL - This document is intended solely for the named recipient and contains privileged information.")
                .FontSize(6).Italic().FontColor(Colors.Grey.Medium);
        });
    }

    // Helper methods

    private static void FacilityRow(TableDescriptor table, string label, string value)
    {
        table.Cell().BorderBottom(1).BorderColor(Color.FromHex(MediumGray))
            .Background(Color.FromHex(LightGray)).Padding(6)
            .Text(label).Bold().FontSize(10);

        table.Cell().BorderBottom(1).BorderColor(Color.FromHex(MediumGray))
            .Padding(6).Text(value).FontSize(10);
    }

    private static void ScheduleHeader(TableDescriptor table, string text)
    {
        table.Cell().Background(Color.FromHex(DarkBlue)).Padding(5)
            .Text(text).Bold().FontSize(8).FontColor(Colors.White);
    }

    private static void ScheduleCell(TableDescriptor table, string text, bool alternate, bool alignRight = false)
    {
        var cell = table.Cell()
            .Background(alternate ? Color.FromHex(LightGray) : Colors.White)
            .BorderBottom(1).BorderColor(Color.FromHex(MediumGray))
            .Padding(4);

        if (alignRight)
            cell.AlignRight().Text(text).FontSize(8);
        else
            cell.Text(text).FontSize(8);
    }

    private static void ScheduleTotalCell(TableDescriptor table, string text, bool alignRight = false)
    {
        var cell = table.Cell()
            .Background(Color.FromHex(MediumGray))
            .Padding(5);

        if (alignRight)
            cell.AlignRight().Text(text).Bold().FontSize(9);
        else
            cell.Text(text).Bold().FontSize(9);
    }
}

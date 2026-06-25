using CRMS.Application.Namp.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CRMS.Infrastructure.Documents;

public class NampOfferLetterPdfGenerator : INampOfferLetterPdfGenerator
{
    // Bank of Agriculture brand palette — see BoaBrand.
    private const string DarkBlue = BoaBrand.Primary;
    private const string LightGray = BoaBrand.LightGray;
    private const string MediumGray = BoaBrand.MediumGray;
    private const string AccentBlue = BoaBrand.Accent;
    private const string GreenDark = BoaBrand.Accent;

    public Task<byte[]> GenerateAsync(NampOfferLetterData data, CancellationToken ct = default)
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

    private void ComposeHeader(IContainer container, NampOfferLetterData data)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(logoCol =>
                {
                    logoCol.Item().Element(c => BoaBrand.RenderLogo(c, 60));
                });

                row.RelativeItem(3).AlignCenter().Column(titleCol =>
                {
                    titleCol.Item().AlignCenter().Text(data.BankName.ToUpperInvariant())
                        .Bold().FontSize(14).FontColor(Color.FromHex(DarkBlue));
                    titleCol.Item().AlignCenter().Text(data.BranchName)
                        .FontSize(10).FontColor(Colors.Grey.Darken1);
                    titleCol.Item().AlignCenter().PaddingTop(2)
                        .Text("National Agricultural Mechanisation Programme (NAMP)")
                        .FontSize(9).Italic().FontColor(Color.FromHex(GreenDark));
                });

                row.RelativeItem().AlignRight().Column(refCol =>
                {
                    refCol.Item().AlignRight().Text($"Date: {data.GeneratedDate:dd-MMM-yyyy}").FontSize(9);
                    refCol.Item().AlignRight().Text($"Ref: {data.ApplicationNumber}").FontSize(9).Bold();
                    refCol.Item().AlignRight().Text($"Staging Ref: {data.ApplicationReference}")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });

            col.Item().PaddingTop(8).LineHorizontal(2).LineColor(Color.FromHex(DarkBlue));
            col.Item().PaddingTop(6).AlignCenter()
                .Text("OFFER LETTER").Bold().FontSize(16).FontColor(Color.FromHex(DarkBlue));
            col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Color.FromHex(MediumGray));
        });
    }

    private void ComposeContent(IContainer container, NampOfferLetterData data)
    {
        container.PaddingVertical(10).Column(col =>
        {
            // Addressee
            col.Item().PaddingBottom(10).Column(c =>
            {
                c.Item().Text(data.ApplicantName).Bold().FontSize(11);
                if (!string.IsNullOrWhiteSpace(data.BoaAccountNumber))
                    c.Item().Text($"BOA Account No: {data.BoaAccountNumber}").FontSize(10);
            });

            // Opening paragraph — use template content when provided, fall back to default
            col.Item().PaddingBottom(12).Column(c =>
            {
                if (!string.IsNullOrWhiteSpace(data.RenderedIntro))
                {
                    foreach (var para in data.RenderedIntro.Split(
                        new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (!string.IsNullOrWhiteSpace(para))
                            c.Item().PaddingBottom(6).Text(para.Trim()).FontSize(10);
                    }
                }
                else
                {
                    c.Item().Text($"Dear {data.ApplicantName},").FontSize(10);
                    c.Item().PaddingTop(8).Text(
                        "We are pleased to inform you that your application under the National Agricultural " +
                        "Mechanisation Programme (NAMP) has been approved by the Credit Committee. " +
                        "The terms of the approved facility, including the proposed repayment schedule, " +
                        "are set out below for your review and acceptance.").FontSize(10);
                }
            });

            // Facility details
            col.Item().PaddingBottom(12).Column(c =>
            {
                c.Item().Text("FACILITY DETAILS").Bold().FontSize(11).FontColor(Color.FromHex(DarkBlue));
                c.Item().PaddingTop(5);

                c.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(1);
                        cols.RelativeColumn(2);
                    });

                    FacilityRow(table, "Applicant Name", data.ApplicantName);
                    FacilityRow(table, "Applicant Category", data.ApplicantCategory);
                    if (!string.IsNullOrWhiteSpace(data.BoaAccountNumber))
                        FacilityRow(table, "BOA Account Number", data.BoaAccountNumber);
                    FacilityRow(table, "Equipment Description", data.EquipmentDescription);
                    FacilityRow(table, "Equipment Value", $"NGN {data.EquipmentValue:N2}");
                    if (data.EquityPercent.HasValue)
                        FacilityRow(table, "Equity Contribution", $"{data.EquityPercent:N2}%");
                    if (data.EquityAmount.HasValue)
                        FacilityRow(table, "Equity Amount (Borrower)", $"NGN {data.EquityAmount:N2}");
                    if (data.LoanAmount.HasValue)
                        FacilityRow(table, "Approved Loan Amount", $"NGN {data.LoanAmount:N2}");
                    if (data.TenorMonths.HasValue)
                        FacilityRow(table, "Loan Tenor", $"{data.TenorMonths} months");
                    FacilityRow(table, "Interest Rate", $"{data.InterestRatePerAnnum:N2}% per annum");
                    FacilityRow(table, "Repayment Frequency", "Monthly");
                    FacilityRow(table, "Amortization Method", "Equal Monthly Installments (EMI)");
                    if (!string.IsNullOrWhiteSpace(data.LoanPurpose))
                        FacilityRow(table, "Purpose", data.LoanPurpose);
                });
            });

            // Repayment summary box
            if (data.LoanAmount.HasValue && data.TotalRepayment > 0)
            {
                col.Item().PaddingBottom(12).Column(c =>
                {
                    c.Item().Text("REPAYMENT SUMMARY").Bold().FontSize(11).FontColor(Color.FromHex(DarkBlue));
                    c.Item().PaddingTop(5);

                    c.Item().Border(1).BorderColor(Color.FromHex(MediumGray)).Column(box =>
                    {
                        box.Item().Row(row =>
                        {
                            row.RelativeItem().Padding(10).Column(cell =>
                            {
                                cell.Item().Text("Total Principal").FontSize(9).FontColor(Colors.Grey.Darken1);
                                cell.Item().Text($"NGN {data.TotalPrincipal:N2}").Bold().FontSize(11);
                            });

                            row.RelativeItem().Padding(10).Column(cell =>
                            {
                                cell.Item().Text("Total Interest").FontSize(9).FontColor(Colors.Grey.Darken1);
                                cell.Item().Text($"NGN {data.TotalInterest:N2}").Bold().FontSize(11);
                            });

                            row.RelativeItem().Padding(10).Column(cell =>
                            {
                                cell.Item().Text("Total Repayment").FontSize(9).FontColor(Colors.Grey.Darken1);
                                cell.Item().Text($"NGN {data.TotalRepayment:N2}").Bold().FontSize(11);
                            });

                            row.RelativeItem().Background(Color.FromHex(DarkBlue)).Padding(10).Column(cell =>
                            {
                                cell.Item().Text("Monthly Installment").FontSize(9).FontColor(Colors.White);
                                cell.Item().Text($"NGN {data.MonthlyInstallment:N2}")
                                    .Bold().FontSize(12).FontColor(Colors.White);
                            });
                        });
                    });

                    c.Item().PaddingTop(3).AlignRight()
                        .Text($"Schedule source: {data.ScheduleSource}")
                        .FontSize(7).Italic().FontColor(Colors.Grey.Darken1);
                });
            }

            // Repayment schedule table
            if (data.RepaymentSchedule.Any())
            {
                col.Item().PaddingBottom(12).Column(c =>
                {
                    c.Item().Text("PROPOSED REPAYMENT SCHEDULE").Bold().FontSize(11).FontColor(Color.FromHex(DarkBlue));
                    c.Item().PaddingTop(5);

                    c.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(40);  // #
                            cols.RelativeColumn(1);   // Due Date
                            cols.RelativeColumn(1);   // Principal
                            cols.RelativeColumn(1);   // Interest
                            cols.RelativeColumn(1);   // Total
                            cols.RelativeColumn(1);   // Balance
                        });

                        ScheduleHeader(table, "#");
                        ScheduleHeader(table, "Due Date");
                        ScheduleHeader(table, "Principal (NGN)");
                        ScheduleHeader(table, "Interest (NGN)");
                        ScheduleHeader(table, "Total Payment (NGN)");
                        ScheduleHeader(table, "Outstanding (NGN)");

                        for (var i = 0; i < data.RepaymentSchedule.Count; i++)
                        {
                            var item = data.RepaymentSchedule[i];
                            var alt = i % 2 == 1;
                            ScheduleCell(table, item.InstallmentNumber.ToString(), alt);
                            ScheduleCell(table, item.DueDate.ToString("dd-MMM-yyyy"), alt);
                            ScheduleCell(table, item.Principal.ToString("N2"), alt, alignRight: true);
                            ScheduleCell(table, item.Interest.ToString("N2"), alt, alignRight: true);
                            ScheduleCell(table, item.TotalPayment.ToString("N2"), alt, alignRight: true);
                            ScheduleCell(table, item.OutstandingBalance.ToString("N2"), alt, alignRight: true);
                        }

                        ScheduleTotalCell(table, "TOTAL");
                        ScheduleTotalCell(table, "");
                        ScheduleTotalCell(table, data.TotalPrincipal.ToString("N2"), alignRight: true);
                        ScheduleTotalCell(table, data.TotalInterest.ToString("N2"), alignRight: true);
                        ScheduleTotalCell(table, data.TotalRepayment.ToString("N2"), alignRight: true);
                        ScheduleTotalCell(table, "");
                    });
                });
            }

            // Committee conditions
            if (!string.IsNullOrWhiteSpace(data.CommitteeConditions))
            {
                col.Item().PaddingBottom(12).Column(c =>
                {
                    c.Item().Text("COMMITTEE CONDITIONS").Bold().FontSize(11).FontColor(Color.FromHex(DarkBlue));
                    c.Item().PaddingTop(5).Text("This offer is subject to the following committee conditions:").FontSize(10).Italic();
                    c.Item().PaddingTop(5).Text(data.CommitteeConditions).FontSize(10);
                });
            }

            // General conditions + acceptance — use template when provided, fall back to defaults
            col.Item().PaddingBottom(12).Column(c =>
            {
                if (!string.IsNullOrWhiteSpace(data.RenderedConditions))
                {
                    foreach (var para in data.RenderedConditions.Split(
                        new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var trimmed = para.Trim();
                        if (string.IsNullOrWhiteSpace(trimmed)) continue;
                        if (trimmed == trimmed.ToUpperInvariant() && trimmed.Length < 80 && !trimmed.Contains('.'))
                            c.Item().PaddingTop(8).PaddingBottom(3)
                                .Text(trimmed).Bold().FontSize(11).FontColor(Color.FromHex(DarkBlue));
                        else
                            c.Item().PaddingBottom(4).Text(trimmed).FontSize(10);
                    }
                }
                else
                {
                    c.Item().Text("GENERAL CONDITIONS").Bold().FontSize(11).FontColor(Color.FromHex(DarkBlue));
                    c.Item().PaddingTop(5);
                    var conditions = new[]
                    {
                        "The equipment shall be used solely for agricultural purposes as stated in the application.",
                        "The borrower shall maintain adequate insurance on the equipment throughout the facility tenor.",
                        "The Bank reserves the right to conduct periodic inspection of the equipment.",
                        "GPS tracking device shall be installed and activated prior to equipment deployment.",
                        "Repayment obligations commence as per the schedule above. Default attracts penalty charges per the Bank's approved schedule of charges.",
                        "This offer is valid for 30 days from the date of issue. Failure to accept within this period renders the offer null and void.",
                        "Standard terms and conditions of the Bank applicable to agricultural credit facilities apply."
                    };
                    for (var i = 0; i < conditions.Length; i++)
                        c.Item().PaddingBottom(3).Text($"{i + 1}. {conditions[i]}").FontSize(10);
                }
            });

            // Acceptance signature block
            col.Item().PaddingTop(5).Column(c =>
            {
                if (string.IsNullOrWhiteSpace(data.RenderedConditions))
                {
                    c.Item().Text("ACCEPTANCE").Bold().FontSize(11).FontColor(Color.FromHex(DarkBlue));
                    c.Item().PaddingTop(5).Text(
                        "I/We hereby accept the terms and conditions of this offer as stated above, including " +
                        "the repayment schedule set out herein. I/We confirm that the information provided in " +
                        "support of this application is true and accurate.").FontSize(10);
                }

                c.Item().PaddingTop(20).Row(row =>
                {
                    row.RelativeItem().Column(sig =>
                    {
                        sig.Item().PaddingBottom(40).Text("");
                        sig.Item().LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                        sig.Item().PaddingTop(3).Text("Applicant Signature").FontSize(9).Bold();
                        sig.Item().PaddingTop(15);
                        sig.Item().LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                        sig.Item().PaddingTop(3).Text("Name").FontSize(9);
                        sig.Item().PaddingTop(15);
                        sig.Item().LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                        sig.Item().PaddingTop(3).Text("Date").FontSize(9);
                    });

                    row.ConstantItem(40);

                    row.RelativeItem().Column(sig =>
                    {
                        sig.Item().PaddingBottom(40).Text("");
                        sig.Item().LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                        sig.Item().PaddingTop(3).Text("Authorised Bank Officer").FontSize(9).Bold();
                        sig.Item().PaddingTop(15);
                        sig.Item().LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                        sig.Item().PaddingTop(3).Text("Name / Designation").FontSize(9);
                        sig.Item().PaddingTop(15);
                        sig.Item().LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                        sig.Item().PaddingTop(3).Text("Date").FontSize(9);
                    });
                });
            });
        });
    }

    private void ComposeFooter(IContainer container, NampOfferLetterData data)
    {
        container.Column(col =>
        {
            col.Item().LineHorizontal(1).LineColor(Color.FromHex(MediumGray));
            col.Item().PaddingTop(5).AlignCenter()
                .Text("This offer is valid for 30 days from the date of issue.")
                .FontSize(8).Bold().FontColor(Color.FromHex(AccentBlue));

            col.Item().PaddingTop(3).Row(row =>
            {
                row.RelativeItem().Text($"Ref: {data.ApplicationNumber}").FontSize(7).FontColor(Colors.Grey.Darken1);
                row.RelativeItem().AlignCenter().DefaultTextStyle(x => x.FontSize(7).FontColor(Colors.Grey.Darken1)).Text(x =>
                {
                    x.Span("Page ");
                    x.CurrentPageNumber();
                    x.Span(" of ");
                    x.TotalPages();
                });
                row.RelativeItem().AlignRight().Text(data.BankName).FontSize(7).FontColor(Colors.Grey.Darken1);
            });

            col.Item().PaddingTop(2).AlignCenter()
                .Text("CONFIDENTIAL — This document is intended solely for the named recipient.")
                .FontSize(6).Italic().FontColor(Colors.Grey.Medium);
        });
    }

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
            .BorderBottom(1).BorderColor(Color.FromHex(MediumGray)).Padding(4);

        if (alignRight) cell.AlignRight().Text(text).FontSize(8);
        else cell.Text(text).FontSize(8);
    }

    private static void ScheduleTotalCell(TableDescriptor table, string text, bool alignRight = false)
    {
        var cell = table.Cell().Background(Color.FromHex(MediumGray)).Padding(5);
        if (alignRight) cell.AlignRight().Text(text).Bold().FontSize(9);
        else cell.Text(text).Bold().FontSize(9);
    }
}

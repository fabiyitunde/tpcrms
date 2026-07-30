using CRMS.Application.OfferLetter.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CRMS.Infrastructure.Documents;

public class OfferLetterPdfGenerator : IOfferLetterPdfGenerator
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
                page.MarginHorizontal(45);
                page.MarginVertical(36);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

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
            // Accent strip
            col.Item().Height(5).Background(Color.FromHex(Primary));
            col.Item().PaddingTop(10).Row(row =>
            {
                // Logo
                row.ConstantItem(70).AlignMiddle().Element(c => BoaBrand.RenderLogo(c, 60));

                // Bank name block
                row.RelativeItem().AlignCenter().AlignMiddle().Column(title =>
                {
                    title.Item().AlignCenter()
                        .Text(data.BankName.ToUpperInvariant())
                        .Bold().FontSize(15).FontColor(Color.FromHex(Primary));
                    title.Item().AlignCenter()
                        .Text(data.BranchName)
                        .FontSize(9).FontColor(Colors.Grey.Darken2);
                    title.Item().AlignCenter().PaddingTop(2)
                        .Text("Corporate Loan Division")
                        .FontSize(8).Italic().FontColor(Color.FromHex(Accent));
                });

                // Date / ref block
                row.ConstantItem(110).AlignMiddle().Column(meta =>
                {
                    meta.Item().AlignRight()
                        .Text($"Date:  {data.GeneratedDate:dd MMM yyyy}")
                        .FontSize(8.5f).FontColor(Colors.Grey.Darken2);
                    meta.Item().AlignRight().PaddingTop(2)
                        .Text($"Ref:  {data.ApplicationNumber}")
                        .FontSize(8.5f).Bold().FontColor(Color.FromHex(Primary));
                    meta.Item().AlignRight().PaddingTop(1)
                        .Text($"Version: {data.Version}")
                        .FontSize(7.5f).FontColor(Colors.Grey.Medium);
                });
            });

            col.Item().PaddingTop(10).LineHorizontal(2.5f).LineColor(Color.FromHex(Primary));
            col.Item().PaddingTop(6).AlignCenter()
                .Text("OFFER LETTER")
                .Bold().FontSize(17).FontColor(Color.FromHex(Primary)).LetterSpacing(0.05f);
            col.Item().PaddingTop(5).LineHorizontal(1).LineColor(Color.FromHex(Subtle));
        });
    }

    // ── Content ──────────────────────────────────────────────────────────────

    private static void ComposeContent(IContainer container, OfferLetterData data)
    {
        container.PaddingTop(14).Column(col =>
        {
            ComposeAddressee(col, data);
            ComposeOpeningParagraph(col, data);
            ComposeFacilityDetails(col, data);
            ComposeRepaymentSummary(col, data);
            if (data.Conditions.Any()) ComposeConditions(col, data);
            ComposeAcceptance(col, data);
        });
    }

    private static void ComposeAddressee(ColumnDescriptor col, OfferLetterData data)
    {
        col.Item().PaddingBottom(12).Column(c =>
        {
            c.Item().Text(data.CustomerName).Bold().FontSize(11);
            foreach (var line in data.CustomerAddress.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                c.Item().Text(line.Trim()).FontSize(10);
        });
    }

    private static void ComposeOpeningParagraph(ColumnDescriptor col, OfferLetterData data)
    {
        col.Item().PaddingBottom(14).Column(c =>
        {
            c.Item().PaddingBottom(6).Text($"Dear {data.CustomerName},").FontSize(10);
            c.Item().Text(
                "We are pleased to inform you that your application for a credit facility with " +
                $"{data.BankName} has been approved. The terms of the approved facility and the " +
                "proposed repayment schedule are set out below for your review and acceptance.")
                .FontSize(10).LineHeight(1.5f);
        });
    }

    private static void ComposeFacilityDetails(ColumnDescriptor col, OfferLetterData data)
    {
        col.Item().PaddingBottom(16).Column(c =>
        {
            SectionHeader(c, "FACILITY DETAILS");
            c.Item().PaddingTop(1).Table(table =>
            {
                table.ColumnsDefinition(def =>
                {
                    def.RelativeColumn(1);
                    def.RelativeColumn(2);
                });

                FacilityRow(table, "Borrower",            data.CustomerName,                         true);
                FacilityRow(table, "Facility Type",       data.ProductName,                          false);
                FacilityRow(table, "Approved Amount",     $"{data.Currency} {data.ApprovedAmount:N2}", true);
                FacilityRow(table, "Tenor",               $"{data.TenorMonths} months",              false);
                FacilityRow(table, "Interest Rate",       $"{data.InterestRatePerAnnum:N2}% per annum", true);
                FacilityRow(table, "Repayment Frequency", data.RepaymentFrequency,                   false);
                FacilityRow(table, "Amortisation Method", data.AmortizationMethod,                   true);
            });
        });
    }

    private static void ComposeRepaymentSummary(ColumnDescriptor col, OfferLetterData data)
    {
        col.Item().PaddingBottom(16).Column(c =>
        {
            SectionHeader(c, "REPAYMENT SUMMARY");
            c.Item().PaddingTop(1)
                .Border(1).BorderColor(Color.FromHex(MedGray))
                .Row(row =>
                {
                    SummaryCell(row, "Total Principal",   $"{data.Currency} {data.TotalPrincipal:N2}",   false);
                    SummaryDivider(row);
                    SummaryCell(row, "Total Interest",    $"{data.Currency} {data.TotalInterest:N2}",    false);
                    SummaryDivider(row);
                    SummaryCell(row, "Total Repayment",   $"{data.Currency} {data.TotalRepayment:N2}",   false);
                    SummaryDivider(row);
                    SummaryCell(row, "Monthly Instalment", $"{data.Currency} {data.MonthlyInstallment:N2}", true);
                });
        });
    }

    private static void ComposeConditions(ColumnDescriptor col, OfferLetterData data)
    {
        col.Item().PaddingBottom(16).Column(c =>
        {
            SectionHeader(c, "CONDITIONS PRECEDENT");
            c.Item().PaddingTop(6)
                .Text("This offer is subject to the satisfaction of the following conditions:")
                .FontSize(10).Italic().FontColor(Colors.Grey.Darken2);
            c.Item().PaddingTop(6).Column(list =>
            {
                for (var i = 0; i < data.Conditions.Count; i++)
                    list.Item().PaddingBottom(3)
                        .Text($"{i + 1}.  {data.Conditions[i]}").FontSize(10).LineHeight(1.4f);
            });
        });
    }

    private static void ComposeAcceptance(ColumnDescriptor col, OfferLetterData data)
    {
        col.Item().PaddingTop(4).Column(c =>
        {
            SectionHeader(c, "ACCEPTANCE");
            c.Item().PaddingTop(8)
                .Text("I/We hereby accept all terms and conditions of this offer as set out above and confirm " +
                      "that all information provided in support of this application is true and accurate.")
                .FontSize(10).LineHeight(1.5f);

            c.Item().PaddingTop(24).Row(row =>
            {
                SignatureBlock(row, "Customer / Authorised Signatory");
                row.ConstantItem(30);
                SignatureBlock(row, "Witness");
                row.ConstantItem(30);
                SignatureBlock(row, "Bank Officer");
            });
        });
    }

    // ── Footer ───────────────────────────────────────────────────────────────

    private static void ComposeFooter(IContainer container, OfferLetterData data)
    {
        container.Column(col =>
        {
            col.Item().LineHorizontal(1).LineColor(Color.FromHex(Subtle));
            col.Item().PaddingTop(4).AlignCenter()
                .Text("This offer is valid for 30 days from the date of issue.")
                .FontSize(8).Bold().FontColor(Color.FromHex(Accent));
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
                .Text("CONFIDENTIAL — This document is intended solely for the named recipient and contains privileged information.")
                .FontSize(6).Italic().FontColor(Colors.Grey.Medium);
            col.Item().PaddingTop(6).Height(4).Background(Color.FromHex(Primary));
        });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void SectionHeader(ColumnDescriptor col, string title)
    {
        col.Item()
            .Background(Color.FromHex(Primary))
            .PaddingHorizontal(10).PaddingVertical(6)
            .Text(title).Bold().FontSize(10).FontColor(Colors.White).LetterSpacing(0.04f);
    }

    private static void FacilityRow(TableDescriptor table, string label, string value, bool shaded)
    {
        var bg = shaded ? Color.FromHex(PanelGreen) : Colors.White;
        table.Cell().Background(bg).BorderBottom(1).BorderColor(Color.FromHex(MedGray))
            .PaddingHorizontal(10).PaddingVertical(7)
            .Text(label).Bold().FontSize(9.5f).FontColor(Color.FromHex(Primary));
        table.Cell().Background(bg).BorderBottom(1).BorderColor(Color.FromHex(MedGray))
            .PaddingHorizontal(10).PaddingVertical(7)
            .Text(value).FontSize(9.5f);
    }

    private static void SummaryCell(RowDescriptor row, string label, string value, bool highlight)
    {
        var bg = highlight ? Color.FromHex(Primary) : Colors.White;
        var labelColor = highlight ? Color.FromHex(Subtle) : Colors.Grey.Darken2;
        var valueColor = highlight ? Colors.White : Color.FromHex(Primary);

        row.RelativeItem().Background(bg).PaddingHorizontal(12).PaddingVertical(12).Column(c =>
        {
            c.Item().Text(label).FontSize(8).FontColor(labelColor);
            c.Item().PaddingTop(3).Text(value).Bold().FontSize(11).FontColor(valueColor);
        });
    }

    private static void SummaryDivider(RowDescriptor row)
    {
        row.ConstantItem(1).Background(Color.FromHex(MedGray));
    }

    private static void SignatureBlock(RowDescriptor row, string role)
    {
        row.RelativeItem().Column(c =>
        {
            c.Item().Height(40);
            c.Item().LineHorizontal(1).LineColor(Colors.Grey.Darken1);
            c.Item().PaddingTop(3).Text(role).FontSize(8.5f).Bold();
            c.Item().PaddingTop(12).LineHorizontal(1).LineColor(Colors.Grey.Darken1);
            c.Item().PaddingTop(3).Text("Name").FontSize(8);
            c.Item().PaddingTop(12).LineHorizontal(1).LineColor(Colors.Grey.Darken1);
            c.Item().PaddingTop(3).Text("Date").FontSize(8);
        });
    }
}

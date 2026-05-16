using CRMS.Application.OfferLetter.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CRMS.Infrastructure.Documents;

public class KfsPdfGenerator : IKfsPdfGenerator
{
    private const string DarkBlue = "#1a365d";
    private const string LightGray = "#f7fafc";
    private const string MediumGray = "#e2e8f0";
    private const string WarningYellow = "#744210";
    private const string WarningBg = "#fffff0";

    public Task<byte[]> GenerateAsync(KfsData data, CancellationToken ct = default)
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

        return Task.FromResult(document.GeneratePdf());
    }

    private void ComposeHeader(IContainer container, KfsData data)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Text(data.BankName.ToUpperInvariant()).Bold().FontSize(13).FontColor(Color.FromHex(DarkBlue));
                row.RelativeItem().AlignRight().Column(c =>
                {
                    c.Item().AlignRight().Text($"Date: {data.GeneratedDate:dd-MMM-yyyy}").FontSize(9);
                    c.Item().AlignRight().Text($"Ref: {data.ApplicationNumber}").FontSize(9).Bold();
                });
            });
            col.Item().PaddingTop(4).PaddingBottom(2).AlignCenter()
                .Text("KEY FACTS STATEMENT").Bold().FontSize(15).FontColor(Color.FromHex(DarkBlue));
            col.Item().AlignCenter().Text("(As required by CBN Consumer Protection Regulations 2022)")
                .FontSize(8).Italic().FontColor(Colors.Grey.Darken1);
            col.Item().PaddingTop(4).LineHorizontal(2).LineColor(Color.FromHex(DarkBlue));
        });
    }

    private void ComposeContent(IContainer container, KfsData data)
    {
        container.PaddingVertical(10).Column(col =>
        {
            // Notice box
            col.Item().PaddingBottom(10).Border(1).BorderColor(Color.FromHex(WarningYellow))
                .Background(Color.FromHex(WarningBg)).Padding(8)
                .Text("IMPORTANT: Please read this document carefully before signing the Offer Letter. " +
                      "This statement summarises all key terms and costs of your loan. " +
                      "You have a 3 (three) working day cooling-off period after signing to cancel this offer at no cost.")
                .FontSize(9).Italic();

            // Borrower details
            col.Item().PaddingBottom(8).Column(section =>
            {
                section.Item().Text("BORROWER INFORMATION").Bold().FontSize(11).FontColor(Color.FromHex(DarkBlue));
                section.Item().PaddingTop(4).Table(t =>
                {
                    t.ColumnsDefinition(c => { c.RelativeColumn(1); c.RelativeColumn(2); });
                    KfsRow(t, "Borrower Name", data.CustomerName);
                    KfsRow(t, "Loan Product", data.ProductName);
                    KfsRow(t, "Reference Number", data.ApplicationNumber);
                });
            });

            // Loan terms
            col.Item().PaddingBottom(8).Column(section =>
            {
                section.Item().Text("LOAN TERMS").Bold().FontSize(11).FontColor(Color.FromHex(DarkBlue));
                section.Item().PaddingTop(4).Table(t =>
                {
                    t.ColumnsDefinition(c => { c.RelativeColumn(1); c.RelativeColumn(2); });
                    KfsRow(t, "Loan Amount", $"{data.Currency} {data.LoanAmount:N2}");
                    KfsRow(t, "Tenor", $"{data.TenorMonths} months");
                    KfsRow(t, "Repayment Frequency", "Monthly");
                    KfsRow(t, "Amortisation Method", "Equal Monthly Installments (EMI)");
                    KfsRowHighlight(t, "Monthly Installment", $"{data.Currency} {data.MonthlyInstallment:N2}");
                });
            });

            // Cost of credit
            col.Item().PaddingBottom(8).Column(section =>
            {
                section.Item().Text("COST OF CREDIT").Bold().FontSize(11).FontColor(Color.FromHex(DarkBlue));
                section.Item().PaddingTop(4).Table(t =>
                {
                    t.ColumnsDefinition(c => { c.RelativeColumn(1); c.RelativeColumn(2); });
                    KfsRow(t, "Nominal Interest Rate (p.a.)", $"{data.NominalRatePerAnnum:N2}%");
                    KfsRow(t, "Effective Annual Rate (EAR)", $"{data.EffectiveAnnualRate:N2}%");
                    KfsRow(t, "Total Interest Payable", $"{data.Currency} {data.TotalInterest:N2}");
                    KfsRow(t, "Processing Fee", data.ProcessingFeeAmount > 0 ? $"{data.Currency} {data.ProcessingFeeAmount:N2}" : "Nil");
                    KfsRow(t, "Management Fee", data.ManagementFeeAmount > 0 ? $"{data.Currency} {data.ManagementFeeAmount:N2}" : "Nil");
                    KfsRowHighlight(t, "Total Cost of Credit", $"{data.Currency} {data.TotalCostOfCredit:N2}");
                    KfsRowHighlight(t, "Total Repayment Amount", $"{data.Currency} {data.TotalRepayment:N2}");
                });
            });

            // Other terms
            col.Item().PaddingBottom(8).Column(section =>
            {
                section.Item().Text("OTHER TERMS & CONDITIONS").Bold().FontSize(11).FontColor(Color.FromHex(DarkBlue));
                section.Item().PaddingTop(4).Table(t =>
                {
                    t.ColumnsDefinition(c => { c.RelativeColumn(1); c.RelativeColumn(2); });
                    KfsRow(t, "Late Payment Penalty", data.LatePaymentPenalty);
                    KfsRow(t, "Early Repayment", data.EarlyRepaymentTerms);
                    KfsRow(t, "Security / Collateral", data.SecurityRequired);
                    KfsRow(t, "Cooling-Off Period", "3 (three) working days from date of signing");
                    KfsRow(t, "Offer Validity", "30 days from date of issue");
                });
            });

            // Complaints
            col.Item().PaddingBottom(12).Column(section =>
            {
                section.Item().Text("COMPLAINTS & ENQUIRIES").Bold().FontSize(11).FontColor(Color.FromHex(DarkBlue));
                section.Item().PaddingTop(4).Text($"If you have any concerns about this offer, please contact us: {data.ComplaintChannel}")
                    .FontSize(9);
                section.Item().PaddingTop(4).Text("If your complaint is not resolved within 2 weeks, you may escalate to the Central Bank of Nigeria (CBN) at: consumerprotection@cbn.gov.ng or 07002255226.")
                    .FontSize(9).Italic();
            });

            // Acceptance
            col.Item().PaddingTop(10).Column(section =>
            {
                section.Item().Text("ACKNOWLEDGEMENT").Bold().FontSize(11).FontColor(Color.FromHex(DarkBlue));
                section.Item().PaddingTop(6).Text(
                    "I/We confirm that I/we have read, understood, and received a copy of this Key Facts Statement " +
                    "prior to signing the Offer Letter. I/We understand all costs and terms stated above.")
                    .FontSize(9);

                section.Item().PaddingTop(20).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().PaddingBottom(30).Text("");
                        c.Item().LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                        c.Item().PaddingTop(3).Text("Customer Signature").FontSize(8).Bold();
                        c.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                        c.Item().PaddingTop(3).Text("Date").FontSize(8);
                    });
                    row.ConstantItem(40);
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().PaddingBottom(30).Text("");
                        c.Item().LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                        c.Item().PaddingTop(3).Text("Bank Officer Signature").FontSize(8).Bold();
                        c.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                        c.Item().PaddingTop(3).Text("Date").FontSize(8);
                    });
                });
            });
        });
    }

    private void ComposeFooter(IContainer container, KfsData data)
    {
        container.Column(col =>
        {
            col.Item().LineHorizontal(1).LineColor(Color.FromHex(MediumGray));
            col.Item().PaddingTop(3).Row(row =>
            {
                row.RelativeItem().Text($"Ref: {data.ApplicationNumber} | KEY FACTS STATEMENT").FontSize(7).FontColor(Colors.Grey.Darken1);
                row.RelativeItem().AlignCenter().DefaultTextStyle(x => x.FontSize(7).FontColor(Colors.Grey.Darken1)).Text(x =>
                {
                    x.Span("Page "); x.CurrentPageNumber(); x.Span(" of "); x.TotalPages();
                });
                row.RelativeItem().AlignRight().Text(data.BankName).FontSize(7).FontColor(Colors.Grey.Darken1);
            });
            col.Item().PaddingTop(2).AlignCenter()
                .Text("CONFIDENTIAL — Issued pursuant to CBN Consumer Protection Regulations 2022")
                .FontSize(6).Italic().FontColor(Colors.Grey.Medium);
        });
    }

    private static void KfsRow(TableDescriptor table, string label, string value)
    {
        table.Cell().BorderBottom(1).BorderColor(Color.FromHex(MediumGray))
            .Background(Color.FromHex(LightGray)).Padding(6)
            .Text(label).Bold().FontSize(9);
        table.Cell().BorderBottom(1).BorderColor(Color.FromHex(MediumGray))
            .Padding(6).Text(value).FontSize(9);
    }

    private static void KfsRowHighlight(TableDescriptor table, string label, string value)
    {
        table.Cell().BorderBottom(1).BorderColor(Color.FromHex(MediumGray))
            .Background(Color.FromHex(DarkBlue)).Padding(6)
            .Text(label).Bold().FontSize(9).FontColor(Colors.White);
        table.Cell().BorderBottom(1).BorderColor(Color.FromHex(MediumGray))
            .Background(Color.FromHex(DarkBlue)).Padding(6)
            .Text(value).Bold().FontSize(9).FontColor(Colors.White);
    }
}

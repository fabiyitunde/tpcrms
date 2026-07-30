using CRMS.Application.OfferLetter.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CRMS.Infrastructure.Documents;

public class KfsPdfGenerator : IKfsPdfGenerator
{
    private const string Primary      = BoaBrand.Primary;    // #14532d dark green
    private const string Accent       = BoaBrand.Accent;     // #1f7a3d mid green
    private const string Subtle       = BoaBrand.Subtle;     // #a7d3b5 soft green
    private const string PanelGreen   = BoaBrand.PanelGreen; // #f0f4f1
    private const string LightGray    = BoaBrand.LightGray;  // #f7fafc
    private const string MedGray      = BoaBrand.MediumGray; // #e2e8f0
    private const string WarningText  = "#744210";           // amber text (regulatory)
    private const string WarningBg    = "#fffbeb";           // warm amber background

    public Task<byte[]> GenerateAsync(KfsData data, CancellationToken ct = default)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(42);
                page.MarginVertical(34);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                page.Header().Element(c => ComposeHeader(c, data));
                page.Content().Element(c => ComposeContent(c, data));
                page.Footer().Element(c => ComposeFooter(c, data));
            });
        });

        return Task.FromResult(document.GeneratePdf());
    }

    // ── Header ───────────────────────────────────────────────────────────────

    private static void ComposeHeader(IContainer container, KfsData data)
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
                        .Bold().FontSize(14).FontColor(Color.FromHex(Primary));
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
                });
            });

            col.Item().PaddingTop(8).LineHorizontal(2.5f).LineColor(Color.FromHex(Primary));
            col.Item().PaddingTop(5).AlignCenter()
                .Text("KEY FACTS STATEMENT")
                .Bold().FontSize(16).FontColor(Color.FromHex(Primary)).LetterSpacing(0.04f);
            col.Item().PaddingTop(3).AlignCenter()
                .Text("As required by CBN Consumer Protection Regulations 2022")
                .FontSize(8).Italic().FontColor(Colors.Grey.Darken2);
            col.Item().PaddingTop(5).LineHorizontal(1).LineColor(Color.FromHex(Subtle));
        });
    }

    // ── Content ──────────────────────────────────────────────────────────────

    private static void ComposeContent(IContainer container, KfsData data)
    {
        container.PaddingTop(12).Column(col =>
        {
            // Regulatory notice — keep amber for compliance visibility
            col.Item().PaddingBottom(14)
                .Border(1.5f).BorderColor(Color.FromHex(WarningText))
                .Background(Color.FromHex(WarningBg))
                .PaddingHorizontal(12).PaddingVertical(9)
                .Column(c =>
                {
                    c.Item().Text("IMPORTANT — PLEASE READ BEFORE SIGNING")
                        .Bold().FontSize(9.5f).FontColor(Color.FromHex(WarningText));
                    c.Item().PaddingTop(4)
                        .Text("Please read this document carefully before signing the Offer Letter. " +
                              "This statement summarises all key terms and costs of your loan. " +
                              "You have a 3 (three) working day cooling-off period after signing to cancel this offer at no cost.")
                        .FontSize(9).Italic().FontColor(Color.FromHex(WarningText)).LineHeight(1.4f);
                });

            // Borrower information
            col.Item().PaddingBottom(10).Column(section =>
            {
                SectionHeader(section, "BORROWER INFORMATION");
                section.Item().PaddingTop(1).Table(t =>
                {
                    t.ColumnsDefinition(c => { c.RelativeColumn(1); c.RelativeColumn(2); });
                    KfsRow(t, "Borrower Name",     data.CustomerName,     true);
                    KfsRow(t, "Loan Product",      data.ProductName,      false);
                    KfsRow(t, "Reference Number",  data.ApplicationNumber, true);
                });
            });

            // Loan terms
            col.Item().PaddingBottom(10).Column(section =>
            {
                SectionHeader(section, "LOAN TERMS");
                section.Item().PaddingTop(1).Table(t =>
                {
                    t.ColumnsDefinition(c => { c.RelativeColumn(1); c.RelativeColumn(2); });
                    KfsRow(t, "Loan Amount",            $"{data.Currency} {data.LoanAmount:N2}",          true);
                    KfsRow(t, "Tenor",                  $"{data.TenorMonths} months",                    false);
                    KfsRow(t, "Repayment Frequency",    "Monthly",                                        true);
                    KfsRow(t, "Amortisation Method",    "Equal Monthly Installments (EMI)",              false);
                    KfsRowHighlight(t, "Monthly Installment", $"{data.Currency} {data.MonthlyInstallment:N2}");
                });
            });

            // Cost of credit
            col.Item().PaddingBottom(10).Column(section =>
            {
                SectionHeader(section, "COST OF CREDIT");
                section.Item().PaddingTop(1).Table(t =>
                {
                    t.ColumnsDefinition(c => { c.RelativeColumn(1); c.RelativeColumn(2); });
                    KfsRow(t, "Nominal Interest Rate (p.a.)", $"{data.NominalRatePerAnnum:N2}%",                                              true);
                    KfsRow(t, "Effective Annual Rate (EAR)",  $"{data.EffectiveAnnualRate:N2}%",                                             false);
                    KfsRow(t, "Total Interest Payable",       $"{data.Currency} {data.TotalInterest:N2}",                                    true);
                    KfsRow(t, "Processing Fee",               data.ProcessingFeeAmount > 0 ? $"{data.Currency} {data.ProcessingFeeAmount:N2}" : "Nil", false);
                    KfsRow(t, "Management Fee",               data.ManagementFeeAmount > 0 ? $"{data.Currency} {data.ManagementFeeAmount:N2}" : "Nil", true);
                    KfsRowHighlight(t, "Total Cost of Credit",    $"{data.Currency} {data.TotalCostOfCredit:N2}");
                    KfsRowHighlight(t, "Total Repayment Amount",  $"{data.Currency} {data.TotalRepayment:N2}");
                });
            });

            // Other terms
            col.Item().PaddingBottom(10).Column(section =>
            {
                SectionHeader(section, "OTHER TERMS & CONDITIONS");
                section.Item().PaddingTop(1).Table(t =>
                {
                    t.ColumnsDefinition(c => { c.RelativeColumn(1); c.RelativeColumn(2); });
                    KfsRow(t, "Late Payment Penalty",  data.LatePaymentPenalty,                               true);
                    KfsRow(t, "Early Repayment",       data.EarlyRepaymentTerms,                             false);
                    KfsRow(t, "Security / Collateral", data.SecurityRequired,                                 true);
                    KfsRow(t, "Cooling-Off Period",    "3 (three) working days from date of signing",        false);
                    KfsRow(t, "Offer Validity",        "30 days from date of issue",                         true);
                });
            });

            // Complaints
            col.Item().PaddingBottom(12).Column(section =>
            {
                SectionHeader(section, "COMPLAINTS & ENQUIRIES");
                section.Item().PaddingTop(8)
                    .Text($"For complaints or enquiries, please contact us at: {data.ComplaintChannel}")
                    .FontSize(9.5f).LineHeight(1.4f);
                section.Item().PaddingTop(5)
                    .Text("If unresolved within 2 weeks, escalate to the Central Bank of Nigeria (CBN): " +
                          "consumerprotection@cbn.gov.ng  |  07002255226")
                    .FontSize(9).Italic().FontColor(Colors.Grey.Darken2).LineHeight(1.4f);
            });

            // Acknowledgement
            col.Item().PaddingTop(4).Column(section =>
            {
                SectionHeader(section, "ACKNOWLEDGEMENT");
                section.Item().PaddingTop(8)
                    .Text("I/We confirm that I/we have read, understood, and received a copy of this Key Facts " +
                          "Statement prior to signing the Offer Letter. I/We understand all costs and terms stated above.")
                    .FontSize(9.5f).LineHeight(1.5f);

                section.Item().PaddingTop(20).Row(row =>
                {
                    SignatureBlock(row, "Customer / Authorised Signatory");
                    row.ConstantItem(40);
                    SignatureBlock(row, "Bank Officer");
                });
            });
        });
    }

    // ── Footer ───────────────────────────────────────────────────────────────

    private static void ComposeFooter(IContainer container, KfsData data)
    {
        container.Column(col =>
        {
            col.Item().LineHorizontal(1).LineColor(Color.FromHex(Subtle));
            col.Item().PaddingTop(3).Row(row =>
            {
                row.RelativeItem()
                    .Text($"Ref: {data.ApplicationNumber}  |  KEY FACTS STATEMENT")
                    .FontSize(7).FontColor(Colors.Grey.Darken1);
                row.RelativeItem().AlignCenter()
                    .DefaultTextStyle(x => x.FontSize(7).FontColor(Colors.Grey.Darken1))
                    .Text(t => { t.Span("Page "); t.CurrentPageNumber(); t.Span(" of "); t.TotalPages(); });
                row.RelativeItem().AlignRight()
                    .Text(data.BankName).FontSize(7).FontColor(Colors.Grey.Darken1);
            });
            col.Item().PaddingTop(3).AlignCenter()
                .Text("CONFIDENTIAL — Issued pursuant to CBN Consumer Protection Regulations 2022")
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

    private static void KfsRow(TableDescriptor table, string label, string value, bool shaded)
    {
        var bg = shaded ? Color.FromHex(PanelGreen) : Colors.White;
        table.Cell().Background(bg).BorderBottom(1).BorderColor(Color.FromHex(MedGray))
            .PaddingHorizontal(10).PaddingVertical(7)
            .Text(label).Bold().FontSize(9.5f).FontColor(Color.FromHex(Primary));
        table.Cell().Background(bg).BorderBottom(1).BorderColor(Color.FromHex(MedGray))
            .PaddingHorizontal(10).PaddingVertical(7)
            .Text(value).FontSize(9.5f);
    }

    private static void KfsRowHighlight(TableDescriptor table, string label, string value)
    {
        table.Cell().Background(Color.FromHex(Primary)).BorderBottom(1).BorderColor(Color.FromHex(Accent))
            .PaddingHorizontal(10).PaddingVertical(8)
            .Text(label).Bold().FontSize(10).FontColor(Colors.White);
        table.Cell().Background(Color.FromHex(Primary)).BorderBottom(1).BorderColor(Color.FromHex(Accent))
            .PaddingHorizontal(10).PaddingVertical(8)
            .Text(value).Bold().FontSize(10).FontColor(Color.FromHex(Subtle));
    }

    private static void SignatureBlock(RowDescriptor row, string role)
    {
        row.RelativeItem().Column(c =>
        {
            c.Item().Height(38);
            c.Item().LineHorizontal(1).LineColor(Colors.Grey.Darken1);
            c.Item().PaddingTop(3).Text(role).FontSize(8.5f).Bold();
            c.Item().PaddingTop(12).LineHorizontal(1).LineColor(Colors.Grey.Darken1);
            c.Item().PaddingTop(3).Text("Date").FontSize(8);
        });
    }
}

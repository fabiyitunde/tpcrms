using CRMS.Application.CreditAppraisal;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CRMS.Infrastructure.Documents;

/// <summary>
/// The stand-alone credit appraisal, one document per loan track, in the bank's branding. All three
/// share one frame (header, case panel, footer) so they look alike and match the loan packs. NAMP and
/// RH-SHF print their appraisal through the same section their loan packs use, so the two documents
/// can never disagree; Corporate's appraisal is not in its loan pack and is composed here.
/// </summary>
public class CreditAppraisalPdfGenerator : ICreditAppraisalPdfGenerator
{
    private const string Primary = BoaBrand.Primary;
    private const string Accent = BoaBrand.Accent;
    private const string PanelGreen = BoaBrand.PanelGreen;
    private const string MedGray = BoaBrand.MediumGray;
    private static readonly TimeSpan Wat = TimeSpan.FromHours(1);

    public byte[] Corporate(CorporateAppraisalDocument d, string printedBy, DateTime printedAt) =>
        Render(d.Case, printedBy, printedAt, col => ComposeCorporate(col, d));

    public byte[] Namp(NampAppraisalDocument d, string printedBy, DateTime printedAt) =>
        Render(d.Case, printedBy, printedAt, col =>
        {
            Title(col, "Financial appraisal");
            NampLoanPackPdfGenerator.ComposeFinancialAppraisal(col, d.Report);
            if (!string.IsNullOrWhiteSpace(d.DecisionNote) || d.DecidedAt is not null)
            {
                Title(col, "Appraisal decision");
                if (!string.IsNullOrWhiteSpace(d.DecisionNote))
                    col.Item().PaddingTop(2).Text(d.DecisionNote).FontSize(9);
                if (d.DecidedAt is { } at)
                    col.Item().PaddingTop(3).Text($"Submitted by {d.DecidedBy ?? "the credit officer"} on {Local(at):dd MMM yyyy HH:mm}")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
            }
        });

    public byte[] Rhshf(RhshfAppraisalDocument d, string printedBy, DateTime printedAt) =>
        Render(d.Case, printedBy, printedAt, col =>
        {
            Title(col, "Financial appraisal (crop economics)");
            RhshfLoanPackPdfGenerator.ComposeFinancialAppraisal(col, d.Appraisal,
                string.IsNullOrWhiteSpace(d.Appraisal.Currency) ? "NGN" : d.Appraisal.Currency);
        });

    // ── The shared frame ─────────────────────────────────────────────────────────────────────────

    private static byte[] Render(AppraisalCaseInfo c, string printedBy, DateTime printedAt, Action<ColumnDescriptor> body)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Darken3));

                page.Header().Element(h => Header(h, c, printedAt));
                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Spacing(10);
                    CasePanel(col, c);
                    body(col);
                });
                // Who printed it sits in the footer, so it can never spill onto a page of its own.
                page.Footer().BorderTop(0.5f).BorderColor(Color.FromHex(MedGray)).PaddingTop(4).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text($"Printed by {printedBy} on {Local(printedAt):dd MMM yyyy HH:mm} WAT, from the appraisal as saved.").FontSize(7.5f).FontColor(Colors.Grey.Darken1);
                        c.Item().Text("Confidential: for the bank's credit decision only.").FontSize(7.5f).FontColor(Colors.Grey.Darken1);
                    });
                    row.ConstantItem(90).AlignRight().Text(t =>
                    {
                        t.DefaultTextStyle(x => x.FontSize(7.5f).FontColor(Colors.Grey.Darken1));
                        t.Span("Page ");
                        t.CurrentPageNumber();
                        t.Span(" of ");
                        t.TotalPages();
                    });
                });
            });
        }).GeneratePdf();
    }

    private static void Header(IContainer container, AppraisalCaseInfo c, DateTime printedAt)
    {
        container.Column(col =>
        {
            col.Item().Height(4).Background(Color.FromHex(Primary));
            col.Item().PaddingTop(8).Row(row =>
            {
                row.ConstantItem(54).Element(e => BoaBrand.RenderLogo(e, 48));
                row.RelativeItem().PaddingLeft(8).Column(t =>
                {
                    t.Item().Text("BANK OF AGRICULTURE").Bold().FontSize(14).FontColor(Color.FromHex(Primary));
                    t.Item().Text("Credit Appraisal").FontSize(11).SemiBold().FontColor(Color.FromHex(Accent));
                    t.Item().Text(c.TrackLabel).FontSize(8).Italic().FontColor(Colors.Grey.Darken1);
                });
                row.ConstantItem(160).AlignRight().Column(t =>
                {
                    t.Item().AlignRight().Text(c.CaseNumber).Bold().FontSize(11);
                    t.Item().AlignRight().Text($"Printed {Local(printedAt):dd MMM yyyy HH:mm}").FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });
            col.Item().PaddingTop(8).LineHorizontal(1.5f).LineColor(Color.FromHex(Primary));
        });
    }

    private static void CasePanel(ColumnDescriptor col, AppraisalCaseInfo c)
    {
        col.Item().Background(Color.FromHex(PanelGreen)).Padding(8).Table(t =>
        {
            t.ColumnsDefinition(d => { d.RelativeColumn(1.1f); d.RelativeColumn(2); d.RelativeColumn(1.1f); d.RelativeColumn(2); });
            Pair(t, "Borrower", c.Borrower);
            Pair(t, "Case", c.CaseNumber);
            Pair(t, c.AmountLabel, c.Amount is { } a ? $"{c.Currency} {a:N2}" : "—");
            Pair(t, "Tenor", c.TenorMonths is { } m ? $"{m} months" : "—");
            Pair(t, "Product", c.Product ?? "—");
            Pair(t, "Stage", c.Stage);
        });

        static void Pair(TableDescriptor t, string label, string value)
        {
            t.Cell().PaddingVertical(2).Text(label).FontSize(8).FontColor(Colors.Grey.Darken1);
            t.Cell().PaddingVertical(2).Text(value).FontSize(9).SemiBold();
        }
    }

    // ── Corporate ────────────────────────────────────────────────────────────────────────────────

    private static void ComposeCorporate(ColumnDescriptor col, CorporateAppraisalDocument d)
    {
        Title(col, "Financial ratios");
        col.Item().Row(row =>
        {
            row.Spacing(8);
            Tile(row, "DSCR", d.Dscr is { } v1 ? $"{v1:0.00}×" : "—", "Cash available to service debt ÷ debt service");
            Tile(row, "Leverage", d.Leverage is { } v2 ? $"{v2:0.00}×" : "—", "Total debt ÷ equity");
            Tile(row, "Current ratio", d.CurrentRatio is { } v3 ? $"{v3:0.00}×" : "—", "Current assets ÷ current liabilities");
            Tile(row, "Loan to value", d.Ltv is { } v4 ? $"{v4:0.##}%" : "—", "Loan ÷ collateral market value");
        });

        Title(col, "Assessment");
        col.Item().Row(row =>
        {
            row.Spacing(8);
            row.RelativeItem().Border(0.75f).BorderColor(Color.FromHex(MedGray)).Padding(8).Column(c =>
            {
                c.Item().Text("Repayment capacity").FontSize(8).FontColor(Colors.Grey.Darken1);
                c.Item().Text(Spaced(d.CapacityRating) ?? "—").Bold().FontSize(12).FontColor(Color.FromHex(CapacityColour(d.CapacityRating)));
            });
            row.RelativeItem().Border(0.75f).BorderColor(Color.FromHex(MedGray)).Padding(8).Column(c =>
            {
                c.Item().Text("Credit officer's recommendation").FontSize(8).FontColor(Colors.Grey.Darken1);
                c.Item().Text(Recommendation(d.Recommendation)).Bold().FontSize(12).FontColor(Color.FromHex(RecommendationColour(d.Recommendation)));
            });
        });

        Title(col, "Appraisal notes");
        col.Item().Border(0.75f).BorderColor(Color.FromHex(MedGray)).Padding(8)
            .Text(string.IsNullOrWhiteSpace(d.Notes) ? "No notes were recorded." : d.Notes).FontSize(9).LineHeight(1.4f);

        if (!string.IsNullOrWhiteSpace(d.MemoFileName))
            col.Item().Text($"Credit appraisal memo on file: {d.MemoFileName} (downloadable from the case).").FontSize(8).Italic().FontColor(Colors.Grey.Darken1);
        if (d.PreparedAt is { } at)
            col.Item().Text($"Prepared by {d.PreparedBy ?? "the credit officer"} on {Local(at.UtcDateTime):dd MMM yyyy HH:mm}").FontSize(8).FontColor(Colors.Grey.Darken1);
    }

    private static void Tile(RowDescriptor row, string label, string value, string meaning)
    {
        row.RelativeItem().Background(Color.FromHex(PanelGreen)).Padding(8).Column(c =>
        {
            c.Item().Text(label).FontSize(8).FontColor(Colors.Grey.Darken1);
            c.Item().Text(value).Bold().FontSize(15).FontColor(Color.FromHex(Primary));
            c.Item().Text(meaning).FontSize(6.5f).FontColor(Colors.Grey.Darken1);
        });
    }

    private static void Title(ColumnDescriptor col, string title) =>
        col.Item().PaddingTop(4).Column(c =>
        {
            c.Item().Text(title).Bold().FontSize(11).FontColor(Color.FromHex(Primary));
            c.Item().PaddingTop(2).LineHorizontal(0.75f).LineColor(Color.FromHex(BoaBrand.Subtle));
        });

    private static string Recommendation(string? r) => r switch
    {
        "Approve" => "Approve",
        "ConditionalApprove" => "Approve with conditions",
        "Decline" => "Decline",
        null or "" => "—",
        _ => r,
    };

    private static string RecommendationColour(string? r) => r switch
    {
        "Approve" => Accent,
        "ConditionalApprove" => "#b45309",
        "Decline" => "#b91c1c",
        _ => "#4a5568",
    };

    private static string CapacityColour(string? c) => c switch
    {
        "Strong" or "Adequate" => Accent,
        "Marginal" => "#b45309",
        "Insufficient" => "#b91c1c",
        _ => "#4a5568",
    };

    private static string? Spaced(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : System.Text.RegularExpressions.Regex.Replace(value, "(?<=[a-z])(?=[A-Z])", " ");

    private static DateTime Local(DateTime utc) => (utc.Kind == DateTimeKind.Local ? utc.ToUniversalTime() : utc) + Wat;
}

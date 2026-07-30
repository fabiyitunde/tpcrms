using CRMS.Application.OfferAcceptance.DTOs;
using CRMS.Application.OfferAcceptance.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CRMS.Infrastructure.Documents;

public class DisbursementMemoPdfGenerator : IDisbursementMemoPdfGenerator
{
    private const string Primary    = BoaBrand.Primary;    // #14532d dark green
    private const string Accent     = BoaBrand.Accent;     // #1f7a3d mid green
    private const string Subtle     = BoaBrand.Subtle;     // #a7d3b5 soft green
    private const string PanelGreen = BoaBrand.PanelGreen; // #f0f4f1
    private const string LightGray  = BoaBrand.LightGray;  // #f7fafc
    private const string MedGray    = BoaBrand.MediumGray; // #e2e8f0
    private const string GreenOk    = BoaBrand.Accent;
    private const string Amber      = "#744210";
    private const string AmberBg    = "#fffbeb";
    private const string Red        = "#742a2a";

    public Task<byte[]> GenerateAsync(DisbursementMemoRequest request, CancellationToken ct = default)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(42);
                page.MarginVertical(34);
                page.DefaultTextStyle(x => x.FontSize(9.5f).FontFamily("Arial"));

                page.Header().Element(c => ComposeHeader(c, request));
                page.Content().Element(c => ComposeContent(c, request));
                page.Footer().Element(c => ComposeFooter(c, request));
            });
        });

        return Task.FromResult(document.GeneratePdf());
    }

    // ── Header ───────────────────────────────────────────────────────────────

    private static void ComposeHeader(IContainer container, DisbursementMemoRequest data)
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
                        .Text("Corporate Loan Division")
                        .FontSize(8.5f).FontColor(Colors.Grey.Darken2);
                    title.Item().AlignCenter().PaddingTop(1)
                        .Text("Pre-Disbursement Conditions Clearance")
                        .FontSize(7.5f).Italic().FontColor(Color.FromHex(Accent));
                });

                row.ConstantItem(110).AlignMiddle().Column(meta =>
                {
                    meta.Item().AlignRight()
                        .Text($"Date:  {data.OfferAcceptedAt:dd MMM yyyy}")
                        .FontSize(8).FontColor(Colors.Grey.Darken2);
                    meta.Item().AlignRight().PaddingTop(2)
                        .Text($"Ref:  {data.ApplicationNumber}")
                        .FontSize(8).Bold().FontColor(Color.FromHex(Primary));
                });
            });

            col.Item().PaddingTop(8).LineHorizontal(2.5f).LineColor(Color.FromHex(Primary));
            col.Item().PaddingTop(5).AlignCenter()
                .Text("DISBURSEMENT MEMO")
                .Bold().FontSize(16).FontColor(Color.FromHex(Primary)).LetterSpacing(0.04f);
            col.Item().PaddingTop(3).LineHorizontal(1).LineColor(Color.FromHex(Subtle));
        });
    }

    // ── Content ──────────────────────────────────────────────────────────────

    private static void ComposeContent(IContainer container, DisbursementMemoRequest data)
    {
        var cpItems = data.ChecklistItems.Where(i => i.ConditionType == "Precedent").ToList();
        var csItems = data.ChecklistItems.Where(i => i.ConditionType == "Subsequent").ToList();

        container.PaddingTop(12).Column(col =>
        {
            // Loan summary panel
            col.Item().PaddingBottom(12).Column(section =>
            {
                SectionHeader(section, "LOAN SUMMARY");
                section.Item().PaddingTop(1).Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(1.2f);
                        c.RelativeColumn(1.8f);
                        c.RelativeColumn(1.2f);
                        c.RelativeColumn(1.8f);
                    });

                    SummaryRow(t, "Customer", data.CustomerName,        "Application No.", data.ApplicationNumber, true);
                    SummaryRow(t, "Approved Amount", $"{data.ApprovedAmount:N2}", "Tenor", $"{data.ApprovedTenorMonths} months", false);
                    SummaryRow(t, "Interest Rate p.a.", $"{data.ApprovedInterestRate:F2}%", "Offer Issued", $"{data.OfferIssuedAt:dd MMM yyyy}", true);
                    SummaryRow(t, "Accepted By", data.AcceptedByUserName, "Accepted On", $"{data.OfferAcceptedAt:dd MMM yyyy}", false);
                });
            });

            // Conditions Precedent
            col.Item().PaddingBottom(12).Column(section =>
            {
                SectionHeader(section, "CONDITIONS PRECEDENT (CP)");
                section.Item().PaddingTop(2).PaddingBottom(4)
                    .Text("All mandatory CP items must be resolved before disbursement.")
                    .FontSize(8.5f).Italic().FontColor(Colors.Grey.Darken1);
                section.Item().Element(c => ComposeChecklistTable(c, cpItems));
            });

            // Conditions Subsequent
            if (csItems.Any())
            {
                col.Item().PaddingBottom(12).Column(section =>
                {
                    SectionHeader(section, "CONDITIONS SUBSEQUENT (CS)");
                    section.Item().PaddingTop(2).PaddingBottom(4)
                        .Text("CS items are monitored post-disbursement. Due dates are confirmed at disbursement.")
                        .FontSize(8.5f).Italic().FontColor(Colors.Grey.Darken1);
                    section.Item().Element(c => ComposeChecklistTable(c, csItems));
                });
            }

            // Certification block
            col.Item().PaddingTop(6).Column(section =>
            {
                SectionHeader(section, "CERTIFICATION");
                section.Item().PaddingTop(10).PaddingHorizontal(2)
                    .Text($"I, {data.AcceptedByUserName}, hereby certify that all Conditions Precedent for " +
                          $"the above-referenced facility have been satisfactorily resolved or waived in " +
                          $"accordance with the bank's credit policy, and the customer has formally accepted " +
                          $"the offer. This memo is issued for disbursement processing.")
                    .FontSize(9.5f).LineHeight(1.5f);

                section.Item().PaddingTop(24).Row(row =>
                {
                    SignatureBlock(row, data.AcceptedByUserName, "Operations Officer", data.OfferAcceptedAt.ToString("dd MMM yyyy"));
                    row.ConstantItem(60);
                    SignatureBlock(row, "________________________", "Branch Authorised Signatory", "Date: _______________");
                    row.ConstantItem(60);
                    SignatureBlock(row, "________________________", "Head of Credit / GM Finance", "Date: _______________");
                });
            });
        });
    }

    // ── Footer ───────────────────────────────────────────────────────────────

    private static void ComposeFooter(IContainer container, DisbursementMemoRequest data)
    {
        container.Column(col =>
        {
            col.Item().LineHorizontal(1).LineColor(Color.FromHex(Subtle));
            col.Item().PaddingTop(3).Row(row =>
            {
                row.RelativeItem()
                    .Text($"Ref: {data.ApplicationNumber}  |  DISBURSEMENT MEMO")
                    .FontSize(7).FontColor(Colors.Grey.Darken1);
                row.RelativeItem().AlignCenter()
                    .DefaultTextStyle(x => x.FontSize(7).FontColor(Colors.Grey.Darken1))
                    .Text(t => { t.Span("Page "); t.CurrentPageNumber(); t.Span(" of "); t.TotalPages(); });
                row.RelativeItem().AlignRight()
                    .Text(data.BankName).FontSize(7).FontColor(Colors.Grey.Darken1);
            });
            col.Item().PaddingTop(3).AlignCenter()
                .Text("CONFIDENTIAL — FOR INTERNAL USE ONLY")
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

    private static void SummaryRow(TableDescriptor t,
        string label1, string value1,
        string label2, string value2,
        bool shaded)
    {
        var bg = shaded ? Color.FromHex(PanelGreen) : Colors.White;
        t.Cell().Background(bg).BorderBottom(1).BorderColor(Color.FromHex(MedGray))
            .PaddingHorizontal(10).PaddingVertical(7)
            .Text(label1).Bold().FontSize(9f).FontColor(Color.FromHex(Primary));
        t.Cell().Background(bg).BorderBottom(1).BorderColor(Color.FromHex(MedGray))
            .PaddingHorizontal(10).PaddingVertical(7)
            .Text(value1).FontSize(9f);
        t.Cell().Background(bg).BorderBottom(1).BorderColor(Color.FromHex(MedGray))
            .PaddingHorizontal(10).PaddingVertical(7)
            .Text(label2).Bold().FontSize(9f).FontColor(Color.FromHex(Primary));
        t.Cell().Background(bg).BorderBottom(1).BorderColor(Color.FromHex(MedGray))
            .PaddingHorizontal(10).PaddingVertical(7)
            .Text(value2).FontSize(9f);
    }

    private static void ComposeChecklistTable(IContainer container, List<DisbursementChecklistItemDto> items)
    {
        if (!items.Any())
        {
            container.PaddingLeft(10).Text("No items.").FontSize(9).FontColor(Colors.Grey.Darken1);
            return;
        }

        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(22);   // #
                cols.RelativeColumn(3);    // Item
                cols.ConstantColumn(60);   // Mandatory
                cols.RelativeColumn(1.4f); // Status
                cols.RelativeColumn(1.8f); // Actioned by
                cols.ConstantColumn(72);   // Date
            });

            // Column header row
            table.Header(h =>
            {
                foreach (var label in new[] { "#", "Item", "Mandatory", "Status", "Actioned By", "Date / Due" })
                {
                    h.Cell().Background(Color.FromHex(Accent))
                        .PaddingHorizontal(6).PaddingVertical(5)
                        .Text(label).Bold().FontSize(8.5f).FontColor(Colors.White);
                }
            });

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var rowBg = i % 2 == 0 ? Colors.White : Color.FromHex(PanelGreen);

                var (statusColor, statusLabel) = item.Status switch
                {
                    "Satisfied"        => (GreenOk, "Satisfied"),
                    "Waived"           => (Amber,   "Waived"),
                    "PendingLegalReview" => (Amber,  "Legal Review"),
                    "LegalReturned"    => (Red,     "Returned"),
                    "WaiverPending"    => (Amber,   "Waiver Pending"),
                    "Overdue"          => (Red,     "Overdue"),
                    "ExtensionPending" => (Amber,   "Ext. Pending"),
                    _                  => (Primary, item.Status)
                };

                var actionedBy = item.Status == "Waived"
                    ? (item.WaiverApprovedByUserName ?? item.WaiverProposedByUserName ?? "—")
                    : (item.SatisfiedByUserName ?? "—");

                var dateValue = item.Status == "Waived"
                    ? item.WaiverRatifiedAt?.ToString("dd MMM yyyy") ?? "—"
                    : item.Status == "Satisfied"
                        ? item.SatisfiedAt?.ToString("dd MMM yyyy") ?? "—"
                        : item.DueDate?.ToString("dd MMM yyyy") ?? "—";

                void Cell(Action<IContainer> render) { }

                table.Cell().Background(rowBg).BorderBottom(1).BorderColor(Color.FromHex(MedGray))
                    .PaddingHorizontal(6).PaddingVertical(5)
                    .Text($"{i + 1}").FontSize(8.5f).FontColor(Colors.Grey.Darken2);

                table.Cell().Background(rowBg).BorderBottom(1).BorderColor(Color.FromHex(MedGray))
                    .PaddingHorizontal(6).PaddingVertical(5).Column(c =>
                    {
                        c.Item().Text(item.ItemName).FontSize(8.5f);
                        if (!string.IsNullOrWhiteSpace(item.WaiverReason))
                            c.Item().PaddingTop(1)
                                .Text($"Waiver: {item.WaiverReason}")
                                .FontSize(7.5f).Italic().FontColor(Colors.Grey.Darken1);
                    });

                table.Cell().Background(rowBg).BorderBottom(1).BorderColor(Color.FromHex(MedGray))
                    .PaddingHorizontal(6).PaddingVertical(5)
                    .Text(item.IsMandatory ? "Yes" : "No").FontSize(8.5f);

                table.Cell().Background(rowBg).BorderBottom(1).BorderColor(Color.FromHex(MedGray))
                    .PaddingHorizontal(6).PaddingVertical(5)
                    .Text(statusLabel).Bold().FontSize(8.5f).FontColor(Color.FromHex(statusColor));

                table.Cell().Background(rowBg).BorderBottom(1).BorderColor(Color.FromHex(MedGray))
                    .PaddingHorizontal(6).PaddingVertical(5)
                    .Text(actionedBy).FontSize(8.5f);

                table.Cell().Background(rowBg).BorderBottom(1).BorderColor(Color.FromHex(MedGray))
                    .PaddingHorizontal(6).PaddingVertical(5)
                    .Text(dateValue).FontSize(8.5f);
            }
        });
    }

    private static void SignatureBlock(RowDescriptor row, string name, string role, string date)
    {
        row.RelativeItem().Column(c =>
        {
            c.Item().Height(36);
            c.Item().LineHorizontal(1).LineColor(Color.FromHex(Subtle));
            c.Item().PaddingTop(3).Text(name).FontSize(8.5f).Bold();
            c.Item().PaddingTop(2).Text(role).FontSize(8).FontColor(Colors.Grey.Darken1);
            c.Item().PaddingTop(10).Text(date).FontSize(8).FontColor(Colors.Grey.Darken1);
        });
    }
}

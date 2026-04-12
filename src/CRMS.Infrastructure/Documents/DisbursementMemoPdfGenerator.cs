using CRMS.Application.OfferAcceptance.DTOs;
using CRMS.Application.OfferAcceptance.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CRMS.Infrastructure.Documents;

/// <summary>
/// Generates the Disbursement Memo PDF at OfferAccepted stage.
/// Summarises CP items (satisfied/waived) and CS items (with due dates).
/// Serves as official pre-disbursement clearance document for audit/CBN compliance.
/// </summary>
public class DisbursementMemoPdfGenerator : IDisbursementMemoPdfGenerator
{
    private const string DarkBlue = "#1a365d";
    private const string MediumGray = "#e2e8f0";
    private const string LightGray = "#f7fafc";
    private const string Green = "#276749";
    private const string Amber = "#744210";
    private const string Red = "#742a2a";

    public Task<byte[]> GenerateAsync(DisbursementMemoRequest request, CancellationToken ct = default)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(9.5f));

                page.Header().Element(c => ComposeHeader(c, request));
                page.Content().Element(c => ComposeContent(c, request));
                page.Footer().Element(c => ComposeFooter(c));
            });
        });

        var bytes = document.GeneratePdf();
        return Task.FromResult(bytes);
    }

    private void ComposeHeader(IContainer container, DisbursementMemoRequest data)
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
                    titleCol.Item().AlignCenter().Text("DISBURSEMENT MEMO")
                        .FontSize(11).Bold().FontColor(Colors.Grey.Darken2);
                    titleCol.Item().AlignCenter().Text("Pre-Disbursement Conditions Clearance")
                        .FontSize(9).FontColor(Colors.Grey.Darken1);
                });

                row.RelativeItem().AlignRight().Column(refCol =>
                {
                    refCol.Item().AlignRight().Text($"Date: {data.OfferAcceptedAt:dd-MMM-yyyy}").FontSize(9);
                    refCol.Item().AlignRight().Text($"Ref: {data.ApplicationNumber}").FontSize(9).Bold();
                });
            });

            col.Item().PaddingTop(8).LineHorizontal(2).LineColor(Color.FromHex(DarkBlue));
        });
    }

    private void ComposeContent(IContainer container, DisbursementMemoRequest data)
    {
        var cpItems = data.ChecklistItems.Where(i => i.ConditionType == "Precedent").ToList();
        var csItems = data.ChecklistItems.Where(i => i.ConditionType == "Subsequent").ToList();

        container.Column(col =>
        {
            col.Spacing(10);

            // Loan summary box
            col.Item().PaddingTop(10).Background(Color.FromHex(LightGray))
                .Border(1).BorderColor(Color.FromHex(MediumGray))
                .Padding(10).Column(summary =>
            {
                summary.Item().Text("LOAN SUMMARY").Bold().FontSize(10).FontColor(Color.FromHex(DarkBlue));
                summary.Item().PaddingTop(6).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn();
                        cols.RelativeColumn();
                        cols.RelativeColumn();
                        cols.RelativeColumn();
                    });

                    table.Cell().Text("Customer:").Bold();
                    table.Cell().Text(data.CustomerName);
                    table.Cell().Text("Application No:").Bold();
                    table.Cell().Text(data.ApplicationNumber);

                    table.Cell().Text("Approved Amount:").Bold();
                    table.Cell().Text($"{data.ApprovedAmount:N2}");
                    table.Cell().Text("Tenor:").Bold();
                    table.Cell().Text($"{data.ApprovedTenorMonths} months");

                    table.Cell().Text("Interest Rate p.a.:").Bold();
                    table.Cell().Text($"{data.ApprovedInterestRate:F2}%");
                    table.Cell().Text("Offer Issued:").Bold();
                    table.Cell().Text($"{data.OfferIssuedAt:dd-MMM-yyyy}");

                    table.Cell().Text("Accepted By:").Bold();
                    table.Cell().Text(data.AcceptedByUserName);
                    table.Cell().Text("Accepted On:").Bold();
                    table.Cell().Text($"{data.OfferAcceptedAt:dd-MMM-yyyy}");
                });
            });

            // Conditions Precedent section
            col.Item().Column(section =>
            {
                section.Item().Text("CONDITIONS PRECEDENT (CP)")
                    .Bold().FontSize(10).FontColor(Color.FromHex(DarkBlue));
                section.Item().Text("All mandatory CP items must be resolved before disbursement.")
                    .FontSize(8.5f).FontColor(Colors.Grey.Darken1);
                section.Item().PaddingTop(4).Element(c => ComposeChecklistTable(c, cpItems));
            });

            // Conditions Subsequent section
            if (csItems.Any())
            {
                col.Item().Column(section =>
                {
                    section.Item().Text("CONDITIONS SUBSEQUENT (CS)")
                        .Bold().FontSize(10).FontColor(Color.FromHex(DarkBlue));
                    section.Item().Text("CS items are monitored post-disbursement. Due dates are set at disbursement.")
                        .FontSize(8.5f).FontColor(Colors.Grey.Darken1);
                    section.Item().PaddingTop(4).Element(c => ComposeChecklistTable(c, csItems));
                });
            }

            // Certification
            col.Item().PaddingTop(10).Border(1).BorderColor(Color.FromHex(MediumGray))
                .Padding(10).Column(cert =>
            {
                cert.Item().Text("CERTIFICATION").Bold().FontSize(10).FontColor(Color.FromHex(DarkBlue));
                cert.Item().PaddingTop(6).Text(
                    $"I, {data.AcceptedByUserName}, hereby certify that all Conditions Precedent for " +
                    $"the above-referenced facility have been satisfactorily resolved/waived in accordance " +
                    $"with the bank's credit policy, and the customer has formally accepted the offer. " +
                    $"This memo is issued for disbursement processing.");
                cert.Item().PaddingTop(12).Row(row =>
                {
                    row.RelativeItem().Column(sig =>
                    {
                        sig.Item().LineHorizontal(1).LineColor(Colors.Black);
                        sig.Item().PaddingTop(2).Text($"{data.AcceptedByUserName}").Bold();
                        sig.Item().Text("Operations Officer").FontColor(Colors.Grey.Darken1);
                        sig.Item().Text($"Date: {data.OfferAcceptedAt:dd-MMM-yyyy}");
                    });
                    row.ConstantItem(80);
                    row.RelativeItem().Column(sig =>
                    {
                        sig.Item().LineHorizontal(1).LineColor(Colors.Black);
                        sig.Item().PaddingTop(2).Text("_____________________").Bold();
                        sig.Item().Text("Authorised Signatory").FontColor(Colors.Grey.Darken1);
                        sig.Item().Text("Date: _______________");
                    });
                });
            });
        });
    }

    private void ComposeChecklistTable(IContainer container, List<DisbursementChecklistItemDto> items)
    {
        if (!items.Any())
        {
            container.Text("No items.").FontColor(Colors.Grey.Darken1);
            return;
        }

        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(20);  // #
                cols.RelativeColumn(3);   // Item name
                cols.ConstantColumn(55);  // Mandatory
                cols.RelativeColumn(1.5f); // Status
                cols.RelativeColumn(2);   // Satisfied by / Waived by
                cols.ConstantColumn(70);  // Date / Due date
            });

            // Header row
            table.Header(header =>
            {
                header.Cell().Background(Color.FromHex(DarkBlue)).Padding(4)
                    .Text("#").Bold().FontColor(Colors.White).FontSize(8.5f);
                header.Cell().Background(Color.FromHex(DarkBlue)).Padding(4)
                    .Text("Item").Bold().FontColor(Colors.White).FontSize(8.5f);
                header.Cell().Background(Color.FromHex(DarkBlue)).Padding(4)
                    .Text("Mandatory").Bold().FontColor(Colors.White).FontSize(8.5f);
                header.Cell().Background(Color.FromHex(DarkBlue)).Padding(4)
                    .Text("Status").Bold().FontColor(Colors.White).FontSize(8.5f);
                header.Cell().Background(Color.FromHex(DarkBlue)).Padding(4)
                    .Text("Actioned By").Bold().FontColor(Colors.White).FontSize(8.5f);
                header.Cell().Background(Color.FromHex(DarkBlue)).Padding(4)
                    .Text("Date / Due").Bold().FontColor(Colors.White).FontSize(8.5f);
            });

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var rowBg = i % 2 == 0 ? Colors.White : Color.FromHex(LightGray);

                var (statusColor, statusLabel) = item.Status switch
                {
                    "Satisfied" => (Green, "Satisfied"),
                    "Waived" => (Amber, "Waived"),
                    "PendingLegalReview" => (Amber, "Legal Review"),
                    "LegalReturned" => (Red, "Returned"),
                    "WaiverPending" => (Amber, "Waiver Pending"),
                    "Overdue" => (Red, "Overdue"),
                    "ExtensionPending" => (Amber, "Ext. Pending"),
                    _ => (DarkBlue, item.Status)
                };

                var actionedBy = item.Status == "Waived"
                    ? (item.WaiverApprovedByUserName ?? item.WaiverProposedByUserName ?? "—")
                    : (item.SatisfiedByUserName ?? "—");

                var dateValue = item.Status == "Waived"
                    ? item.WaiverRatifiedAt?.ToString("dd-MMM-yyyy") ?? "—"
                    : item.Status == "Satisfied"
                        ? item.SatisfiedAt?.ToString("dd-MMM-yyyy") ?? "—"
                        : item.DueDate?.ToString("dd-MMM-yyyy") ?? "—";

                table.Cell().Background(rowBg).Padding(4).Text($"{i + 1}").FontSize(8.5f);
                table.Cell().Background(rowBg).Padding(4).Column(c =>
                {
                    c.Item().Text(item.ItemName).FontSize(8.5f);
                    if (!string.IsNullOrWhiteSpace(item.WaiverReason))
                        c.Item().Text($"Waiver: {item.WaiverReason}").FontSize(7.5f).FontColor(Colors.Grey.Darken1);
                });
                table.Cell().Background(rowBg).Padding(4)
                    .Text(item.IsMandatory ? "Yes" : "No").FontSize(8.5f);
                table.Cell().Background(rowBg).Padding(4)
                    .Text(statusLabel).FontSize(8.5f).FontColor(Color.FromHex(statusColor)).Bold();
                table.Cell().Background(rowBg).Padding(4).Text(actionedBy).FontSize(8.5f);
                table.Cell().Background(rowBg).Padding(4).Text(dateValue).FontSize(8.5f);
            }
        });
    }

    private void ComposeFooter(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Text("CONFIDENTIAL — FOR INTERNAL USE ONLY")
                .FontSize(7).FontColor(Colors.Grey.Darken1);
            row.RelativeItem().AlignRight().Text(x =>
            {
                x.Span("Page ").FontSize(7).FontColor(Colors.Grey.Darken1);
                x.CurrentPageNumber().FontSize(7).FontColor(Colors.Grey.Darken1);
                x.Span(" of ").FontSize(7).FontColor(Colors.Grey.Darken1);
                x.TotalPages().FontSize(7).FontColor(Colors.Grey.Darken1);
            });
        });
    }
}

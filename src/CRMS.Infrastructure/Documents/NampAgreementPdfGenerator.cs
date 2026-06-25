using CRMS.Application.Namp.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CRMS.Infrastructure.Documents;

public class NampLeaseAgreementPdfGenerator : INampLeaseAgreementPdfGenerator
{
    public Task<byte[]> GenerateAsync(NampAgreementDocumentData data, CancellationToken ct = default) =>
        Task.FromResult(AgreementPdfHelper.GenerateAgreementPdf(data));
}

public class NampGpsConsentFormPdfGenerator : INampGpsConsentFormPdfGenerator
{
    public Task<byte[]> GenerateAsync(NampAgreementDocumentData data, CancellationToken ct = default) =>
        Task.FromResult(AgreementPdfHelper.GenerateAgreementPdf(data));
}

file static class AgreementPdfHelper
{
    internal static byte[] GenerateAgreementPdf(NampAgreementDocumentData data)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(45);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                page.Header().Element(c => ComposeHeader(c, data));
                page.Content().Element(c => ComposeContent(c, data));
                page.Footer().Element(c => ComposeFooter(c, data));
            });
        }).GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, NampAgreementDocumentData data)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Element(c => BoaBrand.RenderLogo(c, 60));

                row.RelativeItem(3).AlignCenter().Column(title =>
                {
                    title.Item().AlignCenter().Text(data.BankName.ToUpperInvariant())
                        .Bold().FontSize(13).FontColor(Color.FromHex(BoaBrand.Primary));
                    title.Item().AlignCenter().Text(data.BranchName)
                        .FontSize(9).FontColor(Colors.Grey.Darken1);
                    title.Item().AlignCenter().PaddingTop(2)
                        .Text("National Agricultural Mechanisation Programme (NAMP)")
                        .FontSize(9).Italic().FontColor(Color.FromHex(BoaBrand.Accent));
                });

                row.RelativeItem().AlignRight().Column(ref_ =>
                {
                    ref_.Item().AlignRight().Text($"Date: {data.GeneratedDate:dd-MMM-yyyy}").FontSize(9);
                    ref_.Item().AlignRight().Text($"Ref: {data.ApplicationNumber}").FontSize(9).Bold();
                });
            });

            col.Item().PaddingTop(8).LineHorizontal(2).LineColor(Color.FromHex(BoaBrand.Primary));
            col.Item().PaddingTop(6).AlignCenter()
                .Text(data.DocumentTitle.ToUpperInvariant())
                .Bold().FontSize(15).FontColor(Color.FromHex(BoaBrand.Primary));
            col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Color.FromHex(BoaBrand.MediumGray));
        });
    }

    private static void ComposeContent(IContainer container, NampAgreementDocumentData data)
    {
        container.PaddingVertical(10).Column(col =>
        {
            // Parties block
            col.Item().PaddingBottom(10).Column(c =>
            {
                c.Item().Text("PARTIES").Bold().FontSize(11).FontColor(Color.FromHex(BoaBrand.Primary));
                c.Item().PaddingTop(5).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(1);
                        cols.RelativeColumn(2);
                    });

                    PartyRow(table, "Lender", data.BankName);
                    PartyRow(table, "Borrower / Applicant", data.ApplicantName);
                    PartyRow(table, "BOA Account Number", data.BoaAccountNumber);
                    PartyRow(table, "Equipment", data.EquipmentDescription);
                    if (data.LoanAmount.HasValue)
                        PartyRow(table, "Facility Amount", $"NGN {data.LoanAmount:N2}");
                    if (data.TenorMonths.HasValue)
                        PartyRow(table, "Tenor", $"{data.TenorMonths} months");
                    PartyRow(table, "Interest Rate", $"{data.InterestRatePerAnnum:N2}% per annum");
                    if (data.MonthlyInstallment > 0)
                        PartyRow(table, "Monthly Installment", $"NGN {data.MonthlyInstallment:N2}");
                });
            });

            // Template body — render each paragraph
            col.Item().PaddingTop(5).Column(body =>
            {
                var paragraphs = data.RenderedBody
                    .Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var paragraph in paragraphs)
                {
                    var trimmed = paragraph.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed)) continue;

                    // Section headings: lines that are ALL CAPS and short
                    if (trimmed == trimmed.ToUpperInvariant() && trimmed.Length < 80 && !trimmed.Contains('.'))
                    {
                        body.Item().PaddingTop(10).PaddingBottom(3)
                            .Text(trimmed).Bold().FontSize(11).FontColor(Color.FromHex(BoaBrand.Primary));
                    }
                    else
                    {
                        body.Item().PaddingBottom(6).Text(trimmed).FontSize(10);
                    }
                }
            });

            // Signature block
            col.Item().PaddingTop(20).Column(sig =>
            {
                sig.Item().Text("EXECUTION").Bold().FontSize(11).FontColor(Color.FromHex(BoaBrand.Primary));
                sig.Item().PaddingTop(5).Text(
                    "IN WITNESS WHEREOF, the parties have executed this Agreement as of the date first written above.")
                    .FontSize(10);

                sig.Item().PaddingTop(20).Row(row =>
                {
                    row.RelativeItem().Column(left =>
                    {
                        left.Item().PaddingBottom(35).Text("");
                        left.Item().LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                        left.Item().PaddingTop(3).Text("Borrower Signature").FontSize(9).Bold();
                        left.Item().PaddingTop(12);
                        left.Item().LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                        left.Item().PaddingTop(3).Text("Name").FontSize(9);
                        left.Item().PaddingTop(12);
                        left.Item().LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                        left.Item().PaddingTop(3).Text("Date").FontSize(9);
                    });

                    row.ConstantItem(40);

                    row.RelativeItem().Column(right =>
                    {
                        right.Item().PaddingBottom(35).Text("");
                        right.Item().LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                        right.Item().PaddingTop(3).Text("For Bank of Agriculture Ltd.").FontSize(9).Bold();
                        right.Item().PaddingTop(12);
                        right.Item().LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                        right.Item().PaddingTop(3).Text("Name / Designation").FontSize(9);
                        right.Item().PaddingTop(12);
                        right.Item().LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                        right.Item().PaddingTop(3).Text("Date").FontSize(9);
                    });
                });
            });
        });
    }

    private static void ComposeFooter(IContainer container, NampAgreementDocumentData data)
    {
        container.Column(col =>
        {
            col.Item().LineHorizontal(1).LineColor(Color.FromHex(BoaBrand.MediumGray));
            col.Item().PaddingTop(4).Row(row =>
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

    private static void PartyRow(TableDescriptor table, string label, string value)
    {
        table.Cell().BorderBottom(1).BorderColor(Color.FromHex(BoaBrand.MediumGray))
            .Background(Color.FromHex(BoaBrand.LightGray)).Padding(6)
            .Text(label).Bold().FontSize(10);
        table.Cell().BorderBottom(1).BorderColor(Color.FromHex(BoaBrand.MediumGray))
            .Padding(6).Text(value).FontSize(10);
    }
}


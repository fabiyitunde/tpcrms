using CRMS.Application.LoanPack.DTOs;
using CRMS.Application.LoanPack.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CRMS.Infrastructure.Documents;

public class LoanPackPdfGenerator : ILoanPackGenerator
{
    private const string Primary    = BoaBrand.Primary;    // #14532d dark green
    private const string Accent     = BoaBrand.Accent;     // #1f7a3d mid green
    private const string Subtle     = BoaBrand.Subtle;     // #a7d3b5 soft green
    private const string PanelGreen = BoaBrand.PanelGreen; // #f0f4f1
    private const string MedGray    = BoaBrand.MediumGray; // #e2e8f0

    public Task<byte[]> GenerateAsync(LoanPackData data, CancellationToken ct = default)
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

                page.Header().Element(c => ComposeHeader(c, data));
                page.Content().Element(c => ComposeContent(c, data));
                page.Footer().Element(c => ComposeFooter(c, data));
            });
        });

        return Task.FromResult(document.GeneratePdf());
    }

    // ── Header ───────────────────────────────────────────────────────────────

    private static void ComposeHeader(IContainer container, LoanPackData data)
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
                        .Text("BANK OF AGRICULTURE")
                        .Bold().FontSize(14).FontColor(Color.FromHex(Primary));
                    title.Item().AlignCenter()
                        .Text("LOAN APPLICATION PACK")
                        .FontSize(9).Bold().FontColor(Color.FromHex(Accent)).LetterSpacing(0.04f);
                    title.Item().AlignCenter().PaddingTop(1)
                        .Text("Confidential Credit Document")
                        .FontSize(7.5f).Italic().FontColor(Colors.Grey.Darken2);
                });

                row.ConstantItem(120).AlignMiddle().Column(meta =>
                {
                    meta.Item().AlignRight()
                        .Text($"Application:  {data.ApplicationNumber}")
                        .FontSize(8).Bold().FontColor(Color.FromHex(Primary));
                    meta.Item().AlignRight().PaddingTop(2)
                        .Text($"Generated:  {data.GeneratedAt:dd MMM yyyy HH:mm}")
                        .FontSize(7.5f).FontColor(Colors.Grey.Darken2);
                    meta.Item().AlignRight().PaddingTop(1)
                        .Text($"Version:  {data.Version}")
                        .FontSize(7.5f).FontColor(Colors.Grey.Darken2);
                });
            });

            col.Item().PaddingTop(8).LineHorizontal(2.5f).LineColor(Color.FromHex(Primary));
            col.Item().PaddingTop(3).LineHorizontal(1).LineColor(Color.FromHex(Subtle));
        });
    }

    // ── Content ──────────────────────────────────────────────────────────────

    private static void ComposeContent(IContainer container, LoanPackData data)
    {
        container.PaddingTop(8).Column(col =>
        {
            col.Item().Element(c => ComposeExecutiveSummary(c, data));
            col.Item().PageBreak();

            col.Item().Element(c => ComposeApplicationTimeline(c, data));
            col.Item().PageBreak();

            col.Item().Element(c => ComposeCustomerProfile(c, data));
            col.Item().PageBreak();

            if (data.Directors.Any() || data.Signatories.Any())
            {
                col.Item().Element(c => ComposeDirectorsAndSignatories(c, data));
                col.Item().PageBreak();
            }

            if (data.Documents.Any())
            {
                col.Item().Element(c => ComposeDocuments(c, data));
                col.Item().PageBreak();
            }

            if (data.BureauReports.Any())
            {
                col.Item().Element(c => ComposeBureauReports(c, data));
                col.Item().PageBreak();
            }

            if (data.FinancialStatements.Any())
            {
                col.Item().Element(c => ComposeFinancialAnalysis(c, data));
                col.Item().PageBreak();
            }

            if (data.CashflowAnalysis != null)
            {
                col.Item().Element(c => ComposeCashflowAnalysis(c, data));
                col.Item().PageBreak();
            }

            if (data.Collaterals.Any())
            {
                col.Item().Element(c => ComposeCollateral(c, data));
                col.Item().PageBreak();
            }

            if (data.Guarantors.Any())
            {
                col.Item().Element(c => ComposeGuarantors(c, data));
                col.Item().PageBreak();
            }

            if (data.AIAdvisory != null)
            {
                col.Item().Element(c => ComposeAIAdvisory(c, data));
                col.Item().PageBreak();
            }

            if (data.CreditOfficerNotes.Any())
            {
                col.Item().PageBreak();
                col.Item().Element(c => ComposeCreditOfficerNotes(c, data));
            }

            if (data.CommitteeComments.Any())
            {
                col.Item().PageBreak();
                col.Item().Element(c => ComposeCommitteeComments(c, data));
            }

            if (data.CommitteeDecision != null)
            {
                col.Item().PageBreak();
                col.Item().Element(c => ComposeCommitteeDecision(c, data));
            }

            if (data.ApprovalConditions.Any())
            {
                col.Item().PageBreak();
                col.Item().Element(c => ComposeConditionsOfApproval(c, data));
            }

            if (data.DisbursementChecklist.Any())
            {
                col.Item().PageBreak();
                col.Item().Element(c => ComposeDisbursementChecklist(c, data));
            }

            if (data.ApprovalAuditTrail.Any())
            {
                col.Item().PageBreak();
                col.Item().Element(c => ComposeApprovalAuditTrail(c, data));
            }

            if (data.WorkflowHistory.Any())
            {
                col.Item().PageBreak();
                col.Item().Element(c => ComposeWorkflowHistory(c, data));
            }
        });
    }

    // ── Section composers ─────────────────────────────────────────────────────

    private static void ComposeExecutiveSummary(IContainer container, LoanPackData data)
    {
        container.Column(col =>
        {
            SectionTitle(col, "EXECUTIVE SUMMARY");
            col.Item().PaddingTop(8).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(1); cols.RelativeColumn(2);
                    cols.RelativeColumn(1); cols.RelativeColumn(2);
                });

                DataRow(table, "Customer:", data.Customer.Name, "Application Date:", data.ApplicationDate.ToString("dd MMM yyyy"), true);
                DataRow(table, "Product:", $"{data.LoanProductName} ({data.LoanProductCode})", "Account Number:", data.Customer.AccountNumber, false);
                DataRow(table, "Requested Amount:", $"{data.Currency} {data.RequestedAmount:N2}", "Requested Tenor:", $"{data.RequestedTenorMonths} months", true);
                DataRow(table, "Interest Rate:", $"{data.RequestedInterestRate:N2}% p.a.", "Purpose:", data.Purpose, false);
                DataRow(table, "Current Status:", data.Timeline.CurrentStatus, "Application Type:", data.Timeline.ApplicationType, true);

                if (data.ApprovedAmount.HasValue || data.ApprovedTenorMonths.HasValue || data.ApprovedInterestRate.HasValue)
                {
                    DataRow(table, "Approved Amount:", data.ApprovedAmount.HasValue ? $"{data.Currency} {data.ApprovedAmount:N2}" : "—", "Approved Tenor:", data.ApprovedTenorMonths.HasValue ? $"{data.ApprovedTenorMonths} months" : "—", false);
                    DataRow(table, "Approved Rate:", data.ApprovedInterestRate.HasValue ? $"{data.ApprovedInterestRate:N2}%" : "—", "Committee Decision:", data.CommitteeDecision?.Decision ?? "—", true);
                }
            });

            col.Item().PaddingTop(14);
            SubSectionTitle(col, "KEY METRICS");
            col.Item().PaddingTop(6).Row(row =>
            {
                if (data.AIAdvisory != null)
                {
                    row.RelativeItem()
                        .Border(1.5f).BorderColor(Color.FromHex(MedGray))
                        .Background(Color.FromHex(PanelGreen))
                        .Padding(10).Column(c =>
                        {
                            c.Item().AlignCenter().Text("RISK SCORE").Bold().FontSize(8).FontColor(Color.FromHex(Primary));
                            c.Item().AlignCenter().Text(data.AIAdvisory.OverallRiskScore.ToString())
                                .FontSize(24).Bold().FontColor(GetRiskColor(data.AIAdvisory.RiskRating));
                            c.Item().AlignCenter().Text(data.AIAdvisory.RiskRating).FontSize(9);
                        });
                }

                row.RelativeItem()
                    .Border(1.5f).BorderColor(Color.FromHex(MedGray))
                    .Background(Color.FromHex(PanelGreen))
                    .Padding(10).Column(c =>
                    {
                        c.Item().AlignCenter().Text("COLLATERAL COVERAGE").Bold().FontSize(8).FontColor(Color.FromHex(Primary));
                        c.Item().AlignCenter().Text($"{data.CollateralCoverageRatio:P0}").FontSize(24).Bold().FontColor(Color.FromHex(Accent));
                        c.Item().AlignCenter().Text($"{data.Currency} {data.TotalCollateralValue:N0}").FontSize(9);
                    });

                row.RelativeItem()
                    .Border(1.5f).BorderColor(Color.FromHex(MedGray))
                    .Background(Color.FromHex(PanelGreen))
                    .Padding(10).Column(c =>
                    {
                        c.Item().AlignCenter().Text("BUREAU CHECKS").Bold().FontSize(8).FontColor(Color.FromHex(Primary));
                        var avgScore = data.BureauReports.Where(b => b.CreditScore.HasValue)
                            .Select(b => b.CreditScore!.Value).DefaultIfEmpty(0).Average();
                        c.Item().AlignCenter().Text($"{avgScore:N0}").FontSize(24).Bold().FontColor(Color.FromHex(Accent));
                        c.Item().AlignCenter().Text($"{data.BureauReports.Count} reports").FontSize(9);
                    });

                if (data.AIAdvisory != null)
                {
                    row.RelativeItem()
                        .Border(1.5f).BorderColor(Color.FromHex(Primary))
                        .Background(Color.FromHex(Primary))
                        .Padding(10).Column(c =>
                        {
                            c.Item().AlignCenter().Text("RECOMMENDED").Bold().FontSize(8).FontColor(Colors.White);
                            c.Item().AlignCenter().Text($"{data.Currency} {data.AIAdvisory.RecommendedAmount:N0}")
                                .FontSize(16).Bold().FontColor(Colors.White);
                            c.Item().AlignCenter()
                                .Text($"{data.AIAdvisory.RecommendedTenorMonths} months @ {data.AIAdvisory.RecommendedInterestRate:N2}%")
                                .FontSize(8).FontColor(Color.FromHex(Subtle));
                        });
                }
            });

            if (data.AIAdvisory?.RedFlags.Any() == true)
            {
                col.Item().PaddingTop(12)
                    .Background(Color.FromHex("#fff5f5"))
                    .Border(1).BorderColor(Color.FromHex("#fc8181"))
                    .PaddingHorizontal(10).PaddingVertical(8).Column(c =>
                    {
                        c.Item().Text("RED FLAGS").Bold().FontSize(9).FontColor(Color.FromHex("#c53030"));
                        foreach (var flag in data.AIAdvisory.RedFlags)
                            c.Item().PaddingTop(2).Text($"• {flag}").FontSize(9).FontColor(Color.FromHex("#c53030"));
                    });
            }

            if (data.AIAdvisory?.MitigatingFactors.Any() == true)
            {
                col.Item().PaddingTop(8)
                    .Background(Color.FromHex(PanelGreen))
                    .Border(1).BorderColor(Color.FromHex(Subtle))
                    .PaddingHorizontal(10).PaddingVertical(8).Column(c =>
                    {
                        c.Item().Text("MITIGATING FACTORS").Bold().FontSize(9).FontColor(Color.FromHex(Primary));
                        foreach (var factor in data.AIAdvisory.MitigatingFactors)
                            c.Item().PaddingTop(2).Text($"• {factor}").FontSize(9).FontColor(Color.FromHex(Accent));
                    });
            }
        });
    }

    private static void ComposeApplicationTimeline(IContainer container, LoanPackData data)
    {
        var tl = data.Timeline;

        container.Column(col =>
        {
            SectionTitle(col, "APPLICATION TIMELINE");
            col.Item().PaddingTop(4)
                .Text($"Current Status: {tl.CurrentStatus}  |  Type: {tl.ApplicationType}")
                .FontSize(9).FontColor(Colors.Grey.Darken2).Italic();
            col.Item().PaddingTop(8).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(2); cols.RelativeColumn(2);
                    cols.RelativeColumn(2); cols.RelativeColumn(2);
                });

                TableHeader(table, "Milestone"); TableHeader(table, "Date");
                TableHeader(table, "Milestone"); TableHeader(table, "Date");

                TimelineRow(table, "Application Created", data.ApplicationDate, true);
                TimelineRow(table, "Submitted", tl.SubmittedAt, false);
                TimelineRow(table, "Branch Approved", tl.BranchApprovedAt, true);
                TimelineRow(table, "Credit Check Started", tl.CreditCheckStartedAt, false);
                TimelineRow(table, "Credit Check Completed", tl.CreditCheckCompletedAt, true);
                TimelineRow(table, "Final Approved", tl.FinalApprovedAt, false);
                TimelineRow(table, "Offer Issued", tl.OfferIssuedAt, true);
                TimelineRow(table, "Offer Accepted", tl.OfferAcceptedAt, false);
                TimelineRow(table, "Customer Signed", tl.CustomerSignedAt, true);
                TimelineRow(table, "Disbursed", tl.DisbursedAt, false);
            });

            if (tl.OfferAcceptedAt.HasValue || tl.DisbursedAt.HasValue)
            {
                col.Item().PaddingTop(14);
                SubSectionTitle(col, "Offer & Disbursement Details");
                col.Item().PaddingTop(5).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(2); cols.RelativeColumn(3);
                        cols.RelativeColumn(2); cols.RelativeColumn(3);
                    });

                    DataRow(table, "Acceptance Method:", tl.AcceptanceMethod ?? "—", "KFS Acknowledged:", tl.KfsAcknowledged ? "Yes" : "No", true);
                    DataRow(table, "Core Banking Loan ID:", tl.CoreBankingLoanId ?? "—", "Disbursement Date:", tl.DisbursedAt.HasValue ? tl.DisbursedAt.Value.ToString("dd MMM yyyy") : "—", false);
                });
            }

            if (tl.SubmittedAt.HasValue && tl.FinalApprovedAt.HasValue)
            {
                var days = (int)(tl.FinalApprovedAt.Value - tl.SubmittedAt.Value).TotalDays;
                col.Item().PaddingTop(10)
                    .Background(Color.FromHex(PanelGreen))
                    .Border(1).BorderColor(Color.FromHex(Subtle))
                    .PaddingHorizontal(10).PaddingVertical(7)
                    .Text($"Processing time from submission to final approval: {days} day(s).")
                    .FontSize(9).FontColor(Color.FromHex(Primary));
            }
        });
    }

    private static void ComposeCustomerProfile(IContainer container, LoanPackData data)
    {
        container.Column(col =>
        {
            SectionTitle(col, "CUSTOMER PROFILE");
            col.Item().PaddingTop(8).Table(table =>
            {
                table.ColumnsDefinition(cols => { cols.RelativeColumn(1); cols.RelativeColumn(2); });

                LabelValue(table, "Company Name:", data.Customer.Name, true);
                LabelValue(table, "Registration Number:", data.Customer.RegistrationNumber, false);
                LabelValue(table, "Incorporation Date:", data.Customer.IncorporationDate?.ToString("dd MMM yyyy") ?? "N/A", true);
                LabelValue(table, "Industry:", data.Customer.Industry, false);
                LabelValue(table, "Sector:", string.IsNullOrWhiteSpace(data.Customer.Sector) ? "N/A" : data.Customer.Sector, true);
                LabelValue(table, "Address:", string.IsNullOrWhiteSpace(data.Customer.Address) ? "N/A" : data.Customer.Address, false);
                LabelValue(table, "Phone:", string.IsNullOrWhiteSpace(data.Customer.Phone) ? "N/A" : data.Customer.Phone, true);
                LabelValue(table, "Email:", string.IsNullOrWhiteSpace(data.Customer.Email) ? "N/A" : data.Customer.Email, false);
                LabelValue(table, "Account Number:", data.Customer.AccountNumber, true);
                LabelValue(table, "Account Type:", string.IsNullOrWhiteSpace(data.Customer.AccountType) ? "N/A" : data.Customer.AccountType, false);
                LabelValue(table, "Account Open Date:", data.Customer.AccountOpenDate?.ToString("dd MMM yyyy") ?? "N/A", true);
                LabelValue(table, "Avg Monthly Balance:", data.Customer.AverageMonthlyBalance.HasValue
                    ? $"{data.Currency} {data.Customer.AverageMonthlyBalance:N2}" : "N/A", false);
            });
        });
    }

    private static void ComposeDirectorsAndSignatories(IContainer container, LoanPackData data)
    {
        container.Column(col =>
        {
            SectionTitle(col, "DIRECTORS & SIGNATORIES");

            if (data.Directors.Any())
            {
                col.Item().PaddingTop(10);
                SubSectionTitle(col, "Directors — Credit Summary");
                col.Item().PaddingTop(5).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(2); cols.RelativeColumn(1); cols.RelativeColumn(1);
                        cols.RelativeColumn(1); cols.RelativeColumn(1); cols.RelativeColumn(2);
                    });

                    TableHeader(table, "Name"); TableHeader(table, "Position"); TableHeader(table, "Shareholding");
                    TableHeader(table, "Credit Score"); TableHeader(table, "Delinquencies"); TableHeader(table, "Bureau Summary");

                    for (var i = 0; i < data.Directors.Count; i++)
                    {
                        var d = data.Directors[i];
                        var bg = i % 2 == 0 ? Colors.White : Color.FromHex(PanelGreen);
                        DataCell(table, d.Name, bg); DataCell(table, d.Position, bg);
                        DataCell(table, d.ShareholdingPercentage.HasValue ? $"{d.ShareholdingPercentage:N1}%" : "N/A", bg);
                        var scoreText = d.CreditScore.HasValue
                            ? $"{d.CreditScore} ({d.CreditRating ?? "N/A"})" : "N/A";
                        DataCell(table, scoreText, bg);
                        DataCell(table, d.HasDelinquencies ? "Yes" : "No", bg);
                        DataCell(table, d.CreditSummary ?? "—", bg);
                    }
                });

                var directorsWithContact = data.Directors.Where(d =>
                    !string.IsNullOrWhiteSpace(d.BVN) ||
                    !string.IsNullOrWhiteSpace(d.Phone) ||
                    !string.IsNullOrWhiteSpace(d.Email)).ToList();
                if (directorsWithContact.Any())
                {
                    col.Item().PaddingTop(10);
                    SubSectionTitle(col, "Directors — Identity & Contact");
                    col.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(2); cols.RelativeColumn(2); cols.RelativeColumn(2); cols.RelativeColumn(3);
                        });
                        TableHeader(table, "Name"); TableHeader(table, "BVN"); TableHeader(table, "Phone"); TableHeader(table, "Email");
                        for (var i = 0; i < directorsWithContact.Count; i++)
                        {
                            var d = directorsWithContact[i];
                            var bg = i % 2 == 0 ? Colors.White : Color.FromHex(PanelGreen);
                            DataCell(table, d.Name, bg);
                            DataCell(table, string.IsNullOrWhiteSpace(d.BVN) ? "—" : d.BVN, bg);
                            DataCell(table, string.IsNullOrWhiteSpace(d.Phone) ? "—" : d.Phone, bg);
                            DataCell(table, string.IsNullOrWhiteSpace(d.Email) ? "—" : d.Email, bg);
                        }
                    });
                }
            }

            if (data.Signatories.Any())
            {
                col.Item().PaddingTop(14);
                SubSectionTitle(col, "Account Signatories — Credit Summary");
                col.Item().PaddingTop(5).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(2); cols.RelativeColumn(1); cols.RelativeColumn(1);
                        cols.RelativeColumn(1); cols.RelativeColumn(1);
                    });

                    TableHeader(table, "Name"); TableHeader(table, "Position"); TableHeader(table, "Class");
                    TableHeader(table, "Credit Score"); TableHeader(table, "Delinquencies");

                    for (var i = 0; i < data.Signatories.Count; i++)
                    {
                        var s = data.Signatories[i];
                        var bg = i % 2 == 0 ? Colors.White : Color.FromHex(PanelGreen);
                        DataCell(table, s.Name, bg); DataCell(table, s.Position, bg);
                        DataCell(table, s.SignatoryClass, bg);
                        var scoreText = s.CreditScore.HasValue
                            ? $"{s.CreditScore} ({s.CreditRating ?? "N/A"})" : "N/A";
                        DataCell(table, scoreText, bg);
                        DataCell(table, s.HasDelinquencies ? "Yes" : "No", bg);
                    }
                });

                var signatoriesWithContact = data.Signatories.Where(s =>
                    !string.IsNullOrWhiteSpace(s.BVN) ||
                    !string.IsNullOrWhiteSpace(s.Phone)).ToList();
                if (signatoriesWithContact.Any())
                {
                    col.Item().PaddingTop(10);
                    SubSectionTitle(col, "Signatories — Identity & Contact");
                    col.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(2); cols.RelativeColumn(2); cols.RelativeColumn(2);
                        });
                        TableHeader(table, "Name"); TableHeader(table, "BVN"); TableHeader(table, "Phone");
                        for (var i = 0; i < signatoriesWithContact.Count; i++)
                        {
                            var s = signatoriesWithContact[i];
                            var bg = i % 2 == 0 ? Colors.White : Color.FromHex(PanelGreen);
                            DataCell(table, s.Name, bg);
                            DataCell(table, string.IsNullOrWhiteSpace(s.BVN) ? "—" : s.BVN, bg);
                            DataCell(table, string.IsNullOrWhiteSpace(s.Phone) ? "—" : s.Phone, bg);
                        }
                    });
                }
            }
        });
    }

    private static void ComposeDocuments(IContainer container, LoanPackData data)
    {
        container.Column(col =>
        {
            SectionTitle(col, "SUPPORTING DOCUMENTS");
            col.Item().PaddingTop(3)
                .Text($"Total: {data.Documents.Count} document(s) attached to this application.")
                .FontSize(9).FontColor(Colors.Grey.Darken2).Italic();
            col.Item().PaddingTop(8).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(3); cols.RelativeColumn(2); cols.RelativeColumn(1);
                    cols.RelativeColumn(2); cols.RelativeColumn(3);
                });

                TableHeader(table, "File Name"); TableHeader(table, "Category"); TableHeader(table, "Status");
                TableHeader(table, "Uploaded"); TableHeader(table, "Description");

                for (var i = 0; i < data.Documents.Count; i++)
                {
                    var doc = data.Documents[i];
                    var bg = i % 2 == 0 ? Colors.White : Color.FromHex(PanelGreen);
                    DataCell(table, doc.FileName, bg); DataCell(table, doc.Category, bg);

                    var statusColor = doc.Status == "Verified" ? Color.FromHex(Accent)
                        : doc.Status == "Rejected" ? Color.FromHex("#c53030")
                        : Colors.Grey.Darken2;
                    table.Cell().Background(bg).BorderBottom(1).BorderColor(Color.FromHex(MedGray)).Padding(5)
                        .Text(doc.Status).FontSize(9).FontColor(statusColor).Bold();

                    DataCell(table, doc.UploadedAt.ToString("dd-MMM-yy"), bg);
                    DataCell(table, doc.Description ?? "", bg);
                }
            });
        });
    }

    private static void ComposeBureauReports(IContainer container, LoanPackData data)
    {
        container.Column(col =>
        {
            SectionTitle(col, "CREDIT BUREAU REPORTS");
            col.Item().PaddingTop(8);

            foreach (var report in data.BureauReports)
            {
                col.Item().Border(1.5f).BorderColor(Color.FromHex(MedGray)).Column(card =>
                {
                    card.Item().Background(Color.FromHex(Primary))
                        .PaddingHorizontal(10).PaddingVertical(6).Row(row =>
                        {
                            row.RelativeItem().Text($"{report.SubjectName} ({report.SubjectType})")
                                .Bold().FontSize(10).FontColor(Colors.White);
                            row.AutoItem().Text($"Score: {report.CreditScore?.ToString() ?? "N/A"}")
                                .Bold().FontSize(10).FontColor(Color.FromHex(Subtle));
                        });

                    card.Item().PaddingHorizontal(10).PaddingVertical(6).Column(c =>
                    {
                        c.Item().Text($"Bureau: {report.BureauProvider}  |  Report Date: {report.ReportDate:dd MMM yyyy}  |  Rating: {report.CreditRating ?? "N/A"}  |  Active Loans: {report.ActiveLoanCount}  |  Outstanding: {data.Currency} {report.TotalOutstandingDebt:N0}  |  Delinquent: {report.DelinquentAccountCount}")
                            .FontSize(8.5f).FontColor(Colors.Grey.Darken2);

                        if (report.HasLegalIssues)
                        {
                            c.Item().PaddingTop(4)
                                .Background(Color.FromHex("#fff5f5")).Border(1).BorderColor(Color.FromHex("#fc8181"))
                                .PaddingHorizontal(8).PaddingVertical(5)
                                .Text($"LEGAL ISSUES: {report.LegalIssueDetails}")
                                .FontSize(9).Bold().FontColor(Color.FromHex("#c53030"));
                        }

                        if (report.ActiveLoans.Any())
                        {
                            c.Item().PaddingTop(8).Text("Active Facilities").Bold().FontSize(9).FontColor(Color.FromHex(Primary));
                            c.Item().PaddingTop(3).Table(t =>
                            {
                                t.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(2); cols.RelativeColumn(1); cols.RelativeColumn(1);
                                    cols.RelativeColumn(1); cols.RelativeColumn(1); cols.RelativeColumn(1);
                                });
                                TableHeader(t, "Lender"); TableHeader(t, "Facility Type"); TableHeader(t, "Original");
                                TableHeader(t, "Outstanding"); TableHeader(t, "Maturity"); TableHeader(t, "Status");
                                for (var i = 0; i < Math.Min(10, report.ActiveLoans.Count); i++)
                                {
                                    var loan = report.ActiveLoans[i];
                                    var bg = i % 2 == 0 ? Colors.White : Color.FromHex(PanelGreen);
                                    DataCell(t, loan.LenderName, bg); DataCell(t, loan.FacilityType, bg);
                                    DataCell(t, $"{data.Currency} {loan.OriginalAmount:N0}", bg);
                                    DataCell(t, $"{data.Currency} {loan.OutstandingBalance:N0}", bg);
                                    DataCell(t, loan.MaturityDate.HasValue ? loan.MaturityDate.Value.ToString("dd-MMM-yy") : "—", bg);
                                    DataCell(t, loan.Status, bg);
                                }
                            });
                        }

                        if (report.Delinquencies.Any())
                        {
                            c.Item().PaddingTop(8).Text("Delinquent Accounts").Bold().FontSize(9).FontColor(Color.FromHex("#c53030"));
                            c.Item().PaddingTop(3).Table(t =>
                            {
                                t.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(2); cols.RelativeColumn(1); cols.RelativeColumn(1); cols.RelativeColumn(1);
                                });
                                TableHeader(t, "Lender"); TableHeader(t, "Facility Type"); TableHeader(t, "Amount"); TableHeader(t, "Days Overdue");
                                for (var i = 0; i < Math.Min(5, report.Delinquencies.Count); i++)
                                {
                                    var d = report.Delinquencies[i];
                                    var bg = i % 2 == 0 ? Colors.White : Color.FromHex(PanelGreen);
                                    DataCell(t, d.LenderName, bg); DataCell(t, d.FacilityType, bg);
                                    DataCell(t, $"{data.Currency} {d.Amount:N0}", bg); DataCell(t, d.DaysOverdue.ToString(), bg);
                                }
                            });
                        }
                    });
                });

                col.Item().PaddingTop(10);
            }
        });
    }

    private static void ComposeFinancialAnalysis(IContainer container, LoanPackData data)
    {
        container.Column(col =>
        {
            SectionTitle(col, "FINANCIAL ANALYSIS");
            col.Item().PaddingTop(8);
            SubSectionTitle(col, "Financial Statements");
            col.Item().PaddingTop(5).Table(table =>
            {
                var statements = data.FinancialStatements.OrderByDescending(f => f.Year).Take(3).ToList();
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(2);
                    foreach (var _ in statements) cols.RelativeColumn(1);
                });

                TableHeader(table, "Item");
                foreach (var stmt in statements)
                    TableHeader(table, $"{stmt.Year} ({stmt.StatementType})");

                AddFinancialRow(table, "Revenue", statements.Select(s => s.Revenue), data.Currency, true);
                AddFinancialRow(table, "Gross Profit", statements.Select(s => s.GrossProfit), data.Currency, false);
                AddFinancialRow(table, "Operating Profit", statements.Select(s => s.OperatingProfit), data.Currency, true);
                AddFinancialRow(table, "Net Profit", statements.Select(s => s.NetProfit), data.Currency, false);
                AddFinancialRow(table, "EBITDA", statements.Select(s => s.EBITDA), data.Currency, true);
                AddFinancialRow(table, "Total Assets", statements.Select(s => s.TotalAssets), data.Currency, false);
                AddFinancialRow(table, "Current Assets", statements.Select(s => s.CurrentAssets), data.Currency, true);
                AddFinancialRow(table, "Fixed Assets", statements.Select(s => s.FixedAssets), data.Currency, false);
                AddFinancialRow(table, "Total Liabilities", statements.Select(s => s.TotalLiabilities), data.Currency, true);
                AddFinancialRow(table, "Current Liabilities", statements.Select(s => s.CurrentLiabilities), data.Currency, false);
                AddFinancialRow(table, "Long-term Debt", statements.Select(s => s.LongTermDebt), data.Currency, true);
                AddFinancialRow(table, "Shareholders' Equity", statements.Select(s => s.ShareholdersEquity), data.Currency, false);
            });

            if (data.FinancialRatios != null)
            {
                col.Item().PaddingTop(14);
                SubSectionTitle(col, "Key Financial Ratios");
                col.Item().PaddingTop(6).Row(row =>
                {
                    RatioPanel(row, "Liquidity",
                        $"Current Ratio: {data.FinancialRatios.CurrentRatio:N2}x",
                        $"Quick Ratio: {data.FinancialRatios.QuickRatio:N2}x",
                        $"Cash Ratio: {data.FinancialRatios.CashRatio:N2}x");

                    RatioPanel(row, "Leverage",
                        $"Debt/Equity: {data.FinancialRatios.DebtToEquity:N2}x",
                        $"Debt/Assets: {data.FinancialRatios.DebtToAssets:N2}x",
                        $"Interest Coverage: {data.FinancialRatios.InterestCoverage:N2}x");

                    RatioPanel(row, "Profitability",
                        $"Gross Margin: {data.FinancialRatios.GrossMargin:P1}",
                        $"Operating Margin: {data.FinancialRatios.OperatingMargin:P1}",
                        $"Net Margin: {data.FinancialRatios.NetMargin:P1}",
                        $"ROE: {data.FinancialRatios.ReturnOnEquity:P1}",
                        $"ROA: {data.FinancialRatios.ReturnOnAssets:P1}");

                    var coverageLines = new List<string>
                    {
                        $"DSCR: {data.FinancialRatios.DebtServiceCoverageRatio:N2}x",
                        $"Asset Turnover: {data.FinancialRatios.AssetTurnover:N2}x"
                    };
                    if (data.FinancialRatios.InventoryTurnover.HasValue)
                        coverageLines.Add($"Inventory Turnover: {data.FinancialRatios.InventoryTurnover:N2}x");
                    if (data.FinancialRatios.ReceivablesDays.HasValue)
                        coverageLines.Add($"Receivables Days: {data.FinancialRatios.ReceivablesDays:N0} days");
                    if (data.FinancialRatios.PayablesDays.HasValue)
                        coverageLines.Add($"Payables Days: {data.FinancialRatios.PayablesDays:N0} days");
                    if (data.FinancialRatios.RevenueGrowthYoY.HasValue)
                        coverageLines.Add($"Revenue Growth (YoY): {data.FinancialRatios.RevenueGrowthYoY:N1}%");
                    if (data.FinancialRatios.ProfitGrowthYoY.HasValue)
                        coverageLines.Add($"Profit Growth (YoY): {data.FinancialRatios.ProfitGrowthYoY:N1}%");
                    RatioPanel(row, "Coverage & Efficiency", coverageLines.ToArray());
                });
            }
        });
    }

    private static void ComposeCashflowAnalysis(IContainer container, LoanPackData data)
    {
        var cf = data.CashflowAnalysis!;
        container.Column(col =>
        {
            SectionTitle(col, "CASHFLOW ANALYSIS");
            col.Item().PaddingTop(3)
                .Text($"Based on {cf.MonthsAnalyzed} months of bank statement data.")
                .FontSize(9).Italic().FontColor(Colors.Grey.Darken2);
            col.Item().PaddingTop(8).Row(row =>
            {
                CashflowPanel(row, "Monthly Averages",
                    ($"Avg Inflow", $"{data.Currency} {cf.AverageMonthlyInflow:N0}", false),
                    ($"Avg Outflow", $"{data.Currency} {cf.AverageMonthlyOutflow:N0}", false),
                    ($"Net Cashflow", $"{data.Currency} {cf.NetCashflow:N0}", cf.NetCashflow < 0));

                CashflowPanel(row, "Balance Analysis",
                    ("Avg Balance", $"{data.Currency} {cf.AverageBalance:N0}", false),
                    ("Lowest", $"{data.Currency} {cf.LowestMonthlyBalance:N0}", false),
                    ("Highest", $"{data.Currency} {cf.HighestMonthlyBalance:N0}", false));

                CashflowPanel(row, "Inflow Breakdown",
                    ("Salary", $"{data.Currency} {cf.SalaryInflows:N0}", false),
                    ("Business", $"{data.Currency} {cf.BusinessInflows:N0}", false),
                    ("Other", $"{data.Currency} {cf.OtherInflows:N0}", false));

                CashflowPanel(row, "Outflow Breakdown",
                    ("Loan Repayments", $"{data.Currency} {cf.LoanRepayments:N0}", false),
                    ("Rent/Utilities", $"{data.Currency} {cf.RentUtilities:N0}", false),
                    ("Salary Payments", $"{data.Currency} {cf.SalaryPayments:N0}", false),
                    ("Other", $"{data.Currency} {cf.OtherOutflows:N0}", false));
            });

            col.Item().PaddingTop(8).Row(row =>
            {
                CashflowPanel(row, "Quality Metrics",
                    ("Inflow Volatility", $"{cf.InflowVolatility:P1}", false),
                    ("Balance Volatility", $"{cf.BalanceVolatility:P1}", false),
                    ("Returned Cheques", $"{cf.ReturnedChequeCount}", false),
                    ("Overdraft Usage", $"{cf.OverdraftUtilization:P1}", false));

                row.RelativeItem()
                    .Border(1.5f).BorderColor(Color.FromHex(MedGray))
                    .Background(Color.FromHex(PanelGreen))
                    .Padding(12).Column(c =>
                    {
                        c.Item().Text("Trust Assessment").Bold().FontSize(9).FontColor(Color.FromHex(Primary));
                        var trustColor = cf.TrustLevel == "High" ? Color.FromHex(Accent)
                            : cf.TrustLevel == "Medium" ? Color.FromHex("#c05621")
                            : Color.FromHex("#c53030");
                        c.Item().PaddingTop(4).AlignCenter()
                            .Text(cf.TrustLevel).FontSize(20).Bold().FontColor(trustColor);
                        c.Item().AlignCenter()
                            .Text($"Weighted Score: {cf.TrustWeightedScore:N0}").FontSize(9);
                    });
            });
        });
    }

    private static void ComposeCollateral(IContainer container, LoanPackData data)
    {
        container.Column(col =>
        {
            SectionTitle(col, "COLLATERAL");
            col.Item().PaddingTop(3)
                .Text($"Total Acceptable Value: {data.Currency} {data.TotalCollateralValue:N0}  |  Coverage Ratio: {data.CollateralCoverageRatio:P0}")
                .Bold().FontSize(9.5f).FontColor(Color.FromHex(Primary));
            col.Item().PaddingTop(8).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(1); cols.RelativeColumn(2); cols.RelativeColumn(1);
                    cols.RelativeColumn(1); cols.RelativeColumn(1); cols.RelativeColumn(1);
                });

                TableHeader(table, "Type"); TableHeader(table, "Description"); TableHeader(table, "Market Value");
                TableHeader(table, "FSV"); TableHeader(table, "Acceptable"); TableHeader(table, "Status");

                for (var i = 0; i < data.Collaterals.Count; i++)
                {
                    var c = data.Collaterals[i];
                    var bg = i % 2 == 0 ? Colors.White : Color.FromHex(PanelGreen);
                    DataCell(table, c.Type, bg);
                    DataCell(table, string.IsNullOrWhiteSpace(c.Location) ? c.Description : $"{c.Description}\n{c.Location}", bg);
                    DataCell(table, $"{data.Currency} {c.MarketValue:N0}", bg);
                    DataCell(table, $"{data.Currency} {c.ForcedSaleValue:N0}", bg);
                    DataCell(table, $"{data.Currency} {c.AcceptableValue:N0}", bg);

                    var statusColor = c.Status is "Approved" or "Perfected" ? Color.FromHex(Accent)
                        : c.Status == "Rejected" ? Color.FromHex("#c53030")
                        : Colors.Grey.Darken2;
                    table.Cell().Background(bg).BorderBottom(1).BorderColor(Color.FromHex(MedGray)).Padding(5)
                        .Text(c.Status).FontSize(9).FontColor(statusColor).Bold();
                }
            });

            col.Item().PaddingTop(14);
            SubSectionTitle(col, "Collateral Detail");

            foreach (var c in data.Collaterals)
            {
                col.Item().PaddingTop(8).Border(1.5f).BorderColor(Color.FromHex(MedGray)).Column(card =>
                {
                    card.Item().Background(c.IsLegalCleared ? Color.FromHex(PanelGreen) : Color.FromHex("#fffbeb"))
                        .PaddingHorizontal(10).PaddingVertical(6).Row(row =>
                        {
                            row.RelativeItem().Text($"{c.Type} — {c.Description}").Bold().FontSize(9.5f);
                            var legalColor = c.IsLegalCleared ? Color.FromHex(Accent) : Color.FromHex("#c05621");
                            row.AutoItem().Text(c.IsLegalCleared ? "Legal Cleared ✓" : "Legal Clearance Pending")
                                .FontSize(8.5f).FontColor(legalColor).Bold();
                        });

                    card.Item().PaddingHorizontal(10).PaddingVertical(6).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(1); cols.RelativeColumn(2);
                            cols.RelativeColumn(1); cols.RelativeColumn(2);
                        });

                        DataRow(table, "Valuation Date:", string.IsNullOrWhiteSpace(c.ValuationDate) ? "Not yet valued" : c.ValuationDate, "Valuer:", string.IsNullOrWhiteSpace(c.ValuerName) ? "—" : c.ValuerName, true);
                        DataRow(table, "Lien Type:", string.IsNullOrWhiteSpace(c.LienType) ? "—" : c.LienType, "Lien Reference:", c.LienReference ?? "—", false);
                        DataRow(table, "Insurance Policy:", c.InsurancePolicy ?? "—", "Insurance Expiry:", c.InsuranceExpiry.HasValue ? c.InsuranceExpiry.Value.ToString("dd MMM yyyy") : "—", true);

                        if (c.IsLegalCleared && c.LegalClearedAt.HasValue)
                        {
                            LabelValue(table, "Legal Cleared At:", c.LegalClearedAt.Value.ToString("dd MMM yyyy HH:mm"), false);
                            table.Cell().Padding(5); table.Cell().Padding(5);
                        }
                    });
                });
            }
        });
    }

    private static void ComposeGuarantors(IContainer container, LoanPackData data)
    {
        container.Column(col =>
        {
            SectionTitle(col, "GUARANTORS");
            col.Item().PaddingTop(3)
                .Text($"Total Guarantee Amount: {data.Currency} {data.TotalGuaranteeAmount:N0}")
                .Bold().FontSize(9.5f).FontColor(Color.FromHex(Primary));
            col.Item().PaddingTop(8).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(2); cols.RelativeColumn(1); cols.RelativeColumn(1);
                    cols.RelativeColumn(1); cols.RelativeColumn(1); cols.RelativeColumn(1); cols.RelativeColumn(1); cols.RelativeColumn(1);
                });

                TableHeader(table, "Name"); TableHeader(table, "Type"); TableHeader(table, "Relationship");
                TableHeader(table, "Net Worth"); TableHeader(table, "Guarantee");
                TableHeader(table, "Credit Score"); TableHeader(table, "Rating"); TableHeader(table, "Status");

                for (var i = 0; i < data.Guarantors.Count; i++)
                {
                    var g = data.Guarantors[i];
                    var bg = i % 2 == 0 ? Colors.White : Color.FromHex(PanelGreen);
                    DataCell(table, g.Name, bg); DataCell(table, g.Type, bg); DataCell(table, g.Relationship, bg);
                    DataCell(table, $"{data.Currency} {g.NetWorth:N0}", bg);
                    DataCell(table, $"{data.Currency} {g.GuaranteeAmount:N0}", bg);
                    DataCell(table, g.CreditScore?.ToString() ?? "N/A", bg);
                    DataCell(table, g.CreditRating ?? "—", bg);
                    DataCell(table, g.Status, bg);
                }
            });

            var withAddress = data.Guarantors.Where(g => !string.IsNullOrWhiteSpace(g.Address) || !string.IsNullOrWhiteSpace(g.Phone)).ToList();
            if (withAddress.Any())
            {
                col.Item().PaddingTop(12);
                SubSectionTitle(col, "Guarantor Contact Details");
                col.Item().PaddingTop(5).Table(table =>
                {
                    table.ColumnsDefinition(cols => { cols.RelativeColumn(1); cols.RelativeColumn(3); });
                    foreach (var g in withAddress)
                    {
                        LabelValue(table, g.Name, $"Address: {g.Address}  |  Phone: {g.Phone}", false);
                    }
                });
            }
        });
    }

    private static void ComposeAIAdvisory(IContainer container, LoanPackData data)
    {
        var ai = data.AIAdvisory!;

        var recColor = ai.Recommendation switch
        {
            "Approve"            => Color.FromHex(Accent),
            "ConditionalApprove" => Color.FromHex("#c05621"),
            "Decline"            => Color.FromHex("#c53030"),
            _                    => Colors.Grey.Darken2
        };
        var recLabel = ai.Recommendation switch
        {
            "ConditionalApprove" => "Conditional Approve",
            _ => ai.Recommendation
        };

        container.Column(col =>
        {
            SectionTitle(col, "AI ADVISORY ASSESSMENT");

            // ── Hero banner ──────────────────────────────────────────────────
            col.Item().PaddingTop(10)
                .Background(Color.FromHex(Primary))
                .PaddingHorizontal(14).PaddingVertical(12).Row(row =>
                {
                    row.AutoItem().AlignMiddle().Column(c =>
                    {
                        c.Item().AlignCenter().Text("RISK SCORE").Bold().FontSize(7.5f).FontColor(Color.FromHex(Subtle));
                        c.Item().AlignCenter().Text(ai.OverallRiskScore.ToString())
                            .FontSize(36).Bold().FontColor(GetRiskColor(ai.RiskRating));
                        c.Item().AlignCenter().Text($"{ai.RiskRating} Risk")
                            .Bold().FontSize(10).FontColor(GetRiskColor(ai.RiskRating));
                    });

                    row.ConstantItem(20);

                    row.RelativeItem().AlignMiddle().Column(c =>
                    {
                        if (!string.IsNullOrWhiteSpace(recLabel))
                        {
                            c.Item().Border(1.5f).BorderColor(recColor)
                                .Background(recColor).PaddingHorizontal(10).PaddingVertical(4)
                                .Text(recLabel.ToUpperInvariant())
                                .Bold().FontSize(9).FontColor(Colors.White).LetterSpacing(0.03f);
                        }
                        if (ai.HasCriticalRedFlags)
                        {
                            c.Item().PaddingTop(4).Border(1.5f).BorderColor(Color.FromHex("#c53030"))
                                .Background(Color.FromHex("#c53030")).PaddingHorizontal(10).PaddingVertical(4)
                                .Text("⚠ CRITICAL RED FLAGS").Bold().FontSize(9).FontColor(Colors.White);
                        }
                        c.Item().PaddingTop(6).Text(ai.RiskSummary).FontSize(9.5f).FontColor(Colors.White).LineHeight(1.4f);
                    });

                    row.ConstantItem(100).AlignMiddle().Column(c =>
                    {
                        c.Item().AlignRight().Text(ai.GeneratedAt.ToString("dd MMM yyyy")).FontSize(8).FontColor(Color.FromHex(Subtle));
                        if (!string.IsNullOrWhiteSpace(ai.ModelVersion))
                            c.Item().AlignRight().Text(ai.ModelVersion).FontSize(7.5f).FontColor(Color.FromHex(Subtle)).Italic();
                    });
                });

            // ── Recommended Terms ─────────────────────────────────────────────
            if (ai.RecommendedAmount.HasValue)
            {
                col.Item().PaddingTop(10).Row(row =>
                {
                    row.RelativeItem().Border(1.5f).BorderColor(Color.FromHex(MedGray))
                        .Background(Color.FromHex(PanelGreen)).Padding(10).Column(c =>
                        {
                            c.Item().AlignCenter().Text("RECOMMENDED AMOUNT").Bold().FontSize(7.5f).FontColor(Color.FromHex(Primary));
                            c.Item().AlignCenter().Text($"{data.Currency} {ai.RecommendedAmount:N0}")
                                .FontSize(14).Bold().FontColor(Color.FromHex(Accent));
                        });
                    if (ai.RecommendedTenorMonths.HasValue)
                        row.RelativeItem().Border(1.5f).BorderColor(Color.FromHex(MedGray))
                            .Background(Color.FromHex(PanelGreen)).Padding(10).Column(c =>
                            {
                                c.Item().AlignCenter().Text("TENOR").Bold().FontSize(7.5f).FontColor(Color.FromHex(Primary));
                                c.Item().AlignCenter().Text($"{ai.RecommendedTenorMonths} months")
                                    .FontSize(14).Bold().FontColor(Color.FromHex(Accent));
                            });
                    if (ai.RecommendedInterestRate.HasValue)
                        row.RelativeItem().Border(1.5f).BorderColor(Color.FromHex(MedGray))
                            .Background(Color.FromHex(PanelGreen)).Padding(10).Column(c =>
                            {
                                c.Item().AlignCenter().Text("INTEREST RATE").Bold().FontSize(7.5f).FontColor(Color.FromHex(Primary));
                                c.Item().AlignCenter().Text($"{ai.RecommendedInterestRate:N2}%")
                                    .FontSize(14).Bold().FontColor(Color.FromHex(Accent));
                            });
                });
            }

            // ── Risk Category Breakdown ───────────────────────────────────────
            if (ai.ScoreBreakdown.Any())
            {
                col.Item().PaddingTop(14);
                SubSectionTitle(col, "Risk Category Breakdown");
                col.Item().PaddingTop(6).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(2); cols.ConstantColumn(45); cols.RelativeColumn(1); cols.RelativeColumn(5);
                    });
                    TableHeader(table, "Category"); TableHeader(table, "Score"); TableHeader(table, "Rating"); TableHeader(table, "Rationale");

                    for (var i = 0; i < ai.ScoreBreakdown.Count; i++)
                    {
                        var s = ai.ScoreBreakdown[i];
                        var bg = i % 2 == 0 ? Colors.White : Color.FromHex(PanelGreen);
                        var scoreColor = s.Score >= 65 ? Color.FromHex(Accent)
                            : s.Score >= 40 ? Color.FromHex("#c05621")
                            : Color.FromHex("#c53030");

                        DataCell(table, FormatAdvisoryCategory(s.Category), bg);
                        table.Cell().Background(bg).BorderBottom(1).BorderColor(Color.FromHex(MedGray)).Padding(5)
                            .Text(s.Score.ToString()).Bold().FontSize(9).FontColor(scoreColor);
                        DataCell(table, s.Rating ?? "—", bg);
                        DataCell(table, s.Rationale ?? "—", bg);
                    }
                });
            }

            // ── Red Flags ─────────────────────────────────────────────────────
            if (ai.RedFlags.Any())
            {
                col.Item().PaddingTop(12);
                var flagBg   = ai.HasCriticalRedFlags ? Color.FromHex("#fff5f5") : Color.FromHex("#fff7ed");
                var flagBorder = ai.HasCriticalRedFlags ? Color.FromHex("#fc8181") : Color.FromHex("#fed7aa");
                var flagText  = ai.HasCriticalRedFlags ? Color.FromHex("#c53030") : Color.FromHex("#c2410c");
                var flagLabel = ai.HasCriticalRedFlags ? "CRITICAL RED FLAGS" : "RED FLAGS";

                col.Item().Background(flagBg).Border(1).BorderColor(flagBorder)
                    .PaddingHorizontal(10).PaddingVertical(8).Column(c =>
                    {
                        c.Item().Text(flagLabel).Bold().FontSize(9).FontColor(flagText);
                        foreach (var flag in ai.RedFlags)
                            c.Item().PaddingTop(2).Text($"• {flag}").FontSize(9).FontColor(flagText);
                    });
            }

            // ── Executive Summary ─────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(ai.RiskSummary))
            {
                col.Item().PaddingTop(12)
                    .Background(Color.FromHex("#f8fafc")).Border(1).BorderColor(Color.FromHex(MedGray))
                    .PaddingHorizontal(12).PaddingVertical(10).Column(c =>
                    {
                        c.Item().Text("EXECUTIVE SUMMARY").Bold().FontSize(8.5f).FontColor(Color.FromHex(Primary));
                        c.Item().PaddingTop(4).Text(ai.RiskSummary).FontSize(9.5f).LineHeight(1.6f);
                    });
            }

            // ── Strengths & Weaknesses ────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(ai.StrengthsAnalysis) || !string.IsNullOrWhiteSpace(ai.WeaknessesAnalysis))
            {
                col.Item().PaddingTop(10).Row(row =>
                {
                    if (!string.IsNullOrWhiteSpace(ai.StrengthsAnalysis))
                        row.RelativeItem().Background(Color.FromHex(PanelGreen))
                            .Border(1).BorderColor(Color.FromHex(Subtle))
                            .PaddingHorizontal(10).PaddingVertical(8).Column(c =>
                            {
                                c.Item().Text("STRENGTHS").Bold().FontSize(8.5f).FontColor(Color.FromHex(Primary));
                                c.Item().PaddingTop(4).Text(ai.StrengthsAnalysis).FontSize(9.5f).LineHeight(1.5f).FontColor(Color.FromHex(Primary));
                            });
                    if (!string.IsNullOrWhiteSpace(ai.WeaknessesAnalysis))
                        row.RelativeItem().Background(Color.FromHex("#fff5f5"))
                            .Border(1).BorderColor(Color.FromHex("#fecaca"))
                            .PaddingHorizontal(10).PaddingVertical(8).Column(c =>
                            {
                                c.Item().Text("WEAKNESSES").Bold().FontSize(8.5f).FontColor(Color.FromHex("#c53030"));
                                c.Item().PaddingTop(4).Text(ai.WeaknessesAnalysis).FontSize(9.5f).LineHeight(1.5f).FontColor(Color.FromHex("#7f1d1d"));
                            });
                });
            }

            // ── Mitigating Factors & Key Risks ────────────────────────────────
            if (!string.IsNullOrWhiteSpace(ai.MitigatingFactorsText) || !string.IsNullOrWhiteSpace(ai.KeyRisks))
            {
                col.Item().PaddingTop(10).Row(row =>
                {
                    if (!string.IsNullOrWhiteSpace(ai.MitigatingFactorsText))
                        row.RelativeItem().Background(Color.FromHex("#eff6ff"))
                            .Border(1).BorderColor(Color.FromHex("#bfdbfe"))
                            .PaddingHorizontal(10).PaddingVertical(8).Column(c =>
                            {
                                c.Item().Text("MITIGATING FACTORS").Bold().FontSize(8.5f).FontColor(Color.FromHex("#1e40af"));
                                c.Item().PaddingTop(4).Text(ai.MitigatingFactorsText).FontSize(9.5f).LineHeight(1.5f).FontColor(Color.FromHex("#1e3a8a"));
                            });
                    if (!string.IsNullOrWhiteSpace(ai.KeyRisks))
                        row.RelativeItem().Background(Color.FromHex("#fffbeb"))
                            .Border(1).BorderColor(Color.FromHex("#fde68a"))
                            .PaddingHorizontal(10).PaddingVertical(8).Column(c =>
                            {
                                c.Item().Text("KEY RISKS").Bold().FontSize(8.5f).FontColor(Color.FromHex("#92400e"));
                                c.Item().PaddingTop(4).Text(ai.KeyRisks).FontSize(9.5f).LineHeight(1.5f).FontColor(Color.FromHex("#78350f"));
                            });
                });
            }

            // ── Conditions & Covenants ────────────────────────────────────────
            if (ai.RecommendedConditions.Any() || ai.Covenants.Any())
            {
                col.Item().PaddingTop(12).Row(row =>
                {
                    if (ai.RecommendedConditions.Any())
                        row.RelativeItem()
                            .Border(1).BorderColor(Color.FromHex("#e9d5ff"))
                            .Background(Color.FromHex("#faf5ff"))
                            .PaddingHorizontal(10).PaddingVertical(8).Column(c =>
                            {
                                c.Item().Text("PRECEDENT CONDITIONS").Bold().FontSize(8.5f).FontColor(Color.FromHex("#6d28d9"));
                                for (var i = 0; i < ai.RecommendedConditions.Count; i++)
                                    c.Item().PaddingTop(3).Text($"{i + 1}. {ai.RecommendedConditions[i]}").FontSize(9.5f).FontColor(Color.FromHex("#4c1d95"));
                            });
                    if (ai.Covenants.Any())
                        row.RelativeItem()
                            .Border(1).BorderColor(Color.FromHex("#a5f3fc"))
                            .Background(Color.FromHex("#ecfeff"))
                            .PaddingHorizontal(10).PaddingVertical(8).Column(c =>
                            {
                                c.Item().Text("ONGOING COVENANTS").Bold().FontSize(8.5f).FontColor(Color.FromHex("#0e7490"));
                                for (var i = 0; i < ai.Covenants.Count; i++)
                                    c.Item().PaddingTop(3).Text($"{i + 1}. {ai.Covenants[i]}").FontSize(9.5f).FontColor(Color.FromHex("#164e63"));
                            });
                });
            }
        });
    }

    private static string FormatAdvisoryCategory(string category) => category switch
    {
        "CreditHistory"        => "Credit History",
        "FinancialHealth"      => "Financial Health",
        "CashflowStability"    => "Cashflow Stability",
        "DebtServiceCapacity"  => "Debt Service (DSCR)",
        "CollateralCoverage"   => "Collateral Coverage",
        "ManagementRisk"       => "Management Quality",
        "IndustryRisk"         => "Industry Risk",
        "ConcentrationRisk"    => "Concentration Risk",
        _                      => category
    };

    private static void ComposeCreditOfficerNotes(IContainer container, LoanPackData data)
    {
        container.Column(col =>
        {
            SectionTitle(col, "CREDIT OFFICER NOTES");
            col.Item().PaddingTop(3)
                .Text("Application-level notes and observations logged by the processing team.")
                .FontSize(9).Italic().FontColor(Colors.Grey.Darken2);
            col.Item().PaddingTop(8);

            var grouped = data.CreditOfficerNotes
                .GroupBy(n => string.IsNullOrWhiteSpace(n.Category) ? "General" : n.Category)
                .OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                col.Item().PaddingTop(6)
                    .Background(Color.FromHex(PanelGreen))
                    .PaddingHorizontal(8).PaddingVertical(4)
                    .Text(group.Key.ToUpper()).Bold().FontSize(9).FontColor(Color.FromHex(Primary));

                foreach (var note in group)
                {
                    col.Item().PaddingTop(4)
                        .Border(1).BorderColor(Color.FromHex(MedGray))
                        .Padding(8).Column(c =>
                        {
                            c.Item().AlignRight().Text(note.CreatedAt.ToString("dd MMM yyyy HH:mm"))
                                .FontSize(8).FontColor(Colors.Grey.Darken1);
                            c.Item().PaddingTop(3).Text(note.Content).FontSize(9.5f);
                        });
                }
            }
        });
    }

    private static void ComposeCommitteeComments(IContainer container, LoanPackData data)
    {
        container.Column(col =>
        {
            SectionTitle(col, "COMMITTEE COMMENTS");
            col.Item().PaddingTop(10);

            foreach (var comment in data.CommitteeComments.OrderByDescending(c => c.Timestamp))
            {
                col.Item().Border(1).BorderColor(Color.FromHex(MedGray)).Column(card =>
                {
                    card.Item().Background(Color.FromHex(PanelGreen))
                        .PaddingHorizontal(10).PaddingVertical(5).Row(row =>
                        {
                            row.RelativeItem().Text($"{comment.MemberName} ({comment.MemberRole})")
                                .Bold().FontSize(9.5f).FontColor(Color.FromHex(Primary));
                            row.AutoItem().PaddingRight(8)
                                .Text($"[{comment.Visibility}]")
                                .FontSize(8).Italic().FontColor(Colors.Grey.Darken1);
                            row.AutoItem().Text(comment.Timestamp.ToString("dd-MMM-yy HH:mm"))
                                .FontSize(8.5f).FontColor(Colors.Grey.Darken2);
                        });

                    card.Item().PaddingHorizontal(10).PaddingVertical(6).Column(c =>
                    {
                        if (!string.IsNullOrEmpty(comment.Vote))
                        {
                            var voteColor = comment.Vote == "Approve" ? Color.FromHex(Accent)
                                : comment.Vote == "Reject" ? Color.FromHex("#c53030")
                                : Colors.Grey.Darken2;
                            c.Item().Text($"Vote: {comment.Vote}").Bold().FontSize(9.5f).FontColor(voteColor);
                            c.Item().PaddingTop(3);
                        }
                        c.Item().Text(comment.Comment).FontSize(9.5f).LineHeight(1.4f);
                    });
                });

                col.Item().PaddingTop(6);
            }
        });
    }

    private static void ComposeCommitteeDecision(IContainer container, LoanPackData data)
    {
        var decision = data.CommitteeDecision!;
        container.Column(col =>
        {
            SectionTitle(col, "COMMITTEE DECISION");
            col.Item().PaddingTop(10);

            var decisionColor = decision.Decision == "Approved" ? Color.FromHex(Accent)
                : decision.Decision == "Rejected" ? Color.FromHex("#c53030")
                : Color.FromHex("#c05621");

            var decisionBg = decision.Decision == "Approved" ? Color.FromHex(PanelGreen)
                : decision.Decision == "Rejected" ? Color.FromHex("#fff5f5")
                : Color.FromHex("#fffbeb");

            col.Item().Background(decisionBg)
                .Border(2).BorderColor(decisionColor)
                .PaddingHorizontal(12).PaddingVertical(10).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(decision.Decision.ToUpper()).FontSize(18).Bold().FontColor(decisionColor);
                        if (!string.IsNullOrEmpty(decision.DecisionRationale))
                            c.Item().PaddingTop(4).Text(decision.DecisionRationale).FontSize(9.5f).LineHeight(1.4f);
                    });

                    VoteBadge(row, decision.ApprovalVotes.ToString(), "Approve", Color.FromHex(Accent));
                    VoteBadge(row, decision.RejectionVotes.ToString(), "Reject", Color.FromHex("#c53030"));
                    if (decision.AbstainVotes > 0)
                        VoteBadge(row, decision.AbstainVotes.ToString(), "Abstain", Colors.Grey.Darken2);
                    if (decision.PendingVotes > 0)
                        VoteBadge(row, decision.PendingVotes.ToString(), "Pending", Color.FromHex("#c05621"));
                });

            if (decision.RecommendedAmount.HasValue || decision.RecommendedTenorMonths.HasValue || decision.RecommendedInterestRate.HasValue)
            {
                col.Item().PaddingTop(14);
                SubSectionTitle(col, "Committee Approved Terms");
                col.Item().PaddingTop(5).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(1); cols.RelativeColumn(2);
                        cols.RelativeColumn(1); cols.RelativeColumn(2);
                    });

                    DataRow(table, "Amount:", decision.RecommendedAmount.HasValue ? $"{data.Currency} {decision.RecommendedAmount:N2}" : "—", "Tenor:", decision.RecommendedTenorMonths.HasValue ? $"{decision.RecommendedTenorMonths} months" : "—", true);
                    DataRow(table, "Interest Rate:", decision.RecommendedInterestRate.HasValue ? $"{decision.RecommendedInterestRate:N2}%" : "—", "", "", false);
                });
            }

            if (decision.MemberVotes.Any())
            {
                col.Item().PaddingTop(14);
                SubSectionTitle(col, "Member Votes");
                col.Item().PaddingTop(5).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(2); cols.RelativeColumn(1); cols.RelativeColumn(1);
                        cols.RelativeColumn(1); cols.RelativeColumn(3);
                    });

                    TableHeader(table, "Member"); TableHeader(table, "Role"); TableHeader(table, "Vote");
                    TableHeader(table, "Voted At"); TableHeader(table, "Comment");

                    for (var i = 0; i < decision.MemberVotes.Count; i++)
                    {
                        var vote = decision.MemberVotes[i];
                        var bg = i % 2 == 0 ? Colors.White : Color.FromHex(PanelGreen);
                        DataCell(table, vote.MemberName, bg); DataCell(table, vote.MemberRole, bg);

                        var voteColor = vote.Vote == "Approve" ? Color.FromHex(Accent)
                            : vote.Vote == "Reject" ? Color.FromHex("#c53030")
                            : Colors.Grey.Darken2;
                        table.Cell().Background(bg).BorderBottom(1).BorderColor(Color.FromHex(MedGray)).Padding(5)
                            .Text(vote.Vote ?? "Pending").FontSize(9).Bold().FontColor(voteColor);

                        DataCell(table, vote.VotedAt.HasValue ? vote.VotedAt.Value.ToString("dd-MMM-yy HH:mm") : "—", bg);
                        DataCell(table, vote.VoteComment ?? "", bg);
                    }
                });
            }
        });
    }

    private static void ComposeConditionsOfApproval(IContainer container, LoanPackData data)
    {
        container.Column(col =>
        {
            SectionTitle(col, "CONDITIONS OF APPROVAL");
            col.Item().PaddingTop(4)
                .Text("The following conditions were stipulated by the Credit Committee as part of the approval decision. " +
                      "All Conditions Precedent must be satisfied before disbursement. " +
                      "Conditions Subsequent are monitored post-disbursement per the terms agreed in the offer letter.")
                .FontSize(9).FontColor(Colors.Grey.Darken2).LineHeight(1.4f);

            col.Item().PaddingTop(10).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.ConstantColumn(30); cols.RelativeColumn();
                });

                table.Header(header =>
                {
                    header.Cell().Background(Color.FromHex(Accent)).PaddingHorizontal(8).PaddingVertical(5)
                        .Text("#").Bold().FontSize(9).FontColor(Colors.White);
                    header.Cell().Background(Color.FromHex(Accent)).PaddingHorizontal(8).PaddingVertical(5)
                        .Text("Condition").Bold().FontSize(9).FontColor(Colors.White);
                });

                for (var i = 0; i < data.ApprovalConditions.Count; i++)
                {
                    var bg = i % 2 == 0 ? Colors.White : Color.FromHex(PanelGreen);
                    table.Cell().Background(bg).BorderBottom(1).BorderColor(Color.FromHex(MedGray)).Padding(6)
                        .Text($"{i + 1}").FontSize(9.5f);
                    table.Cell().Background(bg).BorderBottom(1).BorderColor(Color.FromHex(MedGray)).Padding(6)
                        .Text(data.ApprovalConditions[i]).FontSize(9.5f);
                }
            });
        });
    }

    private static void ComposeDisbursementChecklist(IContainer container, LoanPackData data)
    {
        var cpItems = data.DisbursementChecklist.Where(c => c.ConditionType == "Precedent").ToList();
        var csItems = data.DisbursementChecklist.Where(c => c.ConditionType == "Subsequent").ToList();

        container.Column(col =>
        {
            SectionTitle(col, "DISBURSEMENT CHECKLIST");
            col.Item().PaddingTop(4)
                .Text("Conditions Precedent (CP) must be satisfied or waived before offer acceptance is confirmed. " +
                      "Conditions Subsequent (CS) are monitored after disbursement.")
                .FontSize(9).FontColor(Colors.Grey.Darken2).LineHeight(1.4f);

            if (cpItems.Any())
            {
                col.Item().PaddingTop(12);
                SubSectionTitle(col, "Conditions Precedent (CP)");
                col.Item().PaddingTop(5).Element(c => RenderChecklistTable(c, cpItems));
            }

            if (csItems.Any())
            {
                col.Item().PaddingTop(14);
                SubSectionTitle(col, "Conditions Subsequent (CS)");
                col.Item().PaddingTop(5).Element(c => RenderChecklistTable(c, csItems));
            }
        });
    }

    private static void RenderChecklistTable(IContainer container, List<ChecklistItemData> items)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.RelativeColumn(3); cols.RelativeColumn(1); cols.RelativeColumn(1);
                cols.RelativeColumn(2); cols.RelativeColumn(2);
            });

            TableHeader(table, "Condition"); TableHeader(table, "Mandatory"); TableHeader(table, "Status");
            TableHeader(table, "Satisfied / Due"); TableHeader(table, "Waiver / Notes");

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var bg = i % 2 == 0 ? Colors.White : Color.FromHex(PanelGreen);

                var statusColor = item.Status is "Satisfied" or "WaiverApproved"
                    ? Color.FromHex(Accent)
                    : item.Status is "Rejected" or "WaiverRejected"
                        ? Color.FromHex("#c53030")
                        : Color.FromHex("#c05621");

                table.Cell().Background(bg).BorderBottom(1).BorderColor(Color.FromHex(MedGray)).Padding(5).Column(c =>
                {
                    c.Item().Text(item.ItemName).Bold().FontSize(9);
                    if (!string.IsNullOrWhiteSpace(item.Description) && item.Description != item.ItemName)
                        c.Item().Text(item.Description).FontSize(8).FontColor(Colors.Grey.Darken2);
                });

                DataCell(table, item.IsMandatory ? "Yes" : "No", bg);

                table.Cell().Background(bg).BorderBottom(1).BorderColor(Color.FromHex(MedGray)).Padding(5)
                    .Text(item.Status).FontSize(9).FontColor(statusColor).Bold();

                var dateText = item.SatisfiedAt.HasValue ? item.SatisfiedAt.Value.ToString("dd-MMM-yy")
                    : item.DueDate.HasValue ? $"Due: {item.DueDate.Value:dd-MMM-yy}" : "—";
                DataCell(table, dateText, bg);

                var waiverText = !string.IsNullOrWhiteSpace(item.WaiverReason)
                    ? $"Waiver: {item.WaiverReason}" + (item.WaiverProposedAt.HasValue ? $" ({item.WaiverProposedAt.Value:dd-MMM-yy})" : "")
                    : "";
                DataCell(table, waiverText, bg);
            }
        });
    }

    private static void ComposeApprovalAuditTrail(IContainer container, LoanPackData data)
    {
        container.Column(col =>
        {
            SectionTitle(col, "APPROVAL AUDIT TRAIL");
            col.Item().PaddingTop(3)
                .Text("Chronological record of all status transitions for this application.")
                .FontSize(9).Italic().FontColor(Colors.Grey.Darken2);
            col.Item().PaddingTop(8).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(2); cols.RelativeColumn(2); cols.RelativeColumn(2); cols.RelativeColumn(5);
                });

                TableHeader(table, "Date / Time"); TableHeader(table, "Status");
                TableHeader(table, "Actor"); TableHeader(table, "Comment / Action");

                for (var i = 0; i < data.ApprovalAuditTrail.Count; i++)
                {
                    var entry = data.ApprovalAuditTrail[i];
                    var bg = i % 2 == 0 ? Colors.White : Color.FromHex(PanelGreen);
                    DataCell(table, entry.ChangedAt.ToString("dd-MMM-yyyy HH:mm"), bg);
                    DataCell(table, entry.Status, bg);
                    DataCell(table, entry.ActorName, bg);
                    DataCell(table, entry.Comment ?? "", bg);
                }
            });
        });
    }

    private static void ComposeWorkflowHistory(IContainer container, LoanPackData data)
    {
        container.Column(col =>
        {
            SectionTitle(col, "WORKFLOW ACTION LOG");
            col.Item().PaddingTop(3)
                .Text("Detailed log of every workflow action performed — includes system transitions and explicit actor decisions.")
                .FontSize(9).Italic().FontColor(Colors.Grey.Darken2);
            col.Item().PaddingTop(8).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(2); cols.RelativeColumn(2); cols.RelativeColumn(2);
                    cols.RelativeColumn(2); cols.RelativeColumn(4);
                });

                TableHeader(table, "Date / Time"); TableHeader(table, "From Status");
                TableHeader(table, "To Status"); TableHeader(table, "Actor"); TableHeader(table, "Action / Comment");

                for (var i = 0; i < data.WorkflowHistory.Count; i++)
                {
                    var entry = data.WorkflowHistory[i];
                    var bg = i % 2 == 0 ? Colors.White : Color.FromHex(PanelGreen);
                    DataCell(table, entry.Timestamp.ToString("dd-MMM-yyyy HH:mm"), bg);
                    DataCell(table, string.IsNullOrWhiteSpace(entry.FromStatus) ? "—" : entry.FromStatus, bg);
                    DataCell(table, entry.ToStatus, bg);
                    DataCell(table, entry.PerformedBy, bg);
                    table.Cell().Background(bg).BorderBottom(1).BorderColor(Color.FromHex(MedGray)).Padding(5).Column(c =>
                    {
                        c.Item().Text(entry.Action).Bold().FontSize(9).FontColor(Color.FromHex(Primary));
                        if (!string.IsNullOrWhiteSpace(entry.Comment))
                            c.Item().PaddingTop(1).Text(entry.Comment).FontSize(8.5f).FontColor(Colors.Grey.Darken2);
                    });
                }
            });
        });
    }

    // ── Footer ────────────────────────────────────────────────────────────────

    private static void ComposeFooter(IContainer container, LoanPackData data)
    {
        container.Column(col =>
        {
            col.Item().LineHorizontal(1).LineColor(Color.FromHex(Subtle));
            col.Item().PaddingTop(3).Row(row =>
            {
                row.RelativeItem()
                    .Text($"Application: {data.ApplicationNumber}  |  LOAN APPLICATION PACK")
                    .FontSize(7).FontColor(Colors.Grey.Darken1);
                row.RelativeItem().AlignCenter()
                    .DefaultTextStyle(x => x.FontSize(7).FontColor(Colors.Grey.Darken1))
                    .Text(t => { t.Span("Page "); t.CurrentPageNumber(); t.Span(" of "); t.TotalPages(); });
                row.RelativeItem().AlignRight()
                    .Text($"v{data.Version}  |  {data.GeneratedAt:dd MMM yyyy HH:mm}")
                    .FontSize(7).FontColor(Colors.Grey.Darken1);
            });
            col.Item().PaddingTop(3).AlignCenter()
                .Text("CONFIDENTIAL — FOR INTERNAL USE ONLY")
                .FontSize(6).Italic().FontColor(Colors.Grey.Medium);
            col.Item().PaddingTop(6).Height(4).Background(Color.FromHex(Primary));
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void SectionTitle(ColumnDescriptor col, string title)
    {
        col.Item()
            .Background(Color.FromHex(Primary))
            .PaddingHorizontal(10).PaddingVertical(7)
            .Text(title).Bold().FontSize(11).FontColor(Colors.White).LetterSpacing(0.04f);
    }

    private static void SubSectionTitle(ColumnDescriptor col, string title)
    {
        col.Item()
            .Background(Color.FromHex(Accent))
            .PaddingHorizontal(10).PaddingVertical(5)
            .Text(title).Bold().FontSize(9.5f).FontColor(Colors.White);
    }

    private static void TableHeader(TableDescriptor table, string text)
    {
        table.Cell()
            .Background(Color.FromHex(Accent))
            .PaddingHorizontal(6).PaddingVertical(5)
            .Text(text).Bold().FontSize(9).FontColor(Colors.White);
    }

    private static void DataCell(TableDescriptor table, string text, Color bg)
    {
        table.Cell().Background(bg).BorderBottom(1).BorderColor(Color.FromHex(MedGray))
            .Padding(5).Text(text).FontSize(9);
    }

    private static void LabelValue(TableDescriptor table, string label, string value, bool shaded)
    {
        var bg = shaded ? Color.FromHex(PanelGreen) : Colors.White;
        table.Cell().Background(bg).BorderBottom(1).BorderColor(Color.FromHex(MedGray))
            .PaddingHorizontal(8).PaddingVertical(6)
            .Text(label).Bold().FontSize(9).FontColor(Color.FromHex(Primary));
        table.Cell().Background(bg).BorderBottom(1).BorderColor(Color.FromHex(MedGray))
            .PaddingHorizontal(8).PaddingVertical(6)
            .Text(value).FontSize(9);
    }

    private static void DataRow(TableDescriptor table,
        string label1, string value1, string label2, string value2, bool shaded)
    {
        LabelValue(table, label1, value1, shaded);
        LabelValue(table, label2, value2, shaded);
    }

    private static void TimelineRow(TableDescriptor table, string label, DateTime? date, bool shaded)
    {
        var bg = shaded ? Color.FromHex(PanelGreen) : Colors.White;
        table.Cell().Background(bg).BorderBottom(1).BorderColor(Color.FromHex(MedGray))
            .Padding(5).Text(label).FontSize(9).Bold().FontColor(Color.FromHex(Primary));
        table.Cell().Background(bg).BorderBottom(1).BorderColor(Color.FromHex(MedGray))
            .Padding(5).Text(date.HasValue ? date.Value.ToString("dd MMM yyyy HH:mm") : "—")
            .FontSize(9).FontColor(date.HasValue ? Colors.Black : Colors.Grey.Darken1);
    }

    private static void AddFinancialRow(TableDescriptor table, string label, IEnumerable<decimal?> values, string currency, bool shaded)
    {
        var bg = shaded ? Color.FromHex(PanelGreen) : Colors.White;
        table.Cell().Background(bg).BorderBottom(1).BorderColor(Color.FromHex(MedGray))
            .PaddingHorizontal(6).PaddingVertical(5)
            .Text(label).Bold().FontSize(9).FontColor(Color.FromHex(Primary));
        foreach (var value in values)
        {
            table.Cell().Background(bg).BorderBottom(1).BorderColor(Color.FromHex(MedGray))
                .PaddingHorizontal(6).PaddingVertical(5)
                .Text(value.HasValue ? $"{currency} {value:N0}" : "N/A").FontSize(9);
        }
    }

    private static void RatioPanel(RowDescriptor row, string title, params string[] lines)
    {
        row.RelativeItem()
            .Border(1.5f).BorderColor(Color.FromHex(MedGray))
            .Background(Color.FromHex(PanelGreen)).Column(c =>
            {
                c.Item().Background(Color.FromHex(Accent))
                    .PaddingHorizontal(8).PaddingVertical(4)
                    .Text(title).Bold().FontSize(8.5f).FontColor(Colors.White);
                foreach (var line in lines)
                    c.Item().PaddingHorizontal(8).PaddingVertical(3)
                        .Text(line).FontSize(9);
            });
    }

    private static void CashflowPanel(RowDescriptor row, string title, params (string Label, string Value, bool Alert)[] lines)
    {
        row.RelativeItem()
            .Border(1.5f).BorderColor(Color.FromHex(MedGray))
            .Background(Color.FromHex(PanelGreen)).Column(c =>
            {
                c.Item().Background(Color.FromHex(Accent))
                    .PaddingHorizontal(8).PaddingVertical(4)
                    .Text(title).Bold().FontSize(8.5f).FontColor(Colors.White);
                foreach (var (label, value, alert) in lines)
                {
                    c.Item().PaddingHorizontal(8).PaddingVertical(3).Row(r =>
                    {
                        r.RelativeItem().Text(label).FontSize(9).FontColor(Color.FromHex(Primary));
                        r.AutoItem().Text(value).FontSize(9)
                            .FontColor(alert ? Color.FromHex("#c53030") : Colors.Black);
                    });
                }
            });
    }

    private static void VoteBadge(RowDescriptor row, string count, string label, Color color)
    {
        row.AutoItem().Padding(8).Column(c =>
        {
            c.Item().AlignCenter().Text(count).FontSize(22).Bold().FontColor(color);
            c.Item().AlignCenter().Text(label).FontSize(9);
        });
    }

    private static Color GetRiskColor(string? riskRating) => riskRating switch
    {
        "Low"      => Color.FromHex(Accent),
        "Moderate" => Color.FromHex("#c05621"),
        "High"     => Color.FromHex("#c53030"),
        "VeryHigh" => Color.FromHex("#742a2a"),
        _          => Colors.Grey.Darken2
    };

    private static Color GetScoreColor(int? score) => score switch
    {
        >= 700 => Color.FromHex(Accent),
        >= 600 => Color.FromHex("#c05621"),
        < 600  => Color.FromHex("#c53030"),
        _      => Colors.Grey.Darken2
    };
}

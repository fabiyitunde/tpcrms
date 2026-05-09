using CRMS.Application.LoanPack.DTOs;
using CRMS.Application.LoanPack.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CRMS.Infrastructure.Documents;

/// <summary>
/// Generates loan pack PDFs using QuestPDF.
/// </summary>
public class LoanPackPdfGenerator : ILoanPackGenerator
{
    public Task<byte[]> GenerateAsync(LoanPackData data, CancellationToken ct = default)
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

    private void ComposeHeader(IContainer container, LoanPackData data)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(inner =>
                {
                    inner.Item().Text("CREDIT RISK MANAGEMENT SYSTEM").Bold().FontSize(14);
                    inner.Item().Text("LOAN APPLICATION PACK").FontSize(12);
                });

                row.RelativeItem().AlignRight().Column(inner =>
                {
                    inner.Item().Text($"Application: {data.ApplicationNumber}").Bold();
                    inner.Item().Text($"Generated: {data.GeneratedAt:dd-MMM-yyyy HH:mm}");
                    inner.Item().Text($"Version: {data.Version}");
                });
            });

            col.Item().PaddingTop(10).LineHorizontal(1);
        });
    }

    private void ComposeContent(IContainer container, LoanPackData data)
    {
        container.PaddingVertical(10).Column(col =>
        {
            // 1. Executive Summary
            col.Item().Element(c => ComposeExecutiveSummary(c, data));
            col.Item().PageBreak();

            // 2. Application Timeline
            col.Item().Element(c => ComposeApplicationTimeline(c, data));
            col.Item().PageBreak();

            // 3. Customer Profile
            col.Item().Element(c => ComposeCustomerProfile(c, data));
            col.Item().PageBreak();

            // 4. Directors & Signatories
            if (data.Directors.Any() || data.Signatories.Any())
            {
                col.Item().Element(c => ComposeDirectorsAndSignatories(c, data));
                col.Item().PageBreak();
            }

            // 5. Supporting Documents
            if (data.Documents.Any())
            {
                col.Item().Element(c => ComposeDocuments(c, data));
                col.Item().PageBreak();
            }

            // 6. Bureau Reports
            if (data.BureauReports.Any())
            {
                col.Item().Element(c => ComposeBureauReports(c, data));
                col.Item().PageBreak();
            }

            // 7. Financial Analysis
            if (data.FinancialStatements.Any())
            {
                col.Item().Element(c => ComposeFinancialAnalysis(c, data));
                col.Item().PageBreak();
            }

            // 8. Cashflow Analysis
            if (data.CashflowAnalysis != null)
            {
                col.Item().Element(c => ComposeCashflowAnalysis(c, data));
                col.Item().PageBreak();
            }

            // 9. Collateral
            if (data.Collaterals.Any())
            {
                col.Item().Element(c => ComposeCollateral(c, data));
                col.Item().PageBreak();
            }

            // 10. Guarantors
            if (data.Guarantors.Any())
            {
                col.Item().Element(c => ComposeGuarantors(c, data));
                col.Item().PageBreak();
            }

            // 11. AI Advisory
            if (data.AIAdvisory != null)
            {
                col.Item().Element(c => ComposeAIAdvisory(c, data));
                col.Item().PageBreak();
            }

            // 12. Credit Officer Notes
            if (data.CreditOfficerNotes.Any())
            {
                col.Item().PageBreak();
                col.Item().Element(c => ComposeCreditOfficerNotes(c, data));
            }

            // 13. Committee Comments
            if (data.CommitteeComments.Any())
            {
                col.Item().PageBreak();
                col.Item().Element(c => ComposeCommitteeComments(c, data));
            }

            // 14. Committee Decision
            if (data.CommitteeDecision != null)
            {
                col.Item().PageBreak();
                col.Item().Element(c => ComposeCommitteeDecision(c, data));
            }

            // 15. Conditions of Approval (committee-stipulated)
            if (data.ApprovalConditions.Any())
            {
                col.Item().PageBreak();
                col.Item().Element(c => ComposeConditionsOfApproval(c, data));
            }

            // 16. Disbursement Checklist (CP / CS)
            if (data.DisbursementChecklist.Any())
            {
                col.Item().PageBreak();
                col.Item().Element(c => ComposeDisbursementChecklist(c, data));
            }

            // 17. Approval Audit Trail
            if (data.ApprovalAuditTrail.Any())
            {
                col.Item().PageBreak();
                col.Item().Element(c => ComposeApprovalAuditTrail(c, data));
            }
        });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Section composers
    // ──────────────────────────────────────────────────────────────────────────

    private void ComposeExecutiveSummary(IContainer container, LoanPackData data)
    {
        container.Column(col =>
        {
            col.Item().Text("EXECUTIVE SUMMARY").Bold().FontSize(14);
            col.Item().PaddingTop(10);

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(1);
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(1);
                    cols.RelativeColumn(2);
                });

                TableCell(table, "Customer:", true);
                TableCell(table, data.Customer.Name);
                TableCell(table, "Application Date:", true);
                TableCell(table, data.ApplicationDate.ToString("dd-MMM-yyyy"));

                TableCell(table, "Product:", true);
                TableCell(table, $"{data.LoanProductName} ({data.LoanProductCode})");
                TableCell(table, "Account Number:", true);
                TableCell(table, data.Customer.AccountNumber);

                TableCell(table, "Requested Amount:", true);
                TableCell(table, $"{data.Currency} {data.RequestedAmount:N2}");
                TableCell(table, "Requested Tenor:", true);
                TableCell(table, $"{data.RequestedTenorMonths} months");

                TableCell(table, "Interest Rate:", true);
                TableCell(table, $"{data.RequestedInterestRate:N2}% p.a.");
                TableCell(table, "Purpose:", true);
                TableCell(table, data.Purpose);

                TableCell(table, "Current Status:", true);
                TableCell(table, data.Timeline.CurrentStatus);
                TableCell(table, "Application Type:", true);
                TableCell(table, data.Timeline.ApplicationType);

                if (data.ApprovedAmount.HasValue || data.ApprovedTenorMonths.HasValue || data.ApprovedInterestRate.HasValue)
                {
                    TableCell(table, "Approved Amount:", true);
                    TableCell(table, data.ApprovedAmount.HasValue ? $"{data.Currency} {data.ApprovedAmount:N2}" : "—");
                    TableCell(table, "Approved Tenor:", true);
                    TableCell(table, data.ApprovedTenorMonths.HasValue ? $"{data.ApprovedTenorMonths} months" : "—");

                    TableCell(table, "Approved Rate:", true);
                    TableCell(table, data.ApprovedInterestRate.HasValue ? $"{data.ApprovedInterestRate:N2}%" : "—");
                    TableCell(table, "Committee Decision:", true);
                    TableCell(table, data.CommitteeDecision?.Decision ?? "—");
                }
            });

            col.Item().PaddingTop(15);
            col.Item().Text("KEY METRICS").Bold().FontSize(12);
            col.Item().PaddingTop(5);

            col.Item().Row(row =>
            {
                if (data.AIAdvisory != null)
                {
                    row.RelativeItem().Border(1).Padding(10).Column(c =>
                    {
                        c.Item().AlignCenter().Text("RISK SCORE").Bold();
                        c.Item().AlignCenter().Text(data.AIAdvisory.OverallRiskScore.ToString())
                            .FontSize(24).Bold().FontColor(GetRiskColor(data.AIAdvisory.RiskRating));
                        c.Item().AlignCenter().Text(data.AIAdvisory.RiskRating);
                    });
                }

                row.RelativeItem().Border(1).Padding(10).Column(c =>
                {
                    c.Item().AlignCenter().Text("COLLATERAL COVERAGE").Bold();
                    c.Item().AlignCenter().Text($"{data.CollateralCoverageRatio:P0}").FontSize(24).Bold();
                    c.Item().AlignCenter().Text($"{data.Currency} {data.TotalCollateralValue:N0}");
                });

                row.RelativeItem().Border(1).Padding(10).Column(c =>
                {
                    c.Item().AlignCenter().Text("BUREAU CHECKS").Bold();
                    var avgScore = data.BureauReports.Where(b => b.CreditScore.HasValue)
                        .Select(b => b.CreditScore!.Value).DefaultIfEmpty(0).Average();
                    c.Item().AlignCenter().Text($"{avgScore:N0}").FontSize(24).Bold();
                    c.Item().AlignCenter().Text($"{data.BureauReports.Count} reports");
                });

                if (data.AIAdvisory != null)
                {
                    row.RelativeItem().Border(1).Padding(10).Column(c =>
                    {
                        c.Item().AlignCenter().Text("RECOMMENDED").Bold();
                        c.Item().AlignCenter().Text($"{data.Currency} {data.AIAdvisory.RecommendedAmount:N0}")
                            .FontSize(16).Bold();
                        c.Item().AlignCenter().Text($"{data.AIAdvisory.RecommendedTenorMonths} months @ {data.AIAdvisory.RecommendedInterestRate:N2}%");
                    });
                }
            });

            if (data.AIAdvisory?.RedFlags.Any() == true)
            {
                col.Item().PaddingTop(15);
                col.Item().Background(Colors.Red.Lighten4).Padding(10).Column(c =>
                {
                    c.Item().Text("RED FLAGS").Bold().FontColor(Colors.Red.Darken2);
                    foreach (var flag in data.AIAdvisory.RedFlags)
                        c.Item().Text($"• {flag}").FontColor(Colors.Red.Darken2);
                });
            }

            if (data.AIAdvisory?.MitigatingFactors.Any() == true)
            {
                col.Item().PaddingTop(10);
                col.Item().Background(Colors.Green.Lighten4).Padding(10).Column(c =>
                {
                    c.Item().Text("MITIGATING FACTORS").Bold().FontColor(Colors.Green.Darken2);
                    foreach (var factor in data.AIAdvisory.MitigatingFactors)
                        c.Item().Text($"• {factor}").FontColor(Colors.Green.Darken2);
                });
            }
        });
    }

    private void ComposeApplicationTimeline(IContainer container, LoanPackData data)
    {
        var tl = data.Timeline;

        container.Column(col =>
        {
            col.Item().Text("APPLICATION TIMELINE").Bold().FontSize(14);
            col.Item().PaddingTop(4)
                .Text($"Current Status: {tl.CurrentStatus}  |  Type: {tl.ApplicationType}")
                .FontSize(10).FontColor(Colors.Grey.Darken2);
            col.Item().PaddingTop(10);

            // Key dates table
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(2);
                });

                TableHeader(table, "Milestone");
                TableHeader(table, "Date");
                TableHeader(table, "Milestone");
                TableHeader(table, "Date");

                TimelineRow(table, "Application Created", data.ApplicationDate);
                TimelineRow(table, "Submitted", tl.SubmittedAt);

                TimelineRow(table, "Branch Approved", tl.BranchApprovedAt);
                TimelineRow(table, "Credit Check Started", tl.CreditCheckStartedAt);

                TimelineRow(table, "Credit Check Completed", tl.CreditCheckCompletedAt);
                TimelineRow(table, "Final Approved", tl.FinalApprovedAt);

                TimelineRow(table, "Offer Issued", tl.OfferIssuedAt);
                TimelineRow(table, "Offer Accepted", tl.OfferAcceptedAt);

                TimelineRow(table, "Customer Signed", tl.CustomerSignedAt);
                TimelineRow(table, "Disbursed", tl.DisbursedAt);
            });

            // Offer acceptance details
            if (tl.OfferAcceptedAt.HasValue || tl.DisbursedAt.HasValue)
            {
                col.Item().PaddingTop(15).Text("Offer & Disbursement Details").Bold().FontSize(12);
                col.Item().PaddingTop(5).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(3);
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(3);
                    });

                    TableCell(table, "Acceptance Method:", true);
                    TableCell(table, tl.AcceptanceMethod ?? "—");
                    TableCell(table, "KFS Acknowledged:", true);
                    TableCell(table, tl.KfsAcknowledged ? "Yes" : "No");

                    TableCell(table, "Core Banking Loan ID:", true);
                    TableCell(table, tl.CoreBankingLoanId ?? "—");
                    TableCell(table, "Disbursement Date:", true);
                    TableCell(table, tl.DisbursedAt.HasValue ? tl.DisbursedAt.Value.ToString("dd-MMM-yyyy") : "—");
                });
            }

            // Processing duration
            if (tl.SubmittedAt.HasValue && tl.FinalApprovedAt.HasValue)
            {
                var processingDays = (int)(tl.FinalApprovedAt.Value - tl.SubmittedAt.Value).TotalDays;
                col.Item().PaddingTop(10)
                    .Background(Colors.Blue.Lighten5).Padding(8)
                    .Text($"Processing time from submission to final approval: {processingDays} day(s).")
                    .FontSize(9).FontColor(Colors.Blue.Darken2);
            }
        });
    }

    private void ComposeCustomerProfile(IContainer container, LoanPackData data)
    {
        container.Column(col =>
        {
            col.Item().Text("CUSTOMER PROFILE").Bold().FontSize(14);
            col.Item().PaddingTop(10);

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(1);
                    cols.RelativeColumn(2);
                });

                TableCell(table, "Company Name:", true);
                TableCell(table, data.Customer.Name);

                TableCell(table, "Registration Number:", true);
                TableCell(table, data.Customer.RegistrationNumber);

                TableCell(table, "Incorporation Date:", true);
                TableCell(table, data.Customer.IncorporationDate?.ToString("dd-MMM-yyyy") ?? "N/A");

                TableCell(table, "Industry:", true);
                TableCell(table, data.Customer.Industry);

                TableCell(table, "Sector:", true);
                TableCell(table, string.IsNullOrWhiteSpace(data.Customer.Sector) ? "N/A" : data.Customer.Sector);

                TableCell(table, "Address:", true);
                TableCell(table, string.IsNullOrWhiteSpace(data.Customer.Address) ? "N/A" : data.Customer.Address);

                TableCell(table, "Phone:", true);
                TableCell(table, string.IsNullOrWhiteSpace(data.Customer.Phone) ? "N/A" : data.Customer.Phone);

                TableCell(table, "Email:", true);
                TableCell(table, string.IsNullOrWhiteSpace(data.Customer.Email) ? "N/A" : data.Customer.Email);

                TableCell(table, "Account Number:", true);
                TableCell(table, data.Customer.AccountNumber);

                TableCell(table, "Account Type:", true);
                TableCell(table, string.IsNullOrWhiteSpace(data.Customer.AccountType) ? "N/A" : data.Customer.AccountType);

                TableCell(table, "Account Open Date:", true);
                TableCell(table, data.Customer.AccountOpenDate?.ToString("dd-MMM-yyyy") ?? "N/A");

                TableCell(table, "Avg Monthly Balance:", true);
                TableCell(table, data.Customer.AverageMonthlyBalance.HasValue
                    ? $"{data.Currency} {data.Customer.AverageMonthlyBalance:N2}" : "N/A");
            });
        });
    }

    private void ComposeDirectorsAndSignatories(IContainer container, LoanPackData data)
    {
        container.Column(col =>
        {
            col.Item().Text("DIRECTORS & SIGNATORIES").Bold().FontSize(14);

            if (data.Directors.Any())
            {
                col.Item().PaddingTop(10).Text("Directors").Bold().FontSize(12);
                col.Item().PaddingTop(5);

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(1);
                        cols.RelativeColumn(1);
                        cols.RelativeColumn(1);
                        cols.RelativeColumn(1);
                        cols.RelativeColumn(2);
                    });

                    TableHeader(table, "Name");
                    TableHeader(table, "Position");
                    TableHeader(table, "Shareholding");
                    TableHeader(table, "Credit Score");
                    TableHeader(table, "Issues");
                    TableHeader(table, "Bureau Summary");

                    foreach (var director in data.Directors)
                    {
                        TableCell(table, director.Name);
                        TableCell(table, director.Position);
                        TableCell(table, director.ShareholdingPercentage.HasValue
                            ? $"{director.ShareholdingPercentage:N1}%" : "N/A");
                        TableCell(table, director.CreditScore?.ToString() ?? "N/A");
                        TableCell(table, director.HasDelinquencies ? "Yes" : "No");
                        TableCell(table, director.CreditSummary ?? "—");
                    }
                });
            }

            if (data.Signatories.Any())
            {
                col.Item().PaddingTop(15).Text("Account Signatories").Bold().FontSize(12);
                col.Item().PaddingTop(5);

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(1);
                        cols.RelativeColumn(1);
                        cols.RelativeColumn(1);
                        cols.RelativeColumn(1);
                    });

                    TableHeader(table, "Name");
                    TableHeader(table, "Position");
                    TableHeader(table, "Class");
                    TableHeader(table, "Credit Score");
                    TableHeader(table, "Issues");

                    foreach (var sig in data.Signatories)
                    {
                        TableCell(table, sig.Name);
                        TableCell(table, sig.Position);
                        TableCell(table, sig.SignatoryClass);
                        TableCell(table, sig.CreditScore?.ToString() ?? "N/A");
                        TableCell(table, sig.HasDelinquencies ? "Yes" : "No");
                    }
                });
            }
        });
    }

    private void ComposeDocuments(IContainer container, LoanPackData data)
    {
        container.Column(col =>
        {
            col.Item().Text("SUPPORTING DOCUMENTS").Bold().FontSize(14);
            col.Item().PaddingTop(4)
                .Text($"Total: {data.Documents.Count} document(s) attached to this application.")
                .FontSize(10).FontColor(Colors.Grey.Darken2);
            col.Item().PaddingTop(10);

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(3);
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(1);
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(3);
                });

                TableHeader(table, "File Name");
                TableHeader(table, "Category");
                TableHeader(table, "Status");
                TableHeader(table, "Uploaded");
                TableHeader(table, "Description");

                foreach (var doc in data.Documents)
                {
                    TableCell(table, doc.FileName);
                    TableCell(table, doc.Category);

                    var statusColor = doc.Status == "Verified" ? Colors.Green.Darken2
                        : doc.Status == "Rejected" ? Colors.Red.Darken2
                        : Colors.Grey.Darken2;
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                        .Text(doc.Status).FontSize(9).FontColor(statusColor);

                    TableCell(table, doc.UploadedAt.ToString("dd-MMM-yy"));
                    TableCell(table, doc.Description ?? "");
                }
            });
        });
    }

    private void ComposeBureauReports(IContainer container, LoanPackData data)
    {
        container.Column(col =>
        {
            col.Item().Text("CREDIT BUREAU REPORTS").Bold().FontSize(14);
            col.Item().PaddingTop(10);

            foreach (var report in data.BureauReports)
            {
                col.Item().Border(1).Padding(10).Column(c =>
                {
                    c.Item().Row(row =>
                    {
                        row.RelativeItem().Text($"{report.SubjectName} ({report.SubjectType})").Bold();
                        row.RelativeItem().AlignRight().Text($"Score: {report.CreditScore?.ToString() ?? "N/A"}")
                            .Bold().FontColor(GetScoreColor(report.CreditScore));
                    });

                    c.Item().Text($"Bureau: {report.BureauProvider} | Report Date: {report.ReportDate:dd-MMM-yyyy}");
                    c.Item().Text($"Rating: {report.CreditRating ?? "N/A"} | Active Loans: {report.ActiveLoanCount} | Outstanding: {data.Currency} {report.TotalOutstandingDebt:N0} | Delinquent Accounts: {report.DelinquentAccountCount}");

                    if (report.HasLegalIssues)
                    {
                        c.Item().Background(Colors.Red.Lighten4).Padding(5)
                            .Text($"LEGAL ISSUES: {report.LegalIssueDetails}").FontColor(Colors.Red.Darken2);
                    }

                    if (report.ActiveLoans.Any())
                    {
                        c.Item().PaddingTop(5).Text("Active Facilities:").Bold();
                        c.Item().Table(t =>
                        {
                            t.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(2);
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(1);
                            });
                            TableHeader(t, "Lender");
                            TableHeader(t, "Facility Type");
                            TableHeader(t, "Original");
                            TableHeader(t, "Outstanding");
                            TableHeader(t, "Status");
                            foreach (var loan in report.ActiveLoans.Take(10))
                            {
                                TableCell(t, loan.LenderName);
                                TableCell(t, loan.FacilityType);
                                TableCell(t, $"{data.Currency} {loan.OriginalAmount:N0}");
                                TableCell(t, $"{data.Currency} {loan.OutstandingBalance:N0}");
                                TableCell(t, loan.Status);
                            }
                        });
                    }

                    if (report.Delinquencies.Any())
                    {
                        c.Item().PaddingTop(5).Text("Delinquent Accounts:").Bold();
                        c.Item().Table(t =>
                        {
                            t.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(2);
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(1);
                            });
                            TableHeader(t, "Lender");
                            TableHeader(t, "Facility Type");
                            TableHeader(t, "Amount");
                            TableHeader(t, "Days Overdue");
                            foreach (var d in report.Delinquencies.Take(5))
                            {
                                TableCell(t, d.LenderName);
                                TableCell(t, d.FacilityType);
                                TableCell(t, $"{data.Currency} {d.Amount:N0}");
                                TableCell(t, d.DaysOverdue.ToString());
                            }
                        });
                    }
                });

                col.Item().PaddingTop(10);
            }
        });
    }

    private void ComposeFinancialAnalysis(IContainer container, LoanPackData data)
    {
        container.Column(col =>
        {
            col.Item().Text("FINANCIAL ANALYSIS").Bold().FontSize(14);
            col.Item().PaddingTop(10);

            col.Item().Text("Financial Statements").Bold().FontSize(12);
            col.Item().PaddingTop(5);

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(2);
                    foreach (var _ in data.FinancialStatements.OrderByDescending(f => f.Year).Take(3))
                        cols.RelativeColumn(1);
                });

                var statements = data.FinancialStatements.OrderByDescending(f => f.Year).Take(3).ToList();

                TableHeader(table, "Item");
                foreach (var stmt in statements)
                    TableHeader(table, $"{stmt.Year} ({stmt.StatementType})");

                AddFinancialRow(table, "Revenue", statements.Select(s => s.Revenue), data.Currency);
                AddFinancialRow(table, "Gross Profit", statements.Select(s => s.GrossProfit), data.Currency);
                AddFinancialRow(table, "Operating Profit", statements.Select(s => s.OperatingProfit), data.Currency);
                AddFinancialRow(table, "Net Profit", statements.Select(s => s.NetProfit), data.Currency);
                AddFinancialRow(table, "EBITDA", statements.Select(s => s.EBITDA), data.Currency);
                AddFinancialRow(table, "Total Assets", statements.Select(s => s.TotalAssets), data.Currency);
                AddFinancialRow(table, "Current Assets", statements.Select(s => s.CurrentAssets), data.Currency);
                AddFinancialRow(table, "Total Liabilities", statements.Select(s => s.TotalLiabilities), data.Currency);
                AddFinancialRow(table, "Current Liabilities", statements.Select(s => s.CurrentLiabilities), data.Currency);
                AddFinancialRow(table, "Long-term Debt", statements.Select(s => s.LongTermDebt), data.Currency);
                AddFinancialRow(table, "Shareholders' Equity", statements.Select(s => s.ShareholdersEquity), data.Currency);
            });

            if (data.FinancialRatios != null)
            {
                col.Item().PaddingTop(15).Text("Key Financial Ratios").Bold().FontSize(12);
                col.Item().PaddingTop(5);

                col.Item().Row(row =>
                {
                    row.RelativeItem().Border(1).Padding(8).Column(c =>
                    {
                        c.Item().Text("Liquidity").Bold();
                        c.Item().Text($"Current Ratio: {data.FinancialRatios.CurrentRatio:N2}x");
                        c.Item().Text($"Quick Ratio: {data.FinancialRatios.QuickRatio:N2}x");
                        c.Item().Text($"Cash Ratio: {data.FinancialRatios.CashRatio:N2}x");
                    });

                    row.RelativeItem().Border(1).Padding(8).Column(c =>
                    {
                        c.Item().Text("Leverage").Bold();
                        c.Item().Text($"Debt/Equity: {data.FinancialRatios.DebtToEquity:N2}x");
                        c.Item().Text($"Debt/Assets: {data.FinancialRatios.DebtToAssets:N2}x");
                        c.Item().Text($"Interest Coverage: {data.FinancialRatios.InterestCoverage:N2}x");
                    });

                    row.RelativeItem().Border(1).Padding(8).Column(c =>
                    {
                        c.Item().Text("Profitability").Bold();
                        c.Item().Text($"Gross Margin: {data.FinancialRatios.GrossMargin:P1}");
                        c.Item().Text($"Net Margin: {data.FinancialRatios.NetMargin:P1}");
                        c.Item().Text($"ROE: {data.FinancialRatios.ReturnOnEquity:P1}");
                        c.Item().Text($"ROA: {data.FinancialRatios.ReturnOnAssets:P1}");
                    });

                    row.RelativeItem().Border(1).Padding(8).Column(c =>
                    {
                        c.Item().Text("Coverage & Efficiency").Bold();
                        c.Item().Text($"DSCR: {data.FinancialRatios.DebtServiceCoverageRatio:N2}x");
                        c.Item().Text($"Asset Turnover: {data.FinancialRatios.AssetTurnover:N2}x");
                        if (data.FinancialRatios.RevenueGrowthYoY.HasValue)
                            c.Item().Text($"Revenue Growth (YoY): {data.FinancialRatios.RevenueGrowthYoY:N1}%");
                        if (data.FinancialRatios.ProfitGrowthYoY.HasValue)
                            c.Item().Text($"Profit Growth (YoY): {data.FinancialRatios.ProfitGrowthYoY:N1}%");
                    });
                });
            }
        });
    }

    private void ComposeCashflowAnalysis(IContainer container, LoanPackData data)
    {
        var cf = data.CashflowAnalysis!;

        container.Column(col =>
        {
            col.Item().Text("CASHFLOW ANALYSIS").Bold().FontSize(14);
            col.Item().PaddingTop(4).Text($"Based on {cf.MonthsAnalyzed} months of bank statement data.").Italic();
            col.Item().PaddingTop(10);

            col.Item().Row(row =>
            {
                row.RelativeItem().Border(1).Padding(10).Column(c =>
                {
                    c.Item().Text("Monthly Averages").Bold();
                    c.Item().Text($"Avg Inflow: {data.Currency} {cf.AverageMonthlyInflow:N0}");
                    c.Item().Text($"Avg Outflow: {data.Currency} {cf.AverageMonthlyOutflow:N0}");
                    c.Item().Text($"Net Cashflow: {data.Currency} {cf.NetCashflow:N0}")
                        .FontColor(cf.NetCashflow >= 0 ? Colors.Green.Darken2 : Colors.Red.Darken2);
                });

                row.RelativeItem().Border(1).Padding(10).Column(c =>
                {
                    c.Item().Text("Balance Analysis").Bold();
                    c.Item().Text($"Avg Balance: {data.Currency} {cf.AverageBalance:N0}");
                    c.Item().Text($"Lowest: {data.Currency} {cf.LowestMonthlyBalance:N0}");
                    c.Item().Text($"Highest: {data.Currency} {cf.HighestMonthlyBalance:N0}");
                });

                row.RelativeItem().Border(1).Padding(10).Column(c =>
                {
                    c.Item().Text("Inflow Breakdown").Bold();
                    c.Item().Text($"Salary: {data.Currency} {cf.SalaryInflows:N0}");
                    c.Item().Text($"Business: {data.Currency} {cf.BusinessInflows:N0}");
                    c.Item().Text($"Other: {data.Currency} {cf.OtherInflows:N0}");
                });

                row.RelativeItem().Border(1).Padding(10).Column(c =>
                {
                    c.Item().Text("Outflow Breakdown").Bold();
                    c.Item().Text($"Loan Repayments: {data.Currency} {cf.LoanRepayments:N0}");
                    c.Item().Text($"Rent/Utilities: {data.Currency} {cf.RentUtilities:N0}");
                    c.Item().Text($"Other: {data.Currency} {cf.OtherOutflows:N0}");
                });
            });

            col.Item().PaddingTop(10).Row(row =>
            {
                row.RelativeItem().Border(1).Padding(10).Column(c =>
                {
                    c.Item().Text("Quality Metrics").Bold();
                    c.Item().Text($"Inflow Volatility: {cf.InflowVolatility:P1}");
                    c.Item().Text($"Balance Volatility: {cf.BalanceVolatility:P1}");
                    c.Item().Text($"Returned Cheques: {cf.ReturnedChequeCount}");
                    c.Item().Text($"Overdraft Usage: {cf.OverdraftUtilization:P1}");
                });

                row.RelativeItem().Border(1).Padding(15).Column(c =>
                {
                    c.Item().Text("Trust Assessment").Bold();
                    c.Item().AlignCenter().Text(cf.TrustLevel).FontSize(18).Bold()
                        .FontColor(cf.TrustLevel == "High" ? Colors.Green.Darken2 :
                                   cf.TrustLevel == "Medium" ? Colors.Orange.Darken2 : Colors.Red.Darken2);
                    c.Item().AlignCenter().Text($"Weighted Score: {cf.TrustWeightedScore:N0}");
                });
            });
        });
    }

    private void ComposeCollateral(IContainer container, LoanPackData data)
    {
        container.Column(col =>
        {
            col.Item().Text("COLLATERAL").Bold().FontSize(14);
            col.Item().PaddingTop(4)
                .Text($"Total Acceptable Value: {data.Currency} {data.TotalCollateralValue:N0}  |  Coverage Ratio: {data.CollateralCoverageRatio:P0}")
                .Bold();
            col.Item().PaddingTop(10);

            // Valuation summary table
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(1);   // Type
                    cols.RelativeColumn(2);   // Description / Location
                    cols.RelativeColumn(1);   // Market Value
                    cols.RelativeColumn(1);   // FSV
                    cols.RelativeColumn(1);   // Acceptable
                    cols.RelativeColumn(1);   // Status
                });

                TableHeader(table, "Type");
                TableHeader(table, "Description");
                TableHeader(table, "Market Value");
                TableHeader(table, "FSV");
                TableHeader(table, "Acceptable");
                TableHeader(table, "Status");

                foreach (var c in data.Collaterals)
                {
                    TableCell(table, c.Type);
                    TableCell(table, string.IsNullOrWhiteSpace(c.Location)
                        ? c.Description
                        : $"{c.Description}\n{c.Location}");
                    TableCell(table, $"{data.Currency} {c.MarketValue:N0}");
                    TableCell(table, $"{data.Currency} {c.ForcedSaleValue:N0}");
                    TableCell(table, $"{data.Currency} {c.AcceptableValue:N0}");

                    var statusColor = c.Status is "Approved" or "Perfected" ? Colors.Green.Darken2
                        : c.Status == "Rejected" ? Colors.Red.Darken2
                        : Colors.Grey.Darken2;
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                        .Text(c.Status).FontSize(9).FontColor(statusColor);
                }
            });

            // Per-collateral detail cards (valuer, lien, insurance, legal)
            col.Item().PaddingTop(15).Text("Collateral Detail").Bold().FontSize(12);

            foreach (var c in data.Collaterals)
            {
                col.Item().PaddingTop(8).Border(1).Padding(10).Column(card =>
                {
                    card.Item().Row(row =>
                    {
                        row.RelativeItem().Text($"{c.Type} — {c.Description}").Bold();
                        var legalBadge = c.IsLegalCleared
                            ? "Legal Cleared ✓"
                            : "Legal Clearance Pending";
                        var legalColor = c.IsLegalCleared ? Colors.Green.Darken2 : Colors.Orange.Darken2;
                        row.AutoItem().Text(legalBadge).FontSize(9).FontColor(legalColor);
                    });

                    card.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(1);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(1);
                            cols.RelativeColumn(2);
                        });

                        TableCell(table, "Valuation Date:", true);
                        TableCell(table, string.IsNullOrWhiteSpace(c.ValuationDate) ? "Not yet valued" : c.ValuationDate);
                        TableCell(table, "Valuer:", true);
                        TableCell(table, string.IsNullOrWhiteSpace(c.ValuerName) ? "—" : c.ValuerName);

                        TableCell(table, "Lien Type:", true);
                        TableCell(table, string.IsNullOrWhiteSpace(c.LienType) ? "—" : c.LienType);
                        TableCell(table, "Lien Reference:", true);
                        TableCell(table, c.LienReference ?? "—");

                        TableCell(table, "Insurance Policy:", true);
                        TableCell(table, c.InsurancePolicy ?? "—");
                        TableCell(table, "Insurance Expiry:", true);
                        TableCell(table, c.InsuranceExpiry.HasValue ? c.InsuranceExpiry.Value.ToString("dd-MMM-yyyy") : "—");

                        if (c.IsLegalCleared && c.LegalClearedAt.HasValue)
                        {
                            TableCell(table, "Legal Cleared At:", true);
                            TableCell(table, c.LegalClearedAt.Value.ToString("dd-MMM-yyyy HH:mm"));
                            TableCell(table, "", false);
                            TableCell(table, "");
                        }
                    });
                });
            }
        });
    }

    private void ComposeGuarantors(IContainer container, LoanPackData data)
    {
        container.Column(col =>
        {
            col.Item().Text("GUARANTORS").Bold().FontSize(14);
            col.Item().PaddingTop(4)
                .Text($"Total Guarantee Amount: {data.Currency} {data.TotalGuaranteeAmount:N0}").Bold();
            col.Item().PaddingTop(10);

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(1);
                    cols.RelativeColumn(1);
                    cols.RelativeColumn(1);
                    cols.RelativeColumn(1);
                    cols.RelativeColumn(1);
                    cols.RelativeColumn(1);
                });

                TableHeader(table, "Name");
                TableHeader(table, "Type");
                TableHeader(table, "Relationship");
                TableHeader(table, "Net Worth");
                TableHeader(table, "Guarantee");
                TableHeader(table, "Credit Score");
                TableHeader(table, "Status");

                foreach (var g in data.Guarantors)
                {
                    TableCell(table, g.Name);
                    TableCell(table, g.Type);
                    TableCell(table, g.Relationship);
                    TableCell(table, $"{data.Currency} {g.NetWorth:N0}");
                    TableCell(table, $"{data.Currency} {g.GuaranteeAmount:N0}");
                    TableCell(table, g.CreditScore?.ToString() ?? "N/A");
                    TableCell(table, g.Status);
                }
            });

            // Guarantor address/contact cards
            var guarantorsWithAddress = data.Guarantors.Where(g =>
                !string.IsNullOrWhiteSpace(g.Address) || !string.IsNullOrWhiteSpace(g.Phone)).ToList();
            if (guarantorsWithAddress.Any())
            {
                col.Item().PaddingTop(10).Text("Guarantor Contact Details").Bold().FontSize(12);
                foreach (var g in guarantorsWithAddress)
                {
                    col.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(1);
                            cols.RelativeColumn(3);
                        });
                        TableCell(table, g.Name, true);
                        TableCell(table, $"Address: {g.Address}  |  Phone: {g.Phone}");
                    });
                }
            }
        });
    }

    private void ComposeAIAdvisory(IContainer container, LoanPackData data)
    {
        var ai = data.AIAdvisory!;

        container.Column(col =>
        {
            col.Item().Text("AI ADVISORY ASSESSMENT").Bold().FontSize(14);
            col.Item().PaddingTop(10);

            col.Item().Row(row =>
            {
                row.RelativeItem().Border(2).Padding(15).Column(c =>
                {
                    c.Item().AlignCenter().Text("OVERALL RISK SCORE").Bold();
                    c.Item().AlignCenter().Text(ai.OverallRiskScore.ToString())
                        .FontSize(36).Bold().FontColor(GetRiskColor(ai.RiskRating));
                    c.Item().AlignCenter().Text(ai.RiskRating).FontSize(14)
                        .FontColor(GetRiskColor(ai.RiskRating));
                });

                row.RelativeItem(2).Padding(10).Column(c =>
                {
                    c.Item().Text("Risk Summary").Bold();
                    c.Item().Text(ai.RiskSummary);
                });
            });

            col.Item().PaddingTop(15).Text("Component Scores").Bold().FontSize(12);
            col.Item().PaddingTop(5);

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(1);
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(1);
                });

                TableCell(table, "Credit History:", true);
                TableCell(table, ai.CreditHistoryScore.ToString());
                TableCell(table, "Financial Strength:", true);
                TableCell(table, ai.FinancialStrengthScore.ToString());

                TableCell(table, "Cashflow Quality:", true);
                TableCell(table, ai.CashflowQualityScore.ToString());
                TableCell(table, "Collateral Coverage:", true);
                TableCell(table, ai.CollateralCoverageScore.ToString());

                TableCell(table, "Industry Risk:", true);
                TableCell(table, ai.IndustryRiskScore.ToString());
                TableCell(table, "Management Quality:", true);
                TableCell(table, ai.ManagementQualityScore.ToString());

                TableCell(table, "Relationship Strength:", true);
                TableCell(table, ai.RelationshipStrengthScore.ToString());
                TableCell(table, "External Factors:", true);
                TableCell(table, ai.ExternalFactorsScore.ToString());
            });

            col.Item().PaddingTop(15).Text("Recommendations").Bold().FontSize(12);
            col.Item().PaddingTop(5).Border(1).Padding(10).Column(c =>
            {
                c.Item().Text($"Amount: {ai.AmountRecommendation}");
                c.Item().Text($"Tenor: {ai.TenorRecommendation}");
                c.Item().Text($"Pricing: {ai.PricingRecommendation}");
                if (!string.IsNullOrWhiteSpace(ai.StructuringRecommendation))
                    c.Item().Text($"Structuring: {ai.StructuringRecommendation}");
            });

            if (ai.RecommendedConditions.Any())
            {
                col.Item().PaddingTop(10).Text("Recommended Conditions").Bold().FontSize(12);
                col.Item().PaddingTop(5).Border(1).Padding(10).Column(c =>
                {
                    foreach (var condition in ai.RecommendedConditions)
                        c.Item().Text($"• {condition}");
                });
            }
        });
    }

    private void ComposeCreditOfficerNotes(IContainer container, LoanPackData data)
    {
        container.Column(col =>
        {
            col.Item().Text("CREDIT OFFICER NOTES").Bold().FontSize(14);
            col.Item().PaddingTop(4)
                .Text("Application-level notes and observations logged by the processing team.")
                .FontSize(10).FontColor(Colors.Grey.Darken2);
            col.Item().PaddingTop(10);

            // Group notes by category for readability
            var grouped = data.CreditOfficerNotes
                .GroupBy(n => string.IsNullOrWhiteSpace(n.Category) ? "General" : n.Category)
                .OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                col.Item().PaddingTop(6).Text(group.Key.ToUpper()).Bold().FontSize(10)
                    .FontColor(Colors.Grey.Darken3);

                foreach (var note in group)
                {
                    col.Item().PaddingTop(4).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(c =>
                    {
                        c.Item().AlignRight().Text(note.CreatedAt.ToString("dd-MMM-yyyy HH:mm"))
                            .FontSize(8).FontColor(Colors.Grey.Darken1);
                        c.Item().PaddingTop(3).Text(note.Content).FontSize(10);
                    });
                }

                col.Item().PaddingTop(4);
            }
        });
    }

    private void ComposeCommitteeComments(IContainer container, LoanPackData data)
    {
        container.Column(col =>
        {
            col.Item().PaddingTop(15).Text("COMMITTEE COMMENTS").Bold().FontSize(14);
            col.Item().PaddingTop(10);

            foreach (var comment in data.CommitteeComments.OrderByDescending(c => c.Timestamp))
            {
                col.Item().Border(1).Padding(10).Column(c =>
                {
                    c.Item().Row(row =>
                    {
                        row.RelativeItem().Text($"{comment.MemberName} ({comment.MemberRole})").Bold();
                        row.RelativeItem().AlignRight().Text(comment.Timestamp.ToString("dd-MMM-yy HH:mm"));
                    });

                    if (!string.IsNullOrEmpty(comment.Vote))
                    {
                        c.Item().Text($"Vote: {comment.Vote}").Bold()
                            .FontColor(comment.Vote == "Approve" ? Colors.Green.Darken2 :
                                       comment.Vote == "Reject" ? Colors.Red.Darken2 : Colors.Grey.Darken2);
                    }

                    c.Item().PaddingTop(5).Text(comment.Comment);
                });

                col.Item().PaddingTop(5);
            }
        });
    }

    private void ComposeCommitteeDecision(IContainer container, LoanPackData data)
    {
        var decision = data.CommitteeDecision!;

        container.Column(col =>
        {
            col.Item().Text("COMMITTEE DECISION").Bold().FontSize(14);
            col.Item().PaddingTop(10);

            var decisionColor = decision.Decision == "Approved" ? Colors.Green.Darken2
                : decision.Decision == "Rejected" ? Colors.Red.Darken2
                : Colors.Orange.Darken2;

            col.Item().Background(decision.Decision == "Approved" ? Colors.Green.Lighten4
                : decision.Decision == "Rejected" ? Colors.Red.Lighten4
                : Colors.Orange.Lighten4)
                .Padding(12).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(decision.Decision.ToUpper()).FontSize(18).Bold().FontColor(decisionColor);
                        if (!string.IsNullOrEmpty(decision.DecisionRationale))
                            c.Item().PaddingTop(4).Text(decision.DecisionRationale).FontSize(10);
                    });

                    row.AutoItem().Padding(8).Column(c =>
                    {
                        c.Item().AlignCenter().Text($"{decision.ApprovalVotes}").FontSize(22).Bold().FontColor(Colors.Green.Darken2);
                        c.Item().AlignCenter().Text("Approve").FontSize(9);
                    });
                    row.AutoItem().Padding(8).Column(c =>
                    {
                        c.Item().AlignCenter().Text($"{decision.RejectionVotes}").FontSize(22).Bold().FontColor(Colors.Red.Darken2);
                        c.Item().AlignCenter().Text("Reject").FontSize(9);
                    });
                    if (decision.AbstainVotes > 0)
                    {
                        row.AutoItem().Padding(8).Column(c =>
                        {
                            c.Item().AlignCenter().Text($"{decision.AbstainVotes}").FontSize(22).Bold().FontColor(Colors.Grey.Darken2);
                            c.Item().AlignCenter().Text("Abstain").FontSize(9);
                        });
                    }
                    if (decision.PendingVotes > 0)
                    {
                        row.AutoItem().Padding(8).Column(c =>
                        {
                            c.Item().AlignCenter().Text($"{decision.PendingVotes}").FontSize(22).Bold().FontColor(Colors.Orange.Darken2);
                            c.Item().AlignCenter().Text("Pending").FontSize(9);
                        });
                    }
                });

            if (decision.RecommendedAmount.HasValue || decision.RecommendedTenorMonths.HasValue || decision.RecommendedInterestRate.HasValue)
            {
                col.Item().PaddingTop(10).Text("Committee Approved Terms").Bold().FontSize(12);
                col.Item().PaddingTop(5).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(1);
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(1);
                        cols.RelativeColumn(2);
                    });

                    TableCell(table, "Amount:", true);
                    TableCell(table, decision.RecommendedAmount.HasValue ? $"{data.Currency} {decision.RecommendedAmount:N2}" : "—");
                    TableCell(table, "Tenor:", true);
                    TableCell(table, decision.RecommendedTenorMonths.HasValue ? $"{decision.RecommendedTenorMonths} months" : "—");

                    TableCell(table, "Interest Rate:", true);
                    TableCell(table, decision.RecommendedInterestRate.HasValue ? $"{decision.RecommendedInterestRate:N2}%" : "—");
                    TableCell(table, "", false);
                    TableCell(table, "");
                });
            }

            if (decision.MemberVotes.Any())
            {
                col.Item().PaddingTop(15).Text("Member Votes").Bold().FontSize(12);
                col.Item().PaddingTop(5).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(1);
                        cols.RelativeColumn(1);
                        cols.RelativeColumn(1);
                        cols.RelativeColumn(3);
                    });

                    TableHeader(table, "Member");
                    TableHeader(table, "Role");
                    TableHeader(table, "Vote");
                    TableHeader(table, "Voted At");
                    TableHeader(table, "Comment");

                    foreach (var vote in decision.MemberVotes)
                    {
                        TableCell(table, vote.MemberName);
                        TableCell(table, vote.MemberRole);

                        var voteColor = vote.Vote == "Approve" ? Colors.Green.Darken2
                            : vote.Vote == "Reject" ? Colors.Red.Darken2
                            : Colors.Grey.Darken2;
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                            .Text(vote.Vote ?? "Pending").FontSize(9).Bold().FontColor(voteColor);

                        TableCell(table, vote.VotedAt.HasValue ? vote.VotedAt.Value.ToString("dd-MMM-yy HH:mm") : "—");
                        TableCell(table, vote.VoteComment ?? "");
                    }
                });
            }
        });
    }

    private void ComposeConditionsOfApproval(IContainer container, LoanPackData data)
    {
        container.Column(col =>
        {
            col.Item().Text("CONDITIONS OF APPROVAL").Bold().FontSize(14);
            col.Item().PaddingTop(4).Text(
                "The following conditions were stipulated by the Credit Committee as part of the approval decision. " +
                "All Conditions Precedent must be satisfied before disbursement. " +
                "Conditions Subsequent are monitored post-disbursement per the terms agreed in the offer letter.")
                .FontSize(10).FontColor(Colors.Grey.Darken2);

            col.Item().PaddingTop(10).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.ConstantColumn(30);
                    cols.RelativeColumn();
                });

                table.Header(header =>
                {
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(6).Text("#").Bold().FontSize(9);
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(6).Text("Condition").Bold().FontSize(9);
                });

                for (var i = 0; i < data.ApprovalConditions.Count; i++)
                {
                    var rowBg = i % 2 == 0 ? Colors.White : Colors.Grey.Lighten5;
                    table.Cell().Background(rowBg).Padding(6).Text($"{i + 1}").FontSize(10);
                    table.Cell().Background(rowBg).Padding(6).Text(data.ApprovalConditions[i]).FontSize(10);
                }
            });
        });
    }

    private void ComposeDisbursementChecklist(IContainer container, LoanPackData data)
    {
        var cpItems = data.DisbursementChecklist.Where(c => c.ConditionType == "Precedent").ToList();
        var csItems = data.DisbursementChecklist.Where(c => c.ConditionType == "Subsequent").ToList();

        container.Column(col =>
        {
            col.Item().Text("DISBURSEMENT CHECKLIST").Bold().FontSize(14);
            col.Item().PaddingTop(4).Text(
                "Conditions Precedent (CP) must be satisfied or waived before offer acceptance is confirmed. " +
                "Conditions Subsequent (CS) are monitored after disbursement.")
                .FontSize(10).FontColor(Colors.Grey.Darken2);

            if (cpItems.Any())
            {
                col.Item().PaddingTop(12).Text("Conditions Precedent (CP)").Bold().FontSize(12);
                col.Item().PaddingTop(5).Element(c => RenderChecklistTable(c, cpItems, data.Currency));
            }

            if (csItems.Any())
            {
                col.Item().PaddingTop(15).Text("Conditions Subsequent (CS)").Bold().FontSize(12);
                col.Item().PaddingTop(5).Element(c => RenderChecklistTable(c, csItems, data.Currency));
            }
        });
    }

    private void RenderChecklistTable(IContainer container, List<ChecklistItemData> items, string currency)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.RelativeColumn(3);   // Item Name
                cols.RelativeColumn(1);   // Mandatory
                cols.RelativeColumn(1);   // Status
                cols.RelativeColumn(2);   // Satisfied At / Due Date
                cols.RelativeColumn(2);   // Waiver / Note
            });

            TableHeader(table, "Condition");
            TableHeader(table, "Mandatory");
            TableHeader(table, "Status");
            TableHeader(table, "Satisfied / Due");
            TableHeader(table, "Waiver / Notes");

            foreach (var item in items)
            {
                var statusColor = item.Status is "Satisfied" or "WaiverApproved"
                    ? Colors.Green.Darken2
                    : item.Status is "Rejected" or "WaiverRejected"
                        ? Colors.Red.Darken2
                        : Colors.Orange.Darken2;

                // Item name + description
                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Column(c =>
                {
                    c.Item().Text(item.ItemName).Bold().FontSize(9);
                    if (!string.IsNullOrWhiteSpace(item.Description) && item.Description != item.ItemName)
                        c.Item().Text(item.Description).FontSize(8).FontColor(Colors.Grey.Darken2);
                });

                TableCell(table, item.IsMandatory ? "Yes" : "No");

                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                    .Text(item.Status).FontSize(9).FontColor(statusColor);

                var dateText = item.SatisfiedAt.HasValue
                    ? item.SatisfiedAt.Value.ToString("dd-MMM-yy")
                    : item.DueDate.HasValue
                        ? $"Due: {item.DueDate.Value:dd-MMM-yy}"
                        : "—";
                TableCell(table, dateText);

                var waiverText = !string.IsNullOrWhiteSpace(item.WaiverReason)
                    ? $"Waiver: {item.WaiverReason}"
                        + (item.WaiverProposedAt.HasValue
                            ? $" ({item.WaiverProposedAt.Value:dd-MMM-yy})" : "")
                    : "";
                TableCell(table, waiverText);
            }
        });
    }

    private void ComposeApprovalAuditTrail(IContainer container, LoanPackData data)
    {
        container.Column(col =>
        {
            col.Item().Text("APPROVAL AUDIT TRAIL").Bold().FontSize(14);
            col.Item().PaddingTop(4)
                .Text("Chronological record of all status transitions for this application.")
                .FontSize(10).FontColor(Colors.Grey.Darken2);
            col.Item().PaddingTop(10);

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(2);   // Date/Time
                    cols.RelativeColumn(2);   // Status
                    cols.RelativeColumn(2);   // Actor
                    cols.RelativeColumn(5);   // Comment
                });

                TableHeader(table, "Date / Time");
                TableHeader(table, "Status");
                TableHeader(table, "Actor");
                TableHeader(table, "Comment / Action");

                foreach (var entry in data.ApprovalAuditTrail)
                {
                    TableCell(table, entry.ChangedAt.ToString("dd-MMM-yyyy HH:mm"));
                    TableCell(table, entry.Status);
                    TableCell(table, entry.ActorName);
                    TableCell(table, entry.Comment ?? "");
                }
            });
        });
    }

    private void ComposeFooter(IContainer container, LoanPackData data)
    {
        container.Column(col =>
        {
            col.Item().LineHorizontal(1);
            col.Item().PaddingTop(5).Row(row =>
            {
                row.RelativeItem().Text($"Application: {data.ApplicationNumber}").FontSize(8);
                row.RelativeItem().AlignCenter().DefaultTextStyle(x => x.FontSize(8)).Text(x =>
                {
                    x.Span("Page ");
                    x.CurrentPageNumber();
                    x.Span(" of ");
                    x.TotalPages();
                });
                row.RelativeItem().AlignRight().Text($"Generated: {data.GeneratedAt:dd-MMM-yyyy HH:mm} | v{data.Version}").FontSize(8);
            });
        });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helper methods
    // ──────────────────────────────────────────────────────────────────────────

    private static void TableHeader(TableDescriptor table, string text)
    {
        table.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text(text).Bold().FontSize(9);
    }

    private static void TableCell(TableDescriptor table, string text, bool bold = false)
    {
        var cell = table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(text).FontSize(9);
        if (bold) cell.Bold();
    }

    private static void TimelineRow(TableDescriptor table, string label, DateTime? date)
    {
        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
            .Text(label).FontSize(9).Bold();
        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
            .Text(date.HasValue ? date.Value.ToString("dd-MMM-yyyy HH:mm") : "—")
            .FontSize(9)
            .FontColor(date.HasValue ? Colors.Black : Colors.Grey.Darken1);
    }

    private static void AddFinancialRow(TableDescriptor table, string label, IEnumerable<decimal?> values, string currency)
    {
        TableCell(table, label, true);
        foreach (var value in values)
            TableCell(table, value.HasValue ? $"{currency} {value:N0}" : "N/A");
    }

    private static Color GetRiskColor(string? riskRating) => riskRating switch
    {
        "Low" => Colors.Green.Darken2,
        "Moderate" => Colors.Orange.Darken2,
        "High" => Colors.Red.Darken2,
        "VeryHigh" => Colors.Red.Darken4,
        _ => Colors.Grey.Darken2
    };

    private static Color GetScoreColor(int? score) => score switch
    {
        >= 700 => Colors.Green.Darken2,
        >= 600 => Colors.Orange.Darken2,
        < 600 => Colors.Red.Darken2,
        _ => Colors.Grey.Darken2
    };
}

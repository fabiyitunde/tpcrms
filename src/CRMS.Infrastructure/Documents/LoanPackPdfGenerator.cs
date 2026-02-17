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
        container.Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("CREDIT RISK MANAGEMENT SYSTEM").Bold().FontSize(14);
                col.Item().Text("LOAN APPLICATION PACK").FontSize(12);
            });

            row.RelativeItem().AlignRight().Column(col =>
            {
                col.Item().Text($"Application: {data.ApplicationNumber}").Bold();
                col.Item().Text($"Generated: {data.GeneratedAt:dd-MMM-yyyy HH:mm}");
                col.Item().Text($"Version: {data.Version}");
            });
        });

        container.PaddingTop(10).LineHorizontal(1);
    }

    private void ComposeContent(IContainer container, LoanPackData data)
    {
        container.PaddingVertical(10).Column(col =>
        {
            // 1. Executive Summary
            col.Item().Element(c => ComposeExecutiveSummary(c, data));
            col.Item().PageBreak();

            // 2. Customer Profile
            col.Item().Element(c => ComposeCustomerProfile(c, data));
            col.Item().PageBreak();

            // 3. Directors & Signatories
            if (data.Directors.Any() || data.Signatories.Any())
            {
                col.Item().Element(c => ComposeDirectorsAndSignatories(c, data));
                col.Item().PageBreak();
            }

            // 4. Bureau Reports
            if (data.BureauReports.Any())
            {
                col.Item().Element(c => ComposeBureauReports(c, data));
                col.Item().PageBreak();
            }

            // 5. Financial Analysis
            if (data.FinancialStatements.Any())
            {
                col.Item().Element(c => ComposeFinancialAnalysis(c, data));
                col.Item().PageBreak();
            }

            // 6. Cashflow Analysis
            if (data.CashflowAnalysis != null)
            {
                col.Item().Element(c => ComposeCashflowAnalysis(c, data));
                col.Item().PageBreak();
            }

            // 7. Collateral
            if (data.Collaterals.Any())
            {
                col.Item().Element(c => ComposeCollateral(c, data));
                col.Item().PageBreak();
            }

            // 8. Guarantors
            if (data.Guarantors.Any())
            {
                col.Item().Element(c => ComposeGuarantors(c, data));
                col.Item().PageBreak();
            }

            // 9. AI Advisory
            if (data.AIAdvisory != null)
            {
                col.Item().Element(c => ComposeAIAdvisory(c, data));
                col.Item().PageBreak();
            }

            // 10. Workflow History
            if (data.WorkflowHistory.Any())
            {
                col.Item().Element(c => ComposeWorkflowHistory(c, data));
            }

            // 11. Committee Comments
            if (data.CommitteeComments.Any())
            {
                col.Item().Element(c => ComposeCommitteeComments(c, data));
            }
        });
    }

    private void ComposeExecutiveSummary(IContainer container, LoanPackData data)
    {
        container.Column(col =>
        {
            col.Item().Text("EXECUTIVE SUMMARY").Bold().FontSize(14);
            col.Item().PaddingTop(10);

            // Application Overview Table
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(1);
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(1);
                    cols.RelativeColumn(2);
                });

                // Row 1
                TableCell(table, "Customer:", true);
                TableCell(table, data.Customer.Name);
                TableCell(table, "Application Date:", true);
                TableCell(table, data.ApplicationDate.ToString("dd-MMM-yyyy"));

                // Row 2
                TableCell(table, "Product:", true);
                TableCell(table, $"{data.LoanProductName} ({data.LoanProductCode})");
                TableCell(table, "Account Number:", true);
                TableCell(table, data.Customer.AccountNumber);

                // Row 3
                TableCell(table, "Requested Amount:", true);
                TableCell(table, $"{data.Currency} {data.RequestedAmount:N2}");
                TableCell(table, "Tenor:", true);
                TableCell(table, $"{data.RequestedTenorMonths} months");

                // Row 4
                TableCell(table, "Interest Rate:", true);
                TableCell(table, $"{data.RequestedInterestRate:N2}%");
                TableCell(table, "Purpose:", true);
                TableCell(table, data.Purpose);
            });

            col.Item().PaddingTop(15);

            // Key Metrics
            col.Item().Text("KEY METRICS").Bold().FontSize(12);
            col.Item().PaddingTop(5);

            col.Item().Row(row =>
            {
                // Risk Score Box
                if (data.AIAdvisory != null)
                {
                    row.RelativeItem().Border(1).Padding(10).Column(c =>
                    {
                        c.Item().AlignCenter().Text("RISK SCORE").Bold();
                        c.Item().AlignCenter().Text(data.AIAdvisory.OverallRiskScore.ToString())
                            .FontSize(24).Bold()
                            .FontColor(GetRiskColor(data.AIAdvisory.RiskRating));
                        c.Item().AlignCenter().Text(data.AIAdvisory.RiskRating);
                    });
                }

                // Collateral Coverage Box
                row.RelativeItem().Border(1).Padding(10).Column(c =>
                {
                    c.Item().AlignCenter().Text("COLLATERAL COVERAGE").Bold();
                    c.Item().AlignCenter().Text($"{data.CollateralCoverageRatio:P0}").FontSize(24).Bold();
                    c.Item().AlignCenter().Text($"{data.Currency} {data.TotalCollateralValue:N0}");
                });

                // Bureau Summary Box
                row.RelativeItem().Border(1).Padding(10).Column(c =>
                {
                    c.Item().AlignCenter().Text("BUREAU CHECKS").Bold();
                    var avgScore = data.BureauReports.Where(b => b.CreditScore.HasValue)
                        .Select(b => b.CreditScore!.Value).DefaultIfEmpty(0).Average();
                    c.Item().AlignCenter().Text($"{avgScore:N0}").FontSize(24).Bold();
                    c.Item().AlignCenter().Text($"{data.BureauReports.Count} reports");
                });

                // Recommendation Box
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

            // Red Flags Section
            if (data.AIAdvisory?.RedFlags.Any() == true)
            {
                col.Item().PaddingTop(15);
                col.Item().Background(Colors.Red.Lighten4).Padding(10).Column(c =>
                {
                    c.Item().Text("RED FLAGS").Bold().FontColor(Colors.Red.Darken2);
                    foreach (var flag in data.AIAdvisory.RedFlags)
                    {
                        c.Item().Text($"• {flag}").FontColor(Colors.Red.Darken2);
                    }
                });
            }

            // Mitigating Factors
            if (data.AIAdvisory?.MitigatingFactors.Any() == true)
            {
                col.Item().PaddingTop(10);
                col.Item().Background(Colors.Green.Lighten4).Padding(10).Column(c =>
                {
                    c.Item().Text("MITIGATING FACTORS").Bold().FontColor(Colors.Green.Darken2);
                    foreach (var factor in data.AIAdvisory.MitigatingFactors)
                    {
                        c.Item().Text($"• {factor}").FontColor(Colors.Green.Darken2);
                    }
                });
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
                TableCell(table, data.Customer.Sector);

                TableCell(table, "Address:", true);
                TableCell(table, data.Customer.Address);

                TableCell(table, "Phone:", true);
                TableCell(table, data.Customer.Phone);

                TableCell(table, "Email:", true);
                TableCell(table, data.Customer.Email);

                TableCell(table, "Account Number:", true);
                TableCell(table, data.Customer.AccountNumber);

                TableCell(table, "Account Type:", true);
                TableCell(table, data.Customer.AccountType);

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

            // Directors
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

                    // Header
                    TableHeader(table, "Name");
                    TableHeader(table, "Position");
                    TableHeader(table, "Shareholding");
                    TableHeader(table, "Credit Score");
                    TableHeader(table, "Issues");
                    TableHeader(table, "Summary");

                    foreach (var director in data.Directors)
                    {
                        TableCell(table, director.Name);
                        TableCell(table, director.Position);
                        TableCell(table, director.ShareholdingPercentage.HasValue 
                            ? $"{director.ShareholdingPercentage:N1}%" : "N/A");
                        TableCell(table, director.CreditScore?.ToString() ?? "N/A");
                        TableCell(table, director.HasDelinquencies ? "Yes" : "No");
                        TableCell(table, director.CreditSummary ?? "");
                    }
                });
            }

            // Signatories
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
                    c.Item().Text($"Rating: {report.CreditRating ?? "N/A"} | Active Loans: {report.ActiveLoanCount} | Outstanding: {data.Currency} {report.TotalOutstandingDebt:N0}");

                    if (report.HasLegalIssues)
                    {
                        c.Item().Background(Colors.Red.Lighten4).Padding(5)
                            .Text($"LEGAL ISSUES: {report.LegalIssueDetails}").FontColor(Colors.Red.Darken2);
                    }

                    if (report.Delinquencies.Any())
                    {
                        c.Item().PaddingTop(5).Text("Delinquencies:").Bold();
                        foreach (var d in report.Delinquencies.Take(5))
                        {
                            c.Item().Text($"  • {d.LenderName}: {data.Currency} {d.Amount:N0} ({d.DaysOverdue} days overdue)");
                        }
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

            // Financial Statements Summary
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
                    TableHeader(table, stmt.Year.ToString());

                AddFinancialRow(table, "Revenue", statements.Select(s => s.Revenue), data.Currency);
                AddFinancialRow(table, "Gross Profit", statements.Select(s => s.GrossProfit), data.Currency);
                AddFinancialRow(table, "Operating Profit", statements.Select(s => s.OperatingProfit), data.Currency);
                AddFinancialRow(table, "Net Profit", statements.Select(s => s.NetProfit), data.Currency);
                AddFinancialRow(table, "EBITDA", statements.Select(s => s.EBITDA), data.Currency);
                AddFinancialRow(table, "Total Assets", statements.Select(s => s.TotalAssets), data.Currency);
                AddFinancialRow(table, "Total Liabilities", statements.Select(s => s.TotalLiabilities), data.Currency);
                AddFinancialRow(table, "Shareholders' Equity", statements.Select(s => s.ShareholdersEquity), data.Currency);
            });

            // Financial Ratios
            if (data.FinancialRatios != null)
            {
                col.Item().PaddingTop(15).Text("Key Financial Ratios").Bold().FontSize(12);
                col.Item().PaddingTop(5);

                col.Item().Row(row =>
                {
                    // Liquidity Ratios
                    row.RelativeItem().Border(1).Padding(8).Column(c =>
                    {
                        c.Item().Text("Liquidity").Bold();
                        c.Item().Text($"Current Ratio: {data.FinancialRatios.CurrentRatio:N2}");
                        c.Item().Text($"Quick Ratio: {data.FinancialRatios.QuickRatio:N2}");
                    });

                    // Leverage Ratios
                    row.RelativeItem().Border(1).Padding(8).Column(c =>
                    {
                        c.Item().Text("Leverage").Bold();
                        c.Item().Text($"Debt/Equity: {data.FinancialRatios.DebtToEquity:N2}");
                        c.Item().Text($"Interest Coverage: {data.FinancialRatios.InterestCoverage:N2}x");
                    });

                    // Profitability Ratios
                    row.RelativeItem().Border(1).Padding(8).Column(c =>
                    {
                        c.Item().Text("Profitability").Bold();
                        c.Item().Text($"Net Margin: {data.FinancialRatios.NetMargin:P1}");
                        c.Item().Text($"ROE: {data.FinancialRatios.ReturnOnEquity:P1}");
                    });

                    // Coverage
                    row.RelativeItem().Border(1).Padding(8).Column(c =>
                    {
                        c.Item().Text("Coverage").Bold();
                        c.Item().Text($"DSCR: {data.FinancialRatios.DebtServiceCoverageRatio:N2}x");
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
            col.Item().PaddingTop(10);

            col.Item().Text($"Based on {cf.MonthsAnalyzed} months of bank statement data").Italic();
            col.Item().PaddingTop(10);

            col.Item().Row(row =>
            {
                // Inflows/Outflows
                row.RelativeItem().Border(1).Padding(10).Column(c =>
                {
                    c.Item().Text("Monthly Averages").Bold();
                    c.Item().Text($"Avg Inflow: {data.Currency} {cf.AverageMonthlyInflow:N0}");
                    c.Item().Text($"Avg Outflow: {data.Currency} {cf.AverageMonthlyOutflow:N0}");
                    c.Item().Text($"Net Cashflow: {data.Currency} {cf.NetCashflow:N0}")
                        .FontColor(cf.NetCashflow >= 0 ? Colors.Green.Darken2 : Colors.Red.Darken2);
                });

                // Balance Analysis
                row.RelativeItem().Border(1).Padding(10).Column(c =>
                {
                    c.Item().Text("Balance Analysis").Bold();
                    c.Item().Text($"Avg Balance: {data.Currency} {cf.AverageBalance:N0}");
                    c.Item().Text($"Lowest: {data.Currency} {cf.LowestMonthlyBalance:N0}");
                    c.Item().Text($"Highest: {data.Currency} {cf.HighestMonthlyBalance:N0}");
                });

                // Quality Metrics
                row.RelativeItem().Border(1).Padding(10).Column(c =>
                {
                    c.Item().Text("Quality Metrics").Bold();
                    c.Item().Text($"Inflow Volatility: {cf.InflowVolatility:P1}");
                    c.Item().Text($"Returned Cheques: {cf.ReturnedChequeCount}");
                    c.Item().Text($"Overdraft Usage: {cf.OverdraftUtilization:P1}");
                });

                // Trust Score
                row.RelativeItem().Border(1).Padding(10).Column(c =>
                {
                    c.Item().Text("Trust Assessment").Bold();
                    c.Item().AlignCenter().Text(cf.TrustLevel).FontSize(16).Bold()
                        .FontColor(cf.TrustLevel == "High" ? Colors.Green.Darken2 : 
                                   cf.TrustLevel == "Medium" ? Colors.Orange.Darken2 : Colors.Red.Darken2);
                    c.Item().AlignCenter().Text($"Score: {cf.TrustWeightedScore:N0}");
                });
            });
        });
    }

    private void ComposeCollateral(IContainer container, LoanPackData data)
    {
        container.Column(col =>
        {
            col.Item().Text("COLLATERAL").Bold().FontSize(14);
            col.Item().PaddingTop(10);

            col.Item().Text($"Total Collateral Value: {data.Currency} {data.TotalCollateralValue:N0} | Coverage: {data.CollateralCoverageRatio:P0}").Bold();
            col.Item().PaddingTop(10);

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(1);
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(1);
                    cols.RelativeColumn(1);
                    cols.RelativeColumn(1);
                    cols.RelativeColumn(1);
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
                    TableCell(table, c.Description);
                    TableCell(table, $"{data.Currency} {c.MarketValue:N0}");
                    TableCell(table, $"{data.Currency} {c.ForcedSaleValue:N0}");
                    TableCell(table, $"{data.Currency} {c.AcceptableValue:N0}");
                    TableCell(table, c.Status);
                }
            });
        });
    }

    private void ComposeGuarantors(IContainer container, LoanPackData data)
    {
        container.Column(col =>
        {
            col.Item().Text("GUARANTORS").Bold().FontSize(14);
            col.Item().PaddingTop(10);

            col.Item().Text($"Total Guarantee Amount: {data.Currency} {data.TotalGuaranteeAmount:N0}").Bold();
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
                });

                TableHeader(table, "Name");
                TableHeader(table, "Type");
                TableHeader(table, "Net Worth");
                TableHeader(table, "Guarantee");
                TableHeader(table, "Credit Score");
                TableHeader(table, "Status");

                foreach (var g in data.Guarantors)
                {
                    TableCell(table, g.Name);
                    TableCell(table, g.Type);
                    TableCell(table, $"{data.Currency} {g.NetWorth:N0}");
                    TableCell(table, $"{data.Currency} {g.GuaranteeAmount:N0}");
                    TableCell(table, g.CreditScore?.ToString() ?? "N/A");
                    TableCell(table, g.Status);
                }
            });
        });
    }

    private void ComposeAIAdvisory(IContainer container, LoanPackData data)
    {
        var ai = data.AIAdvisory!;
        
        container.Column(col =>
        {
            col.Item().Text("AI ADVISORY ASSESSMENT").Bold().FontSize(14);
            col.Item().PaddingTop(10);

            // Overall Score
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

            // Component Scores
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

            // Recommendations
            col.Item().PaddingTop(15).Text("Recommendations").Bold().FontSize(12);
            col.Item().PaddingTop(5).Border(1).Padding(10).Column(c =>
            {
                c.Item().Text($"Amount: {ai.AmountRecommendation}");
                c.Item().Text($"Tenor: {ai.TenorRecommendation}");
                c.Item().Text($"Pricing: {ai.PricingRecommendation}");
                c.Item().Text($"Structuring: {ai.StructuringRecommendation}");
            });

            // Conditions
            if (ai.RecommendedConditions.Any())
            {
                col.Item().PaddingTop(10).Text("Recommended Conditions").Bold().FontSize(12);
                col.Item().PaddingTop(5).Border(1).Padding(10).Column(c =>
                {
                    foreach (var condition in ai.RecommendedConditions)
                    {
                        c.Item().Text($"• {condition}");
                    }
                });
            }
        });
    }

    private void ComposeWorkflowHistory(IContainer container, LoanPackData data)
    {
        container.Column(col =>
        {
            col.Item().Text("WORKFLOW HISTORY").Bold().FontSize(14);
            col.Item().PaddingTop(10);

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(1);
                    cols.RelativeColumn(1);
                    cols.RelativeColumn(1);
                    cols.RelativeColumn(1);
                    cols.RelativeColumn(1);
                    cols.RelativeColumn(2);
                });

                TableHeader(table, "Date/Time");
                TableHeader(table, "From");
                TableHeader(table, "To");
                TableHeader(table, "Action");
                TableHeader(table, "By");
                TableHeader(table, "Comment");

                foreach (var wf in data.WorkflowHistory.OrderByDescending(w => w.Timestamp))
                {
                    TableCell(table, wf.Timestamp.ToString("dd-MMM-yy HH:mm"));
                    TableCell(table, wf.FromStatus);
                    TableCell(table, wf.ToStatus);
                    TableCell(table, wf.Action);
                    TableCell(table, wf.PerformedBy);
                    TableCell(table, wf.Comment ?? "");
                }
            });
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
                row.RelativeItem().AlignRight().Text($"Generated: {data.GeneratedAt:dd-MMM-yyyy HH:mm}").FontSize(8);
            });
        });
    }

    // Helper methods
    private static void TableHeader(TableDescriptor table, string text)
    {
        table.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text(text).Bold().FontSize(9);
    }

    private static void TableCell(TableDescriptor table, string text, bool bold = false)
    {
        var cell = table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(text).FontSize(9);
        if (bold) cell.Bold();
    }

    private static void AddFinancialRow(TableDescriptor table, string label, IEnumerable<decimal?> values, string currency)
    {
        TableCell(table, label, true);
        foreach (var value in values)
        {
            TableCell(table, value.HasValue ? $"{currency} {value:N0}" : "N/A");
        }
    }

    private static Color GetRiskColor(string? riskRating)
    {
        return riskRating switch
        {
            "Low" => Colors.Green.Darken2,
            "Moderate" => Colors.Orange.Darken2,
            "High" => Colors.Red.Darken2,
            "VeryHigh" => Colors.Red.Darken4,
            _ => Colors.Grey.Darken2
        };
    }

    private static Color GetScoreColor(int? score)
    {
        return score switch
        {
            >= 700 => Colors.Green.Darken2,
            >= 600 => Colors.Orange.Darken2,
            < 600 => Colors.Red.Darken2,
            _ => Colors.Grey.Darken2
        };
    }
}

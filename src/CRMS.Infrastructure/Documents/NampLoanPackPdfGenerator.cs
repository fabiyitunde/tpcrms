using CRMS.Application.Namp.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CRMS.Infrastructure.Documents;

public class NampLoanPackPdfGenerator : INampLoanPackGenerator
{
    private const string DarkBlue  = "#1a365d";
    private const string AccentBlue = "#2b6cb0";
    private const string LightBlue = "#ebf4ff";
    private const string MediumGray = "#e2e8f0";
    private const string LightGray = "#f7fafc";
    private const string GreenDark  = "#276749";
    private const string GreenLight = "#f0fff4";
    private const string OrangeDark = "#c05621";

    public Task<byte[]> GenerateAsync(NampLoanPackData data, CancellationToken ct = default)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Element(c => ComposeHeader(c, data));
                page.Content().Element(c => ComposeContent(c, data));
                page.Footer().Element(c => ComposeFooter(c));
            });
        });

        return Task.FromResult(document.GeneratePdf());
    }

    // ── Header ────────────────────────────────────────────────────────────────

    private static void ComposeHeader(IContainer container, NampLoanPackData data)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(lc =>
                {
                    lc.Item().Width(52).Height(52)
                        .Background(Color.FromHex(DarkBlue))
                        .AlignCenter().AlignMiddle()
                        .Text("BANK").FontSize(10).Bold().FontColor(Colors.White);
                });

                row.RelativeItem(3).AlignCenter().Column(tc =>
                {
                    tc.Item().AlignCenter().Text(data.BankName.ToUpperInvariant())
                        .Bold().FontSize(13).FontColor(Color.FromHex(DarkBlue));
                    tc.Item().AlignCenter().Text(data.BranchName)
                        .FontSize(9).FontColor(Colors.Grey.Darken1);
                    tc.Item().AlignCenter().PaddingTop(2)
                        .Text("NAMP LOAN PACK")
                        .Bold().FontSize(11).FontColor(Color.FromHex(AccentBlue));
                });

                row.RelativeItem().AlignRight().Column(rc =>
                {
                    rc.Item().AlignRight().Text($"Ref: {data.ApplicationNumber}").FontSize(9).Bold();
                    rc.Item().AlignRight().Text($"PAYS: {data.ApplicationReference}").FontSize(8).FontColor(Colors.Grey.Darken1);
                    rc.Item().AlignRight().PaddingTop(2)
                        .Text($"Generated: {data.GeneratedAt.ToLocalTime():dd MMM yyyy HH:mm}")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });

            col.Item().PaddingTop(6).LineHorizontal(2).LineColor(Color.FromHex(DarkBlue));
        });
    }

    // ── Footer ────────────────────────────────────────────────────────────────

    private static void ComposeFooter(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().LineHorizontal(0.5f).LineColor(Color.FromHex(MediumGray));
            col.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Text("CONFIDENTIAL — For authorised bank personnel only. Not for distribution.")
                    .FontSize(7).Italic().FontColor(Colors.Grey.Darken1);
                row.AutoItem().Text(text =>
                {
                    text.Span("Page ").FontSize(7).FontColor(Colors.Grey.Darken1);
                    text.CurrentPageNumber().FontSize(7).FontColor(Colors.Grey.Darken1);
                    text.Span(" of ").FontSize(7).FontColor(Colors.Grey.Darken1);
                    text.TotalPages().FontSize(7).FontColor(Colors.Grey.Darken1);
                });
            });
        });
    }

    // ── Content ───────────────────────────────────────────────────────────────

    private void ComposeContent(IContainer container, NampLoanPackData data)
    {
        container.PaddingTop(8).Column(col =>
        {
            // ── Application Summary ──────────────────────────────────────────
            col.Item().Element(c => SectionHeader(c, "Application Summary"));
            col.Item().PaddingBottom(8).Table(t =>
            {
                t.ColumnsDefinition(cd => { cd.RelativeColumn(2); cd.RelativeColumn(2); cd.RelativeColumn(2); cd.RelativeColumn(2); });
                InfoCell(t, "App Number",    data.ApplicationNumber);
                InfoCell(t, "PAYS Reference",data.ApplicationReference);
                InfoCell(t, "Status",        FormatStatus(data.Status));
                InfoCell(t, "Committee Tier",data.CommitteeTier);
                InfoCell(t, "Created",       data.CreatedAt.ToLocalTime().ToString("dd MMM yyyy"));
                InfoCell(t, "Submitted",     data.SubmittedAt?.ToLocalTime().ToString("dd MMM yyyy") ?? "—");
                InfoCell(t, "Ratified",      data.RatifiedAt?.ToLocalTime().ToString("dd MMM yyyy") ?? "—");
                InfoCell(t, "Ratified By",   data.RatifiedByUserName ?? "—");
            });

            // ── Applicant Profile ────────────────────────────────────────────
            col.Item().Element(c => SectionHeader(c, "Applicant Profile"));
            col.Item().PaddingBottom(8).Table(t =>
            {
                t.ColumnsDefinition(cd => { cd.RelativeColumn(2); cd.RelativeColumn(2); cd.RelativeColumn(2); cd.RelativeColumn(2); });
                InfoCell(t, "Applicant Name",    data.ApplicantName);
                InfoCell(t, "Category",          data.ApplicantCategory == "AgroServiceCompany" ? "Agro-Service Company" : "Farmer");
                InfoCell(t, "BOA Account No.",   data.BoaAccountNumber);
                InfoCell(t, "BOA Account Name",  data.BoaAccountName ?? "—");
                InfoCell(t, "Phone",             data.ApplicantPhone ?? "—");
                InfoCell(t, "Email",             data.ApplicantEmail ?? "—");
                InfoCell(t, "NIN",               data.Nin ?? "—");
                InfoCell(t, "BVN",               data.Bvn ?? "—");

                if (data.ApplicantCategory != "AgroServiceCompany")
                {
                    InfoCell(t, "Date of Birth",     data.DateOfBirth ?? "—");
                    InfoCell(t, "State of Residence",data.StateOfResidence ?? "—");
                    InfoCell(t, "LGA",               data.LocalGovernmentArea ?? "—");
                    InfoCell(t, "Occupation",        data.Occupation ?? "—");
                    InfoCell(t, "Employer",          data.EmployerName ?? "—");
                    InfoCell(t, "Employment Status", data.EmploymentStatus ?? "—");
                    InfoCell(t, "Years of Experience", data.YearsOfExperience?.ToString() ?? "—");
                    InfoCell(t, "No. of Dependants", data.NumberOfDependants?.ToString() ?? "—");
                    InfoCell(t, "Est. Monthly Income", data.EstimatedMonthlyIncome.HasValue ? $"NGN {data.EstimatedMonthlyIncome:N2}" : "—");
                    InfoCell(t, "Monthly Expenses",  data.MonthlyLivingExpenses.HasValue ? $"NGN {data.MonthlyLivingExpenses:N2}" : "—");
                    InfoCell(t, "Est. Net Worth",    data.EstimatedNetWorth.HasValue ? $"NGN {data.EstimatedNetWorth:N2}" : "—");
                    InfoCellWide(t, "Existing Obligations", data.ExistingLoanObligations ?? "None declared");
                }
                else
                {
                    InfoCell(t, "Company Name", data.CompanyName ?? "—");
                    InfoCell(t, "RC Number",    data.RcNumber ?? "—");
                    InfoCell(t, "Industry",     data.IndustrySector ?? "—");
                    InfoCell(t, "State",        data.StateOfResidence ?? "—");
                }
            });

            // ── Equipment & Loan Terms ───────────────────────────────────────
            col.Item().Element(c => SectionHeader(c, "Equipment & Loan Terms"));
            col.Item().PaddingBottom(8).Table(t =>
            {
                t.ColumnsDefinition(cd => { cd.RelativeColumn(2); cd.RelativeColumn(2); cd.RelativeColumn(2); cd.RelativeColumn(2); });
                InfoCellWide(t, "Equipment Description", data.EquipmentDescription);
                InfoCellWide(t, "Loan Purpose",          data.LoanPurpose ?? "—");
                InfoCell(t, "Equipment Value",   $"NGN {data.EquipmentValue:N2}");
                InfoCell(t, "Equity (%)",        data.EquityPercent.HasValue ? $"{data.EquityPercent:N1}%" : "—");
                InfoCell(t, "Equity Amount",     data.EquityAmount.HasValue ? $"NGN {data.EquityAmount:N2}" : "—");
                InfoCell(t, "Loan Amount",       data.LoanAmount.HasValue ? $"NGN {data.LoanAmount:N2}" : "—");
                InfoCell(t, "Tenor",             data.RequestedTenorMonths.HasValue ? $"{data.RequestedTenorMonths} months" : "—");
                InfoCell(t, "LTV",               data.FinancialAppraisal?.LoanToValueRatio.HasValue == true
                    ? $"{data.FinancialAppraisal.LoanToValueRatio:P1}" : "—");
            });


            // ── Financial Appraisal ──────────────────────────────────────────
            if (data.FinancialAppraisal != null)
            {
                var fa = data.FinancialAppraisal;
                col.Item().Element(c => SectionHeader(c, "Financial Appraisal"));
                col.Item().PaddingBottom(8).Table(t =>
                {
                    t.ColumnsDefinition(cd => { cd.RelativeColumn(2); cd.RelativeColumn(2); cd.RelativeColumn(2); cd.RelativeColumn(2); });
                    InfoCell(t, "Monthly Disposable Income", fa.MonthlyDisposableIncome.HasValue ? $"NGN {fa.MonthlyDisposableIncome:N2}" : "—");
                    InfoCell(t, "DSCR",                      fa.DebtServiceCoverageRatio?.ToString("N2") ?? "—");
                    InfoCell(t, "LTV Ratio",                 fa.LoanToValueRatio.HasValue ? $"{fa.LoanToValueRatio:P1}" : "—");
                    InfoCell(t, "Repayment Capacity",        fa.RepaymentCapacityRating);
                    InfoCell(t, "Credit Recommendation",     fa.CreditOfficerRecommendation);
                    InfoCell(t, "Prepared On",               fa.SavedAt.ToLocalTime().ToString("dd MMM yyyy"));
                    if (!string.IsNullOrWhiteSpace(fa.EquityAssessmentNote))
                        InfoCellWide(t, "Equity Assessment", fa.EquityAssessmentNote);
                    if (!string.IsNullOrWhiteSpace(fa.CreditBureauSummary))
                        InfoCellWide(t, "Bureau Summary",    fa.CreditBureauSummary);
                    if (!string.IsNullOrWhiteSpace(fa.SummaryNotes))
                        InfoCellWide(t, "Summary Notes",     fa.SummaryNotes);
                });
            }

            // ── Guarantors ───────────────────────────────────────────────────
            if (data.Guarantors.Count > 0)
            {
                col.Item().Element(c => SectionHeader(c, $"Guarantors ({data.Guarantors.Count})"));
                foreach (var g in data.Guarantors)
                {
                    col.Item().PaddingBottom(4).Table(t =>
                    {
                        t.ColumnsDefinition(cd => { cd.RelativeColumn(2); cd.RelativeColumn(2); cd.RelativeColumn(2); cd.RelativeColumn(2); });
                        InfoCell(t, "Name",           g.FullName);
                        InfoCell(t, "Type",           g.GuarantorType);
                        InfoCell(t, "Guarantee Type", g.GuaranteeType);
                        InfoCell(t, "Relationship",   g.RelationshipToApplicant ?? "—");
                        InfoCell(t, "BVN",            g.Bvn ?? "—");
                        InfoCell(t, "Phone",          g.Phone ?? "—");
                        InfoCell(t, "Net Worth",      g.DeclaredNetWorth.HasValue ? $"NGN {g.DeclaredNetWorth:N2}" : "—");
                        InfoCell(t, "Guarantee Limit",g.IsUnlimited ? "Unlimited" : g.GuaranteeLimit.HasValue ? $"NGN {g.GuaranteeLimit:N2}" : "—");
                        if (!string.IsNullOrWhiteSpace(g.Address))
                            InfoCellWide(t, "Address", g.Address);
                    });
                }
                col.Item().PaddingBottom(4);
            }

            // ── Collaterals ──────────────────────────────────────────────────
            if (data.Collaterals.Count > 0)
            {
                col.Item().Element(c => SectionHeader(c, $"Collaterals ({data.Collaterals.Count})"));
                col.Item().PaddingBottom(8).Table(t =>
                {
                    t.ColumnsDefinition(cd =>
                    {
                        cd.RelativeColumn(2.5f);
                        cd.RelativeColumn(1.5f);
                        cd.RelativeColumn(1.5f);
                        cd.RelativeColumn(1.5f);
                        cd.RelativeColumn(1.5f);
                        cd.RelativeColumn(1.5f);
                    });

                    // Header
                    TableHeaderCell(t, "Description");
                    TableHeaderCell(t, "Type");
                    TableHeaderCell(t, "Market Value");
                    TableHeaderCell(t, "FSV");
                    TableHeaderCell(t, "Lien Type");
                    TableHeaderCell(t, "Insured");

                    foreach (var c in data.Collaterals)
                    {
                        TableBodyCell(t, c.Description);
                        TableBodyCell(t, c.CollateralType);
                        TableBodyCell(t, c.MarketValue.HasValue ? $"NGN {c.MarketValue:N2}" : "—");
                        TableBodyCell(t, c.ForcedSaleValue.HasValue ? $"NGN {c.ForcedSaleValue:N2}" : "—");
                        TableBodyCell(t, c.LienType ?? "—");
                        TableBodyCell(t, c.IsInsured ? "Yes" : "No");
                    }
                });
            }

            // ── Credit Bureau ────────────────────────────────────────────────
            if (data.BureauReports.Count > 0)
            {
                col.Item().Element(c => SectionHeader(c, $"Credit Bureau Reports ({data.BureauReports.Count})"));
                col.Item().PaddingBottom(8).Table(t =>
                {
                    t.ColumnsDefinition(cd =>
                    {
                        cd.RelativeColumn(2.5f);
                        cd.RelativeColumn(1.5f);
                        cd.RelativeColumn();
                        cd.RelativeColumn();
                        cd.RelativeColumn();
                        cd.RelativeColumn();
                    });

                    TableHeaderCell(t, "Subject");
                    TableHeaderCell(t, "Type");
                    TableHeaderCell(t, "Score");
                    TableHeaderCell(t, "Grade");
                    TableHeaderCell(t, "Delinquent");
                    TableHeaderCell(t, "Outstanding");

                    foreach (var b in data.BureauReports)
                    {
                        TableBodyCell(t, b.SubjectName);
                        TableBodyCell(t, b.SubjectType);
                        TableBodyCell(t, b.CreditScore?.ToString() ?? "—");
                        TableBodyCell(t, b.ScoreGrade ?? "—");
                        TableBodyCell(t, b.DelinquentFacilities?.ToString() ?? "—");
                        TableBodyCell(t, b.TotalOutstanding.HasValue ? $"NGN {b.TotalOutstanding:N0}" : "—");
                    }
                });
            }

            // ── Financial Statements (AgroServiceCompany) ────────────────────
            if (data.FinancialStatements.Count > 0)
            {
                col.Item().Element(c => SectionHeader(c, "Financial Statements"));
                col.Item().PaddingBottom(8).Table(t =>
                {
                    t.ColumnsDefinition(cd =>
                    {
                        cd.RelativeColumn(2);
                        foreach (var _ in data.FinancialStatements) cd.RelativeColumn();
                    });

                    // Header row
                    t.Cell().Background(Color.FromHex(DarkBlue)).Padding(4)
                        .Text("Item").FontSize(8).Bold().FontColor(Colors.White);
                    foreach (var fs in data.FinancialStatements)
                    {
                        t.Cell().Background(Color.FromHex(DarkBlue)).Padding(4)
                            .Text(fs.FinancialYear.ToString()).FontSize(8).Bold().FontColor(Colors.White).AlignRight();
                    }

                    FinRow(t, "Revenue",        data.FinancialStatements.Select(f => f.Revenue));
                    FinRow(t, "Gross Profit",   data.FinancialStatements.Select(f => f.GrossProfit));
                    FinRow(t, "EBITDA",         data.FinancialStatements.Select(f => f.Ebitda));
                    FinRow(t, "Net Profit",     data.FinancialStatements.Select(f => f.NetProfit));
                    FinRow(t, "Total Assets",   data.FinancialStatements.Select(f => f.TotalAssets));
                    FinRow(t, "Total Liabilities",data.FinancialStatements.Select(f => f.TotalLiabilities));
                    FinRow(t, "Total Equity",   data.FinancialStatements.Select(f => f.TotalEquity));
                });
            }

            // ── Committee Review ─────────────────────────────────────────────
            if (data.CommitteeMembers.Count > 0 || data.CommitteeDecision != null)
            {
                col.Item().Element(c => SectionHeader(c, "Committee Review"));
                col.Item().PaddingBottom(4).Table(t =>
                {
                    t.ColumnsDefinition(cd => { cd.RelativeColumn(2); cd.RelativeColumn(2); cd.RelativeColumn(2); cd.RelativeColumn(2); });
                    InfoCell(t, "Committee Tier",  data.CommitteeTier);
                    InfoCell(t, "Decision",        data.CommitteeDecision ?? "Pending");
                    InfoCell(t, "Approval Votes",  data.CommitteeApprovalVotes.ToString());
                    InfoCell(t, "Rejection Votes", data.CommitteeRejectionVotes.ToString());
                    if (!string.IsNullOrWhiteSpace(data.CommitteeConditions))
                        InfoCellWide(t, "Approval Conditions", data.CommitteeConditions);
                });

                if (data.CommitteeMembers.Count > 0)
                {
                    col.Item().PaddingTop(4).PaddingBottom(8).Table(t =>
                    {
                        t.ColumnsDefinition(cd =>
                        {
                            cd.RelativeColumn(2.5f);
                            cd.RelativeColumn(2f);
                            cd.RelativeColumn();
                            cd.RelativeColumn();
                            cd.RelativeColumn(2f);
                        });

                        TableHeaderCell(t, "Member");
                        TableHeaderCell(t, "Role");
                        TableHeaderCell(t, "Chairperson");
                        TableHeaderCell(t, "Vote");
                        TableHeaderCell(t, "Comment");

                        foreach (var m in data.CommitteeMembers)
                        {
                            TableBodyCell(t, m.UserName);
                            TableBodyCell(t, m.Role);
                            TableBodyCell(t, m.IsChairperson ? "Yes" : "No");
                            TableBodyCell(t, m.Vote ?? "Pending");
                            TableBodyCell(t, m.VoteComment ?? "—");
                        }
                    });
                }
            }

            // ── Document Register ────────────────────────────────────────────
            if (data.Documents.Count > 0)
            {
                col.Item().Element(c => SectionHeader(c, $"Document Register ({data.Documents.Count})"));
                col.Item().PaddingBottom(8).Table(t =>
                {
                    t.ColumnsDefinition(cd =>
                    {
                        cd.RelativeColumn(3f);
                        cd.RelativeColumn(1.5f);
                        cd.RelativeColumn(1.5f);
                        cd.RelativeColumn(2f);
                    });

                    TableHeaderCell(t, "File Name");
                    TableHeaderCell(t, "Category");
                    TableHeaderCell(t, "Stage");
                    TableHeaderCell(t, "Uploaded");

                    foreach (var d in data.Documents.OrderBy(d => d.Category).ThenBy(d => d.UploadedAt))
                    {
                        TableBodyCell(t, d.FileName);
                        TableBodyCell(t, d.Category);
                        TableBodyCell(t, d.Stage);
                        TableBodyCell(t, d.UploadedAt.ToLocalTime().ToString("dd MMM yyyy"));
                    }
                });
            }

            // ── Workflow History ─────────────────────────────────────────────
            col.Item().Element(c => SectionHeader(c, "Workflow History"));
            col.Item().PaddingBottom(8).Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    cd.RelativeColumn(2f);
                    cd.RelativeColumn(2f);
                    cd.RelativeColumn(2f);
                    cd.RelativeColumn(3f);
                });

                TableHeaderCell(t, "Date");
                TableHeaderCell(t, "Status");
                TableHeaderCell(t, "Actor");
                TableHeaderCell(t, "Note");

                foreach (var h in data.StatusHistory)
                {
                    TableBodyCell(t, h.ChangedAt.ToLocalTime().ToString("dd MMM yyyy HH:mm"));
                    TableBodyCell(t, FormatStatus(h.Status));
                    TableBodyCell(t, h.UserName);
                    TableBodyCell(t, h.Note ?? "—");
                }
            });

            // ── Generated By ─────────────────────────────────────────────────
            col.Item().PaddingTop(4)
                .Background(Color.FromHex(LightGray))
                .Padding(8)
                .Text($"Generated by {data.GeneratedBy} on {data.GeneratedAt.ToLocalTime():dd MMM yyyy HH:mm}")
                .FontSize(8).Italic().FontColor(Colors.Grey.Darken1);
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void SectionHeader(IContainer container, string title)
    {
        container.PaddingTop(6).PaddingBottom(4).Column(col =>
        {
            col.Item().Background(Color.FromHex(DarkBlue)).Padding(5)
                .Text(title).Bold().FontSize(9).FontColor(Colors.White);
        });
    }

    private static void InfoCell(TableDescriptor t, string label, string? value)
    {
        t.Cell().Background(Color.FromHex(LightGray)).BorderBottom(0.5f).BorderColor(Color.FromHex(MediumGray))
            .Padding(4).Text(label).FontSize(8).FontColor(Colors.Grey.Darken2);
        t.Cell().BorderBottom(0.5f).BorderColor(Color.FromHex(MediumGray))
            .Padding(4).Text(value ?? "—").FontSize(8);
    }

    private static void InfoCellWide(TableDescriptor t, string label, string? value)
    {
        t.Cell().Background(Color.FromHex(LightGray)).BorderBottom(0.5f).BorderColor(Color.FromHex(MediumGray))
            .Padding(4).Text(label).FontSize(8).FontColor(Colors.Grey.Darken2);
        t.Cell().ColumnSpan(3).BorderBottom(0.5f).BorderColor(Color.FromHex(MediumGray))
            .Padding(4).Text(value ?? "—").FontSize(8);
    }

    private static void TableHeaderCell(TableDescriptor t, string label)
    {
        t.Cell().Background(Color.FromHex(AccentBlue)).Padding(4)
            .Text(label).FontSize(8).Bold().FontColor(Colors.White);
    }

    private static void TableBodyCell(TableDescriptor t, string? value)
    {
        t.Cell().BorderBottom(0.5f).BorderColor(Color.FromHex(MediumGray))
            .Padding(4).Text(value ?? "—").FontSize(8);
    }

    private static void FinRow(TableDescriptor t, string label, IEnumerable<decimal?> values)
    {
        t.Cell().Background(Color.FromHex(LightGray)).BorderBottom(0.5f).BorderColor(Color.FromHex(MediumGray))
            .Padding(4).Text(label).FontSize(8).FontColor(Colors.Grey.Darken2);
        foreach (var v in values)
        {
            t.Cell().BorderBottom(0.5f).BorderColor(Color.FromHex(MediumGray))
                .Padding(4).AlignRight().Text(v.HasValue ? $"{v:N0}" : "—").FontSize(8);
        }
    }

    private static string FormatStatus(string status) => status switch
    {
        "Draft"                        => "Draft",
        "Submitted"                    => "Submitted",
        "FinancialAppraisal"           => "Financial Appraisal",
        "FinancialDeclined"            => "Financial Declined",
        "BranchCommitteeCirculation"   => "Branch Committee",
        "ZonalCommitteeCirculation"    => "Zonal Committee",
        "RegionalCommitteeCirculation" => "Regional Committee",
        "HOCommitteeCirculation"       => "HO Committee",
        "CommitteeDeclined"            => "Committee Declined",
        "Ratification"                 => "Ratification",
        "Ratified"                     => "Ratified",
        "OfferGenerated"               => "Offer Generated",
        "OfferAccepted"                => "Offer Accepted",
        "OfferLapsed"                  => "Offer Lapsed",
        "PreDeploymentVerification"    => "Pre-Deployment",
        "Deployment"                   => "Deployment",
        "Deployed"                     => "Deployed",
        _ => status
    };
}

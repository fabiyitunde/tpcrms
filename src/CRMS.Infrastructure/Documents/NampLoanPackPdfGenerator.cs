using CRMS.Application.Namp.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CRMS.Infrastructure.Documents;

public class NampLoanPackPdfGenerator : INampLoanPackGenerator
{
    // Bank of Agriculture brand palette — see BoaBrand.
    private const string DarkBlue  = BoaBrand.Primary;
    private const string AccentBlue = BoaBrand.Accent;
    private const string MediumGray = BoaBrand.MediumGray;
    private const string LightGray = BoaBrand.LightGray;

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
                    lc.Item().Element(c => BoaBrand.RenderLogo(c, 52));
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
                InfoCell(t, "Category",          FormatCategory(data.ApplicantCategory));
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
                    InfoCell(t, "Company Name",  data.CompanyName ?? "—");
                    InfoCell(t, "RC Number",     data.RcNumber ?? "—");
                    InfoCell(t, "Industry",      data.IndustrySector ?? "—");
                    InfoCell(t, "State",         data.StateOfResidence ?? "—");
                    if (data.CacFetchedAt.HasValue)
                    {
                        InfoCell(t, "CAC Status",    data.CacStatus ?? "—");
                        InfoCell(t, "Entity Type",   data.CacEntityType ?? "—");
                        InfoCell(t, "Registered On", data.CacRegistrationDate ?? "—");
                        InfoCell(t, "Share Capital", data.CacShareCapital.HasValue ? $"NGN {data.CacShareCapital:N2}" : "—");
                        if (!string.IsNullOrWhiteSpace(data.CacNatureOfBusiness))
                            InfoCellWide(t, "Nature of Business", data.CacNatureOfBusiness);
                        if (!string.IsNullOrWhiteSpace(data.CacAddress))
                            InfoCellWide(t, "Registered Address", data.CacAddress);
                    }
                }
            });

            // ── Directors & Shareholders (Agro-Service) ──────────────────────
            if (data.Directors.Count > 0)
            {
                col.Item().Element(c => SectionHeader(c, $"Directors & Shareholders ({data.Directors.Count})"));
                col.Item().PaddingBottom(8).Table(t =>
                {
                    t.ColumnsDefinition(cd =>
                    {
                        cd.RelativeColumn(2.6f);
                        cd.RelativeColumn(1.6f);
                        cd.RelativeColumn(1.2f);
                        cd.RelativeColumn();
                        cd.RelativeColumn(1.3f);
                        cd.RelativeColumn();
                    });

                    TableHeaderCell(t, "Name");
                    TableHeaderCell(t, "Role");
                    TableHeaderCell(t, "Shares");
                    TableHeaderCell(t, "Holding %");
                    TableHeaderCell(t, "BVN");
                    TableHeaderCell(t, "Source");

                    foreach (var d in data.Directors)
                    {
                        TableBodyCell(t, d.FullName);
                        TableBodyCell(t, d.IsChairman ? "Chairman" : (string.IsNullOrEmpty(d.AffiliateType) ? "Director" : d.AffiliateType));
                        TableBodyCell(t, d.NumSharesAllotted?.ToString("N0") ?? "—");
                        TableBodyCell(t, d.ShareholdingPercent.HasValue ? $"{d.ShareholdingPercent:N2}%" : "—");
                        TableBodyCell(t, string.IsNullOrEmpty(d.Bvn) ? "—" : d.Bvn);
                        TableBodyCell(t, d.SourcedFromCac ? "CAC" : "Manual");
                    }
                });
            }

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
                    ? $"{data.FinancialAppraisal.LoanToValueRatio:N1}%" : "—");
                InfoCell(t, "Approved Rate",     data.ApprovedInterestRate.HasValue ? $"{data.ApprovedInterestRate:N2}% p.a." : "—");
            });


            // ── Financial Appraisal ──────────────────────────────────────────
            if (data.FinancialAppraisal != null)
            {
                var fa = data.FinancialAppraisal;
                col.Item().Element(c => SectionHeader(c, "Financial Appraisal"));
                col.Item().PaddingBottom(4).Table(t =>
                {
                    t.ColumnsDefinition(cd => { cd.RelativeColumn(2); cd.RelativeColumn(2); cd.RelativeColumn(2); cd.RelativeColumn(2); });
                    InfoCell(t, "Repayment Source",          FormatRepaymentSource(fa.RepaymentSource));
                    InfoCell(t, "Monthly Disposable Income", fa.MonthlyDisposableIncome.HasValue ? $"NGN {fa.MonthlyDisposableIncome:N2}" : "—");
                    InfoCell(t, "DSCR",                      fa.DebtServiceCoverageRatio?.ToString("N2") ?? "—");
                    InfoCell(t, "LTV Ratio",                 fa.LoanToValueRatio.HasValue ? $"{fa.LoanToValueRatio:N1}%" : "—");
                    InfoCell(t, "Repayment Capacity",        fa.RepaymentCapacityRating);
                    InfoCell(t, "Credit Recommendation",     fa.CreditOfficerRecommendation);
                    InfoCell(t, "Prepared On",               fa.SavedAt.ToLocalTime().ToString("dd MMM yyyy"));
                    if (fa.ProjectedMonthlyRentalRevenue.HasValue)
                        InfoCell(t, "Projected Rental Rev.", $"NGN {fa.ProjectedMonthlyRentalRevenue:N2}");
                    if (fa.UtilisationRateAssumption.HasValue)
                        InfoCell(t, "Utilisation Assumption", $"{fa.UtilisationRateAssumption:N1}%");
                    if (!string.IsNullOrWhiteSpace(fa.DemandEvidenceNote))
                        InfoCellWide(t, "Demand Evidence", fa.DemandEvidenceNote);
                    if (!string.IsNullOrWhiteSpace(fa.EquityAssessmentNote))
                        InfoCellWide(t, "Equity Assessment", fa.EquityAssessmentNote);
                    if (!string.IsNullOrWhiteSpace(fa.CreditBureauSummary))
                        InfoCellWide(t, "Bureau Summary",    fa.CreditBureauSummary);
                    if (!string.IsNullOrWhiteSpace(fa.SummaryNotes))
                        InfoCellWide(t, "Summary Notes",     fa.SummaryNotes);
                });

                // Viability Calculator — only render if at least one input was captured
                var hasViabilityInputs = fa.HectaresPerMonth.HasValue || fa.RatePerHectare.HasValue ||
                                         fa.MonthlyFuelCost.HasValue  || fa.MonthlyMaintenanceCost.HasValue ||
                                         fa.MonthlyOperatorWage.HasValue;
                var hasViabilityMetrics = fa.NetPresentValue.HasValue || fa.BenefitCostRatio.HasValue ||
                                          fa.InternalRateOfReturn.HasValue || fa.ProfitabilityIndex.HasValue;

                if (hasViabilityInputs || hasViabilityMetrics)
                {
                    col.Item().PaddingTop(2).PaddingBottom(2)
                        .Background(Color.FromHex(AccentBlue)).Padding(4)
                        .Text("Tractor Loan Viability Calculator").Bold().FontSize(8).FontColor(Colors.White);

                    if (hasViabilityInputs)
                    {
                        col.Item().PaddingBottom(4).Table(t =>
                        {
                            t.ColumnsDefinition(cd => { cd.RelativeColumn(2); cd.RelativeColumn(2); cd.RelativeColumn(2); cd.RelativeColumn(2); });
                            InfoCell(t, "Hectares / Month",      fa.HectaresPerMonth.HasValue ? $"{fa.HectaresPerMonth:N1} ha" : "—");
                            InfoCell(t, "Rate / Hectare",        fa.RatePerHectare.HasValue ? $"NGN {fa.RatePerHectare:N2}" : "—");
                            InfoCell(t, "Monthly Fuel Cost",     fa.MonthlyFuelCost.HasValue ? $"NGN {fa.MonthlyFuelCost:N2}" : "—");
                            InfoCell(t, "Maintenance Cost",      fa.MonthlyMaintenanceCost.HasValue ? $"NGN {fa.MonthlyMaintenanceCost:N2}" : "—");
                            InfoCell(t, "Operator Wage",         fa.MonthlyOperatorWage.HasValue ? $"NGN {fa.MonthlyOperatorWage:N2}" : "—");
                        });
                    }

                    if (hasViabilityMetrics)
                    {
                        col.Item().PaddingBottom(4).Table(t =>
                        {
                            t.ColumnsDefinition(cd => { cd.RelativeColumn(2); cd.RelativeColumn(2); cd.RelativeColumn(2); cd.RelativeColumn(2); });
                            ViabilityMetricCell(t, "NPV",  fa.NetPresentValue.HasValue   ? $"NGN {fa.NetPresentValue:N0}" : "—",   fa.NetPresentValue > 0);
                            ViabilityMetricCell(t, "BCR",  fa.BenefitCostRatio.HasValue  ? $"{fa.BenefitCostRatio:N3}" : "—",      fa.BenefitCostRatio > 1.0m);
                            ViabilityMetricCell(t, "IRR",  fa.InternalRateOfReturn.HasValue ? $"{fa.InternalRateOfReturn * 100:N2}%" : "—", fa.InternalRateOfReturn * 100 > 15m);
                            ViabilityMetricCell(t, "PI",   fa.ProfitabilityIndex.HasValue ? $"{fa.ProfitabilityIndex:N3}" : "—",   fa.ProfitabilityIndex > 1.0m);
                        });
                    }
                }

                // Monthly Cash Flow Summary
                if (fa.ProjectedMonthlyRentalRevenue.HasValue || fa.MonthlyDisposableIncome.HasValue)
                {
                    col.Item().PaddingTop(2).PaddingBottom(2)
                        .Background(Color.FromHex(AccentBlue)).Padding(4)
                        .Text("Monthly Cash Flow Summary").Bold().FontSize(8).FontColor(Colors.White);

                    col.Item().PaddingBottom(4).Table(t =>
                    {
                        t.ColumnsDefinition(cd => { cd.RelativeColumn(2); cd.RelativeColumn(2); cd.RelativeColumn(2); cd.RelativeColumn(2); });
                        InfoCell(t, "Gross Monthly Revenue",   fa.ProjectedMonthlyRentalRevenue.HasValue ? $"NGN {fa.ProjectedMonthlyRentalRevenue:N2}" : "—");
                        InfoCell(t, "Surplus After Repayment", fa.MonthlyDisposableIncome.HasValue ? $"NGN {fa.MonthlyDisposableIncome:N2}" : "—");
                        ViabilityMetricCell(t, "Cash Buffer",  fa.MonthlyDisposableIncome.HasValue ? (fa.MonthlyDisposableIncome > 0 ? "Positive" : "Negative") : "—", fa.MonthlyDisposableIncome > 0);
                        ViabilityMetricCell(t, "DSR",          fa.DebtServiceCoverageRatio?.ToString("N2") + "×" ?? "—", fa.DebtServiceCoverageRatio >= 1.0m);
                    });
                }

                // Credit Underwriting Determination
                if (fa.DebtServiceCoverageRatio.HasValue)
                {
                    var dsr = fa.DebtServiceCoverageRatio.Value;
                    string determination;
                    Color determinationColor;
                    if (dsr >= 1.5m)
                    {
                        determination = $"APPROVED — Healthy Viability. Net operating income covers the monthly amortisation by {dsr:N2}×. The beneficiary retains a significant liquidity cushion to absorb seasonal agricultural fluctuations, fully satisfying standard underwriting guidelines.";
                        determinationColor = Colors.Green.Darken2;
                    }
                    else if (dsr >= 1.0m)
                    {
                        determination = $"BORDERLINE VIABILITY — Net operating income covers the monthly amortisation by {dsr:N2}×. Guarantor backing or cooperative cross-guarantee is required before final approval.";
                        determinationColor = Colors.Orange.Darken2;
                    }
                    else
                    {
                        determination = $"DECLINED — Unviable. Projected utilisation does not generate sufficient net income to service the monthly amortisation (DSR {dsr:N2}×). The beneficiary must demonstrate a higher utilisation rate or provide additional equity before reassessment.";
                        determinationColor = Colors.Red.Darken2;
                    }

                    col.Item().PaddingTop(2).PaddingBottom(2)
                        .Background(Color.FromHex(AccentBlue)).Padding(4)
                        .Text("Credit Underwriting Determination").Bold().FontSize(8).FontColor(Colors.White);

                    col.Item().PaddingBottom(8).Row(r =>
                    {
                        r.ConstantItem(4).Background(determinationColor);
                        r.RelativeItem().PaddingLeft(6).PaddingVertical(5)
                            .Background(Color.FromHex(LightGray))
                            .Text(determination).FontSize(8).LineHeight(1.5f);
                    });
                }
                else
                {
                    col.Item().PaddingBottom(8);
                }
            }

            // ── AI Credit Advisory ───────────────────────────────────────────
            if (data.Advisory != null && data.Advisory.Status == "Completed")
            {
                var adv = data.Advisory;
                col.Item().Element(c => SectionHeader(c, "AI Credit Advisory"));

                // Summary
                col.Item().PaddingBottom(4).Table(t =>
                {
                    t.ColumnsDefinition(cd => { cd.RelativeColumn(2); cd.RelativeColumn(2); cd.RelativeColumn(2); cd.RelativeColumn(2); });
                    InfoCell(t, "Overall Score",    $"{adv.OverallScore:N1} / 100");
                    InfoCell(t, "Risk Rating",      adv.OverallRating ?? "—");
                    InfoCell(t, "Recommendation",   adv.Recommendation ?? "—");
                    InfoCell(t, "Critical Red Flags", adv.HasCriticalRedFlags ? "Yes" : "No");
                    if (adv.RecommendedAmount.HasValue)
                        InfoCell(t, "Recommended Amount",  $"NGN {adv.RecommendedAmount:N2}");
                    if (adv.RecommendedTenorMonths.HasValue)
                        InfoCell(t, "Recommended Tenor",   $"{adv.RecommendedTenorMonths} months");
                    if (adv.RecommendedInterestRate.HasValue)
                        InfoCell(t, "Recommended Rate",    $"{adv.RecommendedInterestRate:N2}% p.a.");
                    InfoCell(t, "Generated",               adv.GeneratedAt.ToLocalTime().ToString("dd MMM yyyy HH:mm"));
                });

                // Risk Score table
                if (adv.RiskScores.Count > 0)
                {
                    col.Item().PaddingTop(2).PaddingBottom(2)
                        .Background(Color.FromHex(AccentBlue)).Padding(4)
                        .Text("Risk Category Scores").Bold().FontSize(8).FontColor(Colors.White);

                    col.Item().PaddingBottom(4).Table(t =>
                    {
                        t.ColumnsDefinition(cd =>
                        {
                            cd.RelativeColumn(2.5f);
                            cd.RelativeColumn();
                            cd.RelativeColumn();
                            cd.RelativeColumn();
                            cd.RelativeColumn(4f);
                        });
                        TableHeaderCell(t, "Category");
                        TableHeaderCell(t, "Score");
                        TableHeaderCell(t, "Rating");
                        TableHeaderCell(t, "Weight");
                        TableHeaderCell(t, "Rationale");

                        foreach (var rs in adv.RiskScores)
                        {
                            TableBodyCell(t, rs.Category);
                            TableBodyCell(t, $"{rs.Score:N1}");
                            TableBodyCell(t, rs.Rating ?? "—");
                            TableBodyCell(t, $"{rs.Weight:N0}%");
                            TableBodyCell(t, rs.Rationale ?? "—");
                        }
                    });
                }

                // Narrative sections
                if (!string.IsNullOrWhiteSpace(adv.ExecutiveSummary))
                {
                    col.Item().PaddingTop(2).PaddingBottom(1)
                        .Background(Color.FromHex(AccentBlue)).Padding(4)
                        .Text("Executive Summary").Bold().FontSize(8).FontColor(Colors.White);
                    col.Item().PaddingBottom(4).Padding(4).Text(adv.ExecutiveSummary).FontSize(8);
                }

                if (!string.IsNullOrWhiteSpace(adv.StrengthsAnalysis))
                {
                    col.Item().PaddingTop(2).PaddingBottom(1)
                        .Background(Color.FromHex(AccentBlue)).Padding(4)
                        .Text("Strengths").Bold().FontSize(8).FontColor(Colors.White);
                    col.Item().PaddingBottom(4).Padding(4).Text(adv.StrengthsAnalysis).FontSize(8);
                }

                if (!string.IsNullOrWhiteSpace(adv.WeaknessesAnalysis))
                {
                    col.Item().PaddingTop(2).PaddingBottom(1)
                        .Background(Color.FromHex(AccentBlue)).Padding(4)
                        .Text("Weaknesses").Bold().FontSize(8).FontColor(Colors.White);
                    col.Item().PaddingBottom(4).Padding(4).Text(adv.WeaknessesAnalysis).FontSize(8);
                }

                if (!string.IsNullOrWhiteSpace(adv.MitigatingFactors))
                {
                    col.Item().PaddingTop(2).PaddingBottom(1)
                        .Background(Color.FromHex(AccentBlue)).Padding(4)
                        .Text("Mitigating Factors").Bold().FontSize(8).FontColor(Colors.White);
                    col.Item().PaddingBottom(4).Padding(4).Text(adv.MitigatingFactors).FontSize(8);
                }

                if (!string.IsNullOrWhiteSpace(adv.KeyRisks))
                {
                    col.Item().PaddingTop(2).PaddingBottom(1)
                        .Background(Color.FromHex(AccentBlue)).Padding(4)
                        .Text("Key Risks").Bold().FontSize(8).FontColor(Colors.White);
                    col.Item().PaddingBottom(4).Padding(4).Text(adv.KeyRisks).FontSize(8);
                }

                // Red flags
                if (adv.RedFlags.Count > 0)
                {
                    col.Item().PaddingTop(2).PaddingBottom(1)
                        .Background(Colors.Red.Darken2).Padding(4)
                        .Text($"Red Flags ({adv.RedFlags.Count})").Bold().FontSize(8).FontColor(Colors.White);
                    col.Item().PaddingBottom(4).Table(t =>
                    {
                        t.ColumnsDefinition(cd => cd.RelativeColumn());
                        foreach (var flag in adv.RedFlags)
                            t.Cell().BorderBottom(0.5f).BorderColor(Color.FromHex(MediumGray))
                                .Padding(4).Text($"• {flag}").FontSize(8).FontColor(Colors.Red.Darken3);
                    });
                }

                // Conditions
                if (adv.Conditions.Count > 0)
                {
                    col.Item().PaddingTop(2).PaddingBottom(1)
                        .Background(Color.FromHex(AccentBlue)).Padding(4)
                        .Text($"Conditions ({adv.Conditions.Count})").Bold().FontSize(8).FontColor(Colors.White);
                    col.Item().PaddingBottom(4).Table(t =>
                    {
                        t.ColumnsDefinition(cd => cd.RelativeColumn());
                        foreach (var cond in adv.Conditions)
                            t.Cell().BorderBottom(0.5f).BorderColor(Color.FromHex(MediumGray))
                                .Padding(4).Text($"• {cond}").FontSize(8);
                    });
                }

                // Covenants
                if (adv.Covenants.Count > 0)
                {
                    col.Item().PaddingTop(2).PaddingBottom(1)
                        .Background(Color.FromHex(AccentBlue)).Padding(4)
                        .Text($"Covenants ({adv.Covenants.Count})").Bold().FontSize(8).FontColor(Colors.White);
                    col.Item().PaddingBottom(8).Table(t =>
                    {
                        t.ColumnsDefinition(cd => cd.RelativeColumn());
                        foreach (var cov in adv.Covenants)
                            t.Cell().BorderBottom(0.5f).BorderColor(Color.FromHex(MediumGray))
                                .Padding(4).Text($"• {cov}").FontSize(8);
                    });
                }
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
                    InfoCell(t, "Decision Date",   data.CommitteeDecisionAt?.ToLocalTime().ToString("dd MMM yyyy") ?? "—");
                    InfoCell(t, "Approval Votes",  data.CommitteeRequiredVotes > 0
                        ? $"{data.CommitteeApprovalVotes} (min {data.CommitteeMinimumApprovalVotes} of {data.CommitteeRequiredVotes})"
                        : data.CommitteeApprovalVotes.ToString());
                    InfoCell(t, "Rejection Votes", data.CommitteeRejectionVotes.ToString());
                    InfoCell(t, "Abstentions",     data.CommitteeAbstainVotes.ToString());
                    if (!string.IsNullOrWhiteSpace(data.CommitteeDecisionNote))
                        InfoCellWide(t, "Decision Note", data.CommitteeDecisionNote);
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

    private static void ViabilityMetricCell(TableDescriptor t, string label, string value, bool? pass)
    {
        t.Cell().Background(Color.FromHex(LightGray)).BorderBottom(0.5f).BorderColor(Color.FromHex(MediumGray))
            .Padding(4).Text(label).FontSize(8).FontColor(Colors.Grey.Darken2);
        t.Cell().BorderBottom(0.5f).BorderColor(Color.FromHex(MediumGray)).Padding(4).Row(r =>
        {
            r.AutoItem().Text(value).FontSize(8);
            if (pass.HasValue)
            {
                var verdict = pass.Value ? "PASS" : "FAIL";
                var color   = pass.Value ? Colors.Green.Darken2 : Colors.Red.Darken2;
                r.AutoItem().PaddingLeft(6).Text(verdict).FontSize(7).Bold().FontColor(color);
            }
        });
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

    private static string FormatCategory(string category) => category switch
    {
        "YouthAgripreneur"   => "Youth Agripreneur",
        "WomenAgripreneur"   => "Women Agripreneur",
        "AgroServiceCompany" => "Agro-Service Company",
        _ => category
    };

    private static string FormatRepaymentSource(string source) => source switch
    {
        "PrimaryIncome"     => "Primary Income",
        "RentalHireRevenue" => "Rental / Hire Revenue",
        "Mixed"             => "Mixed",
        "CompanyCashFlow"   => "Company Cash Flow",
        _ => string.IsNullOrWhiteSpace(source) ? "—" : source
    };

    private static string FormatStatus(string status) => status switch
    {
        "Received"                     => "Received",
        "RecallPending"                => "Recall Pending",
        "Draft"                        => "Draft",
        "Submitted"                    => "Submitted",
        "FinancialAppraisal"           => "Financial Appraisal",
        "FinancialDeclined"            => "Financial Declined",
        "RiskReview"                   => "Risk Review",
        "RiskDeclined"                 => "Risk Declined",
        "BranchCommitteeCirculation"   => "Branch Committee",
        "BranchCommitteeDeclined"      => "Branch Committee Declined",
        "ZonalCommitteeCirculation"    => "Zonal Committee",
        "ZonalCommitteeDeclined"       => "Zonal Committee Declined",
        "RegionalCommitteeCirculation" => "Regional Committee",
        "RegionalCommitteeDeclined"    => "Regional Committee Declined",
        "HOCommitteeCirculation"       => "HO Committee",
        "HOCommitteeDeclined"          => "HO Committee Declined",
        "CommitteeDeclined"            => "Committee Declined",
        "Ratification"                 => "Ratification",
        "RatificationDeclined"         => "Ratification Declined",
        "Ratified"                     => "Ratified",
        "OfferGenerated"               => "Offer Generated",
        "OfferAccepted"                => "Offer Accepted",
        "OfferLapsed"                  => "Offer Lapsed",
        "PreDeploymentVerification"    => "Pre-Deployment",
        "Deployment"                   => "Deployment",
        "Deployed"                     => "Deployed",
        "Active"                       => "Active",
        "Closed"                       => "Closed",
        "Declined"                     => "Declined",
        _ => status
    };
}

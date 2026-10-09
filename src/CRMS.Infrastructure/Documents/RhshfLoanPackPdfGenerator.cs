using CRMS.Application.Rhshf.Interfaces;
using CRMS.Application.Rhshf.Queries;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CRMS.Infrastructure.Documents;

/// <summary>
/// RH-SHF Loan Pack PDF — the consolidated case dossier (see IRhshfLoanPackGenerator). Modelled on the
/// NAMP/corporate loan packs but driven by RH-SHF's data; every section is null/empty-safe so a case at
/// any stage from Risk Review onward produces a coherent pack. Uses BoaBrand for consistent branding.
/// </summary>
public class RhshfLoanPackPdfGenerator : IRhshfLoanPackGenerator
{
    public Task<byte[]> GenerateAsync(RhshfLoanPackData data, CancellationToken ct = default)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var ccy = string.IsNullOrWhiteSpace(data.Workspace.Currency) ? "NGN" : data.Workspace.Currency;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Darken3));

                page.Header().Element(c => ComposeHeader(c, data));
                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Spacing(14);
                    ComposeOverview(col, data);
                    ComposeCaseSummary(col, data, ccy);
                    ComposeAppliedFor(col, data, ccy);
                    ComposeDirectors(col, data);
                    ComposeBureau(col, data, ccy);
                    ComposeAppraisal(col, data, ccy);
                    ComposeGuarantors(col, data, ccy);
                    ComposeCollateral(col, data, ccy);
                    ComposeCommittee(col, data);
                    ComposeRatificationAndOffer(col, data, ccy);
                    ComposeDisbursement(col, data, ccy);
                    ComposeAdvisory(col, data, ccy);
                    ComposeAuditTrail(col, data);
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Darken1));
                    text.Span($"RH-SHF Loan Pack — {data.Workspace.Reference}   ·   Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        });

        return Task.FromResult(document.GeneratePdf());
    }

    // ── chrome ──────────────────────────────────────────────────────────────────
    private static void ComposeHeader(IContainer container, RhshfLoanPackData data)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.ConstantItem(54).Element(c => BoaBrand.RenderLogo(c, 48));
                row.RelativeItem().PaddingLeft(8).Column(t =>
                {
                    t.Item().Text(data.BankName.ToUpperInvariant()).Bold().FontSize(14).FontColor(Color.FromHex(BoaBrand.Primary));
                    t.Item().Text("RH-SHF Loan Pack — Credit Case Dossier").FontSize(10).SemiBold().FontColor(Color.FromHex(BoaBrand.Accent));
                    t.Item().Text("Renewed Hope – Smallholder Farmer Input Financing").FontSize(8).Italic().FontColor(Colors.Grey.Darken1);
                });
                row.ConstantItem(150).AlignRight().Column(t =>
                {
                    t.Item().AlignRight().Text(data.Workspace.Reference).Bold().FontSize(11);
                    t.Item().AlignRight().Text($"Generated {data.GeneratedAt.ToLocalTime():dd MMM yyyy HH:mm}").FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });
            col.Item().PaddingTop(8).LineHorizontal(1.5f).LineColor(Color.FromHex(BoaBrand.Primary));
        });
    }

    // ── sections ────────────────────────────────────────────────────────────────
    private static void ComposeOverview(ColumnDescriptor col, RhshfLoanPackData data)
    {
        var w = data.Workspace;
        Section(col, "Case Overview");
        col.Item().Element(c => KeyValues(c,
            ("Company", w.CompanyName),
            ("Status", w.Status.ToString()),
            ("Current stage", w.InternalStage?.ToString() ?? "—"),
            ("Financing cycle", $"Cycle {w.CurrentCycleNumber}"),
            ("Committee tier", string.IsNullOrWhiteSpace(w.CommitteeTier) ? "—"
                : $"{w.CommitteeTier}{(string.IsNullOrWhiteSpace(w.CommitteeTierBand) ? "" : $" ({w.CommitteeTierBand})")}"),
            ("Prepared by", data.GeneratedBy)));
    }

    private static void ComposeCaseSummary(ColumnDescriptor col, RhshfLoanPackData data, string ccy)
    {
        var w = data.Workspace;
        Section(col, "Applicant & Verification");
        col.Item().Element(c => KeyValues(c,
            ("RC number", w.RcNumber),
            ("TIN", w.Tin ?? "—"),
            ("BOA account", w.BoaAccountNumber),
            ("Location", JoinNonBlank(", ", w.Lga, w.State)),
            ("Number of farmers", w.FarmerCount?.ToString("N0") ?? "—"),
            ("Total EOP value", $"{ccy} {w.TotalEopValue:N2}"),
            ("Approved amount", w.ApprovedAmount is { } a ? $"{ccy} {a:N2}" : "—"),
            ("Decision", w.DecisionOutcome?.ToString() ?? "Pending"),
            ("Decided by", w.DecidedBy ?? "—"),
            ("Decided at", w.DecidedAt?.ToLocalTime().ToString("dd MMM yyyy HH:mm") ?? "—")));

        if (!string.IsNullOrWhiteSpace(w.BranchResolutionNote))
            col.Item().PaddingTop(4).Text($"Branch routing: {w.BranchResolutionNote}").FontSize(8).Italic().FontColor(Colors.Grey.Darken1);
        if (!string.IsNullOrWhiteSpace(w.DecisionNotes))
            col.Item().PaddingTop(4).Text($"Decision notes: {w.DecisionNotes}").FontSize(8).FontColor(Colors.Grey.Darken2);
    }

    private static void ComposeAppliedFor(ColumnDescriptor col, RhshfLoanPackData data, string ccy)
    {
        var lines = data.Workspace.EopLines;
        Section(col, "Input Package Applied For");
        if (lines.Count == 0) { Empty(col, "No itemised EOP input package was recorded."); return; }

        col.Item().Table(table =>
        {
            table.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(2); });
            Th(table, "Commodity", true); Th(table, "Quantity (kg)"); Th(table, "Unit price"); Th(table, "Line value");
            foreach (var l in lines)
            {
                Td(table, l.Commodity, true);
                Td(table, l.QuantityKg.ToString("N0"));
                Td(table, $"{ccy} {l.UnitPricePerKg:N2}");
                Td(table, $"{ccy} {l.LineValue:N2}");
            }
            table.Cell().ColumnSpan(3).Background(Color.FromHex(BoaBrand.PanelGreen)).Padding(5).Text("Total").SemiBold();
            table.Cell().Background(Color.FromHex(BoaBrand.PanelGreen)).Padding(5).AlignRight()
                .Text($"{ccy} {lines.Sum(l => l.LineValue):N2}").SemiBold();
        });
    }

    private static void ComposeDirectors(ColumnDescriptor col, RhshfLoanPackData data)
    {
        var d = data.Directors;
        Section(col, "Directors & Shareholders");
        if (d is null || d.Directors.Count == 0) { Empty(col, "No directors recorded."); return; }

        if (!string.IsNullOrWhiteSpace(d.CacStatus) || !string.IsNullOrWhiteSpace(d.CacEntityType))
            col.Item().PaddingBottom(4).Element(c => KeyValues(c,
                ("CAC status", d.CacStatus ?? "—"),
                ("Entity type", d.CacEntityType ?? "—"),
                ("Registered", d.CacRegistrationDate ?? "—"),
                ("Nature of business", d.CacNatureOfBusiness ?? "—")));

        col.Item().Table(table =>
        {
            table.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(1.4f); c.RelativeColumn(1.4f); });
            Th(table, "Name", true); Th(table, "Role / occupation", true); Th(table, "Shareholding"); Th(table, "BVN"); Th(table, "Source");
            foreach (var m in d.Directors)
            {
                Td(table, m.FullName + (m.IsChairman ? " (Chair)" : ""), true);
                Td(table, m.Occupation ?? "—", true);
                Td(table, m.ShareholdingPercent is { } s ? $"{s:0.##}%" : "—");
                Td(table, m.HasBvn ? "Provided" : "Missing");
                Td(table, m.SourcedFromCac ? "CAC" : m.DeclaredByFac ? "FAC" : "Staff");
            }
        });
    }

    private static void ComposeBureau(ColumnDescriptor col, RhshfLoanPackData data, string ccy)
    {
        var reports = data.BureauReports;
        Section(col, "Credit Bureau");
        if (reports.Count == 0) { Empty(col, "No credit bureau reports on file."); return; }

        foreach (var r in reports)
        {
            col.Item().PaddingBottom(6).Border(0.5f).BorderColor(Color.FromHex(BoaBrand.MediumGray)).Padding(8).Column(box =>
            {
                box.Item().Row(row =>
                {
                    row.RelativeItem().Text($"{r.SubjectName}").SemiBold().FontColor(Color.FromHex(BoaBrand.Primary));
                    row.ConstantItem(140).AlignRight().Text(
                        r.CreditScore is { } sc ? $"Score {sc}{(string.IsNullOrWhiteSpace(r.ScoreGrade) ? "" : $" ({r.ScoreGrade})")}" : r.Status)
                        .SemiBold();
                });
                box.Item().PaddingTop(4).Element(c => KeyValues(c,
                    ("Subject type", r.SubjectType),
                    ("Status", r.Status),
                    ("Active loans", r.ActiveLoans.ToString()),
                    ("Total accounts", r.TotalAccounts.ToString()),
                    ("Delinquent facilities", r.DelinquentFacilities.ToString()),
                    ("Max delinquency (days)", r.MaxDelinquencyDays.ToString()),
                    ("Total outstanding", $"{ccy} {r.TotalOutstanding:N2}"),
                    ("Total overdue", $"{ccy} {r.TotalOverdue:N2}"),
                    ("Legal actions", r.HasLegalActions ? "Yes" : "No"),
                    ("Fraud risk", r.FraudRiskScore is { } f ? $"{f}{(string.IsNullOrWhiteSpace(r.FraudRecommendation) ? "" : $" — {r.FraudRecommendation}")}" : "—")));
                if (!string.IsNullOrWhiteSpace(r.ErrorMessage))
                    box.Item().PaddingTop(3).Text($"Note: {r.ErrorMessage}").FontSize(8).Italic().FontColor(Colors.Red.Darken1);
            });
        }
    }

    private static void ComposeAppraisal(ColumnDescriptor col, RhshfLoanPackData data, string ccy)
    {
        var fa = data.FinancialAppraisal;
        Section(col, "Financial Appraisal (Crop Economics)");
        if (fa is null || fa.Report is null) { Empty(col, "No financial appraisal has been saved for this cycle."); return; }
        var r = fa.Report;

        if (fa.FarmPlans.Count > 0)
        {
            col.Item().PaddingBottom(6).Table(table =>
            {
                table.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(1.5f); c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(2); });
                Th(table, "Crop", true); Th(table, "Hectares"); Th(table, "Yield (kg/ha)"); Th(table, "Price (/kg)"); Th(table, "Revenue");
                foreach (var p in fa.FarmPlans)
                {
                    Td(table, p.Crop, true);
                    Td(table, p.Hectares.ToString("N1"));
                    Td(table, p.ExpectedYieldKgPerHectare.ToString("N0"));
                    Td(table, $"{ccy} {p.ExpectedPricePerKg:N2}");
                    Td(table, $"{ccy} {p.ExpectedRevenue:N2}");
                }
            });
        }

        col.Item().Element(c => KeyValues(c,
            ("Interest rate", $"{r.InterestRatePercent:0.##}% for the cycle"),
            ("Crop cycle", $"{r.CycleMonths} month(s)"),
            ("Gross revenue", $"{ccy} {r.GrossRevenue:N2}"),
            ("Financed input cost", $"{ccy} {r.FinancedInputCost:N2}"),
            ("Interest charge", $"{ccy} {r.InterestCharge:N2}"),
            ("Amount due at harvest", $"{ccy} {r.AmountDueAtHarvest:N2}"),
            ("Gross margin", $"{ccy} {r.GrossMargin:N2} ({r.GrossMarginPercent:0.##}%)"),
            ("DSCR", r.Dscr.ToString("0.##")),
            ("IRR", r.Irr is { } irr ? $"{irr:0.##}%" : "—"),
            ("NPV", $"{ccy} {r.NetPresentValue:N2}"),
            ("Net return to farmer", $"{ccy} {r.NetReturnToFarmer:N2}"),
            ("Repayment capacity", r.RepaymentCapacityRating.ToString())));

        // Viability gates
        col.Item().PaddingTop(6).Table(table =>
        {
            table.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); });
            Th(table, "DSCR", true); Th(table, "Gross margin", true); Th(table, "IRR", true); Th(table, "Yield buffer", true); Th(table, "Price buffer", true);
            Gate(table, r.DscrPass); Gate(table, r.GrossMarginPass); Gate(table, r.IrrPass); Gate(table, r.YieldHeadroomPass); Gate(table, r.PriceHeadroomPass);
        });
        col.Item().PaddingTop(4).Text($"Gates passed: {r.GatesPassed}/5 — {(r.AllGatesPass ? "ALL PASS" : "NOT ALL PASS")}")
            .SemiBold().FontColor(Color.FromHex(r.AllGatesPass ? BoaBrand.Accent : "#b45309"));
        col.Item().PaddingTop(4).Text($"Credit Officer recommendation: {r.CreditOfficerRecommendation}").SemiBold();
        if (!string.IsNullOrWhiteSpace(r.SummaryNotes))
            col.Item().PaddingTop(3).Text(r.SummaryNotes).FontSize(8).FontColor(Colors.Grey.Darken2);
        if (!string.IsNullOrWhiteSpace(r.OverrideJustification))
            col.Item().PaddingTop(3).Text($"Override justification: {r.OverrideJustification}").FontSize(8).Italic().FontColor(Colors.Grey.Darken2);
        col.Item().PaddingTop(3).Text($"Prepared by {r.PreparedByName} on {r.SavedAt.ToLocalTime():dd MMM yyyy HH:mm}").FontSize(8).FontColor(Colors.Grey.Darken1);
    }

    private static void ComposeGuarantors(ColumnDescriptor col, RhshfLoanPackData data, string ccy)
    {
        var gs = data.Guarantors;
        Section(col, "Guarantors");
        if (gs.Count == 0) { Empty(col, "No guarantors recorded."); return; }

        col.Item().Table(table =>
        {
            table.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(1.4f); c.RelativeColumn(2); });
            Th(table, "Name", true); Th(table, "Type", true); Th(table, "Relationship", true); Th(table, "ID"); Th(table, "Guarantee");
            foreach (var g in gs)
            {
                Td(table, g.FullName, true);
                Td(table, g.GuarantorType.ToString(), true);
                Td(table, g.Relationship ?? "—", true);
                Td(table, g.HasBvn ? "BVN" : !string.IsNullOrWhiteSpace(g.RcNumber) ? "RC" : "—");
                Td(table, g.GuaranteeAmount is { } ga ? $"{ccy} {ga:N2}" : "—");
            }
        });
    }

    private static void ComposeCollateral(ColumnDescriptor col, RhshfLoanPackData data, string ccy)
    {
        var cs = data.Collaterals;
        Section(col, "Collateral");
        if (cs.Count == 0) { Empty(col, "No collateral recorded."); return; }

        foreach (var c in cs)
        {
            col.Item().PaddingBottom(5).Border(0.5f).BorderColor(Color.FromHex(BoaBrand.MediumGray)).Padding(8).Column(box =>
            {
                box.Item().Row(row =>
                {
                    row.RelativeItem().Text(c.Type.ToString()).SemiBold().FontColor(Color.FromHex(BoaBrand.Primary));
                    row.ConstantItem(150).AlignRight().Text(c.PerfectionStatus?.ToString() ?? "—").FontSize(8);
                });
                box.Item().PaddingTop(3).Element(cc => KeyValues(cc,
                    ("Reference no.", c.ReferenceNumber ?? "—"),
                    ("Issued", c.IssuedDate?.ToString("dd MMM yyyy") ?? "—"),
                    ("Expiry", c.ExpiryDate?.ToString("dd MMM yyyy") ?? "—"),
                    ("Guarantor bank", c.GuarantorBankName ?? "—"),
                    ("Guarantee amount", c.GuaranteeAmount is { } ga ? $"{ccy} {ga:N2}" : "—"),
                    ("CRG coverage", c.CrgCoveragePercentage is { } cov ? $"{cov:0.##}%" : "—"),
                    ("Property", c.PropertyDescription ?? "—"),
                    ("Property value", c.PropertyValue is { } pv ? $"{ccy} {pv:N2}" : "—"),
                    ("Title ref.", c.TitleReferenceNumber ?? "—"),
                    ("Recorded by", $"{c.RecordedByName} ({c.RecordedAt.ToLocalTime():dd MMM yyyy})")));
                if (!string.IsNullOrWhiteSpace(c.Notes))
                    box.Item().PaddingTop(3).Text(c.Notes).FontSize(8).FontColor(Colors.Grey.Darken2);
            });
        }
    }

    private static void ComposeCommittee(ColumnDescriptor col, RhshfLoanPackData data)
    {
        var cr = data.CommitteeReview;
        Section(col, "Committee Decision");
        if (cr is null) { Empty(col, "This case has not reached committee voting."); return; }

        var approvals = cr.Votes.Count(v => v.Vote == Domain.Enums.RhshfCommitteeVoteChoice.Approve);
        col.Item().Element(c => KeyValues(c,
            ("Committee tier", cr.Tier.ToString()),
            ("Decision", cr.FinalDecision?.ToString() ?? "Pending"),
            ("Votes cast", cr.Votes.Count.ToString()),
            ("Approvals", approvals.ToString()),
            ("Required votes", cr.RequiredVotes.ToString()),
            ("Minimum approvals", cr.MinimumApprovalVotes.ToString())));

        if (cr.Votes.Count > 0)
        {
            col.Item().PaddingTop(5).Table(table =>
            {
                table.ColumnsDefinition(c => { c.RelativeColumn(2.5f); c.RelativeColumn(1.5f); c.RelativeColumn(2); c.RelativeColumn(3); });
                Th(table, "Member", true); Th(table, "Vote", true); Th(table, "When", true); Th(table, "Comment", true);
                foreach (var v in cr.Votes.OrderBy(v => v.VotedAt))
                {
                    Td(table, v.UserName, true);
                    Td(table, v.Vote.ToString(), true);
                    Td(table, v.VotedAt.ToLocalTime().ToString("dd MMM yyyy HH:mm"), true);
                    Td(table, v.Comment ?? "—", true);
                }
            });
        }
    }

    private static void ComposeRatificationAndOffer(ColumnDescriptor col, RhshfLoanPackData data, string ccy)
    {
        var rats = data.Workspace.Ratifications;
        var offer = data.Offer;
        Section(col, "Ratification & Offer");
        if (rats.Count == 0 && offer is null) { Empty(col, "This case has not been ratified."); return; }

        foreach (var r in rats.OrderBy(r => r.RatifiedAt))
        {
            col.Item().Text($"{r.Outcome} — {r.FinalApproverName} on {r.RatifiedAt.ToLocalTime():dd MMM yyyy HH:mm}"
                + (r.ApprovedAmount is { } a ? $" — {ccy} {a:N2}" : "")).FontSize(9);
            if (!string.IsNullOrWhiteSpace(r.Notes))
                col.Item().Text(r.Notes).FontSize(8).FontColor(Colors.Grey.Darken2);
        }

        if (offer is not null)
            col.Item().PaddingTop(5).Element(c => KeyValues(c,
                ("Offer status", offer.Status.ToString()),
                ("Offer amount", offer.ApprovedAmount is { } a ? $"{ccy} {a:N2}" : "—"),
                ("Generated", offer.GeneratedAt.ToLocalTime().ToString("dd MMM yyyy HH:mm")),
                ("FAC responded", offer.FacRespondedAt?.ToLocalTime().ToString("dd MMM yyyy HH:mm") ?? "—"),
                ("Signed copies", offer.SignedDocuments.Count.ToString())));
    }

    private static void ComposeDisbursement(ColumnDescriptor col, RhshfLoanPackData data, string ccy)
    {
        var ds = data.Disbursements;
        Section(col, "Disbursement");
        if (ds.Count == 0) { Empty(col, "No disbursement has been booked."); return; }

        col.Item().Table(table =>
        {
            table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(2); });
            Th(table, "Booked", true); Th(table, "Officer", true); Th(table, "Amount"); Th(table, "Supplier", true); Th(table, "Status", true);
            foreach (var d in ds.OrderBy(d => d.BookedAt))
            {
                Td(table, d.BookedAt.ToLocalTime().ToString("dd MMM yyyy"), true);
                Td(table, d.DisbursementOfficerName, true);
                Td(table, $"{ccy} {d.DisbursedAmount:N2}");
                Td(table, d.SupplierName ?? d.SupplierAccountNumber, true);
                Td(table, d.Status.ToString() + (string.IsNullOrWhiteSpace(d.FailureReason) ? "" : $" — {d.FailureReason}"), true);
            }
        });
    }

    private static void ComposeAdvisory(ColumnDescriptor col, RhshfLoanPackData data, string ccy)
    {
        var a = data.Advisory;
        Section(col, "AI Credit Advisory");
        if (a is null) { Empty(col, "No AI advisory has been generated."); return; }

        col.Item().Element(c => KeyValues(c,
            ("Overall rating", $"{a.OverallRating} ({a.OverallScore:0.##})"),
            ("Recommendation", a.Recommendation),
            ("Recommended amount", a.RecommendedAmount is { } ra ? $"{ccy} {ra:N2}" : "—"),
            ("Critical red flags", a.HasCriticalRedFlags ? "Yes" : "No"),
            ("Model", a.ModelVersion),
            ("Generated", a.GeneratedAt.ToLocalTime().ToString("dd MMM yyyy HH:mm"))));

        if (!string.IsNullOrWhiteSpace(a.ExecutiveSummary))
            Para(col, "Executive summary", a.ExecutiveSummary);
        if (!string.IsNullOrWhiteSpace(a.KeyRisks))
            Para(col, "Key risks", a.KeyRisks);
        if (a.RedFlags.Count > 0)
            Para(col, "Red flags", string.Join("; ", a.RedFlags));
        if (a.Conditions.Count > 0)
            Para(col, "Conditions", string.Join("; ", a.Conditions));
    }

    private static void ComposeAuditTrail(ColumnDescriptor col, RhshfLoanPackData data)
    {
        var h = data.StatusHistory;
        Section(col, "Audit Trail");
        if (h.Count == 0) { Empty(col, "No status history recorded."); return; }

        col.Item().Table(table =>
        {
            table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(3); c.RelativeColumn(2); c.RelativeColumn(3); });
            Th(table, "When", true); Th(table, "Action", true); Th(table, "Actor", true); Th(table, "Note", true);
            foreach (var e in h.OrderBy(x => x.ChangedAt))
            {
                Td(table, e.ChangedAt.ToLocalTime().ToString("dd MMM yyyy HH:mm"), true);
                Td(table, e.Action, true);
                Td(table, e.Actor, true);
                Td(table, e.Note ?? "—", true);
            }
        });
    }

    // ── shared helpers ───────────────────────────────────────────────────────────
    private static void Section(ColumnDescriptor col, string title) =>
        col.Item().Column(c =>
        {
            c.Item().Text(title).Bold().FontSize(11).FontColor(Color.FromHex(BoaBrand.Primary));
            c.Item().PaddingTop(2).LineHorizontal(0.75f).LineColor(Color.FromHex(BoaBrand.Subtle));
        });

    private static void Empty(ColumnDescriptor col, string text) =>
        col.Item().PaddingTop(3).Text(text).FontSize(8).Italic().FontColor(Colors.Grey.Darken1);

    private static void Para(ColumnDescriptor col, string label, string body)
    {
        col.Item().PaddingTop(4).Text(label).SemiBold().FontSize(9);
        col.Item().Text(body).FontSize(8).FontColor(Colors.Grey.Darken2);
    }

    /// <summary>Two-column key/value grid, two pairs per row.</summary>
    private static void KeyValues(IContainer container, params (string Label, string Value)[] rows)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c => { c.RelativeColumn(1.3f); c.RelativeColumn(2); c.RelativeColumn(1.3f); c.RelativeColumn(2); });
            for (var i = 0; i < rows.Length; i += 2)
            {
                KvCell(table, rows[i].Label, rows[i].Value);
                if (i + 1 < rows.Length) KvCell(table, rows[i + 1].Label, rows[i + 1].Value);
                else { table.Cell(); table.Cell(); }
            }
        });
    }

    private static void KvCell(TableDescriptor table, string label, string value)
    {
        table.Cell().PaddingVertical(2).Text(label).FontSize(8).FontColor(Colors.Grey.Darken1);
        table.Cell().PaddingVertical(2).Text(value).FontSize(9);
    }

    private static void Th(TableDescriptor table, string text, bool left = false)
    {
        IContainer cell = table.Cell().Background(Color.FromHex(BoaBrand.Primary)).Padding(5);
        if (!left) cell = cell.AlignRight();
        cell.Text(text).SemiBold().FontSize(8).FontColor(Colors.White);
    }

    private static void Td(TableDescriptor table, string text, bool left = false)
    {
        IContainer cell = table.Cell().BorderBottom(0.5f).BorderColor(Color.FromHex(BoaBrand.MediumGray)).Padding(5);
        if (!left) cell = cell.AlignRight();
        cell.Text(text).FontSize(8);
    }

    private static void Gate(TableDescriptor table, bool pass)
    {
        table.Cell().BorderBottom(0.5f).BorderColor(Color.FromHex(BoaBrand.MediumGray)).Padding(5).AlignCenter()
            .Text(pass ? "PASS" : "FAIL").SemiBold().FontSize(8)
            .FontColor(Color.FromHex(pass ? BoaBrand.Accent : "#b91c1c"));
    }

    private static string JoinNonBlank(string sep, params string?[] parts)
    {
        var text = string.Join(sep, parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        return string.IsNullOrWhiteSpace(text) ? "—" : text;
    }
}

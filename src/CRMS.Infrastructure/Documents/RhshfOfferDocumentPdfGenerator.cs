using CRMS.Application.Rhshf.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CRMS.Infrastructure.Documents;

/// <summary>
/// The merged post-approval document — offer terms, conditions, implementation plan and acceptance
/// in one. Content follows the bank's approved template (Sagad Farms, V6); the layout is ours.
/// <para>
/// Two deliberate departures from the old pair of documents. The key facts that used to need a
/// separate signed statement are a panel on page one, so the borrower sees the amount, the rate, the
/// tenor and what falls due before reading any clause. And the implementation plan is printed from
/// the portal's itemised rows, which reconcile to the approved amount by contract — so the table
/// foots, rather than being prose a reader has to total themselves.
/// </para>
/// </summary>
public class RhshfOfferDocumentPdfGenerator : IRhshfOfferDocumentGenerator
{
    private static readonly Color Ink = Color.FromHex("#1a2b21");
    private static readonly Color Muted = Color.FromHex("#5d6b61");
    private static readonly Color Rule = Color.FromHex(BoaBrand.MediumGray);
    private static readonly Color Panel = Color.FromHex(BoaBrand.PanelGreen);
    private static readonly Color Brand = Color.FromHex(BoaBrand.Primary);
    private static readonly Color Accent = Color.FromHex(BoaBrand.Accent);

    public Task<byte[]> GenerateAsync(RhshfOfferDocumentData data, CancellationToken ct = default)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(38);
                page.DefaultTextStyle(x => x.FontSize(9.5f).FontColor(Ink).LineHeight(1.35f));

                page.Header().Element(c => Masthead(c, data));
                page.Content().Element(c => Body(c, data));
                page.Footer().Element(c => Footer(c, data));
            });
        });

        return Task.FromResult(document.GeneratePdf());
    }

    // ── Furniture ─────────────────────────────────────────────────────────────────────────────────

    private static void Masthead(IContainer container, RhshfOfferDocumentData data)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.ConstantItem(58).Element(c => BoaBrand.RenderLogo(c, 48));
                row.RelativeItem().PaddingLeft(10).Column(c =>
                {
                    c.Item().Text(data.BankName.ToUpperInvariant())
                        .Bold().FontSize(13).FontColor(Brand);
                    c.Item().Text("Renewed Hope Smallholder Support and Value-Chain Fund (RH-SHF)")
                        .FontSize(8).Italic().FontColor(Accent);
                });
                row.ConstantItem(150).AlignRight().Column(c =>
                {
                    c.Item().AlignRight().Text($"Ref: {data.Reference}").FontSize(8).SemiBold();
                    c.Item().AlignRight().Text($"Offer version {data.OfferVersion}").FontSize(8).SemiBold();
                    c.Item().AlignRight().Text(data.GeneratedDate.ToString("d MMMM yyyy")).FontSize(8).FontColor(Muted);
                });
            });
            col.Item().PaddingTop(8).LineHorizontal(1.2f).LineColor(Brand);
        });
    }

    private static void Footer(IContainer container, RhshfOfferDocumentData data)
    {
        container.Column(col =>
        {
            col.Item().PaddingBottom(4).LineHorizontal(0.5f).LineColor(Rule);
            col.Item().Row(row =>
            {
                row.RelativeItem().Text(t =>
                {
                    // Version on every page, not just the first: a corrected offer must not be
                    // confusable with the one it replaced from any single page of it.
                    t.Span($"{data.CompanyName} · {data.Reference} · Offer version {data.OfferVersion} · "
                        + $"issued {data.GeneratedDate:d MMM yyyy}").FontSize(7.5f).FontColor(Muted);
                });
                row.ConstantItem(70).AlignRight().Text(t =>
                {
                    t.CurrentPageNumber().FontSize(7.5f).FontColor(Muted);
                    t.Span(" of ").FontSize(7.5f).FontColor(Muted);
                    t.TotalPages().FontSize(7.5f).FontColor(Muted);
                });
            });
        });
    }

    // ── Body ──────────────────────────────────────────────────────────────────────────────────────

    private static void Body(IContainer container, RhshfOfferDocumentData data)
    {
        container.PaddingTop(16).Column(col =>
        {
            col.Spacing(12);

            Addressee(col, data);
            Subject(col, data);
            Opening(col, data);
            KeyFacts(col, data);
            Terms(col, data);
            ImplementationPlan(col, data);
            Security(col, data);
            Repayment(col, data);
            ConditionsPrecedent(col, data);
            OtherConditions(col, data);
            UpFrontCharges(col, data);
            Validity(col, data);
            Signatures(col, data);
            Acceptance(col, data);
        });
    }

    private static void Addressee(ColumnDescriptor col, RhshfOfferDocumentData data)
    {
        col.Item().Column(c =>
        {
            c.Item().Text("The Managing Director,").SemiBold();
            c.Item().Text($"{data.CompanyName},");
            foreach (var line in data.AddressLines)
                c.Item().Text($"{line},");
            c.Item().PaddingTop(10).Text("Dear Sir/Madam,").SemiBold();
        });
    }

    private static void Subject(ColumnDescriptor col, RhshfOfferDocumentData data)
    {
        // Capitalised and underlined, as the approved template sets it. The amount appears in figures
        // and in words — both, because the words are what resolve a disputed digit.
        col.Item().Column(c =>
        {
            c.Item().Text(
                $"PROVISIONAL LOAN OFFER OF {Money(data.Currency, data.ApprovedAmount)} "
                + $"({NairaAmountInWords.FormatUpper(data.ApprovedAmount)}) ONLY TO "
                + $"{data.CompanyName.ToUpperInvariant()} (FARMER AGGREGATION COMPANY)")
                .Bold().FontSize(10).FontColor(Brand);
            c.Item().PaddingTop(4).LineHorizontal(0.75f).LineColor(Accent);
        });
    }

    private static void Opening(ColumnDescriptor col, RhshfOfferDocumentData data)
    {
        col.Item().Text(
            $"We are pleased to inform you that {data.BankName} Limited has approved a loan of "
            + $"{Money(data.Currency, data.ApprovedAmount)} ({NairaAmountInWords.Format(data.ApprovedAmount)}) only "
            + $"under the auspices of the {data.ProgrammeName} in favour of {data.CompanyName} for the direct "
            + "procurement of inputs from accredited input producers and onward distribution of the input bundles "
            + "to certified farmer clusters operating under the supervision and control of the Farmer Aggregation "
            + "Company, subject to the following terms and conditions:");
    }

    /// <summary>
    /// The old Key Facts Statement, reduced to the four figures a borrower actually needs up front.
    /// It exists because this document is long: a reader should not have to assemble the amount, the
    /// rate, the term and what falls due from clauses spread over two pages.
    /// </summary>
    private static void KeyFacts(ColumnDescriptor col, RhshfOfferDocumentData data)
    {
        col.Item().Background(Panel).Padding(12).Column(box =>
        {
            box.Item().PaddingBottom(8).Text("KEY FACTS").Bold().FontSize(8)
                .FontColor(Brand).LetterSpacing(0.08f);

            box.Item().Row(row =>
            {
                Fact(row, "Amount approved", Money(data.Currency, data.ApprovedAmount));
                Fact(row, "Interest rate",
                    data.InterestRatePercent is { } r ? $"{r:0.##}% per annum" : "Per approved credit terms");
                Fact(row, "Tenor",
                    data.TenorMonths is { } m ? $"{m} months" : "Per approved credit terms");
                Fact(row, "Total repayable",
                    data.AmountDueAtHarvest is { } due ? Money(data.Currency, due) : "Per repayment schedule");
            });
        });
    }

    private static void Fact(RowDescriptor row, string label, string value)
    {
        row.RelativeItem().Column(c =>
        {
            c.Item().Text(label).FontSize(7.5f).FontColor(Muted);
            c.Item().PaddingTop(2).Text(value).Bold().FontSize(9.5f).FontColor(Brand);
        });
    }

    private static void Terms(ColumnDescriptor col, RhshfOfferDocumentData data)
    {
        Heading(col, "Facility terms");

        col.Item().Table(table =>
        {
            table.ColumnsDefinition(c => { c.ConstantColumn(130); c.RelativeColumn(); });

            Term(table, "Lender", $"{data.BankName} Limited");
            Term(table, "Borrower", RcNumberText.WithCompany(data.CompanyName, data.RcNumber));
            Term(table, "Loan amount", $"{Money(data.Currency, data.ApprovedAmount)} ({NairaAmountInWords.Format(data.ApprovedAmount)}) only");
            Term(table, "Loan purpose",
                "To finance the direct procurement of inputs from accredited input producers and onward "
                + "distribution of the input bundles to certified farmer clusters under the supervision and "
                + "control of the Farmer Aggregation Company.");
            // No grace period: the facility is repaid in one bullet payment at harvest, so there are
            // no instalments to defer. The cycle itself is the deferral.
            Term(table, "Loan period", data.TenorMonths is { } m
                ? $"{NairaAmountInWords.Convert(m)} ({m}) months from the date of first disbursement."
                : "As set out in the approved credit terms.");

            Term(table, "Interest rate", data.InterestRatePercent is { } rate
                ? $"{rate:0.##}% per annum, subject to variation as advised by the Central Bank of Nigeria."
                : "As set out in the approved credit terms, subject to variation.");

            if (data.FarmerCount is { } farmers)
                Term(table, "Farmers covered", data.RegisteredFarmerCount is { } registered
                    ? $"{farmers:N0} smallholder farmers financed under this facility, of the {registered:N0} registered "
                      + "with this aggregator."
                    : $"{farmers:N0} smallholder farmers under this aggregator.");

            // Fees come from the core banking product, which is what actually deducts them at
            // booking. Stating a figure held anywhere else risks the letter and the deduction
            // disagreeing, which is the one thing this programme's reconciliation rules forbid.
            Term(table, "Charges and fees", null, cell =>
            {
                // Three ways a charge reaches the borrower, stated separately so none reads as payable
                // twice: financed inside the loan (Schedule B), deducted at disbursement by the core
                // banking product, or paid separately before disbursement.
                var financed = (data.Summary ?? [])
                    .Where(s => s.Treatment == RhshfOfferSummaryRow.FinancedInLoan && !IsInputComponent(s.Label))
                    .Select(s => s.Label)
                    .ToList();
                if (financed.Count > 0)
                    cell.Item().PaddingBottom(4).Text(
                        $"{JoinLabels(financed)}: financed in the loan amount, as set out in Schedule B. "
                        + "They are not deducted again at disbursement.");

                if (data.ProductFees.Count == 0)
                {
                    cell.Item().Text("As set out in the approved credit terms, advised before disbursement.");
                }
                else
                {
                    foreach (var fee in data.ProductFees)
                        cell.Item().Text($"{fee.Name}: {fee.Display}, deducted at disbursement.");
                }

                cell.Item().PaddingTop(4).Text(
                    "Documentation fee: to be assessed and forwarded to the borrower for settlement prior to "
                    + "disbursement of the facility.")
                    .FontSize(9);
                cell.Item().PaddingTop(4).Text(
                    "The legal documentation fee covers charges relating to the perfection of legal and other "
                    + "documentation required for the loan. Where the provision proves inadequate, the borrower "
                    + "will be advised to make up the difference.")
                    .FontSize(8.5f).Italic().FontColor(Muted);
            });
        });
    }

    /// <summary>
    /// The implementation plan. With the portal's itemised breakdown it is two schedules: A, each
    /// approved EOP line by line with its supplier, quantities and price; B, the facility by component,
    /// footing to the approved amount. Without one, a single crop-level table and a note saying so.
    /// </summary>
    private static void ImplementationPlan(ColumnDescriptor col, RhshfOfferDocumentData data)
    {
        Heading(col, "Implementation plan");
        col.Item().Text("The approved loan will be applied in accordance with the plan below.").FontSize(9);

        if (data.Schedules is { Count: > 0 } schedules)
        {
            ItemisedSchedules(col, data, schedules);
            return;
        }

        if (data.PlanRows.Count == 0)
        {
            col.Item().PaddingTop(4).Text("No itemised breakdown was recorded for this facility.")
                .Italic().FontColor(Muted);
            return;
        }

        col.Item().PaddingTop(6).Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(2.4f);   // supplier
                c.RelativeColumn(2.8f);   // input / charge
                c.RelativeColumn(1.3f);   // quantity
                c.RelativeColumn(1.6f);   // amount
            });

            table.Header(h =>
            {
                Th(h.Cell(), "Supplier");
                Th(h.Cell(), "Input / charge");
                Th(h.Cell(), "Quantity");
                Th(h.Cell(), "Amount", right: true);
            });

            foreach (var row in data.PlanRows)
            {
                var background = row.IsCharge ? Panel : Colors.White;

                table.Cell().Background(background).BorderBottom(0.5f).BorderColor(Rule).Padding(5)
                    .Text(row.Supplier ?? (row.IsCharge ? "" : "—")).FontSize(8.5f);

                table.Cell().Background(background).BorderBottom(0.5f).BorderColor(Rule).Padding(5)
                    .Column(c =>
                    {
                        var description = c.Item().Text(row.Description).FontSize(8.5f);
                        if (row.IsCharge) description.SemiBold();
                        if (!string.IsNullOrWhiteSpace(row.Basis))
                            c.Item().Text(row.Basis).FontSize(7.5f).FontColor(Muted);
                    });

                table.Cell().Background(background).BorderBottom(0.5f).BorderColor(Rule).Padding(5)
                    .Text(row.QuantityDisplay ?? "").FontSize(8.5f);

                table.Cell().Background(background).BorderBottom(0.5f).BorderColor(Rule).Padding(5)
                    .AlignRight().Text(Money(data.Currency, row.Amount)).FontSize(8.5f);
            }

            // The total is the approved amount itself, not a sum of the rows above it. The portal's
            // contract guarantees they reconcile and submission is refused when they do not, so
            // printing the authoritative figure is right — and if the two ever diverged, the gap
            // would be visible here rather than hidden behind a computed subtotal.
            table.Cell().ColumnSpan(3).BorderTop(1.2f).BorderColor(Brand).Padding(5)
                .Text("Total loan amount").Bold().FontSize(9);
            table.Cell().BorderTop(1.2f).BorderColor(Brand).Padding(5)
                .AlignRight().Text(Money(data.Currency, data.ApprovedAmount)).Bold().FontSize(9).FontColor(Brand);
        });

        if (!data.PlanIsItemised)
            col.Item().PaddingTop(4).Text(
                "The itemised breakdown of inputs, suppliers and charges was not received for this application, "
                + "so the plan above is shown at crop level only. The approved EOP on record governs disbursement.")
                .FontSize(8).Italic().FontColor(Muted);
    }

    private static void ItemisedSchedules(
        ColumnDescriptor col, RhshfOfferDocumentData data, IReadOnlyList<RhshfOfferEopSchedule> schedules)
    {
        for (var i = 0; i < schedules.Count; i++)
        {
            var schedule = schedules[i];
            var name = schedules.Count == 1 ? "Schedule A" : $"Schedule A{i + 1}";

            col.Item().PaddingTop(8).Text($"{name}: approved EOP inputs and services — {schedule.Title}")
                .SemiBold().FontSize(9.5f).FontColor(Brand);

            col.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    // Money columns sized for "NGN 12,884,464.00" on one line: a figure split across
                    // two lines reads as two numbers.
                    c.RelativeColumn(2.3f);   // input and product
                    c.RelativeColumn(2.1f);   // supplier
                    c.RelativeColumn(1.0f);   // per farmer
                    c.RelativeColumn(1.1f);   // total quantity
                    c.RelativeColumn(1.6f);   // unit price
                    c.RelativeColumn(2.1f);   // amount
                });

                table.Header(h =>
                {
                    Th(h.Cell(), "Input");
                    Th(h.Cell(), "Supplier / provider");
                    Th(h.Cell(), "Per farmer");
                    Th(h.Cell(), "Total qty");
                    Th(h.Cell(), "Unit price", right: true);
                    Th(h.Cell(), "Amount", right: true);
                });

                foreach (var row in schedule.Rows)
                {
                    Cell(table).Column(c =>
                    {
                        c.Item().Text(row.Input).FontSize(8.5f).SemiBold();
                        if (row.Product is not null && !string.Equals(row.Product, row.Input, StringComparison.OrdinalIgnoreCase))
                            c.Item().Text(row.Product).FontSize(7.5f).FontColor(Muted);
                        if (row.PackagingExcess != 0m)
                            c.Item().Text($"Plus {Money(data.Currency, row.PackagingExcess)} whole-pack excess")
                                .FontSize(7.5f).FontColor(Muted);
                    });
                    // An unassigned supplier is printed as such, not left blank: the borrower should see
                    // that the provider is still to be confirmed, not wonder whether it was omitted.
                    Cell(table).Text(row.Supplier ?? "To be confirmed").FontSize(8.5f);
                    Cell(table).Text(row.PerFarmer ?? "").FontSize(8.5f);
                    Cell(table).Text(row.TotalQuantity ?? "").FontSize(8.5f);
                    Cell(table).AlignRight().Text(row.UnitPrice is { } p ? Money(data.Currency, p) : "").FontSize(8.5f);
                    Cell(table).AlignRight().Text(Money(data.Currency, row.Amount + row.PackagingExcess)).FontSize(8.5f);
                }

                foreach (var sub in schedule.Subtotals)
                {
                    table.Cell().ColumnSpan(5).Background(Panel).BorderBottom(0.5f).BorderColor(Rule).Padding(4)
                        .Text(sub.Label).FontSize(8.5f);
                    table.Cell().Background(Panel).BorderBottom(0.5f).BorderColor(Rule).Padding(4)
                        .AlignRight().Text(Money(data.Currency, sub.Amount)).FontSize(8.5f);
                }

                if (schedule.Total is { } total)
                {
                    table.Cell().ColumnSpan(5).BorderTop(1f).BorderColor(Brand).Padding(4)
                        .Text("EOP total").Bold().FontSize(8.5f);
                    table.Cell().BorderTop(1f).BorderColor(Brand).Padding(4)
                        .AlignRight().Text(Money(data.Currency, total)).Bold().FontSize(8.5f);
                }
            });
        }

        if (data.Summary is not { Count: > 0 } summary)
            return;

        col.Item().PaddingTop(10).Text("Schedule B: financing components of the facility")
            .SemiBold().FontSize(9.5f).FontColor(Brand);

        col.Item().PaddingTop(4).Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(4f);     // component and basis
                c.RelativeColumn(2.2f);   // treatment
                c.RelativeColumn(1.8f);   // amount
            });

            table.Header(h =>
            {
                Th(h.Cell(), "Component");
                Th(h.Cell(), "How it is paid");
                Th(h.Cell(), "Amount", right: true);
            });

            foreach (var row in summary)
            {
                Cell(table).Column(c =>
                {
                    c.Item().Text(row.Label).FontSize(8.5f);
                    if (!string.IsNullOrWhiteSpace(row.Basis))
                        c.Item().Text(row.Basis).FontSize(7.5f).FontColor(Muted);
                });
                Cell(table).Text(row.Treatment ?? "").FontSize(8.5f);
                Cell(table).AlignRight().Text(Money(data.Currency, row.Amount)).FontSize(8.5f);
            }

            // The approved amount itself, not a sum of the rows: the portal's contract guarantees they
            // reconcile and CRMS refuses a breakdown that does not, so a gap here would be visible.
            table.Cell().ColumnSpan(2).BorderTop(1.2f).BorderColor(Brand).Padding(5)
                .Text("Total loan amount").Bold().FontSize(9);
            table.Cell().BorderTop(1.2f).BorderColor(Brand).Padding(5)
                .AlignRight().Text(Money(data.Currency, data.ApprovedAmount)).Bold().FontSize(9).FontColor(Brand);
        });

        col.Item().ShowEntire().PaddingTop(4).Text(
            "Suppliers and providers are named to show how the facility is applied. Naming them does not by "
            + "itself authorise any supply: deliveries proceed only against the Bank's release for each supplier.")
            .FontSize(8).Italic().FontColor(Muted);
    }

    private static IContainer Cell(TableDescriptor table) =>
        table.Cell().BorderBottom(0.5f).BorderColor(Rule).Padding(4);

    private static bool IsInputComponent(string label) =>
        label is "Physical inputs" or "Services" || label.StartsWith("Packaging excess", StringComparison.Ordinal);

    private static string JoinLabels(IReadOnlyList<string> labels) =>
        labels.Count == 1 ? labels[0] : $"{string.Join(", ", labels.Take(labels.Count - 1))} and {labels[^1]}";

    private static void Security(ColumnDescriptor col, RhshfOfferDocumentData data)
    {
        Heading(col, "Security");

        if (data.SecurityDescriptions.Count == 0)
        {
            col.Item().Text(
                "Security as recorded on this facility, verified and perfected by the Bank's Legal Officer "
                + "before disbursement.");
            return;
        }

        foreach (var security in data.SecurityDescriptions)
            Bullet(col, security);
    }

    private static void Repayment(ColumnDescriptor col, RhshfOfferDocumentData data)
    {
        Heading(col, "Repayment");

        // One bullet payment at harvest, so there is no instalment schedule and no grace period to
        // describe — the wording below is the whole repayment obligation.
        col.Item().Text(
            "Repayment falls due in accordance with the schedule set out in the approved credit terms. Any "
            + "outstanding principal together with accrued but unpaid interest shall be repaid in full on or "
            + "before expiry of the loan tenor.");

        if (data.InterestCharge is { } interest && data.AmountDueAtHarvest is { } due)
            col.Item().PaddingTop(4).Text(
                $"On the terms above, interest over the facility period is {Money(data.Currency, interest)}, "
                + $"and the total repayable is {Money(data.Currency, due)}.")
                .FontSize(9).FontColor(Muted);
    }

    private static void ConditionsPrecedent(ColumnDescriptor col, RhshfOfferDocumentData data)
    {
        Heading(col, "Conditions precedent to draw down");

        Numbered(col, 1, "Acceptance of this offer, including its terms and conditions.");
        Numbered(col, 2,
            "Completion of legal documentation through execution of the loan agreement and submission of the "
            + "security documents recorded on this facility, in form and substance acceptable to the Bank.");
        Numbered(col, 3,
            "Board resolution of the company accepting this loan offer, including the terms and conditions "
            + "set out in it.");
        Numbered(col, 4,
            "CAC status report confirming the company's status as active on the Companies Registration Portal.");
        Numbered(col, 5,
            "Settlement of all documentation fees associated with perfection of the facility, including "
            + "solicitors' fees.");
        Numbered(col, 6, "Evidence of payment of the up-front charges set out below.");
    }

    private static void OtherConditions(ColumnDescriptor col, RhshfOfferDocumentData data)
    {
        Heading(col, "Other conditions");

        Bullet(col,
            "The Bank shall monitor performance of the loan periodically and may request any information "
            + "regarding it, including financial records, which the borrower is obliged to produce.");
        Bullet(col,
            "The Bank reserves the right to vary the interest rate in line with any variation advised by the "
            + "Central Bank of Nigeria. Any such variation shall be communicated formally, takes effect from the "
            + "date specified in the Bank's notice, and shall be deemed to form part of this loan offer.");
        Bullet(col, "Submission of annual financial statements to the Bank during the tenor of the loan.");
        Bullet(col,
            "Acceptance of this loan offer creates no legal obligation on the part of the Bank unless and until "
            + "the conditions have been fulfilled and legal documentation executed.");
        Bullet(col,
            "The Bank may recover past due principal and interest by debiting the borrower's account with any "
            + "financial institution.");
        Bullet(col, "All recovery fees and charges in the event of default shall be debited to the borrower's account.");
        Bullet(col,
            "The facility shall be utilised strictly for input financing through the input suppliers and "
            + "producers approved under the programme.");
        Bullet(col,
            "Disbursement is made to the approved input suppliers, not as cash to the borrower.");
    }

    private static void UpFrontCharges(ColumnDescriptor col, RhshfOfferDocumentData data)
    {
        if (data.FeesAccount is null)
            return;

        Heading(col, "Payment of up-front charges");
        col.Item().Text("On confirmation and acceptance, kindly pay all up-front charges into the account below.")
            .FontSize(9);

        col.Item().PaddingTop(6).Border(0.75f).BorderColor(Rule).Padding(10).Column(c =>
        {
            c.Item().Text(t => { t.Span("Bank name: ").FontColor(Muted); t.Span(data.FeesAccount.BankName).SemiBold(); });
            c.Item().Text(t => { t.Span("Account name: ").FontColor(Muted); t.Span(data.FeesAccount.AccountName).SemiBold(); });
            c.Item().Text(t => { t.Span("Account number: ").FontColor(Muted); t.Span(data.FeesAccount.AccountNumber).SemiBold(); });
        });
    }

    private static void Validity(ColumnDescriptor col, RhshfOfferDocumentData data)
    {
        Heading(col, "Validity of offer");

        col.Item().Text(
            $"This offer remains valid for {NairaAmountInWords.Convert(data.ValidityDays)} ({data.ValidityDays}) days "
            + $"from the date above, and lapses automatically if it is not accepted within that period. "
            + $"The offer therefore expires on {data.GeneratedDate.AddDays(data.ValidityDays):d MMMM yyyy}.");

        col.Item().PaddingTop(4).Text(
            "If these conditions are acceptable, kindly sign over a ₦50 stamp in the acceptance section below "
            + "and return this document to the Bank within the validity period.");
    }

    private static void Signatures(ColumnDescriptor col, RhshfOfferDocumentData data)
    {
        col.Item().PaddingTop(10).Column(c =>
        {
            c.Item().Text("Yours faithfully,");
            c.Item().PaddingTop(2).Text($"{data.BankName.ToUpperInvariant()} LIMITED").Bold().FontColor(Brand);

            c.Item().PaddingTop(34).Row(row =>
            {
                SignatureLine(row, "Head, Product & Segment Department");
                row.ConstantItem(26);
                SignatureLine(row, "Company Secretary / Legal Adviser");
            });

            c.Item().PaddingTop(6).Text("For: Managing Director / Chief Executive Officer")
                .FontSize(8.5f).FontColor(Muted);
        });
    }

    /// <summary>
    /// Two acceptance blocks, as the approved template carries — the borrower's authorised
    /// signatories sign separately. Kept on one page with the instruction above them so a signatory
    /// is never asked to sign a block whose terms sit on another sheet.
    /// </summary>
    private static void Acceptance(ColumnDescriptor col, RhshfOfferDocumentData data)
    {
        col.Item().PageBreak();

        col.Item().Column(c =>
        {
            c.Item().Text("ACCEPTANCE").Bold().FontSize(11).FontColor(Brand);
            c.Item().PaddingTop(2).LineHorizontal(0.75f).LineColor(Accent);
            c.Item().PaddingTop(8).Text(
                $"We accept this loan offer, including the terms and conditions set out in it, for and on behalf "
                + $"of {data.CompanyName}"
                + RcNumberText.Parenthesised(data.RcNumber) + ".");
            c.Item().PaddingTop(2).Text("To be signed over a ₦50 stamp.").FontSize(8.5f).Italic().FontColor(Muted);
        });

        for (var i = 1; i <= 2; i++)
            AcceptanceBlock(col, i);
    }

    private static void AcceptanceBlock(ColumnDescriptor col, int ordinal)
    {
        col.Item().PaddingTop(14).Border(0.75f).BorderColor(Rule).Padding(14).Column(c =>
        {
            c.Item().PaddingBottom(10).Text($"Authorised signatory {ordinal}")
                .Bold().FontSize(8).FontColor(Brand).LetterSpacing(0.08f);

            foreach (var field in new[] { "Name", "Designation", "Signature", "Date" })
            {
                c.Item().PaddingTop(16).Row(row =>
                {
                    row.ConstantItem(84).Text($"{field}:").FontSize(9).FontColor(Muted);
                    row.RelativeItem().PaddingTop(10).LineHorizontal(0.6f).LineColor(Rule);
                });
            }
        });
    }

    // ── Primitives ────────────────────────────────────────────────────────────────────────────────

    private static void Heading(ColumnDescriptor col, string text)
    {
        col.Item().PaddingTop(2).Column(c =>
        {
            c.Item().Text(text.ToUpperInvariant()).Bold().FontSize(8).FontColor(Brand).LetterSpacing(0.08f);
            c.Item().PaddingTop(3).LineHorizontal(0.5f).LineColor(Rule);
        });
    }

    private static void Term(TableDescriptor table, string label, string? value, Action<ColumnDescriptor>? content = null)
    {
        table.Cell().BorderBottom(0.5f).BorderColor(Rule).PaddingVertical(5).PaddingRight(8)
            .Text(label).SemiBold().FontSize(9);

        var cell = table.Cell().BorderBottom(0.5f).BorderColor(Rule).PaddingVertical(5);
        if (content is not null)
            cell.Column(content);
        else
            cell.Text(value ?? "").FontSize(9);
    }

    private static void Th(IContainer container, string text, bool right = false)
    {
        var cell = container.Background(Brand).Padding(5);
        (right ? cell.AlignRight() : cell)
            .Text(text).Bold().FontSize(8).FontColor(Colors.White);
    }

    private static void SignatureLine(RowDescriptor row, string role)
    {
        row.RelativeItem().Column(c =>
        {
            c.Item().LineHorizontal(0.6f).LineColor(Rule);
            c.Item().PaddingTop(3).Text(role).FontSize(8).FontColor(Muted);
        });
    }

    private static void Bullet(ColumnDescriptor col, string text)
    {
        col.Item().PaddingTop(3).Row(row =>
        {
            row.ConstantItem(12).PaddingTop(1).Text("•").FontColor(Accent).Bold();
            row.RelativeItem().Text(text).FontSize(9);
        });
    }

    private static void Numbered(ColumnDescriptor col, int n, string text)
    {
        col.Item().PaddingTop(3).Row(row =>
        {
            row.ConstantItem(18).Text($"{n}.").FontColor(Accent).SemiBold().FontSize(9);
            row.RelativeItem().Text(text).FontSize(9);
        });
    }

    private static string Money(string currency, decimal amount) => $"{currency} {amount:N2}";
}

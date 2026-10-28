using CRMS.Application.Rhshf.Interfaces;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace CRMS.Application.Rhshf.Services;

/// <summary>
/// Assembles the payload for the merged post-approval document.
/// <para>
/// Shared so that ratification and a later reissue produce the same document. They used to build
/// their own: ratification issued the merged letter while a reissue still produced the old
/// letter-plus-Key-Facts pair, so the same facility had two different document shapes depending on
/// which path happened to run.
/// </para>
/// </summary>
public class RhshfOfferDocumentDataBuilder
{
    private const string BankName = "Bank of Agriculture";
    private const int OfferValidityDays = 7;

    /// <summary>Where the borrower pays the up-front charges, as the approved template states it.</summary>
    private static readonly RhshfOfferFeesAccount FeesAccount =
        new("Bank of Agriculture (BOA)", "Renewed Hope Small Holders Fees", "0016840374");

    private readonly IRhshfCollateralRepository _collateralRepo;
    private readonly ILoanProductRepository _loanProductRepo;
    private readonly IFineractDirectService _fineract;
    private readonly ILogger<RhshfOfferDocumentDataBuilder> _logger;

    public RhshfOfferDocumentDataBuilder(
        IRhshfCollateralRepository collateralRepo,
        ILoanProductRepository loanProductRepo,
        IFineractDirectService fineract,
        ILogger<RhshfOfferDocumentDataBuilder> logger)
    {
        _collateralRepo = collateralRepo;
        _loanProductRepo = loanProductRepo;
        _fineract = fineract;
        _logger = logger;
    }

    /// <param name="offerVersion">The version this document will carry. Defaults to the case's
    /// current one; a corrected offer builds its document before the case records the correction, so
    /// it passes the next version itself.</param>
    public async Task<RhshfOfferDocumentBuild> BuildAsync(
        RhshfCreditProfile profile, decimal approvedAmount, CancellationToken ct, int? offerVersion = null)
    {
        var appraisal = profile.GetCurrentCycleFinancialAppraisal();
        var collateral = await _collateralRepo.GetByProfileAndCycleAsync(profile.Id, profile.CurrentCycleNumber, ct);
        var (productFees, suppressed) = await ResolveProductFeesAsync(profile, ct);

        var data = new RhshfOfferDocumentData(
            Reference: profile.Reference,
            CompanyName: profile.CompanyName,
            RcNumber: profile.RcNumber,
            ProgrammeName: profile.ProgrammeName,
            SessionName: profile.SessionName,
            ApprovedAmount: approvedAmount,
            Currency: profile.Currency,
            GeneratedDate: DateTime.UtcNow,
            BankName: BankName,
            AddressLines: BuildAddressLines(profile),
            InterestRatePercent: appraisal?.InterestRatePercent,
            TenorMonths: appraisal?.CycleMonths,
            AmountDueAtHarvest: appraisal?.AmountDueAtHarvest,
            InterestCharge: appraisal?.InterestCharge,
            // The farmers the facility finances, from the approved EOPs — not FarmerCount, which the
            // portal sends as everyone registered under the FAC (CRMSQA#2: 220 printed against 30).
            FarmerCount: profile.FinancedFarmerCount ?? profile.FarmerCount,
            PlanRows: BuildPlanRows(profile, approvedAmount),
            PlanIsItemised: profile.HasItemisedFinancing,
            ProductFees: productFees,
            SecurityDescriptions: collateral.Select(c => DescribeSecurity(c, approvedAmount)).ToList(),
            FeesAccount: FeesAccount,
            ValidityDays: OfferValidityDays,
            OfferVersion: offerVersion ?? Math.Max(profile.OfferVersion, 1),
            RegisteredFarmerCount: profile.FinancedFarmerCount is { } financed && profile.FarmerCount is { } registered
                && financed != registered ? registered : null,
            Schedules: profile.HasItemisedFinancing ? BuildSchedules(profile) : [],
            Summary: profile.HasItemisedFinancing ? BuildSummary(profile, approvedAmount) : []);

        return new RhshfOfferDocumentBuild(data, suppressed);
    }

    /// <summary>
    /// Schedule A, one per approved EOP: its lines in full, then its own subtotals from the portal's
    /// per-EOP summary. Lines are grouped by the EOP the portal tagged them with; a line with no EOP
    /// on a single-EOP case belongs to that EOP, and on a multi-EOP case is printed in its own group
    /// rather than guessed into one.
    /// </summary>
    private static List<RhshfOfferEopSchedule> BuildSchedules(RhshfCreditProfile profile)
    {
        var eops = profile.FinancingEops.ToList();
        var singleEopId = eops.Count == 1 ? eops[0].EopId
            : profile.FinancingLines.Select(l => l.EopId).Where(id => id is not null).Distinct().Count() == 1
                ? profile.FinancingLines.First(l => l.EopId is not null).EopId
                : null;

        var groups = profile.FinancingLines
            .Where(l => l.Amount != 0m || l.PackagingExcess != 0m)
            .GroupBy(l => l.EopId ?? singleEopId)
            .ToList();

        var schedules = new List<RhshfOfferEopSchedule>();
        foreach (var group in groups)
        {
            var eop = eops.FirstOrDefault(e => e.EopId == group.Key);
            var lines = group.ToList();
            var farmers = eop?.Farmers ?? lines.Max(l => l.Farmers);
            var crop = eop?.Crop ?? lines.Select(l => l.Crop).FirstOrDefault(c => c is not null);

            var title = group.Key is null
                ? "Other financed items"
                : string.Join(", ", new[]
                {
                    crop, eop?.State,
                    farmers is { } f ? $"{f:N0} farmers" : null,
                    $"EOP {group.Key.Value.ToString()[..8].ToUpperInvariant()}",
                }.Where(s => !string.IsNullOrWhiteSpace(s)));

            var rows = lines
                .OrderBy(l => IsService(l) ? 1 : 0).ThenBy(l => l.Item)
                .Select(l => new RhshfOfferInputRow(
                    Input: l.Item,
                    Product: string.IsNullOrWhiteSpace(l.SpecificInput) ? null : l.SpecificInput,
                    Supplier: l.Supplier,
                    PerFarmer: IsPerFarmer(l) ? l.QuantityDisplay : null,
                    TotalQuantity: TotalQuantity(l),
                    UnitPrice: l.UnitPrice,
                    Amount: l.Amount,
                    PackagingExcess: l.PackagingExcess,
                    Farmers: IsPerFarmer(l) ? l.Farmers : null))
                .ToList();

            var subtotals = new List<RhshfOfferSummaryRow>();
            if (eop is not null)
            {
                subtotals.Add(new("Inputs and services", eop.Inputs, null, RhshfOfferSummaryRow.FinancedInLoan));
                if (eop.PackagingExcess != 0m)
                    subtotals.Add(new("Packaging excess (whole supplier packs)", eop.PackagingExcess, null, RhshfOfferSummaryRow.FinancedInLoan));
                subtotals.Add(new("Insurance", eop.Insurance, null, RhshfOfferSummaryRow.FinancedInLoan));
                subtotals.Add(new("Working capital", eop.WorkingCapital, null, RhshfOfferSummaryRow.FinancedInLoan));
                subtotals.Add(new("Technology services fee", eop.TechServicesFee, null, RhshfOfferSummaryRow.FinancedInLoan));
            }

            schedules.Add(new RhshfOfferEopSchedule(title, farmers, rows, subtotals, eop?.ApprovedTotal, CostBasis(eop, profile.Currency)));
        }

        return schedules;
    }

    /// <summary>
    /// Schedule B, the whole facility by component, footing to the approved amount. Physical inputs
    /// and services are separated because they are fulfilled differently — a supplier delivers one, a
    /// provider performs the other — and the charges carry the portal's own wording for their basis.
    /// </summary>
    private static List<RhshfOfferSummaryRow> BuildSummary(RhshfCreditProfile profile, decimal approvedAmount)
    {
        var lines = profile.FinancingLines;
        var rows = new List<RhshfOfferSummaryRow>
        {
            new("Physical inputs", lines.Where(l => !IsService(l)).Sum(l => l.Amount), null, RhshfOfferSummaryRow.FinancedInLoan),
        };

        var services = lines.Where(IsService).Sum(l => l.Amount);
        if (services != 0m)
            rows.Add(new("Services", services, null, RhshfOfferSummaryRow.FinancedInLoan));

        var excess = lines.Sum(l => l.PackagingExcess);
        if (excess != 0m)
            rows.Add(new("Packaging excess (whole supplier packs)", excess, null, RhshfOfferSummaryRow.FinancedInLoan));

        rows.AddRange(profile.FinancingCharges.Select(c => new RhshfOfferSummaryRow(
            c.Label, c.Amount, c.RateBasis, c.InLoan ? RhshfOfferSummaryRow.FinancedInLoan : "Not financed")));

        return rows;
    }

    /// <summary>
    /// The EOP total as the approved EOP states it: farmers times the cost per farmer, plus the
    /// whole-EOP adjustments (packaging excess). Grandbai: 30 × 427,268.80 + 66,400.00. Null unless
    /// the per-farmer figure divides to the kobo — a rounded figure that did not multiply back to
    /// the total would be worse on a contract than no figure.
    /// </summary>
    private static string? CostBasis(RhshfFinancingEop? eop, string currency)
    {
        if (eop?.Farmers is not { } farmers || farmers <= 0)
            return null;

        var perFarmerTotal = eop.ApprovedTotal - eop.PackagingExcess;
        var perFarmer = Math.Round(perFarmerTotal / farmers, 2);
        if (perFarmer * farmers != perFarmerTotal)
            return null;

        var basis = $"{farmers:N0} farmers × {currency} {perFarmer:N2} per farmer";
        if (eop.PackagingExcess != 0m)
            basis += $" + {currency} {eop.PackagingExcess:N2} whole-pack adjustments";
        return $"{basis} = {currency} {eop.ApprovedTotal:N2}";
    }

    private static bool IsService(RhshfFinancingLine line) =>
        string.Equals(line.Category, "Services", StringComparison.OrdinalIgnoreCase);

    private static bool IsPerFarmer(RhshfFinancingLine line) =>
        !string.Equals(line.Basis, "wholeEop", StringComparison.OrdinalIgnoreCase);

    /// <summary>Per-farmer quantity times the EOP's farmers, in the line's own unit — the quantity the
    /// EOP finances before rounding up to whole packs. A whole-EOP service is stated as sent.</summary>
    private static string? TotalQuantity(RhshfFinancingLine line)
    {
        if (line.QuantityPerFarmer is not { } perFarmer || string.IsNullOrWhiteSpace(line.Unit))
            return null;
        if (!IsPerFarmer(line) || line.Farmers is not { } farmers)
            return $"{perFarmer:0.###} {line.Unit.Trim()}";

        // "Per Farmer" is a unit of service, not a quantity of goods: 30 farmers served, not "30 Per Farmer".
        return line.Unit.Trim().StartsWith("Per ", StringComparison.OrdinalIgnoreCase)
            ? $"{farmers:N0} farmers"
            : $"{perFarmer * farmers:#,0.###} {line.Unit.Trim()}";
    }

    /// <summary>
    /// The implementation plan rows: the portal's itemised inputs and charges where they exist,
    /// otherwise the crop-level lines plus one balancing row so the plan still foots to the approved
    /// amount.
    /// </summary>
    private static List<RhshfOfferPlanRow> BuildPlanRows(RhshfCreditProfile profile, decimal approvedAmount)
    {
        if (profile.HasItemisedFinancing)
        {
            var rows = profile.FinancingLines
                // A zero-amount line (an extension service bundled into another) would print as a
                // row the borrower cannot act on, so it is left out of the plan.
                .Where(l => l.Amount != 0m || l.PackagingExcess != 0m)
                .OrderBy(l => l.Crop).ThenBy(l => l.Item)
                .Select(l => new RhshfOfferPlanRow(
                    Supplier: l.Supplier,
                    Description: l.Description,
                    QuantityDisplay: l.QuantityDisplay,
                    Amount: l.Amount + l.PackagingExcess,
                    IsCharge: false,
                    Basis: l.PackagingExcess != 0m
                        ? $"Includes {profile.Currency} {l.PackagingExcess:N2} of whole-pack excess"
                        : null))
                .ToList();

            rows.AddRange(profile.FinancingCharges
                .Select(c => new RhshfOfferPlanRow(
                    Supplier: null,
                    Description: c.Label,
                    QuantityDisplay: null,
                    Amount: c.Amount,
                    IsCharge: true,
                    Basis: c.RateBasis)));

            return rows;
        }

        // The portal's contract sends eopLines as a crop-level summary with zero quantities, so a
        // quantity printed here would be "0 kg" — a placeholder a borrower reads as a real figure.
        // Left blank instead; the note under the table says why the plan is not itemised.
        var fallback = profile.EopLines
            .Select(l => new RhshfOfferPlanRow(
                Supplier: null, Description: l.Commodity,
                QuantityDisplay: l.QuantityKg > 0 ? l.QuantityDisplay : null,
                Amount: l.LineValue, IsCharge: false, Basis: null))
            .ToList();

        // Whatever the crop-level lines do not account for is the programme charges, which were not
        // itemised on this case. One row keeps the plan footing rather than leaving a silent gap.
        var remainder = approvedAmount - fallback.Sum(r => r.Amount);
        if (Math.Abs(remainder) > 0.01m)
            fallback.Add(new RhshfOfferPlanRow(
                Supplier: null, Description: "Programme charges", QuantityDisplay: null,
                Amount: remainder, IsCharge: true,
                Basis: "Insurance, working capital and programme fees, as approved"));

        return fallback;
    }

    /// <summary>
    /// The fees the core banking product applies at booking, read from the product rather than held
    /// in CRMS — the letter must state what will actually be deducted. A lookup failure leaves the
    /// list empty and the document falls back to "per the approved credit terms": an offer must not
    /// fail to generate because a product catalogue call did not answer.
    /// <para>
    /// Anything the EOP already finances is dropped before it reaches the letter. The programme's
    /// rule is that no fee may be financed and then deducted again, so a letter that lists insurance
    /// or working capital as "deducted at disbursement" when the borrower has already borrowed it
    /// would be promising exactly the double charge the rule forbids.
    /// </para>
    /// </summary>
    private async Task<(List<RhshfOfferFee> Fees, List<string> Suppressed)> ResolveProductFeesAsync(
        RhshfCreditProfile profile, CancellationToken ct)
    {
        var product = await _loanProductRepo.GetActiveRhshfProductAsync(ct);
        if (product?.FineractProductId is null or <= 0)
            return ([], []);

        var catalogue = await _fineract.GetLoanProductsAsync(activeOnly: true, ct);
        if (catalogue.IsFailure)
            return ([], []);

        var match = catalogue.Value.FirstOrDefault(p => p.Id == product.FineractProductId);
        if (match is null)
            return ([], []);

        var financed = profile.FinancingCharges
            .Where(c => c.InLoan)
            .Select(c => (c.Code, c.Label))
            .ToList();

        var fees = new List<RhshfOfferFee>();
        var suppressed = new List<string>();

        foreach (var charge in match.ProductCharges)
        {
            if (RhshfFinancedChargeMatcher.IsAlreadyFinanced(charge.Name, financed))
            {
                // Suppressing the line keeps the letter honest, but it does not stop core banking
                // deducting the fee — only fixing the product does. Logged with the reference, and
                // returned so the case itself can show it to whoever can act.
                _logger.LogWarning(
                    "RH-SHF {Reference}: core banking product {ProductId} charges \"{Charge}\", which this " +
                    "EOP already finances. Left off the offer letter; the product configuration needs fixing " +
                    "or the borrower will be charged twice.",
                    profile.Reference, match.Id, charge.Name);

                suppressed.Add(charge.Name);
                continue;
            }

            fees.Add(new RhshfOfferFee(charge.Name, charge.Display));
        }

        return (fees, suppressed);
    }


    /// <summary>The CAC registered address where one was fetched, otherwise the location the portal
    /// supplied. Empty rather than a stub when neither is known.</summary>
    private static List<string> BuildAddressLines(RhshfCreditProfile profile)
    {
        var lines = new List<string>();

        if (!string.IsNullOrWhiteSpace(profile.CacAddress))
        {
            lines.Add(profile.CacAddress.Trim());
            var locality = string.Join(", ", new[] { profile.CacCity, profile.CacState }
                .Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()));
            if (locality.Length > 0) lines.Add(locality);
        }
        else
        {
            var locality = string.Join(", ", new[] { profile.Lga, profile.State }
                .Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()));
            if (locality.Length > 0) lines.Add(locality);
        }

        return lines;
    }

    /// <summary>
    /// One security, as recorded. A bank guarantee is described as covering the facility only when its
    /// amount does; otherwise the letter states the share it covers. It used to say every guarantee
    /// covered "the principal and accrued interest" — printed beside a ₦60,000 guarantee on a
    /// ₦12.9m facility (CRMSQA#2), a contractual claim the security cannot support.
    /// </summary>
    private static string DescribeSecurity(RhshfCollateral collateral, decimal approvedAmount) => collateral.Type switch
    {
        RhshfCollateralType.BankGuarantee =>
            $"Bank guarantee{Named(collateral.GuarantorBankName)}{Valued(collateral.GuaranteeAmount)}"
            + GuaranteeCoverage(collateral.GuaranteeAmount, approvedAmount) + ".",
        RhshfCollateralType.NirsalCrg =>
            $"NIRSAL Credit Risk Guarantee{Covering(collateral.CrgCoveragePercentage)}"
            + $"{Referenced(collateral.ReferenceNumber)}.",
        RhshfCollateralType.LegalMortgage =>
            $"Legal mortgage over {collateral.PropertyDescription ?? "the property recorded on this facility"}"
            + $"{Valued(collateral.PropertyValue)}.",
        _ => "Security as recorded on this facility.",
    };

    private static string GuaranteeCoverage(decimal? amount, decimal approvedAmount) =>
        amount is not { } guaranteed || approvedAmount <= 0m ? ""
        : guaranteed >= approvedAmount ? ", covering the principal of the facility"
        : $", covering {guaranteed / approvedAmount * 100m:0.##}% of the facility amount";

    private static string Named(string? name) => string.IsNullOrWhiteSpace(name) ? "" : $" from {name.Trim()}";
    private static string Valued(decimal? amount) => amount is null ? "" : $" for NGN {amount:N2}";
    private static string Covering(decimal? percent) => percent is null ? "" : $" covering {percent:0.##}% of the facility";
    private static string Referenced(string? reference) =>
        string.IsNullOrWhiteSpace(reference) ? "" : $" (reference {reference.Trim()})";

}

/// <summary>
/// The offer document payload, plus anything the caller needs to act on afterwards.
/// <para>
/// The suppressed fees travel with the data rather than being left to a log line: the letter is
/// correct without them, but core banking will still deduct them until the product is fixed, and
/// that is a thing a person has to do.
/// </para>
/// </summary>
public record RhshfOfferDocumentBuild(
    RhshfOfferDocumentData Data,
    IReadOnlyList<string> SuppressedProductFees);

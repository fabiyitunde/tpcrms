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

    public async Task<RhshfOfferDocumentBuild> BuildAsync(
        RhshfCreditProfile profile, decimal approvedAmount, CancellationToken ct)
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
            FarmerCount: profile.FarmerCount,
            PlanRows: BuildPlanRows(profile, approvedAmount),
            PlanIsItemised: profile.HasItemisedFinancing,
            ProductFees: productFees,
            SecurityDescriptions: collateral.Select(DescribeSecurity).ToList(),
            FeesAccount: FeesAccount,
            ValidityDays: OfferValidityDays);

        return new RhshfOfferDocumentBuild(data, suppressed);
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

        var fallback = profile.EopLines
            .Select(l => new RhshfOfferPlanRow(
                Supplier: null, Description: l.Commodity, QuantityDisplay: l.QuantityDisplay,
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

    private static string DescribeSecurity(RhshfCollateral collateral) => collateral.Type switch
    {
        RhshfCollateralType.BankGuarantee =>
            $"Bank guarantee{Named(collateral.GuarantorBankName)}{Valued(collateral.GuaranteeAmount)}, covering the "
            + "principal and accrued interest of the facility.",
        RhshfCollateralType.NirsalCrg =>
            $"NIRSAL Credit Risk Guarantee{Covering(collateral.CrgCoveragePercentage)}"
            + $"{Referenced(collateral.ReferenceNumber)}.",
        RhshfCollateralType.LegalMortgage =>
            $"Legal mortgage over {collateral.PropertyDescription ?? "the property recorded on this facility"}"
            + $"{Valued(collateral.PropertyValue)}.",
        _ => "Security as recorded on this facility.",
    };

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

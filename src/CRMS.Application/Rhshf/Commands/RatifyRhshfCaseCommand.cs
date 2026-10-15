using CRMS.Application.Common;
using CRMS.Application.Rhshf.Interfaces;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>
/// Final Approver's ratification (design doc §3.6, Phase 6). On Ratified, generates the offer
/// document and creates the RhshfOffer for this cycle in the same transaction — external status
/// stays UnderReview throughout (design doc §6 #9); no webhook fires here.
/// </summary>
public record RatifyRhshfCaseCommand(
    string Reference, Guid FinalApproverId, RhshfRatificationOutcome Outcome, decimal? ApprovedAmount,
    string? Notes, RhshfProfilingStage? ReturnToStage) : IRequest<ApplicationResult>;

public class RatifyRhshfCaseHandler : IRequestHandler<RatifyRhshfCaseCommand, ApplicationResult>
{
    private const string BankName = "Bank of Agriculture";
    private const string OfferContainerName = "rhshf-offers";

    /// <summary>Seven days, per the approved template. Shorter than the thirty the old letter
    /// stated — a deliberate change, taken from the document the bank approved.</summary>
    private const int OfferValidityDays = 7;

    /// <summary>Where the borrower pays the up-front charges, as the approved template states it.</summary>
    private static readonly RhshfOfferFeesAccount FeesAccount =
        new("Bank of Agriculture (BOA)", "Renewed Hope Small Holders Fees", "0016840374");

    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfCommitteeReviewRepository _committeeRepo;
    private readonly IRhshfLegalClearanceRepository _legalRepo;
    private readonly IRhshfOfferRepository _offerRepo;
    private readonly IRhshfCollateralRepository _collateralRepo;
    private readonly IRhshfOfferDocumentGenerator _offerDocumentGenerator;
    private readonly IRhshfKfsPdfGenerator _kfsGenerator;
    private readonly ILoanProductRepository _loanProductRepo;
    private readonly IFineractDirectService _fineract;
    private readonly IFileStorageService _fileStorage;
    private readonly IUnitOfWork _uow;

    public RatifyRhshfCaseHandler(
        IRhshfCreditProfileRepository repo,
        IRhshfCommitteeReviewRepository committeeRepo,
        IRhshfLegalClearanceRepository legalRepo,
        IRhshfOfferRepository offerRepo,
        IRhshfCollateralRepository collateralRepo,
        IRhshfOfferDocumentGenerator offerDocumentGenerator,
        IRhshfKfsPdfGenerator kfsGenerator,
        ILoanProductRepository loanProductRepo,
        IFineractDirectService fineract,
        IFileStorageService fileStorage,
        IUnitOfWork uow)
    {
        _repo = repo;
        _committeeRepo = committeeRepo;
        _legalRepo = legalRepo;
        _offerRepo = offerRepo;
        _collateralRepo = collateralRepo;
        _offerDocumentGenerator = offerDocumentGenerator;
        _kfsGenerator = kfsGenerator;
        _loanProductRepo = loanProductRepo;
        _fineract = fineract;
        _fileStorage = fileStorage;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(RatifyRhshfCaseCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        var cycleNumber = profile.CurrentCycleNumber;
        var excludedActorIds = profile.GetCurrentCycleAppraisalAndRiskActorIds().ToList();
        var committeeReview = await _committeeRepo.GetByProfileAndCycleAsync(profile.Id, cycleNumber, ct);
        if (committeeReview is not null)
        {
            excludedActorIds.AddRange(committeeReview.Votes
                .Where(v => v.Vote == RhshfCommitteeVoteChoice.Approve)
                .Select(v => v.UserId));
        }

        // The Legal Officer who cleared the security before circulation is part of the same chain.
        var clearances = await _legalRepo.GetByProfileAndCycleAsync(profile.Id, cycleNumber, ct);
        excludedActorIds.AddRange(clearances.Select(c => c.LegalOfficerId));

        var result = profile.Ratify(request.FinalApproverId, request.Outcome, request.ApprovedAmount, request.Notes, request.ReturnToStage, excludedActorIds);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        if (request.Outcome == RhshfRatificationOutcome.Ratified)
        {
            // One document now, not two. The Key Facts Statement is folded in as a panel, so the
            // FAC signs and returns a single file — and RhshfOffer.AttachKfs is deliberately NOT
            // called, which is what makes HasRequiredSignedDocuments ask for one signature instead
            // of two. Offers issued before this change keep their KFS and still require both.
            var pdfBytes = await _offerDocumentGenerator.GenerateAsync(
                await BuildOfferDocumentDataAsync(profile, request.ApprovedAmount!.Value, ct), ct);

            var storagePath = await _fileStorage.UploadAsync(
                OfferContainerName, $"{profile.Reference}/offer-cycle{cycleNumber}.pdf", pdfBytes, "application/pdf", ct);

            var offerResult = RhshfOffer.Create(profile.Id, cycleNumber, storagePath);
            if (offerResult.IsFailure)
                return ApplicationResult.Failure(offerResult.Error);

            await _offerRepo.AddAsync(offerResult.Value, ct);
        }

        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }

    /// <summary>
    /// Assembles the merged offer document. Internal and static-friendly so the regenerate path
    /// produces a byte-identical document — a reissued offer that differs from the original would be
    /// a second set of terms for the same facility.
    /// </summary>
    internal async Task<RhshfOfferDocumentData> BuildOfferDocumentDataAsync(
        RhshfCreditProfile profile, decimal approvedAmount, CancellationToken ct)
    {
        var appraisal = profile.GetCurrentCycleFinancialAppraisal();
        var collateral = await _collateralRepo.GetByProfileAndCycleAsync(profile.Id, profile.CurrentCycleNumber, ct);
        var productFees = await ResolveProductFeesAsync(ct);

        return new RhshfOfferDocumentData(
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
            // Not modelled yet: the bank has still to settle whether the grace period comes from the
            // core banking product or is set per case. Null leaves it unstated rather than printing
            // a number nobody has approved.
            GraceMonths: null,
            AmountDueAtHarvest: appraisal?.AmountDueAtHarvest,
            InterestCharge: appraisal?.InterestCharge,
            FarmerCount: profile.FarmerCount,
            PlanRows: BuildPlanRows(profile, approvedAmount),
            PlanIsItemised: profile.HasItemisedFinancing,
            ProductFees: productFees,
            SecurityDescriptions: collateral.Select(DescribeSecurity).ToList(),
            FeesAccount: FeesAccount,
            ValidityDays: OfferValidityDays);
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
    /// </summary>
    private async Task<List<RhshfOfferFee>> ResolveProductFeesAsync(CancellationToken ct)
    {
        var product = await _loanProductRepo.GetActiveRhshfProductAsync(ct);
        if (product?.FineractProductId is null or <= 0)
            return [];

        var catalogue = await _fineract.GetLoanProductsAsync(activeOnly: true, ct);
        if (catalogue.IsFailure)
            return [];

        var match = catalogue.Value.FirstOrDefault(p => p.Id == product.FineractProductId);
        return match is null
            ? []
            : match.ProductCharges.Select(c => new RhshfOfferFee(c.Name, c.Display)).ToList();
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

    private async Task<string> GenerateAndStoreKfsAsync(
        Domain.Aggregates.Rhshf.RhshfCreditProfile profile, decimal approvedAmount, int cycleNumber, CancellationToken ct)
    {
        var appraisal = profile.GetCurrentCycleFinancialAppraisal();
        var kfsBytes = await _kfsGenerator.GenerateAsync(BuildKfsData(profile, approvedAmount, appraisal), ct);
        return await _fileStorage.UploadAsync(
            OfferContainerName, $"{profile.Reference}/kfs-cycle{cycleNumber}.pdf", kfsBytes, "application/pdf", ct);
    }

    /// <summary>Builds the KFS payload — the applied-for input package plus facility facts — shared so
    /// the ratify and regenerate paths produce an identical statement.</summary>
    internal static RhshfKfsData BuildKfsData(
        Domain.Aggregates.Rhshf.RhshfCreditProfile profile, decimal approvedAmount,
        Domain.Aggregates.Rhshf.RhshfFinancialAppraisalReport? appraisal)
        => new(
            Reference: profile.Reference,
            CompanyName: profile.CompanyName,
            RcNumber: profile.RcNumber,
            ProgrammeName: profile.ProgrammeName,
            SessionName: profile.SessionName,
            ApprovedAmount: approvedAmount,
            Currency: profile.Currency,
            GeneratedDate: DateTime.UtcNow,
            BankName: BankName,
            InterestRatePercent: appraisal?.InterestRatePercent,
            CycleMonths: appraisal?.CycleMonths,
            AmountDueAtHarvest: appraisal?.AmountDueAtHarvest,
            AppliedForLines: profile.EopLines
                .Select(l => new RhshfKfsEopLine(l.Commodity, l.QuantityKg, l.UnitPricePerKg, l.LineValue, l.UnitOfMeasurement))
                .ToList(),
            FarmerCount: profile.FarmerCount,
            State: profile.State,
            Lga: profile.Lga,
            FinancedInputCost: appraisal?.FinancedInputCost,
            InterestCharge: appraisal?.InterestCharge);
}

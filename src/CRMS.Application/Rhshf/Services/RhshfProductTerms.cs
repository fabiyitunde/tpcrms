using CRMS.Domain.Common;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Services;

/// <summary>
/// The RH-SHF facility's tenor, rate and interest method as the core banking product defines them.
/// One source for the appraisal, the offer letter and the booking, so the letter the FAC signs
/// describes the loan core banking actually books. Grandbai's version 4 said six months (the
/// officer's cycle length) while the product books nine — that is what this exists to prevent.
/// </summary>
public record RhshfProductTerms(
    int ProductId,
    string ProductName,
    decimal AnnualRatePercent,
    int TenorMonths,
    int NumberOfRepayments,
    string InterestType,
    IReadOnlyList<FineractLoanProductCharge> Charges)
{
    /// <summary>
    /// What a loan of <paramref name="principal"/> costs in interest over the full tenor, as an
    /// estimate. Core banking charges daily on the outstanding principal from the actual disbursement
    /// date, counting actual days, so the final figure moves by a few days' interest either way.
    /// </summary>
    public decimal EstimatedInterest(decimal principal)
        => Math.Round(principal * AnnualRatePercent / 100m * TenorMonths / 12m, 2, MidpointRounding.AwayFromZero);
}

public class RhshfProductTermsResolver
{
    private readonly ILoanProductRepository _loanProductRepo;
    private readonly IFineractDirectService _fineract;

    public RhshfProductTermsResolver(ILoanProductRepository loanProductRepo, IFineractDirectService fineract)
    {
        _loanProductRepo = loanProductRepo;
        _fineract = fineract;
    }

    /// <summary>
    /// The active RH-SHF product's terms, read live from core banking. Fails — never guesses — when
    /// no product is configured, core banking does not answer, or the product's repayment period is
    /// not counted in months.
    /// </summary>
    public async Task<Result<RhshfProductTerms>> ResolveAsync(CancellationToken ct = default)
    {
        var product = await _loanProductRepo.GetActiveRhshfProductAsync(ct);
        if (product?.FineractProductId is not { } productId || productId <= 0)
            return Result.Failure<RhshfProductTerms>(
                "No active RH-SHF loan product is linked to core banking. Set it up in Admin > Products first.");

        var catalogue = await _fineract.GetLoanProductsAsync(activeOnly: false, ct);
        if (catalogue.IsFailure)
            return Result.Failure<RhshfProductTerms>(
                $"Could not read the RH-SHF product from core banking: {catalogue.Error}");

        var match = catalogue.Value.FirstOrDefault(p => p.Id == productId);
        if (match is null)
            return Result.Failure<RhshfProductTerms>("The linked RH-SHF product was not found in core banking.");

        return Build(match);
    }

    /// <summary>Public so a caller holding the catalogue already can reuse it without a second call.</summary>
    public static Result<RhshfProductTerms> Build(FineractLoanProduct product)
    {
        if (!product.RepaymentFrequencyType.StartsWith("Month", StringComparison.OrdinalIgnoreCase))
            return Result.Failure<RhshfProductTerms>(
                $"The core banking product repays every {product.RepaymentEvery} {product.RepaymentFrequencyType}; "
                + "the CRMS expects a tenor counted in months.");
        if (product.DefaultNumberOfRepayments <= 0 || product.RepaymentEvery <= 0)
            return Result.Failure<RhshfProductTerms>("The core banking product has no repayment schedule set.");

        return Result.Success(new RhshfProductTerms(
            product.Id,
            product.Name,
            product.AnnualInterestRate,
            product.DefaultNumberOfRepayments * product.RepaymentEvery,
            product.DefaultNumberOfRepayments,
            product.InterestType,
            product.ProductCharges));
    }
}

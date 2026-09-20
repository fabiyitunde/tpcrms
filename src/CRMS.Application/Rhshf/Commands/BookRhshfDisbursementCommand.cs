using CRMS.Application.Common;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>
/// Disbursement Officer's booking (design doc §3.6, Phase 9) — sixth and final stage. Resolves the
/// admin-configured RH-SHF Fineract product (own LoanProductType segment, mirroring NAMP's) and
/// books via the same generic IFineractDirectService NAMP uses, called independently — not through
/// NAMP's own booking command/handler classes. Interest rate and tenor are always pulled live from
/// the Fineract product at booking time, never entered manually here, so the figures actually
/// booked can never drift from what core banking has configured (no "approved rate" captured
/// earlier in this pipeline, unlike NAMP's ratification-time capture). Loan proceeds go to a
/// supplier/vendor account (DisburseToSavings=false) rather than the FAC's own savings — the FAC's
/// BoaAccountNumber is only ever used for repayment collection.
/// </summary>
public record BookRhshfDisbursementCommand(
    string Reference, Guid DisbursementOfficerId, string SupplierAccountNumber, string? SupplierName) : IRequest<ApplicationResult>;

public class BookRhshfDisbursementHandler : IRequestHandler<BookRhshfDisbursementCommand, ApplicationResult>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly ILoanProductRepository _productRepo;
    private readonly IFineractDirectService _fineract;
    private readonly IUnitOfWork _uow;

    public BookRhshfDisbursementHandler(
        IRhshfCreditProfileRepository repo, ILoanProductRepository productRepo, IFineractDirectService fineract, IUnitOfWork uow)
    {
        _repo = repo;
        _productRepo = productRepo;
        _fineract = fineract;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(BookRhshfDisbursementCommand request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.SupplierAccountNumber))
            return ApplicationResult.Failure("The input-supplier account number is required to book a disbursement.");

        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");
        if (profile.InternalStage != RhshfInternalStage.Disbursement)
            return ApplicationResult.Failure("Case is not at the disbursement stage.");
        if (profile.ApprovedAmount is null or <= 0)
            return ApplicationResult.Failure("This case has no ratified approved amount to disburse.");

        var product = await _productRepo.GetActiveRhshfProductAsync(ct);
        if (product?.FineractProductId is null || product.FineractProductId <= 0)
            return ApplicationResult.Failure(
                "No active RH-SHF loan product with a linked Fineract product is configured. Contact an administrator.");

        var accountResult = await _fineract.GetNampBoaAccountAsync(profile.BoaAccountNumber, ct);
        if (accountResult.IsFailure)
            return await RecordAndSaveAsync(profile, request, null, null, $"Could not resolve BOA account: {accountResult.Error}", ct);

        var productsResult = await _fineract.GetLoanProductsAsync(activeOnly: false, ct);
        if (productsResult.IsFailure)
            return await RecordAndSaveAsync(profile, request, null, null, $"Could not fetch Fineract product catalogue: {productsResult.Error}", ct);

        var fineractProduct = productsResult.Value.FirstOrDefault(p => p.Id == product.FineractProductId);
        if (fineractProduct is null)
            return await RecordAndSaveAsync(profile, request, null, null, $"Fineract product {product.FineractProductId} not found in catalogue.", ct);

        var bookingRequest = new FineractLoanBookingRequest(
            ClientId: accountResult.Value.ClientId,
            ProductId: product.FineractProductId.Value,
            Principal: profile.ApprovedAmount.Value,
            TenorMonths: fineractProduct.DefaultNumberOfRepayments,
            InterestRatePerAnnum: fineractProduct.AnnualInterestRate,
            ValueDate: DateTime.UtcNow,
            RepaymentAccountNumber: profile.BoaAccountNumber,
            DisburseToSavings: false);

        var bookingResult = await _fineract.BookApprovedLoanAsync(bookingRequest, ct);
        if (bookingResult.IsFailure)
            return await RecordAndSaveAsync(profile, request, null, null, bookingResult.Error, ct);

        if (!bookingResult.Value.Booked)
            return await RecordAndSaveAsync(profile, request, null, null, $"Booking did not complete (status: {bookingResult.Value.Status}).", ct);

        return await RecordAndSaveAsync(
            profile, request, bookingResult.Value.LoanId, bookingResult.Value.LoanAccountNumber, null, ct);
    }

    private async Task<ApplicationResult> RecordAndSaveAsync(
        RhshfCreditProfile profile, BookRhshfDisbursementCommand request,
        long? fineractLoanId, string? fineractLoanAccountNumber, string? failureReason, CancellationToken ct)
    {
        var status = failureReason is null ? RhshfDisbursementStatus.Booked : RhshfDisbursementStatus.Failed;
        var result = profile.RecordDisbursementAttempt(
            request.DisbursementOfficerId, profile.ApprovedAmount!.Value, request.SupplierAccountNumber, request.SupplierName,
            status, fineractLoanId, fineractLoanAccountNumber, failureReason);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        await _uow.SaveChangesAsync(ct);
        return status == RhshfDisbursementStatus.Booked
            ? ApplicationResult.Success()
            : ApplicationResult.Failure(failureReason!);
    }
}

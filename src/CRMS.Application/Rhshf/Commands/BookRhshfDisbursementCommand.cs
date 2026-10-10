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
                "No active RH-SHF loan product with a linked Core Banking product is configured. Contact an administrator.");

        var accountResult = await _fineract.GetNampBoaAccountAsync(profile.BoaAccountNumber, ct);
        if (accountResult.IsFailure)
            return await RecordAndSaveAsync(profile, request, null, null, $"Could not resolve BOA account: {accountResult.Error}", ct);

        var productsResult = await _fineract.GetLoanProductsAsync(activeOnly: false, ct);
        if (productsResult.IsFailure)
            return await RecordAndSaveAsync(profile, request, null, null, $"Could not fetch the Core Banking product catalogue: {productsResult.Error}", ct);

        var fineractProduct = productsResult.Value.FirstOrDefault(p => p.Id == product.FineractProductId);
        if (fineractProduct is null)
            return await RecordAndSaveAsync(profile, request, null, null, $"The configured Core Banking product was not found in the catalogue.", ct);

        var bookingRequest = new FineractLoanBookingRequest(
            ClientId: accountResult.Value.ClientId,
            ProductId: product.FineractProductId.Value,
            Principal: profile.ApprovedAmount.Value,
            TenorMonths: fineractProduct.DefaultNumberOfRepayments,
            InterestRatePerAnnum: fineractProduct.AnnualInterestRate,
            ValueDate: DateTime.UtcNow,
            RepaymentAccountNumber: profile.BoaAccountNumber,
            DisburseToSavings: false,
            // One loan per cycle, so the case reference alone would collide on a second cycle.
            ExternalId: BuildExternalId(profile));

        var bookingResult = await _fineract.BookApprovedLoanAsync(bookingRequest, ct);
        if (bookingResult.IsFailure)
            return await RecordAndSaveAsync(profile, request, null, null, bookingResult.Error, ct);

        var booking = bookingResult.Value;
        if (!booking.Booked)
            return await RecordAndSaveAsync(profile, request, null, null, $"Booking did not complete (status: {booking.Status}).", ct);

        // Core banking can create (and even approve) the loan and still fail to disburse it. That is a
        // failed disbursement: the money has not moved, so the case must not be recorded as disbursed.
        // The loan id is kept on the failed attempt, and a retry resumes that same loan via its externalId.
        if (!booking.Disbursed)
            return await RecordAndSaveAsync(
                profile, request, booking.LoanId, null,
                $"Core Banking created the loan (id {booking.LoanId}) but did not disburse it (status: {booking.Status}). " +
                "No funds have moved. Check Core Banking, then retry — the existing loan will be resumed, not duplicated.",
                ct);

        return await RecordAndSaveAsync(
            profile, request, booking.LoanId, booking.LoanAccountNumber, null, ct);
    }

    /// <summary>The stable, unique reference core banking stores on the loan as its externalId. A case
    /// books one loan per review cycle, so the cycle number is part of the identity.</summary>
    private static string BuildExternalId(RhshfCreditProfile profile) =>
        $"{profile.Reference}-C{profile.CurrentCycleNumber}";

    private async Task<ApplicationResult> RecordAndSaveAsync(
        RhshfCreditProfile profile, BookRhshfDisbursementCommand request,
        long? fineractLoanId, string? fineractLoanAccountNumber, string? failureReason, CancellationToken ct)
    {
        // Never surface the core-banking vendor name to officers — sanitise raw service errors too.
        failureReason = Sanitize(failureReason);
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

    /// <summary>Replaces the core-banking vendor name with the neutral "Core Banking" in any message that
    /// could reach an officer (raw service errors can carry it).</summary>
    private static string? Sanitize(string? message) => message is null
        ? null
        : System.Text.RegularExpressions.Regex.Replace(message, "fineract", "Core Banking",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
}

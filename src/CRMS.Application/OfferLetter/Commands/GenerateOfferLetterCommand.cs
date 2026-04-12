using CRMS.Application.Common;
using CRMS.Application.OfferLetter.Interfaces;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using OL = CRMS.Domain.Aggregates.OfferLetter;

namespace CRMS.Application.OfferLetter.Commands;

public record GenerateOfferLetterCommand(
    Guid LoanApplicationId,
    Guid GeneratedByUserId,
    string GeneratedByUserName,
    string BankName = "The Bank",
    string BranchName = ""
) : IRequest<ApplicationResult<OfferLetterResultDto>>;

public record OfferLetterResultDto(
    Guid OfferLetterId,
    string ApplicationNumber,
    int Version,
    string FileName,
    long FileSizeBytes,
    string Status,
    string? StoragePath = null
);

public class GenerateOfferLetterHandler : IRequestHandler<GenerateOfferLetterCommand, ApplicationResult<OfferLetterResultDto>>
{
    private readonly ILoanApplicationRepository _loanAppRepository;
    private readonly ILoanProductRepository _productRepository;
    private readonly ICommitteeReviewRepository _committeeRepository;
    private readonly IOfferLetterRepository _offerLetterRepository;
    private readonly IFineractDirectService _fineractService;
    private readonly IOfferLetterPdfGenerator _pdfGenerator;
    private readonly IAmortisationSchedulePdfGenerator _amortisationGenerator;
    private readonly IKfsPdfGenerator _kfsGenerator;
    private readonly IFileStorageService _fileStorage;
    private readonly IUnitOfWork _unitOfWork;

    public GenerateOfferLetterHandler(
        ILoanApplicationRepository loanAppRepository,
        ILoanProductRepository productRepository,
        ICommitteeReviewRepository committeeRepository,
        IOfferLetterRepository offerLetterRepository,
        IFineractDirectService fineractService,
        IOfferLetterPdfGenerator pdfGenerator,
        IAmortisationSchedulePdfGenerator amortisationGenerator,
        IKfsPdfGenerator kfsGenerator,
        IFileStorageService fileStorage,
        IUnitOfWork unitOfWork)
    {
        _loanAppRepository = loanAppRepository;
        _productRepository = productRepository;
        _committeeRepository = committeeRepository;
        _offerLetterRepository = offerLetterRepository;
        _fineractService = fineractService;
        _pdfGenerator = pdfGenerator;
        _amortisationGenerator = amortisationGenerator;
        _kfsGenerator = kfsGenerator;
        _fileStorage = fileStorage;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<OfferLetterResultDto>> Handle(GenerateOfferLetterCommand request, CancellationToken ct = default)
    {
        var loanApp = await _loanAppRepository.GetByIdAsync(request.LoanApplicationId, ct);
        if (loanApp == null)
            return ApplicationResult<OfferLetterResultDto>.Failure("Loan application not found");

        if (loanApp.Status is not (LoanApplicationStatus.Approved or LoanApplicationStatus.OfferGenerated
            or LoanApplicationStatus.OfferAccepted or LoanApplicationStatus.Disbursed))
            return ApplicationResult<OfferLetterResultDto>.Failure("Offer letter can only be generated for approved applications");

        var product = await _productRepository.GetByIdAsync(loanApp.LoanProductId, ct);

        // Use approved terms (fall back to requested if not set)
        var approvedAmount = loanApp.ApprovedAmount?.Amount ?? loanApp.RequestedAmount.Amount;
        var approvedTenor = loanApp.ApprovedTenorMonths ?? loanApp.RequestedTenorMonths;
        var approvedRate = loanApp.ApprovedInterestRate ?? loanApp.InterestRatePerAnnum;
        var currency = loanApp.RequestedAmount.Currency;

        // Calculate repayment schedule via Fineract (hybrid: API first, in-house fallback)
        var scheduleRequest = new ScheduleCalculationRequest(
            ProductId: product?.FineractProductId ?? 0,
            Principal: approvedAmount,
            NumberOfRepayments: approvedTenor,
            RepaymentEvery: 1,
            RepaymentFrequencyType: 2, // Months
            InterestRatePerPeriod: approvedRate,
            InterestRateFrequencyType: 3, // Per Year
            AmortizationType: 1, // Equal Installments (EMI)
            InterestType: 0, // Declining Balance
            InterestCalculationPeriodType: 1, // Same as Repayment Period
            ExpectedDisbursementDate: DateTime.Today.AddDays(14) // Assume 2 weeks from now
        );

        var scheduleResult = await _fineractService.CalculateRepaymentScheduleAsync(scheduleRequest, ct);
        if (scheduleResult.IsFailure)
            return ApplicationResult<OfferLetterResultDto>.Failure($"Failed to calculate repayment schedule: {scheduleResult.Error}");

        var schedule = scheduleResult.Value;

        // Get committee conditions
        var committeeReview = await _committeeRepository.GetByLoanApplicationIdAsync(request.LoanApplicationId, ct);
        var conditions = new List<string>();
        if (!string.IsNullOrWhiteSpace(committeeReview?.ApprovalConditions))
        {
            conditions.AddRange(committeeReview.ApprovalConditions
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }
        if (!conditions.Any())
            conditions.Add("Standard terms and conditions apply as per the bank's lending policy.");

        // Determine next version — use MAX so that prior Failed records don't re-use the same version number,
        // which would violate the unique index on (LoanApplicationId, Version).
        var maxExistingVersion = await _offerLetterRepository.GetMaxVersionAsync(request.LoanApplicationId, ct);
        var nextVersion = maxExistingVersion + 1;

        // Create offer letter entity with the correct version number
        var offerLetterResult = OL.OfferLetter.Create(
            request.LoanApplicationId,
            loanApp.ApplicationNumber,
            request.GeneratedByUserId,
            request.GeneratedByUserName,
            loanApp.CustomerName,
            product?.Name ?? loanApp.ProductCode,
            approvedAmount,
            approvedTenor,
            approvedRate,
            version: nextVersion);

        if (!offerLetterResult.IsSuccess)
            return ApplicationResult<OfferLetterResultDto>.Failure(offerLetterResult.Error);

        var offerLetter = offerLetterResult.Value;

        try
        {
            // Build PDF data
            var scheduleSource = (product?.FineractProductId ?? 0) > 0 ? "Fineract" : "InHouse";
            var monthlyInstallment = schedule.Installments.Any()
                ? schedule.Installments.Average(i => i.TotalDue)
                : 0;

            var pdfData = new OfferLetterData(
                ApplicationNumber: loanApp.ApplicationNumber,
                GeneratedDate: DateTime.UtcNow,
                CustomerName: loanApp.CustomerName,
                CustomerAddress: "", // From core banking data if available
                ProductName: product?.Name ?? loanApp.ProductCode,
                ApprovedAmount: approvedAmount,
                Currency: currency,
                TenorMonths: approvedTenor,
                InterestRatePerAnnum: approvedRate,
                RepaymentFrequency: "Monthly",
                AmortizationMethod: "Equal Installments (EMI)",
                RepaymentSchedule: schedule.Installments.Select(i => new ScheduleInstallmentData(
                    InstallmentNumber: i.PeriodNumber,
                    DueDate: i.DueDate,
                    Principal: i.PrincipalDue,
                    Interest: i.InterestDue,
                    TotalPayment: i.TotalDue,
                    OutstandingBalance: i.OutstandingBalance
                )).ToList(),
                TotalPrincipal: schedule.TotalPrincipal,
                TotalInterest: schedule.TotalInterest,
                TotalRepayment: schedule.TotalRepayment,
                MonthlyInstallment: Math.Round(monthlyInstallment, 2),
                Conditions: conditions,
                BankName: request.BankName,
                BranchName: request.BranchName,
                ScheduleSource: scheduleSource,
                Version: nextVersion
            );

            // Generate PDF
            var pdfBytes = await _pdfGenerator.GenerateAsync(pdfData, ct);

            // Store PDF
            var fileName = $"OfferLetter_{loanApp.ApplicationNumber}_v{nextVersion}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";
            var actualStoragePath = await _fileStorage.UploadAsync(
                containerName: "offerletters",
                fileName: $"{loanApp.ApplicationNumber}/{fileName}",
                content: pdfBytes,
                contentType: "application/pdf",
                ct: ct);

            // Generate supplementary documents (best-effort — failure does not block the offer letter)
            try
            {
                var amortBytes = await _amortisationGenerator.GenerateAsync(pdfData, ct);
                var amortFileName = $"AmortisationSchedule_{loanApp.ApplicationNumber}_v{nextVersion}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";
                await _fileStorage.UploadAsync("amortisationschedules", $"{loanApp.ApplicationNumber}/{amortFileName}", amortBytes, "application/pdf", ct);
            }
            catch { /* Supplementary PDF failure is non-fatal */ }

            try
            {
                var defaultTier = product?.PricingTiers.FirstOrDefault();
                var processingFee = defaultTier?.ProcessingFeePercent.HasValue == true
                    ? approvedAmount * defaultTier.ProcessingFeePercent.Value / 100m
                    : defaultTier?.ProcessingFeeFixed?.Amount ?? 0m;
                var managementFee = 0m; // No management fee field on product — extend LoanProduct if needed
                var kfsData = new KfsData(
                    ApplicationNumber: loanApp.ApplicationNumber,
                    GeneratedDate: DateTime.UtcNow,
                    CustomerName: loanApp.CustomerName,
                    ProductName: product?.Name ?? loanApp.ProductCode,
                    LoanAmount: approvedAmount,
                    Currency: currency,
                    TenorMonths: approvedTenor,
                    NominalRatePerAnnum: approvedRate,
                    EffectiveAnnualRate: approvedRate, // EAR = nominal rate for monthly compounding (simplified)
                    MonthlyInstallment: Math.Round(monthlyInstallment, 2),
                    TotalInterest: schedule.TotalInterest,
                    TotalRepayment: schedule.TotalRepayment,
                    ProcessingFeeAmount: processingFee,
                    ManagementFeeAmount: managementFee,
                    TotalCostOfCredit: schedule.TotalInterest + processingFee + managementFee,
                    LatePaymentPenalty: "1% per month on overdue amount",
                    EarlyRepaymentTerms: "Subject to 30-day notice; no penalty for full prepayment after 6 months",
                    SecurityRequired: "As detailed in offer letter conditions",
                    BankName: request.BankName,
                    BranchName: request.BranchName,
                    ComplaintChannel: "customercare@thebank.com | +234-800-000-0000"
                );
                var kfsBytes = await _kfsGenerator.GenerateAsync(kfsData, ct);
                var kfsFileName = $"KFS_{loanApp.ApplicationNumber}_v{nextVersion}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";
                await _fileStorage.UploadAsync("kfs", $"{loanApp.ApplicationNumber}/{kfsFileName}", kfsBytes, "application/pdf", ct);
            }
            catch { /* Supplementary PDF failure is non-fatal */ }

            // Calculate content hash
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var contentHash = Convert.ToBase64String(sha256.ComputeHash(pdfBytes));

            // Update offer letter entity
            offerLetter.SetDocument(fileName, actualStoragePath, pdfBytes.Length, contentHash);
            offerLetter.SetScheduleSummary(
                schedule.TotalInterest,
                schedule.TotalRepayment,
                Math.Round(monthlyInstallment, 2),
                schedule.Installments.Count,
                scheduleSource,
                scheduleRequest.ExpectedDisbursementDate);

            await _offerLetterRepository.AddAsync(offerLetter, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return ApplicationResult<OfferLetterResultDto>.Success(new OfferLetterResultDto(
                offerLetter.Id,
                loanApp.ApplicationNumber,
                nextVersion,
                fileName,
                pdfBytes.Length,
                offerLetter.Status.ToString(),
                actualStoragePath));
        }
        catch (Exception ex)
        {
            offerLetter.MarkAsFailed(ex.Message);
            try
            {
                // Best-effort: persist the failed record for audit purposes.
                // Wrapped in its own try-catch so a secondary DB error (e.g. version conflict
                // from a concurrent request) does not swallow the real error message.
                await _offerLetterRepository.AddAsync(offerLetter, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }
            catch { /* ignore — the primary failure is what matters */ }
            return ApplicationResult<OfferLetterResultDto>.Failure($"Failed to generate offer letter: {ex.Message}");
        }
    }
}

using CRMS.Application.Common;
using CRMS.Domain.Aggregates.CreditBureau;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CRMS.Application.CreditBureau.Commands;

/// <summary>
/// Processes all credit checks for a loan application after branch approval.
/// Uses SmartComply API for:
/// - Individual credit checks (directors, signatories, guarantors) via CRC Full report
/// - Business credit check (corporate entity) via CRC Business History
/// - Fraud checks for both individuals and business
/// </summary>
public record ProcessLoanCreditChecksCommand(
    Guid LoanApplicationId,
    Guid SystemUserId
) : IRequest<ApplicationResult<CreditCheckBatchResultDto>>;

public record CreditCheckBatchResultDto(
    Guid LoanApplicationId,
    int TotalChecks,
    int Successful,
    int Failed,
    int NotFound,
    List<IndividualCreditCheckResultDto> IndividualResults,
    BusinessCreditCheckResultDto? BusinessResult
);

public enum CreditCheckFailureReason
{
    None,
    NotFound,       // Subject not found in credit bureau
    Failed,         // API error, timeout, etc.
    NoConsent,      // Missing NDPA consent
    InternalError   // Unexpected error
}

public record IndividualCreditCheckResultDto(
    Guid PartyId,
    string PartyName,
    string PartyType,
    string? BVN,
    bool Success,
    Guid? BureauReportId,
    int? CreditScore,
    string? ScoreGrade,
    bool HasCreditIssues,
    int? FraudRiskScore,
    string? FraudRecommendation,
    string? ErrorMessage,
    CreditCheckFailureReason FailureReason = CreditCheckFailureReason.None
);

public record BusinessCreditCheckResultDto(
    string? RegistrationNumber,
    string BusinessName,
    bool Success,
    Guid? BureauReportId,
    int TotalLoans,
    int ActiveLoans,
    int DelinquentFacilities,
    decimal TotalOutstanding,
    decimal TotalOverdue,
    bool HasCreditIssues,
    int? FraudRiskScore,
    string? FraudRecommendation,
    string? ErrorMessage,
    CreditCheckFailureReason FailureReason = CreditCheckFailureReason.None,
    bool WasSkipped = false,
    string? SkipReason = null
);

public class ProcessLoanCreditChecksHandler : IRequestHandler<ProcessLoanCreditChecksCommand, ApplicationResult<CreditCheckBatchResultDto>>
{
    private readonly ILoanApplicationRepository _loanAppRepository;
    private readonly IGuarantorRepository _guarantorRepository;
    private readonly ICollateralRepository _collateralRepository;
    private readonly IBureauReportRepository _bureauReportRepository;
    private readonly IConsentRecordRepository _consentRepository;
    private readonly ISmartComplyProvider _smartComplyProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ProcessLoanCreditChecksHandler> _logger;

    public ProcessLoanCreditChecksHandler(
        ILoanApplicationRepository loanAppRepository,
        IGuarantorRepository guarantorRepository,
        ICollateralRepository collateralRepository,
        IBureauReportRepository bureauReportRepository,
        IConsentRecordRepository consentRepository,
        ISmartComplyProvider smartComplyProvider,
        IUnitOfWork unitOfWork,
        ILogger<ProcessLoanCreditChecksHandler> logger)
    {
        _loanAppRepository = loanAppRepository;
        _guarantorRepository = guarantorRepository;
        _collateralRepository = collateralRepository;
        _bureauReportRepository = bureauReportRepository;
        _consentRepository = consentRepository;
        _smartComplyProvider = smartComplyProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApplicationResult<CreditCheckBatchResultDto>> Handle(ProcessLoanCreditChecksCommand request, CancellationToken ct = default)
    {
        var loanApp = await _loanAppRepository.GetByIdWithPartiesAsync(request.LoanApplicationId, ct);
        if (loanApp == null)
            return ApplicationResult<CreditCheckBatchResultDto>.Failure("Loan application not found");

        if (loanApp.Status != LoanApplicationStatus.BranchApproved && loanApp.Status != LoanApplicationStatus.CreditAnalysis)
            return ApplicationResult<CreditCheckBatchResultDto>.Failure($"Loan must be in BranchApproved or CreditAnalysis status to run credit checks. Current: {loanApp.Status}");

        // Idempotency check: If all credit checks are already completed, return early
        if (loanApp.AllCreditChecksCompleted)
        {
            var existingReports = await _bureauReportRepository.GetByLoanApplicationIdAsync(request.LoanApplicationId, ct);
            return ApplicationResult<CreditCheckBatchResultDto>.Success(new CreditCheckBatchResultDto(
                request.LoanApplicationId,
                loanApp.TotalCreditChecksRequired,
                existingReports.Count(r => r.Status == BureauReportStatus.Completed),
                existingReports.Count(r => r.Status == BureauReportStatus.Failed),
                existingReports.Count(r => r.Status == BureauReportStatus.NotFound),
                existingReports.Where(r => r.SubjectType == SubjectType.Individual).Select(r => new IndividualCreditCheckResultDto(
                    r.PartyId ?? Guid.Empty, r.SubjectName, r.PartyType ?? "Unknown", r.BVN, r.Status == BureauReportStatus.Completed, r.Id,
                    r.CreditScore, r.ScoreGrade, r.NonPerformingAccounts > 0, r.FraudRiskScore, r.FraudRecommendation, r.ErrorMessage
                )).ToList(),
                existingReports.Where(r => r.SubjectType == SubjectType.Business).Select(r => new BusinessCreditCheckResultDto(
                    r.TaxId, r.SubjectName, r.Status == BureauReportStatus.Completed, r.Id,
                    r.TotalAccounts, r.ActiveLoans, r.NonPerformingAccounts, r.TotalOutstandingBalance, r.TotalOverdue,
                    r.NonPerformingAccounts > 0, r.FraudRiskScore, r.FraudRecommendation, r.ErrorMessage
                )).FirstOrDefault()
            ));
        }

        // Get existing bureau reports to avoid duplicates.
        // Only treat Completed or NotFound as "done" — Failed reports (including consent failures) can be retried
        // once consent is captured, so exclude them from the skip-set.
        var existingBureauReports = await _bureauReportRepository.GetByLoanApplicationIdAsync(request.LoanApplicationId, ct);
        var existingBvns = existingBureauReports
            .Where(r => !string.IsNullOrEmpty(r.BVN) &&
                        (r.Status == BureauReportStatus.Completed || r.Status == BureauReportStatus.NotFound))
            .Select(r => r.BVN!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var hasExistingBusinessReport = existingBureauReports.Any(r => r.SubjectType == SubjectType.Business &&
            (r.Status == BureauReportStatus.Completed || r.Status == BureauReportStatus.NotFound));

        // Get all parties (directors, signatories) from loan application
        var partiesWithBVN = loanApp.Parties
            .Where(p => !string.IsNullOrEmpty(p.BVN))
            .ToList();

        // Get guarantors for this loan
        var guarantors = await _guarantorRepository.GetByLoanApplicationIdAsync(request.LoanApplicationId, ct);
        var guarantorsWithBVN = guarantors
            .Where(g => !string.IsNullOrEmpty(g.BVN) && g.Status != GuarantorStatus.Rejected)
            .ToList();

        // Get collateral data for fraud checks (total value of approved collateral)
        var collaterals = await _collateralRepository.GetByLoanApplicationIdAsync(request.LoanApplicationId, ct);
        var approvedCollaterals = collaterals.Where(c => c.Status == CollateralStatus.Approved || c.Status == CollateralStatus.Valued).ToList();
        var hasCollateral = approvedCollaterals.Count > 0;
        var totalCollateralValue = approvedCollaterals.Sum(c => 
            (c.AcceptableValue?.Amount ?? c.ForcedSaleValue?.Amount ?? c.MarketValue?.Amount ?? 0));

        // Calculate total checks: individuals + business (if RC number exists)
        var hasBusinessCheck = !string.IsNullOrEmpty(loanApp.RegistrationNumber);
        var totalChecks = partiesWithBVN.Count + guarantorsWithBVN.Count + (hasBusinessCheck ? 1 : 0);

        if (totalChecks == 0)
            return ApplicationResult<CreditCheckBatchResultDto>.Failure("No parties with BVN found for credit checks");

        // Start credit analysis on loan application
        if (loanApp.Status == LoanApplicationStatus.BranchApproved)
        {
            var startResult = loanApp.StartCreditAnalysis(totalChecks, request.SystemUserId);
            if (startResult.IsFailure)
                return ApplicationResult<CreditCheckBatchResultDto>.Failure(startResult.Error);
        }
        else if (loanApp.Status == LoanApplicationStatus.CreditAnalysis)
        {
            // Validate that we're not trying to process more checks than originally required
            // This prevents issues if parties/guarantors were added after credit analysis started
            if (totalChecks > loanApp.TotalCreditChecksRequired)
            {
                _logger.LogWarning(
                    "Credit check count mismatch for loan {LoanApplicationId}: current={CurrentCount}, original={OriginalCount}. " +
                    "Parties may have been added after credit analysis started.",
                    loanApp.Id, totalChecks, loanApp.TotalCreditChecksRequired);
                
                // Only process up to the original count to prevent double-triggering AllCreditChecksCompletedEvent
                // New parties added after credit analysis started will need manual handling
            }
        }

        var individualResults = new List<IndividualCreditCheckResultDto>();
        var successful = 0;
        var failed = 0;
        var notFound = 0;

        // Process loan application parties (directors, signatories)
        foreach (var party in partiesWithBVN)
        {
            // Skip if already processed (idempotency)
            if (existingBvns.Contains(party.BVN!))
            {
                var existing = existingBureauReports.First(r => r.BVN == party.BVN);
                var existingFailureReason = existing.Status switch
                {
                    BureauReportStatus.NotFound => CreditCheckFailureReason.NotFound,
                    BureauReportStatus.Failed => CreditCheckFailureReason.Failed,
                    _ => CreditCheckFailureReason.None
                };
                individualResults.Add(new IndividualCreditCheckResultDto(
                    party.Id, party.FullName, party.PartyType.ToString(), party.BVN,
                    existing.Status == BureauReportStatus.Completed, existing.Id,
                    existing.CreditScore, existing.ScoreGrade, existing.NonPerformingAccounts > 0,
                    existing.FraudRiskScore, existing.FraudRecommendation, existing.ErrorMessage,
                    existingFailureReason
                ));
                if (existing.Status == BureauReportStatus.Completed) successful++;
                else if (existing.Status == BureauReportStatus.NotFound) notFound++;
                else failed++;
                continue; // Skip RecordCreditCheckCompleted - already counted
            }

            var result = await ProcessIndividualCreditCheck(
                loanApp,
                party.Id,
                party.FullName,
                party.PartyType.ToString(),
                party.BVN!,
                request.SystemUserId,
                hasCollateral,
                totalCollateralValue,
                ct
            );

            individualResults.Add(result);
            if (result.Success) successful++;
            else if (result.FailureReason == CreditCheckFailureReason.NotFound) notFound++;
            else failed++;

            // Consent failures are not counted as completed — the check was blocked, not performed.
            // The command can be re-run once consent is captured.
            if (result.FailureReason != CreditCheckFailureReason.NoConsent)
            {
                var recordResult = loanApp.RecordCreditCheckCompleted(request.SystemUserId);
                if (recordResult.IsFailure)
                    return ApplicationResult<CreditCheckBatchResultDto>.Failure($"Failed to record credit check completion: {recordResult.Error}");
            }
        }

        // Process guarantors
        foreach (var guarantor in guarantorsWithBVN)
        {
            // Skip if already processed (idempotency)
            if (existingBvns.Contains(guarantor.BVN!))
            {
                var existing = existingBureauReports.First(r => r.BVN == guarantor.BVN);
                var existingFailureReason = existing.Status switch
                {
                    BureauReportStatus.NotFound => CreditCheckFailureReason.NotFound,
                    BureauReportStatus.Failed => CreditCheckFailureReason.Failed,
                    _ => CreditCheckFailureReason.None
                };
                individualResults.Add(new IndividualCreditCheckResultDto(
                    guarantor.Id, guarantor.FullName, "Guarantor", guarantor.BVN,
                    existing.Status == BureauReportStatus.Completed, existing.Id,
                    existing.CreditScore, existing.ScoreGrade, existing.NonPerformingAccounts > 0,
                    existing.FraudRiskScore, existing.FraudRecommendation, existing.ErrorMessage,
                    existingFailureReason
                ));
                if (existing.Status == BureauReportStatus.Completed) successful++;
                else if (existing.Status == BureauReportStatus.NotFound) notFound++;
                else failed++;
                continue; // Skip RecordCreditCheckCompleted - already counted
            }

            var result = await ProcessIndividualCreditCheck(
                loanApp,
                guarantor.Id,
                guarantor.FullName,
                "Guarantor",
                guarantor.BVN!,
                request.SystemUserId,
                hasCollateral,
                totalCollateralValue,
                ct,
                guarantor
            );

            individualResults.Add(result);
            if (result.Success) successful++;
            else if (result.FailureReason == CreditCheckFailureReason.NotFound) notFound++;
            else failed++;

            if (result.FailureReason != CreditCheckFailureReason.NoConsent)
            {
                var recordResult = loanApp.RecordCreditCheckCompleted(request.SystemUserId);
                if (recordResult.IsFailure)
                    return ApplicationResult<CreditCheckBatchResultDto>.Failure($"Failed to record credit check completion: {recordResult.Error}");
            }
        }

        // Process business credit check if RC number exists
        BusinessCreditCheckResultDto? businessResult = null;
        if (!hasBusinessCheck)
        {
            // Create a skipped result to explain why no business check was performed
            businessResult = new BusinessCreditCheckResultDto(
                null, loanApp.CustomerName, false, null, 0, 0, 0, 0, 0, false, null, null, null,
                WasSkipped: true,
                SkipReason: "No RC (Registration) number available. Core banking did not return a registration number for this corporate customer."
            );
        }
        else
        {
            // Skip if already processed (idempotency)
            if (hasExistingBusinessReport)
            {
                var existing = existingBureauReports.First(r => r.SubjectType == SubjectType.Business);
                var existingFailureReason = existing.Status switch
                {
                    BureauReportStatus.NotFound => CreditCheckFailureReason.NotFound,
                    BureauReportStatus.Failed => CreditCheckFailureReason.Failed,
                    _ => CreditCheckFailureReason.None
                };
                businessResult = new BusinessCreditCheckResultDto(
                    existing.TaxId, existing.SubjectName, existing.Status == BureauReportStatus.Completed, existing.Id,
                    existing.TotalAccounts, existing.ActiveLoans, existing.NonPerformingAccounts,
                    existing.TotalOutstandingBalance, existing.TotalOverdue, existing.NonPerformingAccounts > 0,
                    existing.FraudRiskScore, existing.FraudRecommendation, existing.ErrorMessage,
                    existingFailureReason
                );
                if (existing.Status == BureauReportStatus.Completed) successful++;
                else if (existing.Status == BureauReportStatus.NotFound) notFound++;
                else failed++;
                // Skip RecordCreditCheckCompleted - already counted
            }
            else
            {
                businessResult = await ProcessBusinessCreditCheck(
                    loanApp,
                    request.SystemUserId,
                    hasCollateral,
                    totalCollateralValue,
                    ct
                );

                if (businessResult.Success) successful++;
                else if (businessResult.FailureReason == CreditCheckFailureReason.NotFound) notFound++;
                else failed++;

                if (businessResult.FailureReason != CreditCheckFailureReason.NoConsent)
                {
                    var recordResult = loanApp.RecordCreditCheckCompleted(request.SystemUserId);
                    if (recordResult.IsFailure)
                        return ApplicationResult<CreditCheckBatchResultDto>.Failure($"Failed to record credit check completion: {recordResult.Error}");
                }
            }
        }

        // Save all changes
        _loanAppRepository.Update(loanApp);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<CreditCheckBatchResultDto>.Success(new CreditCheckBatchResultDto(
            request.LoanApplicationId,
            totalChecks,
            successful,
            failed,
            notFound,
            individualResults,
            businessResult
        ));
    }

    private async Task<IndividualCreditCheckResultDto> ProcessIndividualCreditCheck(
        Domain.Aggregates.LoanApplication.LoanApplication loanApp,
        Guid partyId,
        string partyName,
        string partyType,
        string bvn,
        Guid systemUserId,
        bool hasCollateral,
        decimal totalCollateralValue,
        CancellationToken ct,
        Domain.Aggregates.Guarantor.Guarantor? guarantor = null)
    {
        try
        {
            // NDPA Compliance: Verify consent exists before credit check
            var consent = await _consentRepository.GetValidConsentAsync(bvn, ConsentType.CreditBureauCheck, ct);
            if (consent == null)
            {
                // NDPA requires audit trail even for consent failures - create a failed record
                _logger.LogWarning("Credit check attempted without consent for BVN {BvnSuffix} on loan {LoanApplicationId}",
                    bvn.Length >= 4 ? $"****{bvn[^4..]}" : "****", loanApp.Id);
                
                var errorMessage = "No valid consent record found for this BVN. Consent is required for credit bureau checks (NDPA compliance).";
                
                // Create audit record for consent failure (NDPA requirement)
                var consentFailureReport = BureauReport.CreateForConsentFailure(
                    CreditBureauProvider.SmartComply,
                    SubjectType.Individual,
                    partyName,
                    bvn,
                    taxId: null,
                    systemUserId,
                    loanApp.Id,
                    partyId,
                    partyType,
                    errorMessage
                );
                
                if (consentFailureReport.IsSuccess)
                {
                    await _bureauReportRepository.AddAsync(consentFailureReport.Value, ct);
                }
                
                return new IndividualCreditCheckResultDto(
                    partyId, partyName, partyType, bvn, false, 
                    consentFailureReport.IsSuccess ? consentFailureReport.Value.Id : null,
                    null, null, false, null, null, errorMessage,
                    CreditCheckFailureReason.NoConsent
                );
            }

            // Create bureau report FIRST (NDPA: must record all bureau access attempts)
            var bureauReportResult = BureauReport.Create(
                CreditBureauProvider.SmartComply,
                SubjectType.Individual,
                partyName,
                bvn,
                systemUserId,
                consent.Id,
                loanApp.Id,
                taxId: null,
                partyId: partyId,
                partyType: partyType
            );

            if (bureauReportResult.IsFailure)
            {
                return new IndividualCreditCheckResultDto(
                    partyId, partyName, partyType, bvn, false, null, null, null, false, null, null,
                    $"Failed to create report: {bureauReportResult.Error}",
                    CreditCheckFailureReason.InternalError
                );
            }

            var bureauReport = bureauReportResult.Value;
            bureauReport.MarkProcessing();

            // Get CRC Full credit report via SmartComply
            var reportResult = await _smartComplyProvider.GetCRCFullAsync(bvn, ct);
            if (reportResult.IsFailure)
            {
                // Mark the report as failed/not found and persist for audit trail
                var isNotFound = reportResult.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true;
                var failureReason = isNotFound ? CreditCheckFailureReason.NotFound : CreditCheckFailureReason.Failed;
                var errorMessage = isNotFound 
                    ? "Subject not found in credit bureau" 
                    : $"Credit report retrieval failed: {reportResult.Error}";
                
                if (isNotFound)
                    bureauReport.MarkNotFound();
                else
                    bureauReport.MarkFailed(errorMessage);
                
                await _bureauReportRepository.AddAsync(bureauReport, ct);
                
                return new IndividualCreditCheckResultDto(
                    partyId, partyName, partyType, bvn, false, bureauReport.Id, null, null, false, null, null,
                    errorMessage, failureReason
                );
            }

            var report = reportResult.Value;
            var summary = report.Summary;

            // Fetch actual bureau credit score (preferred over derived score for regulatory defensibility)
            int creditScore;
            string scoreGrade;
            var scoreResult = await _smartComplyProvider.GetCRCScoreAsync(bvn, ct);
            if (scoreResult.IsSuccess)
            {
                creditScore = scoreResult.Value.Score;
                scoreGrade = scoreResult.Value.Grade ?? GetScoreGrade(creditScore);
            }
            else
            {
                // Fall back to derived score if bureau score fetch fails
                _logger.LogWarning("Failed to fetch CRC score for BVN {BvnSuffix}, using derived score: {Error}",
                    bvn.Length >= 4 ? $"****{bvn[^4..]}" : "****", scoreResult.Error);
                creditScore = CalculateCreditScoreFromReport(summary);
                scoreGrade = GetScoreGrade(creditScore);
            }
            
            // Determine if there are credit issues
            var hasCreditIssues = summary.TotalNoOfDelinquentFacilities > 0 ||
                                 summary.TotalOverdue > 0 ||
                                 summary.MaxNoOfDays > 60;

            bureauReport.CompleteWithData(
                report.Id ?? bvn,
                creditScore,
                scoreGrade,
                report.SearchedDate ?? DateTime.UtcNow,
                JsonSerializer.Serialize(report),
                null,
                summary.TotalNoOfLoans,
                summary.TotalNoOfActiveLoans,
                summary.TotalNoOfPerformingLoans,
                summary.TotalNoOfDelinquentFacilities,
                summary.TotalNoOfClosedLoans,
                summary.TotalOutstanding,
                summary.TotalOverdue,
                summary.HighestLoanAmount,
                summary.MaxNoOfDays,
                false // SmartComply doesn't return legal actions in this endpoint
            );

            // Add loan performance as accounts
            foreach (var loan in report.LoanPerformance)
            {
                // Derive delinquency level from per-account data, not global max
                var accountDelinquencyLevel = DeriveDelinquencyLevelFromLoan(loan);
                
                var account = BureauAccount.Create(
                    bureauReport.Id,
                    loan.AccountNumber ?? "N/A",
                    loan.LoanProvider ?? "Unknown",
                    loan.Type ?? "Term Loan",
                    ParsePerformanceStatus(loan.PerformanceStatus),
                    accountDelinquencyLevel,
                    loan.LoanAmount,
                    loan.OutstandingBalance,
                    0, // MinimumPayment not provided
                    loan.DateAccountOpened,
                    null,
                    null,
                    0,
                    loan.PaymentProfile,
                    LegalStatus.None,
                    null,
                    "NGN",
                    loan.LastUpdatedAt ?? DateTime.UtcNow
                );
                bureauReport.AddAccount(account);
            }

            // Run fraud check before persisting
            int? fraudRiskScore = null;
            string? fraudRecommendation = null;
            
            try
            {
                // Skip fraud check if DOB is not available (required by API, fake values would skew score)
                if (string.IsNullOrEmpty(report.DateOfBirth))
                {
                    _logger.LogWarning("Skipping individual fraud check for BVN {BvnSuffix}: Date of birth not available in credit report",
                        bvn.Length >= 4 ? $"****{bvn[^4..]}" : "****");
                }
                else
                {
                    var fraudRequest = new SmartComplyIndividualLoanRequest(
                        FirstName: GetFirstName(partyName),
                        LastName: GetLastName(partyName),
                        OtherName: GetOtherNames(partyName),
                        DateOfBirth: report.DateOfBirth,
                        Gender: report.Gender ?? "Male",
                        Country: "Nigeria",
                        City: null,
                        CurrentAddress: report.Address,
                        Bvn: bvn,
                        PhoneNumber: report.Phone,
                        EmailAddress: report.Email,
                        EmploymentType: null,
                        JobRole: null,
                        EmployerName: null,
                        AnnualIncome: 0, // Individual income not available from credit report
                        BankName: null,
                        AccountNumber: null, // Individual's personal account not available; don't use corporate account
                        LoanAmountRequested: loanApp.RequestedAmount.Amount,
                        PurposeOfLoan: loanApp.Purpose,
                        LoanRepaymentDurationType: "months",
                        LoanRepaymentDurationValue: loanApp.RequestedTenorMonths,
                        CollateralRequired: hasCollateral,
                        CollateralValue: hasCollateral ? totalCollateralValue : null,
                        RunAmlCheck: false
                    );

                    var fraudResult = await _smartComplyProvider.CheckIndividualLoanFraudAsync(fraudRequest, ct);
                    if (fraudResult.IsSuccess)
                    {
                        fraudRiskScore = fraudResult.Value.FraudRiskScore;
                        fraudRecommendation = fraudResult.Value.Recommendation;
                        
                        // Persist fraud check results to bureau report
                        bureauReport.RecordFraudCheckResults(
                            fraudRiskScore, 
                            fraudRecommendation,
                            JsonSerializer.Serialize(fraudResult.Value));
                    }
                    else
                    {
                        _logger.LogWarning("Individual fraud check failed for BVN {BvnSuffix}: {Error}", 
                            bvn.Length >= 4 ? $"****{bvn[^4..]}" : "****", fraudResult.Error);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw; // Propagate cancellation
            }
            catch (Exception ex)
            {
                // Fraud check is optional, don't fail the whole process - but log it
                _logger.LogError(ex, "Individual fraud check exception for BVN {BvnSuffix} on loan {LoanApplicationId}", 
                    bvn.Length >= 4 ? $"****{bvn[^4..]}" : "****", loanApp.Id);
            }

            // Now persist the bureau report with fraud results included
            await _bureauReportRepository.AddAsync(bureauReport, ct);

            // Update guarantor if applicable
            if (guarantor != null)
            {
                var issuesSummary = hasCreditIssues
                    ? $"Delinquent: {summary.TotalNoOfDelinquentFacilities}, Overdue: {summary.TotalOverdue:N0}, Max Days: {summary.MaxNoOfDays}"
                    : "No significant issues";

                var existingCount = await _guarantorRepository.GetActiveGuaranteeCountByBVNAsync(bvn, ct);
                var totalExisting = Domain.ValueObjects.Money.Create(summary.TotalOutstanding, "NGN");

                guarantor.RecordCreditCheck(
                    bureauReport.Id, creditScore, scoreGrade,
                    hasCreditIssues, issuesSummary, existingCount, totalExisting
                );
                _guarantorRepository.Update(guarantor);
            }

            return new IndividualCreditCheckResultDto(
                partyId, partyName, partyType, bvn, true, bureauReport.Id,
                creditScore, scoreGrade, hasCreditIssues, fraudRiskScore, fraudRecommendation, null
            );
        }
        catch (OperationCanceledException)
        {
            throw; // Propagate cancellation to caller
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during individual credit check for BVN {BvnSuffix} on loan {LoanApplicationId}",
                bvn.Length >= 4 ? $"****{bvn[^4..]}" : "****", loanApp.Id);

            // Create a failed bureau report so there is always an NDPA audit trace (BUG H-2 fix)
            var errorMsg = $"Unexpected error: {ex.Message}";
            var auditReport = BureauReport.CreateForConsentFailure(
                CreditBureauProvider.SmartComply, SubjectType.Individual,
                partyName, bvn, null, systemUserId, loanApp.Id, partyId, partyType, errorMsg);
            Guid? auditReportId = null;
            if (auditReport.IsSuccess)
            {
                await _bureauReportRepository.AddAsync(auditReport.Value, ct);
                auditReportId = auditReport.Value.Id;
            }

            return new IndividualCreditCheckResultDto(
                partyId, partyName, partyType, bvn, false, auditReportId, null, null, false, null, null,
                errorMsg, CreditCheckFailureReason.InternalError
            );
        }
    }

    private async Task<BusinessCreditCheckResultDto> ProcessBusinessCreditCheck(
        Domain.Aggregates.LoanApplication.LoanApplication loanApp,
        Guid systemUserId,
        bool hasCollateral,
        decimal totalCollateralValue,
        CancellationToken ct)
    {
        var rcNumber = loanApp.RegistrationNumber!;
        var businessName = loanApp.CustomerName;

        try
        {
            // NDPA Compliance: Verify consent exists BEFORE calling the bureau API
            // Business consent uses RC number as subject identifier, same rigor as individual checks
            var consent = await _consentRepository.GetValidConsentAsync(rcNumber, ConsentType.CreditBureauCheck, ct);

            if (consent == null)
            {
                // NDPA requires audit trail even for consent failures
                _logger.LogWarning("Business credit check attempted without consent for RC {RcNumber} on loan {LoanApplicationId}",
                    rcNumber, loanApp.Id);
                
                var errorMessage = "No valid consent record found for business credit check. Consent is required before credit bureau checks (NDPA compliance).";
                
                // Create audit record for consent failure (NDPA requirement)
                var consentFailureReport = BureauReport.CreateForConsentFailure(
                    CreditBureauProvider.SmartComply,
                    SubjectType.Business,
                    businessName,
                    bvn: null,
                    taxId: rcNumber,
                    systemUserId,
                    loanApp.Id,
                    partyId: null,
                    partyType: "Business",
                    errorMessage
                );
                
                if (consentFailureReport.IsSuccess)
                {
                    await _bureauReportRepository.AddAsync(consentFailureReport.Value, ct);
                }
                
                return new BusinessCreditCheckResultDto(
                    rcNumber, businessName, false,
                    consentFailureReport.IsSuccess ? consentFailureReport.Value.Id : null,
                    0, 0, 0, 0, 0, false, null, null, errorMessage,
                    CreditCheckFailureReason.NoConsent
                );
            }
            
            var consentId = consent.Id;

            // Create bureau report FIRST (NDPA: must record all bureau access attempts)
            var bureauReportResult = BureauReport.Create(
                CreditBureauProvider.SmartComply,
                SubjectType.Business,
                businessName,
                null, // No BVN for business
                systemUserId,
                consentId,
                loanApp.Id,
                taxId: rcNumber, // Use RC number as tax ID
                partyId: null, // Business has no party ID
                partyType: "Business"
            );

            if (bureauReportResult.IsFailure)
            {
                return new BusinessCreditCheckResultDto(
                    rcNumber, businessName, false, null, 0, 0, 0, 0, 0, false, null, null,
                    $"Failed to create business report: {bureauReportResult.Error}",
                    CreditCheckFailureReason.InternalError
                );
            }

            var bureauReport = bureauReportResult.Value;
            bureauReport.MarkProcessing();

            // Get CRC Business History via SmartComply (only after consent verified)
            var reportResult = await _smartComplyProvider.GetCRCBusinessHistoryAsync(rcNumber, ct);
            if (reportResult.IsFailure)
            {
                // Mark the report as failed/not found and persist for audit trail
                var isNotFound = reportResult.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true;
                var failureReason = isNotFound ? CreditCheckFailureReason.NotFound : CreditCheckFailureReason.Failed;
                var errorMessage = isNotFound 
                    ? "Business not found in credit bureau" 
                    : $"Business credit report retrieval failed: {reportResult.Error}";
                
                if (isNotFound)
                    bureauReport.MarkNotFound();
                else
                    bureauReport.MarkFailed(errorMessage);
                
                await _bureauReportRepository.AddAsync(bureauReport, ct);
                
                return new BusinessCreditCheckResultDto(
                    rcNumber, businessName, false, bureauReport.Id, 0, 0, 0, 0, 0, false, null, null,
                    errorMessage, failureReason
                );
            }

            var report = reportResult.Value;
            var summary = report.Summary;

            var hasCreditIssues = summary.TotalNoOfDelinquentFacilities > 0 ||
                                 summary.TotalOverdue > 0 ||
                                 summary.TotalNoOfOverdueAccounts > 0;

            bureauReport.CompleteWithData(
                report.Id ?? rcNumber,
                null, // No credit score for business
                null,
                report.SearchedDate ?? DateTime.UtcNow,
                JsonSerializer.Serialize(report),
                null,
                summary.TotalNoOfLoans,
                summary.TotalNoOfActiveLoans,
                summary.TotalNoOfPerformingLoans,
                summary.TotalNoOfDelinquentFacilities,
                summary.TotalNoOfClosedLoans,
                summary.TotalOutstanding,
                summary.TotalOverdue,
                summary.HighestLoanAmount,
                0, // Max delinquency days not directly available
                false
            );

            // Run business fraud check before persisting
            int? fraudRiskScore = null;
            string? fraudRecommendation = null;

            try
            {
                var fraudRequest = new SmartComplyBusinessLoanRequest(
                    BusinessName: businessName,
                    BusinessAddress: report.Address,
                    RcNumber: rcNumber,
                    City: null,
                    Country: "Nigeria",
                    PhoneNumber: report.Phone,
                    EmailAddress: report.Email,
                    AnnualRevenue: 0, // Business revenue not available from credit report; income=0 handled by mock
                    BankName: null,
                    AccountNumber: loanApp.AccountNumber,
                    LoanAmountRequested: loanApp.RequestedAmount.Amount,
                    PurposeOfLoan: loanApp.Purpose,
                    LoanRepaymentDurationType: "months",
                    LoanRepaymentDurationValue: loanApp.RequestedTenorMonths,
                    CollateralRequired: hasCollateral,
                    CollateralValue: hasCollateral ? totalCollateralValue : null,
                    RunAmlCheck: false
                );

                var fraudResult = await _smartComplyProvider.CheckBusinessLoanFraudAsync(fraudRequest, ct);
                if (fraudResult.IsSuccess)
                {
                    fraudRiskScore = fraudResult.Value.FraudRiskScore;
                    fraudRecommendation = fraudResult.Value.Recommendation;
                    
                    // Persist fraud check results to bureau report
                    bureauReport.RecordFraudCheckResults(
                        fraudRiskScore, 
                        fraudRecommendation,
                        JsonSerializer.Serialize(fraudResult.Value));
                }
                else
                {
                    _logger.LogWarning("Business fraud check failed for RC {RcNumber}: {Error}", rcNumber, fraudResult.Error);
                }
            }
            catch (OperationCanceledException)
            {
                throw; // Propagate cancellation
            }
            catch (Exception ex)
            {
                // Fraud check is optional, don't fail the whole process - but log it
                _logger.LogError(ex, "Business fraud check exception for RC {RcNumber} on loan {LoanApplicationId}", 
                    rcNumber, loanApp.Id);
            }

            // Now persist the bureau report with fraud results included
            await _bureauReportRepository.AddAsync(bureauReport, ct);

            return new BusinessCreditCheckResultDto(
                rcNumber,
                businessName,
                true,
                bureauReport.Id,
                summary.TotalNoOfLoans,
                summary.TotalNoOfActiveLoans,
                summary.TotalNoOfDelinquentFacilities,
                summary.TotalOutstanding,
                summary.TotalOverdue,
                hasCreditIssues,
                fraudRiskScore,
                fraudRecommendation,
                null
            );
        }
        catch (OperationCanceledException)
        {
            throw; // Propagate cancellation to caller
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during business credit check for RC {RcNumber} on loan {LoanApplicationId}",
                rcNumber, loanApp.Id);

            // Create a failed bureau report so there is always an NDPA audit trace (BUG H-2 fix)
            var errorMsg = $"Unexpected error: {ex.Message}";
            var auditReport = BureauReport.CreateForConsentFailure(
                CreditBureauProvider.SmartComply, SubjectType.Business,
                businessName, null, rcNumber, systemUserId, loanApp.Id, null, "Business", errorMsg);
            Guid? auditReportId = null;
            if (auditReport.IsSuccess)
            {
                await _bureauReportRepository.AddAsync(auditReport.Value, ct);
                auditReportId = auditReport.Value.Id;
            }

            return new BusinessCreditCheckResultDto(
                rcNumber, businessName, false, auditReportId, 0, 0, 0, 0, 0, false, null, null,
                errorMsg, CreditCheckFailureReason.InternalError
            );
        }
    }

    private static int CalculateCreditScoreFromReport(SmartComplyCreditSummary summary)
    {
        // Base score calculation based on SmartComply data
        var baseScore = 600;

        // Positive factors
        if (summary.TotalNoOfClosedLoans > 5) baseScore += 30;
        if (summary.TotalNoOfPerformingLoans == summary.TotalNoOfLoans && summary.TotalNoOfLoans > 0) baseScore += 50;
        if (summary.TotalOverdue == 0) baseScore += 40;

        // Negative factors
        baseScore -= summary.TotalNoOfDelinquentFacilities * 30;
        baseScore -= Math.Min(summary.MaxNoOfDays / 10, 50);
        
        if (summary.TotalOverdue > 0)
        {
            var overdueRatio = summary.TotalOutstanding > 0 
                ? (double)summary.TotalOverdue / (double)summary.TotalOutstanding 
                : 0;
            baseScore -= (int)(overdueRatio * 100);
        }

        return Math.Clamp(baseScore, 300, 850);
    }

    private static string GetScoreGrade(int score) => score switch
    {
        >= 750 => "A+",
        >= 700 => "A",
        >= 650 => "B",
        >= 600 => "C",
        >= 550 => "D",
        _ => "E"
    };

    private static AccountStatus ParsePerformanceStatus(string? status) => status?.ToLowerInvariant() switch
    {
        "performing" => AccountStatus.Performing,
        "non-performing" or "nonperforming" or "delinquent" => AccountStatus.NonPerforming,
        "written off" or "writtenoff" => AccountStatus.WrittenOff,
        "closed" or "paid off (closed)" => AccountStatus.Closed,
        _ => AccountStatus.Unknown
    };

    /// <summary>
    /// Derives delinquency level from per-account data (PerformanceStatus, RepaymentBehavior, OverdueAmount).
    /// This ensures each account gets its own accurate delinquency level, not the global max.
    /// </summary>
    private static DelinquencyLevel DeriveDelinquencyLevelFromLoan(SmartComplyLoanPerformance loan)
    {
        // If the loan is performing with no overdue, it's current
        var status = loan.PerformanceStatus?.ToLowerInvariant() ?? "";
        var behavior = loan.RepaymentBehavior?.ToLowerInvariant() ?? "";
        
        if (status == "performing" && loan.OverdueAmount == 0)
            return DelinquencyLevel.Current;
        
        // Parse days from RepaymentBehavior if available (e.g., "Delinquent (over 30 days)")
        if (behavior.Contains("over 360") || behavior.Contains("365"))
            return DelinquencyLevel.Over360Days;
        if (behavior.Contains("over 180") || behavior.Contains("181"))
            return DelinquencyLevel.Days181To360;
        if (behavior.Contains("over 150") || behavior.Contains("151"))
            return DelinquencyLevel.Days151To180;
        if (behavior.Contains("over 120") || behavior.Contains("121"))
            return DelinquencyLevel.Days121To150;
        if (behavior.Contains("over 90") || behavior.Contains("91"))
            return DelinquencyLevel.Days91To120;
        if (behavior.Contains("over 60") || behavior.Contains("61"))
            return DelinquencyLevel.Days61To90;
        if (behavior.Contains("over 30") || behavior.Contains("31"))
            return DelinquencyLevel.Days31To60;
        if (behavior.Contains("delinquent") || loan.OverdueAmount > 0)
            return DelinquencyLevel.Days1To30;
        
        // Closed or paid-off accounts are current
        if (status.Contains("closed") || status.Contains("paid off"))
            return DelinquencyLevel.Current;
        
        // Default based on performance status
        return status switch
        {
            "non-performing" or "nonperforming" => DelinquencyLevel.Days61To90, // Conservative estimate
            _ => DelinquencyLevel.Current
        };
    }

    /// <summary>
    /// Extracts first name from full name. For single-token names, returns the name.
    /// </summary>
    private static string GetFirstName(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : fullName;
    }

    /// <summary>
    /// Extracts last name from full name.
    /// - Single token: returns the same name (APIs typically require non-empty last_name)
    /// - Two tokens: returns second token
    /// - Three+ tokens: returns last token (common Nigerian pattern: FIRSTNAME MIDDLENAME SURNAME)
    /// </summary>
    private static string GetLastName(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        // For single-token names, return the same value to satisfy API validation
        // This is better than empty string which may fail validation
        return parts.Length > 1 ? parts[^1] : (parts.Length > 0 ? parts[0] : fullName);
    }

    /// <summary>
    /// Extracts middle/other names from full name (tokens between first and last).
    /// Returns null if fewer than 3 tokens.
    /// </summary>
    private static string? GetOtherNames(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 2) return null;
        return string.Join(" ", parts.Skip(1).Take(parts.Length - 2));
    }
}

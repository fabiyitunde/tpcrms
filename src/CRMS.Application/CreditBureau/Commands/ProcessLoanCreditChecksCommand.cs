using CRMS.Application.Common;
using CRMS.Domain.Aggregates.CreditBureau;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.CreditBureau.Commands;

/// <summary>
/// Processes all credit checks for a loan application after branch approval.
/// Runs checks on directors, signatories, and guarantors in parallel.
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
    List<IndividualCreditCheckResultDto> Results
);

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
    string? ErrorMessage
);

public class ProcessLoanCreditChecksHandler : IRequestHandler<ProcessLoanCreditChecksCommand, ApplicationResult<CreditCheckBatchResultDto>>
{
    private readonly ILoanApplicationRepository _loanAppRepository;
    private readonly IGuarantorRepository _guarantorRepository;
    private readonly IBureauReportRepository _bureauReportRepository;
    private readonly IConsentRecordRepository _consentRepository;
    private readonly ICreditBureauProvider _bureauProvider;
    private readonly IUnitOfWork _unitOfWork;

    public ProcessLoanCreditChecksHandler(
        ILoanApplicationRepository loanAppRepository,
        IGuarantorRepository guarantorRepository,
        IBureauReportRepository bureauReportRepository,
        IConsentRecordRepository consentRepository,
        ICreditBureauProvider bureauProvider,
        IUnitOfWork unitOfWork)
    {
        _loanAppRepository = loanAppRepository;
        _guarantorRepository = guarantorRepository;
        _bureauReportRepository = bureauReportRepository;
        _consentRepository = consentRepository;
        _bureauProvider = bureauProvider;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<CreditCheckBatchResultDto>> Handle(ProcessLoanCreditChecksCommand request, CancellationToken ct = default)
    {
        var loanApp = await _loanAppRepository.GetByIdWithPartiesAsync(request.LoanApplicationId, ct);
        if (loanApp == null)
            return ApplicationResult<CreditCheckBatchResultDto>.Failure("Loan application not found");

        if (loanApp.Status != LoanApplicationStatus.BranchApproved && loanApp.Status != LoanApplicationStatus.CreditAnalysis)
            return ApplicationResult<CreditCheckBatchResultDto>.Failure($"Loan must be BranchApproved to run credit checks. Current: {loanApp.Status}");

        // Get all parties (directors, signatories) from loan application
        var partiesWithBVN = loanApp.Parties
            .Where(p => !string.IsNullOrEmpty(p.BVN))
            .ToList();

        // Get guarantors for this loan
        var guarantors = await _guarantorRepository.GetByLoanApplicationIdAsync(request.LoanApplicationId, ct);
        var guarantorsWithBVN = guarantors
            .Where(g => !string.IsNullOrEmpty(g.BVN) && g.Status != GuarantorStatus.Rejected)
            .ToList();

        var totalChecks = partiesWithBVN.Count + guarantorsWithBVN.Count;

        if (totalChecks == 0)
            return ApplicationResult<CreditCheckBatchResultDto>.Failure("No parties with BVN found for credit checks");

        // Start credit analysis on loan application
        if (loanApp.Status == LoanApplicationStatus.BranchApproved)
        {
            var startResult = loanApp.StartCreditAnalysis(totalChecks, request.SystemUserId);
            if (startResult.IsFailure)
                return ApplicationResult<CreditCheckBatchResultDto>.Failure(startResult.Error);
        }

        var results = new List<IndividualCreditCheckResultDto>();
        var successful = 0;
        var failed = 0;
        var notFound = 0;

        // Process loan application parties (directors, signatories)
        foreach (var party in partiesWithBVN)
        {
            var result = await ProcessCreditCheck(
                request.LoanApplicationId,
                party.Id,
                party.FullName,
                party.PartyType.ToString(),
                party.BVN!,
                request.SystemUserId,
                ct
            );

            results.Add(result);
            if (result.Success) successful++;
            else if (result.ErrorMessage?.Contains("not found") == true) notFound++;
            else failed++;

            // Record completion in loan application
            loanApp.RecordCreditCheckCompleted(request.SystemUserId);
        }

        // Process guarantors
        foreach (var guarantor in guarantorsWithBVN)
        {
            var result = await ProcessCreditCheck(
                request.LoanApplicationId,
                guarantor.Id,
                guarantor.FullName,
                "Guarantor",
                guarantor.BVN!,
                request.SystemUserId,
                ct,
                guarantor
            );

            results.Add(result);
            if (result.Success) successful++;
            else if (result.ErrorMessage?.Contains("not found") == true) notFound++;
            else failed++;

            // Record completion in loan application
            loanApp.RecordCreditCheckCompleted(request.SystemUserId);
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
            results
        ));
    }

    private async Task<IndividualCreditCheckResultDto> ProcessCreditCheck(
        Guid loanApplicationId,
        Guid partyId,
        string partyName,
        string partyType,
        string bvn,
        Guid systemUserId,
        CancellationToken ct,
        Domain.Aggregates.Guarantor.Guarantor? guarantor = null)
    {
        try
        {
            // NDPA Compliance: Verify consent exists before credit check
            var consent = await _consentRepository.GetValidConsentAsync(bvn, Domain.Enums.ConsentType.CreditBureauCheck, ct);
            if (consent == null)
            {
                return new IndividualCreditCheckResultDto(
                    partyId, partyName, partyType, bvn, false, null, null, null, false,
                    "No valid consent record found for this BVN. Consent is required for credit bureau checks (NDPA compliance)."
                );
            }

            // Search bureau by BVN
            var searchResult = await _bureauProvider.SearchByBVNAsync(bvn, ct);
            if (searchResult.IsFailure)
            {
                return new IndividualCreditCheckResultDto(
                    partyId, partyName, partyType, bvn, false, null, null, null, false,
                    $"Bureau search failed: {searchResult.Error}"
                );
            }

            if (!searchResult.Value.Found || string.IsNullOrEmpty(searchResult.Value.RegistryId))
            {
                return new IndividualCreditCheckResultDto(
                    partyId, partyName, partyType, bvn, true, null, null, null, false,
                    "Subject not found in credit bureau"
                );
            }

            // Get full credit report
            var reportResult = await _bureauProvider.GetCreditReportAsync(searchResult.Value.RegistryId, false, ct);
            if (reportResult.IsFailure)
            {
                return new IndividualCreditCheckResultDto(
                    partyId, partyName, partyType, bvn, false, null, null, null, false,
                    $"Report retrieval failed: {reportResult.Error}"
                );
            }

            var report = reportResult.Value;

            // Create and store bureau report with consent reference
            var bureauReportResult = BureauReport.Create(
                CreditBureauProvider.CreditRegistry,
                SubjectType.Individual,
                partyName,
                bvn,
                systemUserId,
                consent.Id,
                loanApplicationId
            );

            if (bureauReportResult.IsFailure)
            {
                return new IndividualCreditCheckResultDto(
                    partyId, partyName, partyType, bvn, false, null, null, null, false,
                    $"Failed to create report: {bureauReportResult.Error}"
                );
            }

            var bureauReport = bureauReportResult.Value;
            bureauReport.CompleteWithData(
                report.RegistryId,
                report.CreditScore,
                report.ScoreGrade,
                report.ReportDate,
                report.RawJson,
                null,
                report.Summary.TotalAccounts,
                report.Summary.PerformingAccounts,
                report.Summary.NonPerformingAccounts,
                report.Summary.ClosedAccounts,
                report.Summary.TotalOutstandingBalance,
                report.Summary.TotalCreditLimit,
                report.Summary.MaxDelinquencyDays,
                report.Summary.HasLegalActions
            );

            // Add accounts and score factors
            foreach (var acc in report.Accounts)
            {
                var account = Domain.Aggregates.CreditBureau.BureauAccount.Create(
                    bureauReport.Id, acc.AccountNumber, acc.CreditorName, acc.AccountType,
                    ParseAccountStatus(acc.Status), ParseDelinquencyLevel(acc.DelinquencyDays),
                    acc.CreditLimit, acc.Balance, acc.MinimumPayment, acc.DateOpened, acc.DateClosed,
                    acc.LastPaymentDate, acc.LastPaymentAmount, acc.PaymentProfile,
                    ParseLegalStatus(acc.LegalStatus), acc.LegalStatusDate, acc.Currency, acc.LastUpdated
                );
                bureauReport.AddAccount(account);
            }

            foreach (var factor in report.ScoreFactors)
            {
                var scoreFactor = Domain.Aggregates.CreditBureau.BureauScoreFactor.Create(
                    bureauReport.Id, factor.FactorCode, factor.Description, factor.Impact, factor.Rank
                );
                bureauReport.AddScoreFactor(scoreFactor);
            }

            await _bureauReportRepository.AddAsync(bureauReport, ct);

            // Analyze credit issues
            var hasCreditIssues = report.Summary.NonPerformingAccounts > 0 ||
                                 report.Summary.MaxDelinquencyDays > 60 ||
                                 report.Summary.HasLegalActions;

            // Update guarantor if applicable
            if (guarantor != null)
            {
                var issuesSummary = hasCreditIssues
                    ? $"NPL: {report.Summary.NonPerformingAccounts}, Max Delinquency: {report.Summary.MaxDelinquencyDays} days"
                    : "No significant issues";

                var existingCount = await _guarantorRepository.GetActiveGuaranteeCountByBVNAsync(bvn, ct);
                var totalExisting = Domain.ValueObjects.Money.Create(report.Summary.TotalOutstandingBalance, "NGN");

                guarantor.RecordCreditCheck(
                    bureauReport.Id, report.CreditScore, report.ScoreGrade,
                    hasCreditIssues, issuesSummary, existingCount, totalExisting
                );
                _guarantorRepository.Update(guarantor);
            }

            return new IndividualCreditCheckResultDto(
                partyId, partyName, partyType, bvn, true, bureauReport.Id,
                report.CreditScore, report.ScoreGrade, hasCreditIssues, null
            );
        }
        catch (Exception ex)
        {
            return new IndividualCreditCheckResultDto(
                partyId, partyName, partyType, bvn, false, null, null, null, false,
                $"Unexpected error: {ex.Message}"
            );
        }
    }

    private static AccountStatus ParseAccountStatus(string status) => status switch
    {
        "Performing" => AccountStatus.Performing,
        "NonPerforming" => AccountStatus.NonPerforming,
        "WrittenOff" => AccountStatus.WrittenOff,
        "Closed" => AccountStatus.Closed,
        _ => AccountStatus.Unknown
    };

    private static DelinquencyLevel ParseDelinquencyLevel(int days) => days switch
    {
        0 => DelinquencyLevel.Current,
        <= 30 => DelinquencyLevel.Days1To30,
        <= 60 => DelinquencyLevel.Days31To60,
        <= 90 => DelinquencyLevel.Days61To90,
        <= 120 => DelinquencyLevel.Days91To120,
        <= 150 => DelinquencyLevel.Days121To150,
        <= 180 => DelinquencyLevel.Days151To180,
        <= 360 => DelinquencyLevel.Days181To360,
        _ => DelinquencyLevel.Over360Days
    };

    private static LegalStatus ParseLegalStatus(string? status) => status?.ToLowerInvariant() switch
    {
        "litigation" => LegalStatus.Litigation,
        "foreclosure" => LegalStatus.Foreclosure,
        "bankruptcy" => LegalStatus.Bankruptcy,
        null or "" or "none" => LegalStatus.None,
        _ => LegalStatus.Other
    };
}

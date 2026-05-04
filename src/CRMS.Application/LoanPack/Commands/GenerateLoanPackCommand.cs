using CRMS.Application.Common;
using CRMS.Application.LoanPack.DTOs;
using CRMS.Application.LoanPack.Interfaces;
using CRMS.Domain.Interfaces;
using LP = CRMS.Domain.Aggregates.LoanPack;

namespace CRMS.Application.LoanPack.Commands;

public record GenerateLoanPackCommand(
    Guid LoanApplicationId,
    Guid GeneratedByUserId,
    string GeneratedByUserName
) : IRequest<ApplicationResult<LoanPackResultDto>>;

public record LoanPackResultDto(
    Guid LoanPackId,
    string ApplicationNumber,
    int Version,
    string FileName,
    long FileSizeBytes,
    string Status,
    string? StoragePath = null
);

public class GenerateLoanPackHandler : IRequestHandler<GenerateLoanPackCommand, ApplicationResult<LoanPackResultDto>>
{
    private readonly ILoanApplicationRepository _loanAppRepository;
    private readonly ILoanProductRepository _productRepository;
    private readonly IBureauReportRepository _bureauRepository;
    private readonly IFinancialStatementRepository _financialRepository;
    private readonly IBankStatementRepository _bankStatementRepository;
    private readonly ICollateralRepository _collateralRepository;
    private readonly IGuarantorRepository _guarantorRepository;
    private readonly ICreditAdvisoryRepository _advisoryRepository;
    private readonly IWorkflowInstanceRepository _workflowRepository;
    private readonly ICommitteeReviewRepository _committeeRepository;
    private readonly ILoanPackRepository _loanPackRepository;
    private readonly ILoanPackGenerator _pdfGenerator;
    private readonly IFileStorageService _fileStorage;
    private readonly IUnitOfWork _unitOfWork;

    public GenerateLoanPackHandler(
        ILoanApplicationRepository loanAppRepository,
        ILoanProductRepository productRepository,
        IBureauReportRepository bureauRepository,
        IFinancialStatementRepository financialRepository,
        IBankStatementRepository bankStatementRepository,
        ICollateralRepository collateralRepository,
        IGuarantorRepository guarantorRepository,
        ICreditAdvisoryRepository advisoryRepository,
        IWorkflowInstanceRepository workflowRepository,
        ICommitteeReviewRepository committeeRepository,
        ILoanPackRepository loanPackRepository,
        ILoanPackGenerator pdfGenerator,
        IFileStorageService fileStorage,
        IUnitOfWork unitOfWork)
    {
        _loanAppRepository = loanAppRepository;
        _productRepository = productRepository;
        _bureauRepository = bureauRepository;
        _financialRepository = financialRepository;
        _bankStatementRepository = bankStatementRepository;
        _collateralRepository = collateralRepository;
        _guarantorRepository = guarantorRepository;
        _advisoryRepository = advisoryRepository;
        _workflowRepository = workflowRepository;
        _committeeRepository = committeeRepository;
        _loanPackRepository = loanPackRepository;
        _pdfGenerator = pdfGenerator;
        _fileStorage = fileStorage;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<LoanPackResultDto>> Handle(GenerateLoanPackCommand request, CancellationToken ct = default)
    {
        // Load loan application
        var loanApp = await _loanAppRepository.GetByIdAsync(request.LoanApplicationId, ct);
        if (loanApp == null)
            return ApplicationResult<LoanPackResultDto>.Failure("Loan application not found");

        // Use MAX version so Failed records don't cause duplicate version numbers
        // (a unique index on (LoanApplicationId, Version) would otherwise be violated).
        var maxExistingVersion = await _loanPackRepository.GetMaxVersionAsync(request.LoanApplicationId, ct);
        var nextVersion = maxExistingVersion + 1;

        // Create loan pack entity with the correct version number
        var loanPackResult = LP.LoanPack.Create(
            request.LoanApplicationId,
            loanApp.ApplicationNumber,
            request.GeneratedByUserId,
            request.GeneratedByUserName,
            loanApp.CustomerName,
            loanApp.ProductCode,
            loanApp.RequestedAmount.Amount,
            version: nextVersion);

        if (!loanPackResult.IsSuccess)
            return ApplicationResult<LoanPackResultDto>.Failure(loanPackResult.Error);

        var loanPack = loanPackResult.Value;

        try
        {
            // Gather all data for PDF generation
            var packData = await BuildLoanPackDataAsync(loanApp, nextVersion, request.GeneratedByUserName, ct);

            // Generate PDF
            var pdfBytes = await _pdfGenerator.GenerateAsync(packData, ct);

            // Generate file name and storage path
            var fileName = $"LoanPack_{loanApp.ApplicationNumber}_v{nextVersion}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";
            var storagePath = $"loanpacks/{loanApp.ApplicationNumber}/{fileName}";

            // Calculate content hash
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashBytes = sha256.ComputeHash(pdfBytes);
            var contentHash = Convert.ToBase64String(hashBytes);

            // Update loan pack with document info
            loanPack.SetDocument(fileName, storagePath, pdfBytes.Length, contentHash);

            // Set content summary using data already loaded in packData (no duplicate DB queries)
            loanPack.SetContentSummary(
                packData.AIAdvisory?.RecommendedAmount,
                packData.AIAdvisory?.OverallRiskScore,
                packData.AIAdvisory?.RiskRating,
                packData.Directors.Count,
                packData.BureauReports.Count,
                packData.Collaterals.Count,
                packData.Guarantors.Count);

            loanPack.SetIncludedSections(
                executiveSummary: true,
                bureauReports: packData.BureauReports.Any(),
                financialAnalysis: packData.FinancialStatements.Any(),
                cashflowAnalysis: packData.CashflowAnalysis != null,
                collateralDetails: packData.Collaterals.Any(),
                guarantorDetails: packData.Guarantors.Any(),
                aiAdvisory: packData.AIAdvisory != null,
                workflowHistory: packData.WorkflowHistory.Any(),
                committeeComments: packData.CommitteeComments.Any());

            // Save PDF bytes to file storage
            var actualStoragePath = await _fileStorage.UploadAsync(
                containerName: "loanpacks",
                fileName: $"{loanApp.ApplicationNumber}/{fileName}",
                content: pdfBytes,
                contentType: "application/pdf",
                ct: ct);

            // Update storage path with actual path from storage service
            loanPack.SetDocument(fileName, actualStoragePath, pdfBytes.Length, contentHash);

            // Save loan pack metadata
            await _loanPackRepository.AddAsync(loanPack, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return ApplicationResult<LoanPackResultDto>.Success(new LoanPackResultDto(
                loanPack.Id,
                loanPack.ApplicationNumber,
                nextVersion,
                fileName,
                pdfBytes.Length,
                loanPack.Status.ToString(),
                actualStoragePath));
        }
        catch (Exception ex)
        {
            loanPack.MarkAsFailed(ex.Message);
            try
            {
                await _loanPackRepository.AddAsync(loanPack, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }
            catch { /* Best-effort audit record — ignore if it fails */ }

            return ApplicationResult<LoanPackResultDto>.Failure($"Failed to generate loan pack: {ex.Message}");
        }
    }

    private async Task<LoanPackData> BuildLoanPackDataAsync(
        Domain.Aggregates.LoanApplication.LoanApplication loanApp,
        int version,
        string generatedBy,
        CancellationToken ct)
    {
        // Load all related data sequentially — EF Core DbContext is not thread-safe.
        var loanAppWithParties  = await _loanAppRepository.GetByIdWithPartiesAsync(loanApp.Id, ct) ?? loanApp;
        var product             = await _productRepository.GetByIdAsync(loanApp.LoanProductId, ct);
        var bureauReports       = await _bureauRepository.GetByLoanApplicationIdWithDetailsAsync(loanApp.Id, ct);
        var financialStatements = await _financialRepository.GetByLoanApplicationIdAsync(loanApp.Id, ct);
        var bankStatements      = await _bankStatementRepository.GetByLoanApplicationIdAsync(loanApp.Id, ct);
        var collaterals         = await _collateralRepository.GetByLoanApplicationIdAsync(loanApp.Id, ct);
        var guarantors          = await _guarantorRepository.GetByLoanApplicationIdAsync(loanApp.Id, ct);
        var advisory            = await _advisoryRepository.GetLatestByLoanApplicationIdAsync(loanApp.Id, ct);
        var workflow            = await _workflowRepository.GetByLoanApplicationIdAsync(loanApp.Id, ct);
        var committeeReview     = await _committeeRepository.GetByLoanApplicationIdAsync(loanApp.Id, ct);

        var productName = product?.Name ?? loanApp.ProductCode;

        // Build bureau lookup by PartyId for director/signatory cross-referencing
        var bureauByPartyId = bureauReports
            .Where(b => b.PartyId.HasValue && b.Status == Domain.Enums.BureauReportStatus.Completed)
            .GroupBy(b => b.PartyId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(b => b.CompletedAt).First());

        // Customer profile
        var customerProfile = new CustomerProfileData(
            loanAppWithParties.CustomerName,
            loanAppWithParties.RegistrationNumber ?? "",
            loanAppWithParties.IncorporationDate,
            loanAppWithParties.IndustrySector ?? "",
            "",
            "",
            "",
            "",
            loanAppWithParties.AccountNumber,
            "",
            null,
            null);

        // Directors with bureau cross-reference
        var directors = loanAppWithParties.Parties
            .Where(p => p.PartyType == Domain.Enums.PartyType.Director)
            .Select(d =>
            {
                bureauByPartyId.TryGetValue(d.Id, out var bureau);
                return new DirectorData(
                    d.FullName,
                    d.Designation ?? "",
                    d.BVN ?? "",
                    d.PhoneNumber ?? "",
                    d.Email ?? "",
                    d.ShareholdingPercent,
                    bureau?.CreditScore,
                    bureau?.ScoreGrade,
                    bureau != null && bureau.ActiveLoans > 0,
                    bureau != null && bureau.DelinquentFacilities > 0,
                    bureau != null
                        ? $"Score: {bureau.CreditScore?.ToString() ?? "N/A"} | Outstanding: {loanApp.RequestedAmount.Currency} {bureau.TotalOutstandingBalance:N0} | Delinquent facilities: {bureau.DelinquentFacilities}"
                        : null);
            }).ToList();

        // Signatories with bureau cross-reference
        var signatories = loanAppWithParties.Parties
            .Where(p => p.PartyType == Domain.Enums.PartyType.Signatory)
            .Select(s =>
            {
                bureauByPartyId.TryGetValue(s.Id, out var bureau);
                return new SignatoryData(
                    s.FullName,
                    s.Designation ?? "",
                    s.BVN ?? "",
                    s.PhoneNumber ?? "",
                    "",
                    bureau?.CreditScore,
                    bureau?.ScoreGrade,
                    bureau != null && bureau.ActiveLoans > 0,
                    bureau != null && bureau.DelinquentFacilities > 0);
            }).ToList();

        // Bureau reports with actual metrics and account breakdowns
        var bureauData = bureauReports.Select(b =>
        {
            var activeLoans = b.Accounts
                .Where(a => a.Status == Domain.Enums.AccountStatus.Performing)
                .Select(a => new ActiveLoanData(
                    a.CreditorName ?? "",
                    a.AccountType ?? "",
                    a.CreditLimit,
                    a.Balance,
                    a.DateClosed,
                    a.Status.ToString()))
                .ToList();

            var delinquencies = b.Accounts
                .Where(a => a.DelinquencyLevel != Domain.Enums.DelinquencyLevel.Current)
                .Select(a => new DelinquencyData(
                    a.CreditorName ?? "",
                    a.AccountType ?? "",
                    a.Balance,
                    a.GetDelinquencyDays(),
                    a.DelinquencyLevel.ToString()))
                .ToList();

            return new BureauReportData(
                b.SubjectName,
                b.SubjectType.ToString(),
                b.Provider.ToString(),
                b.CompletedAt ?? b.RequestedAt,
                b.CreditScore,
                b.ScoreGrade,
                b.ActiveLoans,
                b.TotalOutstandingBalance,
                b.DelinquentFacilities,
                b.HasLegalActions,
                b.HasLegalActions ? $"Max delinquency: {b.MaxDelinquencyDays} days" : null,
                activeLoans,
                delinquencies);
        }).ToList();

        // Financial statements — full balance sheet + income statement
        var financialData = financialStatements
            .OrderByDescending(f => f.FinancialYear)
            .Select(f => new FinancialStatementData(
                f.FinancialYear,
                f.YearType.ToString(),
                f.AuditorName ?? "",
                f.BalanceSheet?.TotalAssets,
                f.BalanceSheet?.TotalCurrentAssets,
                f.BalanceSheet?.TotalNonCurrentAssets,
                f.BalanceSheet?.TotalLiabilities,
                f.BalanceSheet?.TotalCurrentLiabilities,
                f.BalanceSheet?.LongTermDebt,
                f.BalanceSheet?.TotalEquity,
                f.IncomeStatement?.Revenue,
                f.IncomeStatement?.GrossProfit,
                f.IncomeStatement?.OperatingProfit,
                f.IncomeStatement?.NetProfit,
                f.IncomeStatement?.EBITDA))
            .ToList();

        // Financial ratios from most recent statement
        var latestFinancial = financialStatements.OrderByDescending(f => f.FinancialYear).FirstOrDefault();
        FinancialRatiosData? ratiosData = null;
        if (latestFinancial?.CalculatedRatios != null)
        {
            var r = latestFinancial.CalculatedRatios;
            // Revenue growth: compare two most recent years if available
            var prevFinancial = financialStatements.OrderByDescending(f => f.FinancialYear).Skip(1).FirstOrDefault();
            decimal? revenueGrowth = null;
            decimal? profitGrowth = null;
            if (prevFinancial?.IncomeStatement != null && latestFinancial.IncomeStatement != null
                && prevFinancial.IncomeStatement.Revenue > 0)
            {
                revenueGrowth = (latestFinancial.IncomeStatement.Revenue - prevFinancial.IncomeStatement.Revenue)
                    / prevFinancial.IncomeStatement.Revenue * 100;
            }
            if (prevFinancial?.IncomeStatement != null && latestFinancial.IncomeStatement != null
                && prevFinancial.IncomeStatement.NetProfit != 0)
            {
                profitGrowth = (latestFinancial.IncomeStatement.NetProfit - prevFinancial.IncomeStatement.NetProfit)
                    / Math.Abs(prevFinancial.IncomeStatement.NetProfit) * 100;
            }

            ratiosData = new FinancialRatiosData(
                r.CurrentRatio, r.QuickRatio, r.CashRatio,
                r.DebtToEquityRatio, r.DebtToAssetsRatio, r.InterestCoverageRatio,
                r.GrossMarginPercent, r.OperatingMarginPercent, r.NetProfitMarginPercent,
                r.ReturnOnAssets, r.ReturnOnEquity,
                r.AssetTurnover, r.InventoryTurnover, r.ReceivablesDays, r.PayablesDays,
                r.DebtServiceCoverageRatio,
                revenueGrowth, profitGrowth);
        }

        // Cashflow analysis — aggregate across all analysed bank statements
        CashflowAnalysisData? cashflowData = null;
        var analysedStatements = bankStatements.Where(b => b.CashflowSummary != null).ToList();
        if (analysedStatements.Any())
        {
            var totalMonths = analysedStatements.Sum(b => b.CashflowSummary!.PeriodMonths);
            var avgMonthlyInflow = totalMonths > 0
                ? analysedStatements.Sum(b => b.CashflowSummary!.TotalCredits) / totalMonths : 0;
            var avgMonthlyOutflow = totalMonths > 0
                ? analysedStatements.Sum(b => b.CashflowSummary!.TotalDebits) / totalMonths : 0;
            var netCashflow = avgMonthlyInflow - avgMonthlyOutflow;
            var lowestBalance = analysedStatements.Min(b => b.CashflowSummary!.LowestBalance);
            var highestBalance = analysedStatements.Max(b => b.CashflowSummary!.HighestBalance);
            var avgBalance = analysedStatements.Average(b => b.CashflowSummary!.AverageMonthlyBalance);
            var totalBounced = analysedStatements.Sum(b => b.CashflowSummary!.BouncedTransactionCount);
            var balanceVol = analysedStatements.Average(b => b.CashflowSummary!.BalanceVolatility);
            var incomeVol = analysedStatements.Average(b => b.CashflowSummary!.IncomeVolatility);
            var loanRepayments = analysedStatements.Sum(b => b.CashflowSummary!.DetectedLoanRepayments);
            var rentUtils = analysedStatements.Sum(b => b.CashflowSummary!.DetectedRentPayments + b.CashflowSummary!.DetectedUtilityPayments);
            var salaryIn = analysedStatements.Sum(b => b.CashflowSummary!.DetectedMonthlySalary ?? 0);
            var businessIn = avgMonthlyInflow - (salaryIn / Math.Max(totalMonths, 1));
            var trustLevel = balanceVol < 0.2m ? "High" : balanceVol < 0.5m ? "Medium" : "Low";

            cashflowData = new CashflowAnalysisData(
                totalMonths,
                Math.Round(avgMonthlyInflow, 2),
                Math.Round(avgMonthlyOutflow, 2),
                Math.Round(netCashflow, 2),
                Math.Round(lowestBalance, 2),
                Math.Round(highestBalance, 2),
                Math.Round(avgBalance, 2),
                Math.Round(salaryIn, 2),
                Math.Round(businessIn, 2),
                0,
                Math.Round(loanRepayments, 2),
                Math.Round(rentUtils, 2),
                0,
                avgMonthlyOutflow - loanRepayments - rentUtils > 0 ? Math.Round(avgMonthlyOutflow - loanRepayments - rentUtils, 2) : 0,
                Math.Round(incomeVol, 4),
                Math.Round(balanceVol, 4),
                totalBounced,
                0,
                0,
                0,
                trustLevel);
        }

        // Collaterals — include latest valuation date and valuer
        var collateralData = collaterals.Select(c =>
        {
            var latestValuation = c.Valuations.OrderByDescending(v => v.ValuationDate).FirstOrDefault();
            return new CollateralData(
                c.Type.ToString(),
                c.Description,
                c.Location ?? "",
                c.MarketValue?.Amount ?? 0,
                c.ForcedSaleValue?.Amount ?? 0,
                c.AcceptableValue?.Amount ?? 0,
                latestValuation?.ValuationDate.ToString("dd-MMM-yyyy") ?? "",
                latestValuation != null
                    ? $"{latestValuation.ValuerName ?? ""}{(string.IsNullOrWhiteSpace(latestValuation.ValuerCompany) ? "" : $" ({latestValuation.ValuerCompany})")}"
                    : "",
                c.Status.ToString(),
                c.LienType?.ToString() ?? "",
                c.LienReference,
                c.InsurancePolicyNumber,
                c.InsuranceExpiryDate);
        }).ToList();

        var totalCollateralValue = collaterals.Sum(c => c.AcceptableValue?.Amount ?? 0);
        var approvedOrRequested = loanApp.ApprovedAmount?.Amount ?? loanApp.RequestedAmount.Amount;
        var collateralCoverage = approvedOrRequested > 0 ? totalCollateralValue / approvedOrRequested : 0;

        // Guarantors
        var guarantorData = guarantors.Select(g => new GuarantorData(
            g.FullName,
            g.Type.ToString(),
            g.RelationshipToApplicant ?? "",
            g.Address ?? "",
            g.Phone ?? "",
            g.DeclaredNetWorth?.Amount ?? 0,
            g.GuaranteeLimit?.Amount ?? 0,
            g.CreditScore,
            g.CreditScoreGrade,
            g.Status.ToString(),
            false, false))
            .ToList();

        var totalGuaranteeAmount = guarantors.Sum(g => g.GuaranteeLimit?.Amount ?? 0);

        // AI advisory — include mitigating factors (stored as newline-separated string)
        AIAdvisoryData? aiData = null;
        if (advisory != null)
        {
            decimal getScore(Domain.Enums.RiskCategory category) =>
                advisory.RiskScores.FirstOrDefault(s => s.Category == category)?.Score ?? 0;

            var mitigatingFactors = string.IsNullOrWhiteSpace(advisory.MitigatingFactors)
                ? new List<string>()
                : advisory.MitigatingFactors
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();

            aiData = new AIAdvisoryData(
                (int)advisory.OverallScore,
                advisory.OverallRating.ToString(),
                advisory.ExecutiveSummary ?? "",
                (int)getScore(Domain.Enums.RiskCategory.CreditHistory),
                (int)getScore(Domain.Enums.RiskCategory.FinancialHealth),
                (int)getScore(Domain.Enums.RiskCategory.CashflowStability),
                (int)getScore(Domain.Enums.RiskCategory.CollateralCoverage),
                (int)getScore(Domain.Enums.RiskCategory.IndustryRisk),
                (int)getScore(Domain.Enums.RiskCategory.ManagementRisk),
                (int)getScore(Domain.Enums.RiskCategory.ConcentrationRisk),
                (int)getScore(Domain.Enums.RiskCategory.DebtServiceCapacity),
                advisory.RecommendedAmount.HasValue
                    ? $"Recommend {loanApp.RequestedAmount.Currency} {advisory.RecommendedAmount:N0}" : "",
                advisory.RecommendedAmount,
                advisory.RecommendedTenorMonths.HasValue
                    ? $"{advisory.RecommendedTenorMonths} months" : "",
                advisory.RecommendedTenorMonths,
                advisory.RecommendedInterestRate.HasValue
                    ? $"{advisory.RecommendedInterestRate:N2}% per annum" : "",
                advisory.RecommendedInterestRate,
                "",
                advisory.RedFlags.ToList(),
                mitigatingFactors,
                advisory.Conditions.ToList());
        }

        // Workflow history — ordered chronologically for readability
        var workflowHistory = workflow?.TransitionHistory
            .OrderBy(t => t.PerformedAt)
            .Select(t => new WorkflowHistoryData(
                t.PerformedAt,
                t.FromStatus?.ToString() ?? "",
                t.ToStatus.ToString(),
                t.Action.ToString(),
                t.PerformedByUserId.ToString(), // User name not stored on transition log
                t.Comment))
            .ToList() ?? new List<WorkflowHistoryData>();

        // Committee comments — use stored UserName from committee members
        var memberLookup = committeeReview?.Members
            .ToDictionary(m => m.UserId, m => (m.UserName, m.Role))
            ?? new Dictionary<Guid, (string, string)>();

        var committeeComments = committeeReview?.Comments
            .OrderBy(c => c.CreatedAt)
            .Select(c =>
            {
                memberLookup.TryGetValue(c.UserId, out var member);
                return new CommitteeCommentData(
                    c.CreatedAt,
                    string.IsNullOrWhiteSpace(member.UserName) ? c.UserId.ToString() : member.UserName,
                    member.Role ?? "",
                    c.Content,
                    null,
                    c.Visibility.ToString());
            })
            .ToList() ?? new List<CommitteeCommentData>();

        // Committee decision summary with member votes
        CommitteeDecisionData? committeeDecision = null;
        if (committeeReview != null)
        {
            var memberVotes = committeeReview.Members
                .OrderBy(m => m.UserName)
                .Select(m => new CommitteeMemberVoteData(
                    m.UserName,
                    m.Role,
                    m.Vote?.ToString(),
                    m.VoteComment,
                    m.VotedAt))
                .ToList();

            committeeDecision = new CommitteeDecisionData(
                committeeReview.FinalDecision?.ToString() ?? "Pending",
                committeeReview.ApprovalVotes,
                committeeReview.RejectionVotes,
                committeeReview.AbstainVotes,
                committeeReview.PendingVotes,
                committeeReview.DecisionRationale,
                committeeReview.RecommendedAmount,
                committeeReview.RecommendedTenorMonths,
                committeeReview.RecommendedInterestRate,
                memberVotes);
        }

        // Approval conditions
        var approvalConditions = new List<string>();
        if (!string.IsNullOrWhiteSpace(committeeReview?.ApprovalConditions))
        {
            approvalConditions.AddRange(committeeReview.ApprovalConditions
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        return new LoanPackData(
            loanApp.ApplicationNumber,
            loanApp.CreatedAt,
            productName,
            loanApp.ProductCode,
            loanApp.RequestedAmount.Amount,
            loanApp.RequestedAmount.Currency,
            loanApp.RequestedTenorMonths,
            loanApp.InterestRatePerAnnum,
            loanApp.Purpose ?? "",
            customerProfile,
            directors,
            signatories,
            bureauData,
            financialData,
            ratiosData,
            cashflowData,
            collateralData,
            totalCollateralValue,
            collateralCoverage,
            guarantorData,
            totalGuaranteeAmount,
            aiData,
            workflowHistory,
            committeeComments,
            approvalConditions,
            loanApp.ApprovedAmount?.Amount,
            loanApp.ApprovedTenorMonths,
            loanApp.ApprovedInterestRate,
            committeeDecision,
            DateTime.UtcNow,
            generatedBy,
            version);
    }
}

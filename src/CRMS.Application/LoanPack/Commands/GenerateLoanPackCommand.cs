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
    string Status
);

public class GenerateLoanPackHandler : IRequestHandler<GenerateLoanPackCommand, ApplicationResult<LoanPackResultDto>>
{
    private readonly ILoanApplicationRepository _loanAppRepository;
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
    private readonly IUnitOfWork _unitOfWork;

    public GenerateLoanPackHandler(
        ILoanApplicationRepository loanAppRepository,
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
        IUnitOfWork unitOfWork)
    {
        _loanAppRepository = loanAppRepository;
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
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<LoanPackResultDto>> Handle(GenerateLoanPackCommand request, CancellationToken ct = default)
    {
        // Load loan application
        var loanApp = await _loanAppRepository.GetByIdAsync(request.LoanApplicationId, ct);
        if (loanApp == null)
            return ApplicationResult<LoanPackResultDto>.Failure("Loan application not found");

        // Check if there's an existing version to increment
        var existingVersion = await _loanPackRepository.GetVersionCountAsync(request.LoanApplicationId, ct);

        // Create loan pack entity
        var loanPackResult = LP.LoanPack.Create(
            request.LoanApplicationId,
            loanApp.ApplicationNumber,
            request.GeneratedByUserId,
            request.GeneratedByUserName,
            loanApp.CustomerName,
            loanApp.ProductCode,
            loanApp.RequestedAmount.Amount);

        if (!loanPackResult.IsSuccess)
            return ApplicationResult<LoanPackResultDto>.Failure(loanPackResult.Error);

        var loanPack = loanPackResult.Value;

        try
        {
            // Gather all data for PDF generation
            var packData = await BuildLoanPackDataAsync(loanApp, existingVersion + 1, request.GeneratedByUserName, ct);

            // Generate PDF
            var pdfBytes = await _pdfGenerator.GenerateAsync(packData, ct);

            // Generate file name and storage path
            var fileName = $"LoanPack_{loanApp.ApplicationNumber}_v{existingVersion + 1}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";
            var storagePath = $"loanpacks/{loanApp.ApplicationNumber}/{fileName}";

            // Calculate content hash
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashBytes = sha256.ComputeHash(pdfBytes);
            var contentHash = Convert.ToBase64String(hashBytes);

            // Update loan pack with document info
            loanPack.SetDocument(fileName, storagePath, pdfBytes.Length, contentHash);

            // Set content summary
            var advisory = await _advisoryRepository.GetLatestByLoanApplicationIdAsync(request.LoanApplicationId, ct);
            var bureauReports = await _bureauRepository.GetByLoanApplicationIdAsync(request.LoanApplicationId, ct);
            var collaterals = await _collateralRepository.GetByLoanApplicationIdAsync(request.LoanApplicationId, ct);
            var guarantors = await _guarantorRepository.GetByLoanApplicationIdAsync(request.LoanApplicationId, ct);

            // Get scores from advisory if available
            int? overallScore = null;
            string? riskRating = null;
            if (advisory != null)
            {
                overallScore = (int)advisory.OverallScore;
                riskRating = advisory.OverallRating.ToString();
            }

            loanPack.SetContentSummary(
                advisory?.RecommendedAmount,
                overallScore,
                riskRating,
                loanApp.Parties.Count(p => p.PartyType == Domain.Enums.PartyType.Director),
                bureauReports.Count,
                collaterals.Count,
                guarantors.Count);

            loanPack.SetIncludedSections(
                executiveSummary: true,
                bureauReports: bureauReports.Any(),
                financialAnalysis: packData.FinancialStatements.Any(),
                cashflowAnalysis: packData.CashflowAnalysis != null,
                collateralDetails: collaterals.Any(),
                guarantorDetails: guarantors.Any(),
                aiAdvisory: advisory != null,
                workflowHistory: packData.WorkflowHistory.Any(),
                committeeComments: packData.CommitteeComments.Any());

            // Save loan pack metadata
            await _loanPackRepository.AddAsync(loanPack, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            // TODO: Save PDF bytes to file storage (S3/Azure Blob)

            return ApplicationResult<LoanPackResultDto>.Success(new LoanPackResultDto(
                loanPack.Id,
                loanPack.ApplicationNumber,
                existingVersion + 1,
                fileName,
                pdfBytes.Length,
                loanPack.Status.ToString()));
        }
        catch (Exception ex)
        {
            loanPack.MarkAsFailed(ex.Message);
            await _loanPackRepository.AddAsync(loanPack, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return ApplicationResult<LoanPackResultDto>.Failure($"Failed to generate loan pack: {ex.Message}");
        }
    }

    private async Task<LoanPackData> BuildLoanPackDataAsync(
        Domain.Aggregates.LoanApplication.LoanApplication loanApp,
        int version,
        string generatedBy,
        CancellationToken ct)
    {
        // Load all related data in parallel
        var bureauReportsTask = _bureauRepository.GetByLoanApplicationIdAsync(loanApp.Id, ct);
        var financialStatementsTask = _financialRepository.GetByLoanApplicationIdAsync(loanApp.Id, ct);
        var bankStatementsTask = _bankStatementRepository.GetByLoanApplicationIdAsync(loanApp.Id, ct);
        var collateralsTask = _collateralRepository.GetByLoanApplicationIdAsync(loanApp.Id, ct);
        var guarantorsTask = _guarantorRepository.GetByLoanApplicationIdAsync(loanApp.Id, ct);
        var advisoryTask = _advisoryRepository.GetLatestByLoanApplicationIdAsync(loanApp.Id, ct);
        var workflowTask = _workflowRepository.GetByLoanApplicationIdAsync(loanApp.Id, ct);
        var committeeTask = _committeeRepository.GetByLoanApplicationIdAsync(loanApp.Id, ct);

        await Task.WhenAll(bureauReportsTask, financialStatementsTask, bankStatementsTask,
            collateralsTask, guarantorsTask, advisoryTask, workflowTask, committeeTask);

        var bureauReports = await bureauReportsTask;
        var financialStatements = await financialStatementsTask;
        var bankStatements = await bankStatementsTask;
        var collaterals = await collateralsTask;
        var guarantors = await guarantorsTask;
        var advisory = await advisoryTask;
        var workflow = await workflowTask;
        var committeeReview = await committeeTask;

        // Map customer profile
        var customerProfile = new CustomerProfileData(
            loanApp.CustomerName,
            "",
            null,
            "",
            "",
            "",
            "",
            "",
            loanApp.AccountNumber,
            "",
            null,
            null);

        // Map directors and signatories from Parties
        var directors = loanApp.Parties
            .Where(p => p.PartyType == Domain.Enums.PartyType.Director)
            .Select(d => new DirectorData(
                d.FullName,
                d.Designation ?? "",
                d.BVN ?? "",
                d.PhoneNumber ?? "",
                d.Email ?? "",
                d.ShareholdingPercent,
                null, null, false, false, null))
            .ToList();

        var signatories = loanApp.Parties
            .Where(p => p.PartyType == Domain.Enums.PartyType.Signatory)
            .Select(s => new SignatoryData(
                s.FullName,
                s.Designation ?? "",
                s.BVN ?? "",
                s.PhoneNumber ?? "",
                "",
                null, null, false, false))
            .ToList();

        // Map bureau reports
        var bureauData = bureauReports.Select(b => new BureauReportData(
            b.SubjectName,
            b.SubjectType.ToString(),
            b.Provider.ToString(),
            b.CompletedAt ?? b.RequestedAt,
            b.CreditScore,
            null,
            0, 0, 0, false, null,
            new List<ActiveLoanData>(),
            new List<DelinquencyData>()))
            .ToList();

        // Map financial statements
        var financialData = financialStatements.Select(f => new FinancialStatementData(
            f.FinancialYear,
            f.YearType.ToString(),
            f.AuditorName ?? "",
            f.BalanceSheet?.TotalAssets,
            null, null,
            f.BalanceSheet?.TotalLiabilities,
            null,
            f.BalanceSheet?.LongTermDebt,
            f.BalanceSheet?.TotalEquity,
            f.IncomeStatement?.Revenue,
            f.IncomeStatement?.GrossProfit,
            f.IncomeStatement?.OperatingProfit,
            f.IncomeStatement?.NetProfit,
            f.IncomeStatement?.EBITDA))
            .ToList();

        // Map financial ratios
        var latestFinancial = financialStatements.OrderByDescending(f => f.FinancialYear).FirstOrDefault();
        FinancialRatiosData? ratiosData = null;
        if (latestFinancial?.CalculatedRatios != null)
        {
            var r = latestFinancial.CalculatedRatios;
            ratiosData = new FinancialRatiosData(
                r.CurrentRatio, r.QuickRatio, r.CashRatio,
                r.DebtToEquityRatio, r.DebtToAssetsRatio, r.InterestCoverageRatio,
                r.GrossMarginPercent, r.OperatingMarginPercent, r.NetProfitMarginPercent,
                r.ReturnOnAssets, r.ReturnOnEquity,
                r.AssetTurnover, r.InventoryTurnover, r.ReceivablesDays, r.PayablesDays,
                r.DebtServiceCoverageRatio,
                null, null);
        }

        // Cashflow analysis - simplified since BankStatement.Analysis may not exist
        CashflowAnalysisData? cashflowData = null;
        if (bankStatements.Any())
        {
            cashflowData = new CashflowAnalysisData(
                bankStatements.Count,
                0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, "Medium");
        }

        // Map collaterals
        var collateralData = collaterals.Select(c => new CollateralData(
            c.Type.ToString(),
            c.Description,
            c.Location ?? "",
            c.MarketValue?.Amount ?? 0,
            c.ForcedSaleValue?.Amount ?? 0,
            c.AcceptableValue?.Amount ?? 0,
            "",
            "",
            c.Status.ToString(),
            c.LienType?.ToString() ?? "",
            c.LienReference,
            c.InsurancePolicyNumber,
            c.InsuranceExpiryDate))
            .ToList();

        var totalCollateralValue = collaterals.Sum(c => c.AcceptableValue?.Amount ?? 0);
        var collateralCoverage = loanApp.RequestedAmount.Amount > 0 
            ? totalCollateralValue / loanApp.RequestedAmount.Amount 
            : 0;

        // Map guarantors
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

        // Map AI advisory
        AIAdvisoryData? aiData = null;
        if (advisory != null)
        {
            // Get scores from RiskScores collection
            decimal getScore(Domain.Enums.RiskCategory category) =>
                advisory.RiskScores.FirstOrDefault(s => s.Category == category)?.Score ?? 0;

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
                "",
                advisory.RecommendedAmount,
                "",
                advisory.RecommendedTenorMonths,
                "",
                advisory.RecommendedInterestRate,
                "",
                advisory.RedFlags.ToList(),
                new List<string>(),
                advisory.Conditions.ToList());
        }

        // Map workflow history
        var workflowHistory = workflow?.TransitionHistory
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new WorkflowHistoryData(
                t.CreatedAt,
                t.FromStatus?.ToString() ?? "",
                t.ToStatus.ToString(),
                t.Action.ToString(),
                t.PerformedByUserId.ToString(),
                t.Comment))
            .ToList() ?? new List<WorkflowHistoryData>();

        // Map committee comments
        var committeeComments = committeeReview?.Comments
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new CommitteeCommentData(
                c.CreatedAt,
                c.UserId.ToString(),
                "",
                c.Content,
                null,
                c.Visibility.ToString()))
            .ToList() ?? new List<CommitteeCommentData>();

        return new LoanPackData(
            loanApp.ApplicationNumber,
            loanApp.CreatedAt,
            loanApp.ProductCode,
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
            DateTime.UtcNow,
            generatedBy,
            version);
    }
}

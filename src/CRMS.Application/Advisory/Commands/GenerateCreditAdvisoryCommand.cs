using CRMS.Application.Advisory.DTOs;
using CRMS.Application.Advisory.Interfaces;
using CRMS.Application.Common;
using CRMS.Domain.Aggregates.Advisory;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using CRMS.Domain.Services;

namespace CRMS.Application.Advisory.Commands;

public record GenerateCreditAdvisoryCommand(
    Guid LoanApplicationId,
    Guid GeneratedByUserId
) : IRequest<ApplicationResult<CreditAdvisoryDto>>;

public class GenerateCreditAdvisoryHandler : IRequestHandler<GenerateCreditAdvisoryCommand, ApplicationResult<CreditAdvisoryDto>>
{
    private readonly ICreditAdvisoryRepository _advisoryRepository;
    private readonly ILoanApplicationRepository _loanAppRepository;
    private readonly IFinancialStatementRepository _financialRepository;
    private readonly ICollateralRepository _collateralRepository;
    private readonly IGuarantorRepository _guarantorRepository;
    private readonly IBankStatementRepository _bankStatementRepository;
    private readonly IAIAdvisoryService _aiService;
    private readonly IUnitOfWork _unitOfWork;

    public GenerateCreditAdvisoryHandler(
        ICreditAdvisoryRepository advisoryRepository,
        ILoanApplicationRepository loanAppRepository,
        IFinancialStatementRepository financialRepository,
        ICollateralRepository collateralRepository,
        IGuarantorRepository guarantorRepository,
        IBankStatementRepository bankStatementRepository,
        IAIAdvisoryService aiService,
        IUnitOfWork unitOfWork)
    {
        _advisoryRepository = advisoryRepository;
        _loanAppRepository = loanAppRepository;
        _financialRepository = financialRepository;
        _collateralRepository = collateralRepository;
        _guarantorRepository = guarantorRepository;
        _bankStatementRepository = bankStatementRepository;
        _aiService = aiService;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<CreditAdvisoryDto>> Handle(GenerateCreditAdvisoryCommand request, CancellationToken ct = default)
    {
        // Get loan application with all parties
        var loanApp = await _loanAppRepository.GetByIdWithPartiesAsync(request.LoanApplicationId, ct);
        if (loanApp == null)
            return ApplicationResult<CreditAdvisoryDto>.Failure("Loan application not found");

        // Create advisory record
        var advisoryResult = CreditAdvisory.Create(
            request.LoanApplicationId,
            request.GeneratedByUserId,
            _aiService.GetModelVersion()
        );

        if (advisoryResult.IsFailure)
            return ApplicationResult<CreditAdvisoryDto>.Failure(advisoryResult.Error);

        var advisory = advisoryResult.Value;
        advisory.StartProcessing();

        try
        {
            // Gather all input data
            var aiRequest = await BuildAIRequest(loanApp, ct);

            // Call AI service
            var aiResponse = await _aiService.GenerateAdvisoryAsync(aiRequest, ct);

            if (!aiResponse.Success)
            {
                advisory.MarkFailed(aiResponse.ErrorMessage ?? "AI service failed");
                await _advisoryRepository.AddAsync(advisory, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return ApplicationResult<CreditAdvisoryDto>.Failure(aiResponse.ErrorMessage ?? "AI advisory generation failed");
            }

            // Map AI response to domain
            foreach (var scoreOutput in aiResponse.RiskScores)
            {
                if (Enum.TryParse<RiskCategory>(scoreOutput.Category, out var category))
                {
                    var riskScore = RiskScore.Create(
                        category,
                        scoreOutput.Score,
                        scoreOutput.Weight,
                        scoreOutput.Rationale,
                        scoreOutput.RedFlags,
                        scoreOutput.PositiveIndicators
                    );
                    advisory.AddRiskScore(riskScore);
                }
            }

            // Set recommendation
            if (Enum.TryParse<AdvisoryRecommendation>(aiResponse.Recommendation, out var recommendation))
            {
                advisory.SetRecommendation(
                    recommendation,
                    aiResponse.RecommendedAmount,
                    aiResponse.RecommendedTenorMonths,
                    aiResponse.RecommendedInterestRate,
                    aiResponse.MaxExposure
                );
            }

            // Add conditions and covenants
            foreach (var condition in aiResponse.Conditions)
                advisory.AddCondition(condition);

            foreach (var covenant in aiResponse.Covenants)
                advisory.AddCovenant(covenant);

            // Set analysis content
            advisory.SetAnalysisContent(
                aiResponse.ExecutiveSummary,
                aiResponse.StrengthsAnalysis,
                aiResponse.WeaknessesAnalysis,
                aiResponse.MitigatingFactors,
                aiResponse.KeyRisks
            );

            // Complete the advisory
            var completeResult = advisory.Complete();
            if (completeResult.IsFailure)
            {
                advisory.MarkFailed(completeResult.Error);
            }
        }
        catch (Exception ex)
        {
            advisory.MarkFailed($"Error generating advisory: {ex.Message}");
        }

        await _advisoryRepository.AddAsync(advisory, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<CreditAdvisoryDto>.Success(MapToDto(advisory));
    }

    private async Task<AIAdvisoryRequest> BuildAIRequest(
        Domain.Aggregates.LoanApplication.LoanApplication loanApp, 
        CancellationToken ct)
    {
        // Get financial statements
        var financials = await _financialRepository.GetByLoanApplicationIdAsync(loanApp.Id, ct);
        var financialInputs = financials
            .Where(f => f.Status == FinancialStatementStatus.Verified && f.CalculatedRatios != null)
            .Select(f => new FinancialDataInput(
                f.Id,
                f.FinancialYear,
                f.YearType.ToString(),
                f.BalanceSheet?.TotalAssets ?? 0,
                f.BalanceSheet?.TotalLiabilities ?? 0,
                f.BalanceSheet?.TotalEquity ?? 0,
                f.IncomeStatement?.TotalRevenue ?? 0,
                f.IncomeStatement?.NetProfit ?? 0,
                f.IncomeStatement?.EBITDA ?? 0,
                f.CalculatedRatios!.CurrentRatio,
                f.CalculatedRatios.QuickRatio,
                f.CalculatedRatios.DebtToEquityRatio,
                f.CalculatedRatios.InterestCoverageRatio,
                f.CalculatedRatios.DebtServiceCoverageRatio,
                f.CalculatedRatios.NetProfitMarginPercent,
                f.CalculatedRatios.ReturnOnEquity,
                f.CalculatedRatios.GetLiquidityAssessment(),
                f.CalculatedRatios.GetLeverageAssessment(),
                f.CalculatedRatios.GetProfitabilityAssessment(),
                f.CalculatedRatios.GetOverallAssessment()
            ))
            .ToList();

        // Get collateral
        var collaterals = await _collateralRepository.GetByLoanApplicationIdAsync(loanApp.Id, ct);
        var approvedCollaterals = collaterals.Where(c => c.Status == CollateralStatus.Approved).ToList();
        
        CollateralDataInput? collateralInput = null;
        if (approvedCollaterals.Any())
        {
            collateralInput = new CollateralDataInput(
                approvedCollaterals.Count,
                approvedCollaterals.Sum(c => c.MarketValue?.Amount ?? 0),
                approvedCollaterals.Sum(c => c.ForcedSaleValue?.Amount ?? 0),
                approvedCollaterals.Average(c => c.AcceptableValue?.Amount ?? 0) / loanApp.RequestedAmount.Amount * 100,
                approvedCollaterals.Select(c => c.Type.ToString()).Distinct().ToList(),
                approvedCollaterals.All(c => c.PerfectionStatus == PerfectionStatus.Perfected)
            );
        }

        // Get guarantors
        var guarantors = await _guarantorRepository.GetByLoanApplicationIdAsync(loanApp.Id, ct);
        var guarantorInputs = guarantors
            .Where(g => g.Status == GuarantorStatus.Approved)
            .Select(g => new GuarantorDataInput(
                g.Id,
                g.FullName,
                g.Type.ToString(),
                g.VerifiedNetWorth?.Amount ?? g.DeclaredNetWorth?.Amount ?? 0,
                g.GuaranteeLimit?.Amount ?? 0,
                g.CreditScore,
                g.Status.ToString()
            ))
            .ToList();

        // Build bureau data from parties (directors, signatories)
        var bureauInputs = new List<BureauDataInput>();
        
        var directors = loanApp.Parties.Where(p => p.PartyType == PartyType.Director);
        foreach (var director in directors)
        {
            // Bureau data would come from a separate bureau report lookup
            // For now, create placeholder - actual integration would fetch from BureauReport table
            bureauInputs.Add(new BureauDataInput(
                Guid.NewGuid(), // Would be actual BureauReportId
                director.FullName,
                "Director",
                null, // CreditScore - from bureau
                0, 0, 0, 0, 0, null,
                DateTime.UtcNow
            ));
        }

        var signatories = loanApp.Parties.Where(p => p.PartyType == PartyType.Signatory);
        foreach (var signatory in signatories)
        {
            bureauInputs.Add(new BureauDataInput(
                Guid.NewGuid(),
                signatory.FullName,
                "Signatory",
                null, 0, 0, 0, 0, 0, null,
                DateTime.UtcNow
            ));
        }

        // Get bank statements and perform aggregated cashflow analysis
        var bankStatements = await _bankStatementRepository.GetByLoanApplicationIdAsync(loanApp.Id, ct);
        CashflowDataInput? cashflowInput = null;
        
        if (bankStatements.Any())
        {
            var aggregatedAnalysisService = new AggregatedCashflowAnalysisService();
            var aggregatedResult = aggregatedAnalysisService.AnalyzeMultipleStatements(bankStatements);
            
            if (aggregatedResult.IsSuccess)
            {
                cashflowInput = new CashflowDataInput(
                    aggregatedResult.StatementSummaries.FirstOrDefault()?.StatementId ?? Guid.Empty,
                    aggregatedResult.TotalMonthsCovered,
                    aggregatedResult.WeightedTotalCredits / Math.Max(1, aggregatedResult.TotalMonthsCovered),
                    aggregatedResult.WeightedTotalDebits / Math.Max(1, aggregatedResult.TotalMonthsCovered),
                    aggregatedResult.WeightedNetMonthlyCashflow,
                    aggregatedResult.WeightedBalanceVolatility,
                    0, // RecurringCreditsCount - would need to aggregate from individual summaries
                    0, // RecurringDebitsCount
                    aggregatedResult.WeightedMonthlyObligations > 0 
                        ? aggregatedResult.WeightedMonthlyObligations / (aggregatedResult.DetectedMonthlySalary ?? aggregatedResult.WeightedTotalCredits / Math.Max(1, aggregatedResult.TotalMonthsCovered))
                        : 0,
                    aggregatedResult.HasRegularSalary,
                    aggregatedResult.CashflowHealthAssessment,
                    // Additional fields for AI analysis
                    aggregatedResult.HasInternalStatement,
                    aggregatedResult.ExternalStatementsCount,
                    aggregatedResult.AllExternalStatementsVerified,
                    aggregatedResult.OverallTrustScore,
                    aggregatedResult.TotalGamblingTransactions,
                    aggregatedResult.TotalGamblingAmount,
                    aggregatedResult.TotalBouncedTransactions,
                    aggregatedResult.MaxDaysWithNegativeBalance,
                    aggregatedResult.DetectedMonthlySalary,
                    aggregatedResult.SalarySource,
                    aggregatedResult.GetWarnings()
                );
            }
        }

        return new AIAdvisoryRequest(
            loanApp.Id,
            loanApp.RequestedAmount.Amount,
            loanApp.RequestedTenorMonths,
            loanApp.ProductCode ?? "Corporate Loan",
            loanApp.Purpose ?? "General Business",
            bureauInputs,
            financialInputs,
            cashflowInput,
            collateralInput,
            guarantorInputs,
            0, // ExistingExposure - to be fetched from CoreBanking
            0  // ExistingFacilitiesCount
        );
    }

    private static CreditAdvisoryDto MapToDto(CreditAdvisory advisory)
    {
        return new CreditAdvisoryDto(
            advisory.Id,
            advisory.LoanApplicationId,
            advisory.Status.ToString(),
            advisory.OverallScore,
            advisory.OverallRating.ToString(),
            advisory.Recommendation.ToString(),
            advisory.RiskScores.Select(s => new RiskScoreDto(
                s.Category.ToString(),
                s.Score,
                s.Weight,
                s.WeightedScore,
                s.Rating.ToString(),
                s.Rationale,
                s.RedFlags.ToList(),
                s.PositiveIndicators.ToList()
            )).ToList(),
            advisory.RecommendedAmount,
            advisory.RecommendedTenorMonths,
            advisory.RecommendedInterestRate,
            advisory.MaxExposure,
            advisory.Conditions.ToList(),
            advisory.Covenants.ToList(),
            advisory.ExecutiveSummary,
            advisory.StrengthsAnalysis,
            advisory.WeaknessesAnalysis,
            advisory.MitigatingFactors,
            advisory.KeyRisks,
            advisory.RedFlags.ToList(),
            advisory.HasCriticalRedFlags,
            advisory.ModelVersion,
            advisory.GeneratedAt,
            advisory.ErrorMessage
        );
    }
}

using CRMS.Application.Advisory.DTOs;
using CRMS.Application.Advisory.Interfaces;
using CRMS.Application.Common;
using CRMS.Domain.Aggregates.Advisory;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using CRMS.Domain.Services;
using System.Text.Json;

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
    private readonly IBureauReportRepository _bureauReportRepository;
    private readonly IFineractDirectService _fineractService;
    private readonly IAIAdvisoryService _aiService;
    private readonly IUnitOfWork _unitOfWork;

    public GenerateCreditAdvisoryHandler(
        ICreditAdvisoryRepository advisoryRepository,
        ILoanApplicationRepository loanAppRepository,
        IFinancialStatementRepository financialRepository,
        ICollateralRepository collateralRepository,
        IGuarantorRepository guarantorRepository,
        IBankStatementRepository bankStatementRepository,
        IBureauReportRepository bureauReportRepository,
        IFineractDirectService fineractService,
        IAIAdvisoryService aiService,
        IUnitOfWork unitOfWork)
    {
        _advisoryRepository = advisoryRepository;
        _loanAppRepository = loanAppRepository;
        _financialRepository = financialRepository;
        _collateralRepository = collateralRepository;
        _guarantorRepository = guarantorRepository;
        _bankStatementRepository = bankStatementRepository;
        _bureauReportRepository = bureauReportRepository;
        _fineractService = fineractService;
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
            else
            {
                // Serialize risk data to JSON so it survives DB round-trips
                var jsonOpts = new JsonSerializerOptions { PropertyNamingPolicy = null };
                var riskScoresJson = JsonSerializer.Serialize(
                    advisory.RiskScores.Select(s => new
                    {
                        Category = s.Category.ToString(),
                        s.Score,
                        s.Weight,
                        Rating = s.Rating.ToString(),
                        s.Rationale,
                        s.RedFlags,
                        s.PositiveIndicators
                    }), jsonOpts);
                var redFlagsJson = JsonSerializer.Serialize(advisory.RedFlags.ToList(), jsonOpts);
                var conditionsJson = JsonSerializer.Serialize(advisory.Conditions.ToList(), jsonOpts);
                var covenantsJson = JsonSerializer.Serialize(advisory.Covenants.ToList(), jsonOpts);
                advisory.SetPersistedData(riskScoresJson, redFlagsJson, conditionsJson, covenantsJson);
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
        // Get financial statements — include Verified and Submitted (with IsUnverified flag)
        var financials = await _financialRepository.GetByLoanApplicationIdAsync(loanApp.Id, ct);
        var financialInputs = financials
            .Where(f => (f.Status == FinancialStatementStatus.Verified || f.Status == FinancialStatementStatus.PendingReview)
                        && f.CalculatedRatios != null)
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
                f.CalculatedRatios.GetOverallAssessment(),
                IsUnverified: f.Status != FinancialStatementStatus.Verified
            ))
            .ToList();

        // Get collateral — include Approved and Valued (not yet approved) with separate counts
        var collaterals = await _collateralRepository.GetByLoanApplicationIdAsync(loanApp.Id, ct);
        var approvedCollaterals = collaterals.Where(c => c.Status == CollateralStatus.Approved).ToList();
        var valuedCollaterals = collaterals.Where(c => c.Status == CollateralStatus.Valued).ToList();
        var allUsableCollaterals = approvedCollaterals.Concat(valuedCollaterals).ToList();
        
        CollateralDataInput? collateralInput = null;
        if (allUsableCollaterals.Any())
        {
            collateralInput = new CollateralDataInput(
                allUsableCollaterals.Count,
                allUsableCollaterals.Sum(c => c.MarketValue?.Amount ?? 0),
                allUsableCollaterals.Sum(c => c.ForcedSaleValue?.Amount ?? 0),
                allUsableCollaterals.Average(c => c.AcceptableValue?.Amount ?? 0) / loanApp.RequestedAmount.Amount * 100,
                allUsableCollaterals.Select(c => c.Type.ToString()).Distinct().ToList(),
                approvedCollaterals.All(c => c.PerfectionStatus == PerfectionStatus.Perfected) && approvedCollaterals.Any(),
                ApprovedCount: approvedCollaterals.Count,
                ValuedButNotApprovedCount: valuedCollaterals.Count,
                ValuedButNotApprovedMarketValue: valuedCollaterals.Sum(c => c.MarketValue?.Amount ?? 0)
            );
        }

        // Get guarantors
        var guarantors = await _guarantorRepository.GetByLoanApplicationIdAsync(loanApp.Id, ct);
        var approvedGuarantors = guarantors.Where(g => g.Status == GuarantorStatus.Approved).ToList();

        // Build bureau data from actual BureauReport records persisted after credit checks
        var bureauInputs = new List<BureauDataInput>();
        var allBureauReports = await _bureauReportRepository.GetByLoanApplicationIdAsync(loanApp.Id, ct);

        // Index completed reports by PartyId for fast lookup
        var reportsByParty = allBureauReports
            .Where(r => r.Status == BureauReportStatus.Completed && r.PartyId.HasValue)
            .GroupBy(r => r.PartyId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.ReportDate ?? r.CompletedAt ?? r.RequestedAt).First());

        // Index corporate report (no PartyId, SubjectType = Business)
        var corporateBureauReport = allBureauReports
            .Where(r => r.Status == BureauReportStatus.Completed && r.SubjectType == SubjectType.Business)
            .OrderByDescending(r => r.ReportDate ?? r.CompletedAt ?? r.RequestedAt)
            .FirstOrDefault();

        // Map individual party reports
        foreach (var party in loanApp.Parties)
        {
            var subjectType = party.PartyType.ToString(); // Director, Signatory, Guarantor

            if (party.Id != Guid.Empty && reportsByParty.TryGetValue(party.Id, out var report))
            {
                bureauInputs.Add(MapBureauReport(report, party.FullName, subjectType));
            }
            else
            {
                // No bureau data for this party — include as placeholder so AI knows the gap
                bureauInputs.Add(new BureauDataInput(
                    Guid.NewGuid(),
                    party.FullName,
                    subjectType,
                    CreditScore: null,
                    ActiveLoansCount: 0,
                    TotalOutstandingDebt: 0,
                    PerformingLoansCount: 0,
                    DelinquentLoansCount: 0,
                    DefaultedLoansCount: 0,
                    WorstStatus: null,
                    ReportDate: DateTime.UtcNow,
                    IsPlaceholder: true
                ));
            }
        }

        // Add corporate bureau entry if available
        if (corporateBureauReport != null)
        {
            bureauInputs.Add(MapBureauReport(corporateBureauReport, loanApp.CustomerName, "Corporate"));
        }

        // Build guarantor inputs with bureau report availability flag
        var guarantorInputs = approvedGuarantors
            .Select(g =>
            {
                // Check if this guarantor has a corresponding party with a bureau report
                var guarantorParty = loanApp.Parties.FirstOrDefault(p =>
                    p.PartyType == PartyType.Guarantor &&
                    string.Equals(p.FullName, g.FullName, StringComparison.OrdinalIgnoreCase));
                var hasBureau = guarantorParty != null
                    && guarantorParty.Id != Guid.Empty
                    && reportsByParty.ContainsKey(guarantorParty.Id);

                return new GuarantorDataInput(
                    g.Id,
                    g.FullName,
                    g.Type.ToString(),
                    g.VerifiedNetWorth?.Amount ?? g.DeclaredNetWorth?.Amount ?? 0,
                    g.GuaranteeLimit?.Amount ?? 0,
                    g.CreditScore,
                    g.Status.ToString(),
                    HasBureauReport: hasBureau
                );
            })
            .ToList();

        // Get bank statements and perform aggregated cashflow analysis
        var bankStatements = await _bankStatementRepository.GetByLoanApplicationIdAsync(loanApp.Id, ct);
        CashflowDataInput? cashflowInput = null;
        
        if (bankStatements.Any())
        {
            var aggregatedAnalysisService = new AggregatedCashflowAnalysisService();
            var aggregatedResult = aggregatedAnalysisService.AnalyzeMultipleStatements(bankStatements);
            
            if (aggregatedResult.IsSuccess)
            {
                // Aggregate recurring transaction counts from all bank statements
                var recurringCredits = bankStatements
                    .SelectMany(s => s.Transactions)
                    .Count(t => t.IsRecurring && t.Type == Domain.Enums.StatementTransactionType.Credit);
                var recurringDebits = bankStatements
                    .SelectMany(s => s.Transactions)
                    .Count(t => t.IsRecurring && t.Type == Domain.Enums.StatementTransactionType.Debit);

                cashflowInput = new CashflowDataInput(
                    aggregatedResult.StatementSummaries.FirstOrDefault()?.StatementId ?? Guid.Empty,
                    aggregatedResult.TotalMonthsCovered,
                    aggregatedResult.WeightedTotalCredits / Math.Max(1, aggregatedResult.TotalMonthsCovered),
                    aggregatedResult.WeightedTotalDebits / Math.Max(1, aggregatedResult.TotalMonthsCovered),
                    aggregatedResult.WeightedNetMonthlyCashflow,
                    aggregatedResult.WeightedBalanceVolatility,
                    recurringCredits,
                    recurringDebits,
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

        // Derive existing exposure: Fineract live data first, fall back to bureau report
        var existingExposure = corporateBureauReport?.TotalOutstandingBalance ?? 0m;
        var existingFacilitiesCount = corporateBureauReport?.ActiveLoans ?? 0;
        if (long.TryParse(loanApp.CustomerId, out var fineractClientId))
        {
            var exposureResult = await _fineractService.GetCustomerExposureAsync(
                fineractClientId, loanApp.AccountNumber, loanApp.CustomerName, ct);
            if (exposureResult.IsSuccess)
            {
                existingExposure = exposureResult.Value.TotalOutstandingBalance;
                existingFacilitiesCount = exposureResult.Value.ActiveFacilitiesCount;
            }
        }

        return new AIAdvisoryRequest(
            loanApp.Id,
            loanApp.RequestedAmount.Amount,
            loanApp.RequestedTenorMonths,
            loanApp.ProductCode ?? "Corporate Loan",
            loanApp.IndustrySector ?? "General Business",
            bureauInputs,
            financialInputs,
            cashflowInput,
            collateralInput,
            guarantorInputs,
            existingExposure,
            existingFacilitiesCount
        );
    }

    private static BureauDataInput MapBureauReport(
        Domain.Aggregates.CreditBureau.BureauReport report,
        string subjectName,
        string subjectType)
    {
        // Derive worst status from delinquency days
        var worstStatus = report.MaxDelinquencyDays switch
        {
            0 => report.DelinquentFacilities > 0 ? "Non-Performing" : "Performing",
            < 30 => "Overdue",
            < 90 => "Watch",
            _ => "Non-Performing"
        };

        return new BureauDataInput(
            ReportId: report.Id,
            SubjectName: subjectName,
            SubjectType: subjectType,
            CreditScore: report.CreditScore,
            ActiveLoansCount: report.ActiveLoans,
            TotalOutstandingDebt: report.TotalOutstandingBalance,
            PerformingLoansCount: report.PerformingAccounts,
            DelinquentLoansCount: report.DelinquentFacilities,
            DefaultedLoansCount: report.MaxDelinquencyDays >= 90 && report.DelinquentFacilities > 0 ? report.DelinquentFacilities : 0,
            WorstStatus: worstStatus,
            ReportDate: report.ReportDate ?? report.CompletedAt ?? report.RequestedAt,
            MaxDelinquencyDays: report.MaxDelinquencyDays,
            HasLegalActions: report.HasLegalActions,
            TotalOverdue: report.TotalOverdue,
            FraudRiskScore: report.FraudRiskScore,
            FraudRecommendation: report.FraudRecommendation
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

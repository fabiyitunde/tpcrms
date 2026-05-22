using CRMS.Application.Advisory.Interfaces;
using CRMS.Application.Common;
using CRMS.Application.Namp.DTOs;
using CRMS.Domain.Aggregates.CreditBureau;
using CRMS.Domain.Aggregates.Namp;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using System.Text.Json;

namespace CRMS.Application.Namp.Commands;

public record GenerateNampAdvisoryCommand(
    Guid NampApplicationId,
    Guid GeneratedByUserId
) : IRequest<ApplicationResult<NampAdvisoryDto>>;

public class GenerateNampAdvisoryHandler
    : IRequestHandler<GenerateNampAdvisoryCommand, ApplicationResult<NampAdvisoryDto>>
{
    private readonly INampApplicationRepository _nampRepo;
    private readonly INampAdvisoryRepository _advisoryRepo;
    private readonly INampViabilityScoreConfigRepository _viabilityConfigRepo;
    private readonly IBureauReportRepository _bureauRepo;
    private readonly IAIAdvisoryService _aiService;
    private readonly IUnitOfWork _unitOfWork;

    public GenerateNampAdvisoryHandler(
        INampApplicationRepository nampRepo,
        INampAdvisoryRepository advisoryRepo,
        INampViabilityScoreConfigRepository viabilityConfigRepo,
        IBureauReportRepository bureauRepo,
        IAIAdvisoryService aiService,
        IUnitOfWork unitOfWork)
    {
        _nampRepo = nampRepo;
        _advisoryRepo = advisoryRepo;
        _viabilityConfigRepo = viabilityConfigRepo;
        _bureauRepo = bureauRepo;
        _aiService = aiService;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<NampAdvisoryDto>> Handle(
        GenerateNampAdvisoryCommand request, CancellationToken ct = default)
    {
        // Load application with full details (guarantors, collaterals, financial statements)
        var app = await _nampRepo.GetByIdWithDetailsAsync(request.NampApplicationId, ct);
        if (app is null)
            return ApplicationResult<NampAdvisoryDto>.Failure("NAMP application not found.");

        var techReport = await _nampRepo.GetTechnicalAppraisalReportAsync(request.NampApplicationId, ct);
        var finReport = await _nampRepo.GetFinancialAppraisalReportAsync(request.NampApplicationId, ct);

        // Technical appraisal must exist — we need the viability rating
        if (techReport is null)
            return ApplicationResult<NampAdvisoryDto>.Failure(
                "Technical Appraisal report not found. The Agricultural Engineer must complete the report before generating an advisory.");

        // Load bureau reports
        var bureauReports = await _bureauRepo.GetByNampApplicationIdAsync(request.NampApplicationId, ct);
        var completedBureauReports = bureauReports
            .Where(r => r.Status == BureauReportStatus.Completed)
            .ToList();

        if (completedBureauReports.Count == 0)
            return ApplicationResult<NampAdvisoryDto>.Failure(
                "No completed bureau reports found. Bureau checks must be run before generating an advisory.");

        // Look up viability score config for the engineer's rating
        var viabilityConfig = await _viabilityConfigRepo.GetByRatingAsync(techReport.OverallViabilityRating, ct);
        if (viabilityConfig is null)
            return ApplicationResult<NampAdvisoryDto>.Failure(
                $"No viability score configuration found for rating '{techReport.OverallViabilityRating}'. " +
                "Please configure the NAMP Viability Score settings in Admin.");

        // Create advisory record
        var advisoryResult = NampAdvisory.Create(
            request.NampApplicationId,
            request.GeneratedByUserId,
            _aiService.GetModelVersion());

        if (advisoryResult.IsFailure)
            return ApplicationResult<NampAdvisoryDto>.Failure(advisoryResult.Error);

        var advisory = advisoryResult.Value;
        advisory.StartProcessing();
        advisory.SetTechnicalViability(
            techReport.OverallViabilityRating.ToString(),
            viabilityConfig.Score,
            viabilityConfig.CategoryWeight);

        try
        {
            var aiRequest = BuildAIRequest(app, techReport, finReport, completedBureauReports);
            var aiResponse = await _aiService.GenerateAdvisoryAsync(aiRequest, ct);

            if (!aiResponse.Success)
            {
                advisory.MarkFailed(aiResponse.ErrorMessage ?? "AI service failed");
                await _advisoryRepo.AddAsync(advisory, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return ApplicationResult<NampAdvisoryDto>.Failure(aiResponse.ErrorMessage ?? "Advisory generation failed");
            }

            // Build the full list of scores: AI result (4 standard categories) + TechnicalViability
            var allScores = aiResponse.RiskScores.ToList();
            var techViabilityScore = new RiskScoreOutput(
                "TechnicalViability",
                viabilityConfig.Score,
                viabilityConfig.CategoryWeight,
                NampAdvisory.DetermineRating(viabilityConfig.Score).ToString(),
                $"Agricultural Engineer rated the application as {techReport.OverallViabilityRating}. " +
                (string.IsNullOrWhiteSpace(techReport.SummaryNotes) ? "" : techReport.SummaryNotes),
                string.IsNullOrWhiteSpace(techReport.RisksIdentified)
                    ? []
                    : [techReport.RisksIdentified],
                techReport.EngineerRecommendation == NampTechnicalRecommendation.Pass
                    ? [$"Engineer recommended Pass — {techReport.ProposedEquipmentSuitability}"]
                    : []
            );
            allScores.Add(techViabilityScore);

            // Compute overall score (weighted average across all 5 categories)
            var totalWeight = allScores.Sum(s => s.Weight);
            var overallScore = totalWeight > 0
                ? Math.Round(allScores.Sum(s => s.Score * s.Weight) / totalWeight, 2)
                : 0m;
            var overallRating = NampAdvisory.DetermineRating(overallScore);

            // Aggregate red flags
            var allRedFlags = aiResponse.RedFlags.ToList();
            if (!string.IsNullOrWhiteSpace(techReport.RisksIdentified) &&
                techReport.OverallViabilityRating == NampViabilityRating.NotViable)
            {
                allRedFlags.Add($"Technical Viability: {techReport.RisksIdentified}");
            }
            var hasCriticalRedFlags = allRedFlags.Count >= 3 ||
                allScores.Any(s => NampAdvisory.DetermineRating(s.Score) == RiskRating.VeryHigh);

            // Map AI recommendation
            if (Enum.TryParse<AdvisoryRecommendation>(aiResponse.Recommendation, out var recommendation))
            {
                advisory.SetRecommendation(
                    recommendation,
                    aiResponse.RecommendedAmount,
                    aiResponse.RecommendedTenorMonths,
                    aiResponse.RecommendedInterestRate);
            }

            advisory.SetAnalysisContent(
                aiResponse.ExecutiveSummary,
                aiResponse.StrengthsAnalysis,
                aiResponse.WeaknessesAnalysis,
                aiResponse.MitigatingFactors,
                aiResponse.KeyRisks);

            // Serialize to JSON for persistence
            var jsonOpts = new JsonSerializerOptions { PropertyNamingPolicy = null };
            var riskScoresJson = JsonSerializer.Serialize(allScores.Select(s => new
            {
                s.Category,
                s.Score,
                s.Weight,
                WeightedScore = s.Score * s.Weight / Math.Max(1, totalWeight),
                Rating = NampAdvisory.DetermineRating(s.Score).ToString(),
                s.Rationale,
                s.RedFlags,
                s.PositiveIndicators,
            }), jsonOpts);

            advisory.SetPersistedData(
                overallScore,
                overallRating,
                hasCriticalRedFlags,
                riskScoresJson,
                JsonSerializer.Serialize(allRedFlags, jsonOpts),
                JsonSerializer.Serialize(aiResponse.Conditions, jsonOpts),
                JsonSerializer.Serialize(aiResponse.Covenants, jsonOpts));
        }
        catch (Exception ex)
        {
            advisory.MarkFailed($"Error generating advisory: {ex.Message}");
        }

        advisory.SetAuditInfo(request.GeneratedByUserId.ToString(), isNew: true);
        await _advisoryRepo.AddAsync(advisory, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<NampAdvisoryDto>.Success(MapToDto(advisory));
    }

    // ── AI Request Builder ─────────────────────────────────────────────────

    private static AIAdvisoryRequest BuildAIRequest(
        NampApplication app,
        NampTechnicalAppraisalReport techReport,
        NampFinancialAppraisalReport? finReport,
        List<BureauReport> bureauReports)
    {
        var requestedAmount = app.LoanAmount ?? app.EquipmentValue;
        var tenorMonths = app.RequestedTenorMonths ?? 36;

        // Bureau data
        var bureauInputs = bureauReports.Select(r => new BureauDataInput(
            r.Id,
            r.SubjectName ?? app.ApplicantName,
            r.PartyType ?? "NampApplicant",
            r.CreditScore,
            r.ActiveLoans,
            r.TotalOutstandingBalance,
            r.PerformingAccounts,
            r.DelinquentFacilities,
            r.MaxDelinquencyDays >= 90 && r.DelinquentFacilities > 0 ? r.DelinquentFacilities : 0,
            r.MaxDelinquencyDays switch
            {
                0 => r.DelinquentFacilities > 0 ? "Non-Performing" : "Performing",
                < 30 => "Overdue",
                < 90 => "Watch",
                _ => "Non-Performing",
            },
            r.ReportDate ?? r.CompletedAt ?? r.RequestedAt,
            r.MaxDelinquencyDays,
            r.HasLegalActions,
            r.TotalOverdue,
            r.FraudRiskScore,
            r.FraudRecommendation
        )).ToList();

        // Financial data — AgroServiceCompany uses NampFinancialStatements; individuals use simplified
        var financialInputs = new List<FinancialDataInput>();

        if (app.FinancialStatements.Any())
        {
            foreach (var fs in app.FinancialStatements.OrderByDescending(f => f.FinancialYear))
            {
                var totalAssets = fs.TotalAssets ?? 0;
                var totalLiabilities = fs.TotalLiabilities ?? 0;
                var totalEquity = fs.TotalEquity ?? 0;
                var revenue = fs.Revenue ?? 0;
                var netProfit = fs.NetProfit ?? 0;
                var ebitda = fs.Ebitda ?? 0;
                var currentAssets = fs.TotalCurrentAssets ?? 0;
                var currentLiabilities = fs.TotalCurrentLiabilities ?? 0;

                var currentRatio = currentLiabilities > 0 ? currentAssets / currentLiabilities : 0;
                var debtToEquity = totalEquity > 0 ? totalLiabilities / totalEquity : 0;
                var netMargin = revenue > 0 ? (netProfit / revenue) * 100 : 0;

                financialInputs.Add(new FinancialDataInput(
                    fs.Id,
                    fs.FinancialYear,
                    fs.FinancialYearType,
                    totalAssets,
                    totalLiabilities,
                    totalEquity,
                    revenue,
                    netProfit,
                    ebitda,
                    currentRatio,
                    QuickRatio: currentRatio,    // approximation
                    debtToEquity,
                    InterestCoverageRatio: ebitda > 0 && (fs.InterestExpense ?? 0) > 0
                        ? ebitda / fs.InterestExpense!.Value : 0,
                    DebtServiceCoverageRatio: finReport?.DebtServiceCoverageRatio ?? 0,
                    netMargin,
                    ReturnOnEquity: totalEquity > 0 ? (netProfit / totalEquity) * 100 : 0,
                    LiquidityAssessment: currentRatio >= 1.5m ? "Good" : currentRatio >= 1.0m ? "Adequate" : "Weak",
                    LeverageAssessment: debtToEquity <= 1.0m ? "Low" : debtToEquity <= 2.5m ? "Moderate" : "High",
                    ProfitabilityAssessment: netMargin >= 10 ? "Strong" : netMargin >= 5 ? "Moderate" : "Weak",
                    OverallAssessment: "See individual ratios",
                    IsUnverified: false
                ));
            }
        }
        else if (app.EstimatedMonthlyIncome.HasValue)
        {
            // Individual applicant — build a simplified financial snapshot
            var annualIncome = app.EstimatedMonthlyIncome.Value * 12;
            var otherIncome = (app.OtherMonthlyIncome ?? 0) * 12;
            var annualExpenses = (app.MonthlyLivingExpenses ?? 0) * 12;
            var dscr = finReport?.DebtServiceCoverageRatio ?? 0;

            financialInputs.Add(new FinancialDataInput(
                Guid.NewGuid(),
                DateTime.UtcNow.Year,
                "Individual",
                TotalAssets: app.EstimatedNetWorth ?? 0,
                TotalLiabilities: 0,
                TotalEquity: app.EstimatedNetWorth ?? 0,
                Revenue: annualIncome + otherIncome,
                NetProfit: annualIncome + otherIncome - annualExpenses,
                EBITDA: annualIncome + otherIncome - annualExpenses,
                CurrentRatio: 0,
                QuickRatio: 0,
                DebtToEquityRatio: 0,
                InterestCoverageRatio: 0,
                DebtServiceCoverageRatio: dscr,
                NetProfitMarginPercent: annualIncome > 0
                    ? ((annualIncome + otherIncome - annualExpenses) / annualIncome) * 100 : 0,
                ReturnOnEquity: 0,
                LiquidityAssessment: "N/A (Individual)",
                LeverageAssessment: "N/A (Individual)",
                ProfitabilityAssessment: dscr >= 1.5m ? "Adequate" : dscr >= 1.0m ? "Marginal" : "Insufficient",
                OverallAssessment: "Individual applicant — income-based assessment",
                IsUnverified: false
            ));
        }

        // Collateral
        CollateralDataInput? collateralInput = null;
        var collaterals = app.Collaterals.ToList();
        if (collaterals.Any())
        {
            collateralInput = new CollateralDataInput(
                collaterals.Count,
                collaterals.Sum(c => c.MarketValue ?? 0),
                collaterals.Sum(c => c.ForcedSaleValue ?? 0),
                requestedAmount > 0 && collaterals.Sum(c => c.MarketValue ?? 0) > 0
                    ? (requestedAmount / collaterals.Sum(c => c.MarketValue ?? 0)) * 100 : 0,
                collaterals.Select(c => c.CollateralType).Distinct().ToList(),
                HasPerfectedLiens: collaterals.Any(c => !string.IsNullOrWhiteSpace(c.LienReference))
            );
        }

        // Guarantors
        var guarantorInputs = app.Guarantors.Select(g => new GuarantorDataInput(
            g.Id,
            g.FullName,
            g.GuarantorType,
            g.DeclaredNetWorth ?? 0,
            g.GuaranteeLimit ?? 0,
            CreditScore: null,
            CreditStatus: "Unknown",
            HasBureauReport: false
        )).ToList();

        // Build additional context from technical appraisal
        var additionalContext = BuildTechnicalAppraisalContext(app, techReport, finReport);

        return new AIAdvisoryRequest(
            app.Id,
            requestedAmount,
            tenorMonths,
            $"NAMP - {app.ApplicantCategory}",
            app.IndustrySector ?? "Agriculture",
            bureauInputs,
            financialInputs,
            CashflowAnalysis: null,   // NAMP doesn't use bank statements
            collateralInput,
            guarantorInputs,
            ExistingExposure: 0,
            ExistingFacilitiesCount: 0,
            additionalContext
        );
    }

    private static string BuildTechnicalAppraisalContext(
        NampApplication app,
        NampTechnicalAppraisalReport techReport,
        NampFinancialAppraisalReport? finReport)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"NAMP Application — {app.ApplicantCategory}");
        sb.AppendLine($"Equipment: {app.EquipmentDescription} (Value: NGN {app.EquipmentValue:N0})");
        sb.AppendLine($"Equity: {app.EquityPercent:N1}% (NGN {app.EquityAmount:N0})");

        sb.AppendLine();
        sb.AppendLine("TECHNICAL APPRAISAL SUMMARY:");
        sb.AppendLine($"  Overall Viability: {techReport.OverallViabilityRating}");
        sb.AppendLine($"  Engineer Recommendation: {techReport.EngineerRecommendation}");
        sb.AppendLine($"  Equipment Suitability: {techReport.ProposedEquipmentSuitability}");

        if (!string.IsNullOrWhiteSpace(techReport.RisksIdentified))
            sb.AppendLine($"  Risks Identified: {techReport.RisksIdentified}");
        if (!string.IsNullOrWhiteSpace(techReport.RecommendedMitigations))
            sb.AppendLine($"  Mitigations: {techReport.RecommendedMitigations}");
        if (!string.IsNullOrWhiteSpace(techReport.SummaryNotes))
            sb.AppendLine($"  Summary: {techReport.SummaryNotes}");

        if (finReport != null)
        {
            sb.AppendLine();
            sb.AppendLine("CREDIT OFFICER FINANCIAL APPRAISAL:");
            if (finReport.MonthlyDisposableIncome.HasValue)
                sb.AppendLine($"  Monthly Disposable Income: NGN {finReport.MonthlyDisposableIncome:N0}");
            if (finReport.DebtServiceCoverageRatio.HasValue)
                sb.AppendLine($"  DSCR: {finReport.DebtServiceCoverageRatio:N2}x");
            if (finReport.LoanToValueRatio.HasValue)
                sb.AppendLine($"  LTV: {finReport.LoanToValueRatio:N1}%");
            sb.AppendLine($"  Repayment Capacity: {finReport.RepaymentCapacityRating}");
            sb.AppendLine($"  Credit Officer Recommendation: {finReport.CreditOfficerRecommendation}");
            if (!string.IsNullOrWhiteSpace(finReport.SummaryNotes))
                sb.AppendLine($"  Notes: {finReport.SummaryNotes}");
        }

        return sb.ToString();
    }

    // ── DTO Mapper ─────────────────────────────────────────────────────────

    internal static NampAdvisoryDto MapToDto(NampAdvisory advisory)
    {
        var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        List<NampAdvisoryRiskScoreDto> riskScores = [];
        if (!string.IsNullOrEmpty(advisory.RiskScoresJson))
        {
            try
            {
                var raw = JsonSerializer.Deserialize<List<JsonElement>>(advisory.RiskScoresJson, jsonOpts);
                if (raw != null)
                {
                    riskScores = raw.Select(e => new NampAdvisoryRiskScoreDto(
                        e.TryGetProperty("Category", out var cat) ? cat.GetString() ?? "" : "",
                        e.TryGetProperty("Score", out var sc) ? sc.GetDecimal() : 0,
                        e.TryGetProperty("Weight", out var wt) ? wt.GetDecimal() : 0,
                        e.TryGetProperty("WeightedScore", out var ws) ? ws.GetDecimal() : 0,
                        e.TryGetProperty("Rating", out var rt) ? rt.GetString() ?? "" : "",
                        e.TryGetProperty("Rationale", out var ra) ? ra.GetString() ?? "" : "",
                        e.TryGetProperty("RedFlags", out var rf) ? rf.Deserialize<List<string>>(jsonOpts) ?? [] : [],
                        e.TryGetProperty("PositiveIndicators", out var pi) ? pi.Deserialize<List<string>>(jsonOpts) ?? [] : []
                    )).ToList();
                }
            }
            catch { /* deserialization error — return empty list */ }
        }

        var redFlags = DeserializeList(advisory.RedFlagsJson, jsonOpts);
        var conditions = DeserializeList(advisory.ConditionsJson, jsonOpts);
        var covenants = DeserializeList(advisory.CovenantsJson, jsonOpts);

        return new NampAdvisoryDto(
            advisory.Id,
            advisory.NampApplicationId,
            advisory.Status.ToString(),
            advisory.OverallScore,
            advisory.OverallRating.ToString(),
            advisory.Recommendation.ToString(),
            advisory.RecommendedAmount,
            advisory.RecommendedTenorMonths,
            advisory.RecommendedInterestRate,
            advisory.ExecutiveSummary,
            advisory.StrengthsAnalysis,
            advisory.WeaknessesAnalysis,
            advisory.MitigatingFactors,
            advisory.KeyRisks,
            advisory.TechnicalViabilityRating,
            advisory.TechnicalViabilityScore,
            advisory.HasCriticalRedFlags,
            advisory.ModelVersion,
            advisory.GeneratedAt,
            advisory.ErrorMessage,
            riskScores,
            redFlags,
            conditions,
            covenants
        );
    }

    private static List<string> DeserializeList(string? json, JsonSerializerOptions opts)
    {
        if (string.IsNullOrEmpty(json)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(json, opts) ?? []; }
        catch { return []; }
    }
}

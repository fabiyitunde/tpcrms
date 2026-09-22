using System.Text.Json;
using CRMS.Application.Advisory.Interfaces;
using CRMS.Application.Common;
using CRMS.Application.Rhshf.DTOs;
using CRMS.Domain.Aggregates.CreditBureau;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>
/// Generates an AI advisory for a RH-SHF case, using the product-agnostic IAIAdvisoryService that
/// Corporate and NAMP already share (rule-based scoring plus an optional LLM narrative).
///
/// The request is assembled from the case's real data: one BureauDataInput per checked subject
/// (company + directors, Phase B), and a FinancialDataInput derived from the saved crop-economics
/// appraisal (Phase C). Both were previously stubbed — a single synthesised bureau input with no
/// score, and an empty financial list — which meant the advisory's credit-history and
/// repayment-capacity scores were computed from nothing.
///
/// Generation is deliberately not gated on the appraisal existing: an officer may reasonably want
/// the advisory before modelling the case. Where data is genuinely absent the request says so
/// explicitly rather than passing zeros that read as good news.
/// </summary>
public record GenerateRhshfAdvisoryCommand(string Reference, Guid GeneratedByUserId) : IRequest<ApplicationResult<RhshfAdvisoryDto>>;

public class GenerateRhshfAdvisoryHandler : IRequestHandler<GenerateRhshfAdvisoryCommand, ApplicationResult<RhshfAdvisoryDto>>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfAdvisoryRepository _advisoryRepo;
    private readonly IRhshfCollateralRepository _collateralRepo;
    private readonly IRhshfEligibilityCheckRepository _eligibilityRepo;
    private readonly IBureauReportRepository _bureauRepo;
    private readonly IAIAdvisoryService _aiService;
    private readonly IUnitOfWork _uow;

    public GenerateRhshfAdvisoryHandler(
        IRhshfCreditProfileRepository repo, IRhshfAdvisoryRepository advisoryRepo,
        IRhshfCollateralRepository collateralRepo, IRhshfEligibilityCheckRepository eligibilityRepo,
        IBureauReportRepository bureauRepo,
        IAIAdvisoryService aiService, IUnitOfWork uow)
    {
        _repo = repo;
        _advisoryRepo = advisoryRepo;
        _collateralRepo = collateralRepo;
        _eligibilityRepo = eligibilityRepo;
        _bureauRepo = bureauRepo;
        _aiService = aiService;
        _uow = uow;
    }

    public async Task<ApplicationResult<RhshfAdvisoryDto>> Handle(GenerateRhshfAdvisoryCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult<RhshfAdvisoryDto>.Failure("Case not found.");

        if (profile.BureauCheckOutcome == RhshfBureauOutcome.NotRun)
            return ApplicationResult<RhshfAdvisoryDto>.Failure("The credit bureau check must run before generating an advisory.");

        var collateral = await _collateralRepo.GetByProfileAndCycleAsync(profile.Id, profile.CurrentCycleNumber, ct);
        var eligibility = await _eligibilityRepo.GetByProfileAndCycleAsync(profile.Id, profile.CurrentCycleNumber, ct);
        var bureauReports = await _bureauRepo.GetByRhshfCreditProfileIdAsync(profile.Id, ct);
        var appraisal = profile.GetCurrentCycleFinancialAppraisal();
        var farmPlans = profile.GetCurrentCycleFarmPlans();

        var advisoryResult = RhshfAdvisory.Create(profile.Id, request.GeneratedByUserId, _aiService.GetModelVersion());
        if (advisoryResult.IsFailure)
            return ApplicationResult<RhshfAdvisoryDto>.Failure(advisoryResult.Error);

        var advisory = advisoryResult.Value;
        advisory.StartProcessing();

        try
        {
            var aiRequest = BuildAIRequest(profile, bureauReports, appraisal, farmPlans, collateral, eligibility);
            var aiResponse = await _aiService.GenerateAdvisoryAsync(aiRequest, ct);

            if (!aiResponse.Success)
            {
                advisory.MarkFailed(aiResponse.ErrorMessage ?? "AI service failed");
                advisory.SetAuditInfo(request.GeneratedByUserId.ToString(), isNew: true);
                await _advisoryRepo.AddAsync(advisory, ct);
                await _uow.SaveChangesAsync(ct);
                return ApplicationResult<RhshfAdvisoryDto>.Failure(aiResponse.ErrorMessage ?? "Advisory generation failed");
            }

            var allScores = aiResponse.RiskScores.ToList();
            var totalWeight = allScores.Sum(s => s.Weight);
            var overallScore = totalWeight > 0 ? Math.Round(allScores.Sum(s => s.Score * s.Weight) / totalWeight, 2) : 0m;
            var overallRating = RhshfAdvisory.DetermineRating(overallScore);

            var allRedFlags = aiResponse.RedFlags.ToList();
            var hasCriticalRedFlags = allRedFlags.Count >= 3
                || allScores.Any(s => RhshfAdvisory.DetermineRating(s.Score) == RiskRating.VeryHigh);

            if (Enum.TryParse<AdvisoryRecommendation>(aiResponse.Recommendation, out var recommendation))
                advisory.SetRecommendation(recommendation, aiResponse.RecommendedAmount);

            advisory.SetAnalysisContent(
                aiResponse.ExecutiveSummary, aiResponse.StrengthsAnalysis, aiResponse.WeaknessesAnalysis,
                aiResponse.MitigatingFactors, aiResponse.KeyRisks);

            var jsonOpts = new JsonSerializerOptions { PropertyNamingPolicy = null };
            var riskScoresJson = JsonSerializer.Serialize(allScores.Select(s => new
            {
                s.Category,
                s.Score,
                s.Weight,
                WeightedScore = s.Score * s.Weight / Math.Max(1, totalWeight),
                Rating = RhshfAdvisory.DetermineRating(s.Score).ToString(),
                s.Rationale,
                s.RedFlags,
                s.PositiveIndicators,
            }), jsonOpts);

            advisory.SetPersistedData(
                overallScore, overallRating, hasCriticalRedFlags,
                riskScoresJson, JsonSerializer.Serialize(allRedFlags, jsonOpts),
                JsonSerializer.Serialize(aiResponse.Conditions, jsonOpts), JsonSerializer.Serialize(aiResponse.Covenants, jsonOpts));
        }
        catch (Exception ex)
        {
            advisory.MarkFailed($"Error generating advisory: {ex.Message}");
        }

        // Each generation is a new, append-only row — regenerating keeps prior advisories around as
        // an audit trail; reads always take the most recent (GetByRhshfCreditProfileIdAsync orders
        // by GeneratedAt descending).
        advisory.SetAuditInfo(request.GeneratedByUserId.ToString(), isNew: true);
        await _advisoryRepo.AddAsync(advisory, ct);
        await _uow.SaveChangesAsync(ct);

        return ApplicationResult<RhshfAdvisoryDto>.Success(MapToDto(advisory));
    }

    private static AIAdvisoryRequest BuildAIRequest(
        RhshfCreditProfile profile,
        IReadOnlyList<BureauReport> bureauReports,
        RhshfFinancialAppraisalReport? appraisal,
        IReadOnlyList<RhshfFarmPlan> farmPlans,
        IReadOnlyList<RhshfCollateral> collateral,
        IReadOnlyList<RhshfEligibilityCheck> eligibility)
    {
        var completedReports = bureauReports.Where(r => r.Status == BureauReportStatus.Completed).ToList();

        return new AIAdvisoryRequest(
            LoanApplicationId: profile.Id,
            RequestedAmount: profile.TotalEopValue,
            // The facility is repaid in one bullet at harvest, so the crop cycle *is* the tenor.
            // Previously hard-coded to 0, which told the model the loan had no term at all.
            RequestedTenorMonths: appraisal?.CycleMonths ?? 6,
            ProductType: "RH-SHF Dry Season Input Financing",
            Industry: "Agriculture",
            BureauReports: BuildBureauInputs(profile, completedReports),
            FinancialStatements: BuildFinancialInputs(appraisal),
            CashflowAnalysis: null, // no bank-statement analysis in RH-SHF
            CollateralSummary: BuildCollateralInput(profile, collateral),
            Guarantors: [],
            ExistingExposure: completedReports.Sum(r => r.TotalOutstandingBalance),
            ExistingFacilitiesCount: completedReports.Sum(r => r.ActiveLoans),
            AdditionalContext: BuildApplicationContext(profile, appraisal, farmPlans, eligibility));
    }

    /// <summary>
    /// One input per bureau subject — the company plus every director checked — mirroring how NAMP
    /// feeds the same service. Before Phase B this was a single synthesised input carrying the
    /// profile's flat summary fields: no score, no delinquency depth, no fraud signal. The model
    /// was scoring credit history it could not actually see.
    /// </summary>
    private static List<BureauDataInput> BuildBureauInputs(
        RhshfCreditProfile profile, List<BureauReport> completed)
    {
        if (completed.Count == 0)
        {
            // Checks ran but returned nothing usable (NotFound / Failed). Flag it as a placeholder
            // rather than passing zeros, which read as a clean credit history.
            return
            [
                new BureauDataInput(
                    ReportId: Guid.NewGuid(),
                    SubjectName: profile.CompanyName,
                    SubjectType: "Corporate",
                    CreditScore: null,
                    ActiveLoansCount: 0,
                    TotalOutstandingDebt: 0,
                    PerformingLoansCount: 0,
                    DelinquentLoansCount: 0,
                    DefaultedLoansCount: 0,
                    WorstStatus: null,
                    ReportDate: profile.BureauCheckedAt ?? profile.ReceivedAt,
                    IsPlaceholder: true),
            ];
        }

        return completed.Select(r => new BureauDataInput(
            ReportId: r.Id,
            SubjectName: r.SubjectName,
            SubjectType: r.PartyType ?? "Corporate",
            CreditScore: r.CreditScore,
            ActiveLoansCount: r.ActiveLoans,
            TotalOutstandingDebt: r.TotalOutstandingBalance,
            PerformingLoansCount: r.PerformingAccounts,
            DelinquentLoansCount: r.DelinquentFacilities,
            // Only facilities delinquent past 90 days count as defaulted — the same rule NAMP
            // applies, so both products' advisories score off one definition.
            DefaultedLoansCount: r.MaxDelinquencyDays >= 90 ? r.DelinquentFacilities : 0,
            WorstStatus: r.MaxDelinquencyDays switch
            {
                0 => r.DelinquentFacilities > 0 ? "Non-Performing" : "Performing",
                < 30 => "Overdue",
                < 90 => "Watch",
                _ => "Non-Performing",
            },
            ReportDate: r.ReportDate ?? r.CompletedAt ?? r.RequestedAt,
            MaxDelinquencyDays: r.MaxDelinquencyDays,
            HasLegalActions: r.HasLegalActions,
            TotalOverdue: r.TotalOverdue,
            FraudRiskScore: r.FraudRiskScore,
            FraudRecommendation: r.FraudRecommendation)).ToList();
    }

    /// <summary>
    /// The crop cycle restated in the generic financial shape. RH-SHF has no balance sheet, so the
    /// balance-sheet fields stay zero and the assessment strings say why — the alternative, which
    /// this replaces, was an empty list that left the model scoring repayment capacity on nothing.
    ///
    /// Nothing here is recomputed: every figure is read off the saved appraisal, which derived it
    /// server-side in RhshfViabilityCalculator. A second implementation of the same arithmetic is
    /// exactly how the two would drift apart.
    /// </summary>
    private static List<FinancialDataInput> BuildFinancialInputs(RhshfFinancialAppraisalReport? appraisal)
    {
        if (appraisal is null)
            return [];

        return
        [
            new FinancialDataInput(
                StatementId: appraisal.Id,
                Year: appraisal.SavedAt.Year,
                YearType: "Crop cycle",
                TotalAssets: 0,
                TotalLiabilities: 0,
                TotalEquity: 0,
                Revenue: appraisal.GrossRevenue,
                NetProfit: appraisal.NetReturnToFarmer,
                // The cycle's operating surplus before financing cost — the closest honest analogue
                // to EBITDA for a single-season crop budget.
                EBITDA: appraisal.GrossRevenue - appraisal.OwnCashCosts - appraisal.FinancedInputCost,
                CurrentRatio: 0,
                QuickRatio: 0,
                DebtToEquityRatio: 0,
                InterestCoverageRatio: appraisal.InterestCharge > 0
                    ? appraisal.CashAvailableForDebtService / appraisal.InterestCharge : 0,
                DebtServiceCoverageRatio: appraisal.Dscr,
                NetProfitMarginPercent: appraisal.GrossRevenue > 0
                    ? (appraisal.NetReturnToFarmer / appraisal.GrossRevenue) * 100 : 0,
                ReturnOnEquity: 0,
                LiquidityAssessment: "N/A — single-cycle input financing, no balance sheet",
                LeverageAssessment: "N/A — single-cycle input financing, no balance sheet",
                ProfitabilityAssessment: appraisal.RepaymentCapacityRating.ToString(),
                OverallAssessment: appraisal.AllGatesPass
                    ? "All five viability gates passed."
                    : $"{appraisal.GatesPassed} of 5 viability gates passed. Failing: {FailedGates(appraisal)}.",
                IsUnverified: false),
        ];
    }

    private static CollateralDataInput? BuildCollateralInput(
        RhshfCreditProfile profile, IReadOnlyList<RhshfCollateral> collateral)
    {
        if (collateral.Count == 0)
            return null;

        var marketValue = collateral.Sum(c => c.GuaranteeAmount ?? c.PropertyValue ?? 0);

        return new CollateralDataInput(
            TotalCollateralCount: collateral.Count,
            TotalMarketValue: marketValue,
            // RH-SHF security is guarantees and charges, not realisable property carrying a
            // valuation — a forced-sale figure here would be invented, so it stays zero.
            TotalForcedSaleValue: 0,
            AverageLTV: marketValue > 0 ? (profile.TotalEopValue / marketValue) * 100 : 0,
            CollateralTypes: collateral.Select(c => c.Type.ToString()).Distinct().ToList(),
            HasPerfectedLiens: collateral.Any(c => c.PerfectionStatus == RhshfCollateralPerfectionStatus.Perfected));
    }

    private static string FailedGates(RhshfFinancialAppraisalReport a)
    {
        var failed = new List<string>();
        if (!a.DscrPass) failed.Add($"DSCR {a.Dscr:N2} below {a.ThresholdMinDscr:N2}");
        if (!a.GrossMarginPass) failed.Add($"gross margin {a.GrossMarginPercent:N1}% below {a.ThresholdMinGrossMarginPercent:N1}%");
        if (!a.IrrPass) failed.Add(a.Irr is null
            ? "IRR not computable from the cash-flow series"
            : $"IRR {a.Irr:N1}% below the {a.ThresholdHurdleRatePercent:N1}% hurdle");
        if (!a.YieldHeadroomPass) failed.Add($"yield headroom {a.YieldHeadroomPercent:N1}% below {a.ThresholdMinYieldHeadroomPercent:N1}%");
        if (!a.PriceHeadroomPass) failed.Add($"price headroom {a.PriceHeadroomPercent:N1}% below {a.ThresholdMinPriceHeadroomPercent:N1}%");
        return string.Join("; ", failed);
    }

    private static string BuildApplicationContext(
        RhshfCreditProfile profile,
        RhshfFinancialAppraisalReport? appraisal,
        IReadOnlyList<RhshfFarmPlan> farmPlans,
        IReadOnlyList<RhshfEligibilityCheck> eligibility)
    {
        var lines = new List<string>
        {
            $"Programme: {profile.ProgrammeName} ({profile.SessionName}).",
            $"Farmer count served: {profile.FarmerCount?.ToString("N0") ?? "not recorded"}.",
            $"EOP commodities: {string.Join(", ", profile.EopLines.Select(l => $"{l.Commodity} ({l.QuantityKg:N0}kg @ {l.UnitPricePerKg:N2}/kg)"))}.",
            $"Total EOP value: {profile.Currency} {profile.TotalEopValue:N2}.",
        };

        // The agronomic basis. Without it the model sees a loan amount with no productive activity
        // behind it — which, for input financing, is the entire credit case.
        lines.Add(farmPlans.Count > 0
            ? $"Farm plan: {string.Join(", ", farmPlans.Select(p => $"{p.Crop} on {p.Hectares:N1}ha at {p.ExpectedYieldKgPerHectare:N0}kg/ha, sold at {p.ExpectedPricePerKg:N2}/kg"))}."
            : "No farm plan has been recorded — the production assumptions behind this facility are unstated.");

        if (appraisal is not null)
        {
            lines.Add(
                $"Viability appraisal over a {appraisal.CycleMonths}-month cycle at {appraisal.InterestRatePercent:N1}% interest: " +
                $"gross revenue {profile.Currency} {appraisal.GrossRevenue:N0}, amount due at harvest {profile.Currency} {appraisal.AmountDueAtHarvest:N0}, " +
                $"DSCR {appraisal.Dscr:N2}, gross margin {appraisal.GrossMarginPercent:N1}%, " +
                $"IRR {(appraisal.Irr is null ? "not computable" : $"{appraisal.Irr:N1}%")}, " +
                $"NPV {profile.Currency} {appraisal.NetPresentValue:N0}, " +
                $"net return to the farmer {profile.Currency} {appraisal.NetReturnToFarmer:N0}.");

            // Break-even headroom is the part a general-purpose model cannot infer from the ratios
            // above, and it is the sensitivity that actually decides a season.
            if (appraisal.YieldHeadroomPercent is not null || appraisal.PriceHeadroomPercent is not null)
            {
                lines.Add(
                    $"Break-even sensitivity: yield can fall {appraisal.YieldHeadroomPercent:N1}% " +
                    $"(to {appraisal.BreakEvenYieldKgPerHectare:N0}kg/ha) and price {appraisal.PriceHeadroomPercent:N1}% " +
                    $"(to {profile.Currency} {appraisal.BreakEvenPricePerKg:N2}/kg) before the facility stops covering itself.");
            }

            lines.Add(appraisal.AllGatesPass
                ? "All five viability gates passed."
                : $"Viability gates failed: {FailedGates(appraisal)}.");

            lines.Add($"Credit Officer recommendation: {appraisal.CreditOfficerRecommendation}.");

            // An override is the single most decision-relevant fact on the appraisal: a human
            // knowingly recommended a facility the numbers reject.
            if (!string.IsNullOrWhiteSpace(appraisal.OverrideJustification))
                lines.Add($"The officer overrode the failing gates, justified as: {appraisal.OverrideJustification}");

            if (!string.IsNullOrWhiteSpace(appraisal.AssumptionBasisNote))
                lines.Add($"Basis for the assumptions: {appraisal.AssumptionBasisNote}");
        }
        else
        {
            lines.Add("No financial appraisal has been saved for this cycle — repayment capacity is unassessed.");
        }

        if (profile.Directors.Count > 0)
        {
            var withoutBvn = profile.Directors.Count(d => string.IsNullOrWhiteSpace(d.Bvn));
            lines.Add($"Directors on record: {profile.Directors.Count}" +
                (withoutBvn > 0
                    ? $", of which {withoutBvn} have no BVN and were therefore not credit-checked."
                    : ", all credit-checked."));
        }

        if (eligibility.Count > 0)
        {
            var unsatisfied = eligibility.Where(e => !e.IsSatisfied).Select(e => e.Criterion.ToString()).ToList();
            lines.Add(unsatisfied.Count == 0
                ? "All institutional eligibility criteria were satisfied."
                : $"Eligibility criteria NOT satisfied: {string.Join(", ", unsatisfied)}.");
        }

        return string.Join(" ", lines);
    }

    internal static RhshfAdvisoryDto MapToDto(RhshfAdvisory advisory)
    {
        var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        List<RhshfAdvisoryRiskScoreDto> riskScores = [];
        if (!string.IsNullOrEmpty(advisory.RiskScoresJson))
        {
            try
            {
                var raw = JsonSerializer.Deserialize<List<JsonElement>>(advisory.RiskScoresJson, jsonOpts);
                if (raw != null)
                {
                    riskScores = raw.Select(e => new RhshfAdvisoryRiskScoreDto(
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

        return new RhshfAdvisoryDto(
            advisory.Id,
            advisory.RhshfCreditProfileId,
            advisory.Status.ToString(),
            advisory.OverallScore,
            advisory.OverallRating.ToString(),
            advisory.Recommendation.ToString(),
            advisory.RecommendedAmount,
            advisory.ExecutiveSummary,
            advisory.StrengthsAnalysis,
            advisory.WeaknessesAnalysis,
            advisory.MitigatingFactors,
            advisory.KeyRisks,
            advisory.HasCriticalRedFlags,
            advisory.ModelVersion,
            advisory.GeneratedAt,
            advisory.ErrorMessage,
            riskScores,
            DeserializeList(advisory.RedFlagsJson, jsonOpts),
            DeserializeList(advisory.ConditionsJson, jsonOpts),
            DeserializeList(advisory.CovenantsJson, jsonOpts)
        );
    }

    private static List<string> DeserializeList(string? json, JsonSerializerOptions opts)
    {
        if (string.IsNullOrEmpty(json)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(json, opts) ?? []; }
        catch { return []; }
    }
}

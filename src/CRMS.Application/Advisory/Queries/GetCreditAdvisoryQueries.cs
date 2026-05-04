using CRMS.Application.Advisory.DTOs;
using CRMS.Application.Common;
using CRMS.Domain.Aggregates.Advisory;
using CRMS.Domain.Interfaces;
using System.Text.Json;

namespace CRMS.Application.Advisory.Queries;

public record GetCreditAdvisoryByIdQuery(Guid Id) : IRequest<ApplicationResult<CreditAdvisoryDto>>;

public class GetCreditAdvisoryByIdHandler : IRequestHandler<GetCreditAdvisoryByIdQuery, ApplicationResult<CreditAdvisoryDto>>
{
    private readonly ICreditAdvisoryRepository _repository;

    public GetCreditAdvisoryByIdHandler(ICreditAdvisoryRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<CreditAdvisoryDto>> Handle(GetCreditAdvisoryByIdQuery request, CancellationToken ct = default)
    {
        var advisory = await _repository.GetByIdAsync(request.Id, ct);
        if (advisory == null)
            return ApplicationResult<CreditAdvisoryDto>.Failure("Credit advisory not found");

        return ApplicationResult<CreditAdvisoryDto>.Success(MapToDto(advisory));
    }

    private static CreditAdvisoryDto MapToDto(CreditAdvisory advisory) => CreditAdvisoryMapper.ToDto(advisory);
}

public record GetLatestAdvisoryByLoanApplicationQuery(Guid LoanApplicationId) : IRequest<ApplicationResult<CreditAdvisoryDto>>;

public class GetLatestAdvisoryByLoanApplicationHandler : IRequestHandler<GetLatestAdvisoryByLoanApplicationQuery, ApplicationResult<CreditAdvisoryDto>>
{
    private readonly ICreditAdvisoryRepository _repository;

    public GetLatestAdvisoryByLoanApplicationHandler(ICreditAdvisoryRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<CreditAdvisoryDto>> Handle(GetLatestAdvisoryByLoanApplicationQuery request, CancellationToken ct = default)
    {
        var advisory = await _repository.GetLatestByLoanApplicationIdAsync(request.LoanApplicationId, ct);
        if (advisory == null)
            return ApplicationResult<CreditAdvisoryDto>.Failure("No credit advisory found for this loan application");

        return ApplicationResult<CreditAdvisoryDto>.Success(CreditAdvisoryMapper.ToDto(advisory));
    }
}

public record GetAdvisoryHistoryByLoanApplicationQuery(Guid LoanApplicationId) : IRequest<ApplicationResult<List<CreditAdvisorySummaryDto>>>;

public class GetAdvisoryHistoryByLoanApplicationHandler : IRequestHandler<GetAdvisoryHistoryByLoanApplicationQuery, ApplicationResult<List<CreditAdvisorySummaryDto>>>
{
    private readonly ICreditAdvisoryRepository _repository;

    public GetAdvisoryHistoryByLoanApplicationHandler(ICreditAdvisoryRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<List<CreditAdvisorySummaryDto>>> Handle(GetAdvisoryHistoryByLoanApplicationQuery request, CancellationToken ct = default)
    {
        var advisories = await _repository.GetAllByLoanApplicationIdAsync(request.LoanApplicationId, ct);

        var summaries = advisories.Select(a => new CreditAdvisorySummaryDto(
            a.Id,
            a.LoanApplicationId,
            a.Status.ToString(),
            a.OverallScore,
            a.OverallRating.ToString(),
            a.Recommendation.ToString(),
            a.HasCriticalRedFlags,
            a.RedFlags.Count,
            a.GeneratedAt
        )).OrderByDescending(x => x.GeneratedAt).ToList();

        return ApplicationResult<List<CreditAdvisorySummaryDto>>.Success(summaries);
    }
}

public record GetScoreMatrixQuery(Guid AdvisoryId) : IRequest<ApplicationResult<ScoreMatrixDto>>;

public class GetScoreMatrixHandler : IRequestHandler<GetScoreMatrixQuery, ApplicationResult<ScoreMatrixDto>>
{
    private readonly ICreditAdvisoryRepository _repository;

    public GetScoreMatrixHandler(ICreditAdvisoryRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<ScoreMatrixDto>> Handle(GetScoreMatrixQuery request, CancellationToken ct = default)
    {
        var advisory = await _repository.GetByIdAsync(request.AdvisoryId, ct);
        if (advisory == null)
            return ApplicationResult<ScoreMatrixDto>.Failure("Credit advisory not found");

        var matrix = new ScoreMatrixDto(
            advisory.Id,
            advisory.CreditHistoryScore,
            advisory.FinancialHealthScore,
            advisory.CashflowScore,
            advisory.DSCRScore,
            advisory.RiskScores.FirstOrDefault(s => s.Category == Domain.Enums.RiskCategory.CollateralCoverage)?.Score,
            advisory.RiskScores.FirstOrDefault(s => s.Category == Domain.Enums.RiskCategory.ManagementRisk)?.Score,
            advisory.OverallScore,
            advisory.OverallRating.ToString()
        );

        return ApplicationResult<ScoreMatrixDto>.Success(matrix);
    }
}

internal static class CreditAdvisoryMapper
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    // Deserializable shape matching what GenerateCreditAdvisoryHandler serializes
    private sealed record PersistedRiskScore(
        string Category, decimal Score, decimal Weight,
        string Rating, string Rationale,
        List<string> RedFlags, List<string> PositiveIndicators);

    public static CreditAdvisoryDto ToDto(CreditAdvisory advisory)
    {
        // Prefer in-memory collections (freshly generated); fall back to persisted JSON on DB load
        List<RiskScoreDto> riskScores;
        if (advisory.RiskScores.Count > 0)
        {
            riskScores = advisory.RiskScores.Select(s => new RiskScoreDto(
                s.Category.ToString(), s.Score, s.Weight, s.WeightedScore,
                s.Rating.ToString(), s.Rationale,
                s.RedFlags.ToList(), s.PositiveIndicators.ToList()
            )).ToList();
        }
        else if (!string.IsNullOrEmpty(advisory.RiskScoresJson))
        {
            var persisted = JsonSerializer.Deserialize<List<PersistedRiskScore>>(advisory.RiskScoresJson, JsonOpts) ?? [];
            riskScores = persisted.Select(s => new RiskScoreDto(
                s.Category, s.Score, s.Weight, s.Score * s.Weight,
                s.Rating, s.Rationale, s.RedFlags, s.PositiveIndicators
            )).ToList();
        }
        else
        {
            riskScores = [];
        }

        var redFlags = advisory.RedFlags.Count > 0
            ? advisory.RedFlags.ToList()
            : (!string.IsNullOrEmpty(advisory.RedFlagsJson)
                ? JsonSerializer.Deserialize<List<string>>(advisory.RedFlagsJson, JsonOpts) ?? []
                : []);

        var conditions = advisory.Conditions.Count > 0
            ? advisory.Conditions.ToList()
            : (!string.IsNullOrEmpty(advisory.ConditionsJson)
                ? JsonSerializer.Deserialize<List<string>>(advisory.ConditionsJson, JsonOpts) ?? []
                : []);

        var covenants = advisory.Covenants.Count > 0
            ? advisory.Covenants.ToList()
            : (!string.IsNullOrEmpty(advisory.CovenantsJson)
                ? JsonSerializer.Deserialize<List<string>>(advisory.CovenantsJson, JsonOpts) ?? []
                : []);

        var hasCriticalRedFlags = redFlags.Count >= 3 ||
            riskScores.Any(s => s.Rating == "VeryHigh");

        return new CreditAdvisoryDto(
            advisory.Id,
            advisory.LoanApplicationId,
            advisory.Status.ToString(),
            advisory.OverallScore,
            advisory.OverallRating.ToString(),
            advisory.Recommendation.ToString(),
            riskScores,
            advisory.RecommendedAmount,
            advisory.RecommendedTenorMonths,
            advisory.RecommendedInterestRate,
            advisory.MaxExposure,
            conditions,
            covenants,
            advisory.ExecutiveSummary,
            advisory.StrengthsAnalysis,
            advisory.WeaknessesAnalysis,
            advisory.MitigatingFactors,
            advisory.KeyRisks,
            redFlags,
            hasCriticalRedFlags,
            advisory.ModelVersion,
            advisory.GeneratedAt,
            advisory.ErrorMessage
        );
    }
}

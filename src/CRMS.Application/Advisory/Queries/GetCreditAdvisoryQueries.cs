using CRMS.Application.Advisory.DTOs;
using CRMS.Application.Common;
using CRMS.Domain.Aggregates.Advisory;
using CRMS.Domain.Interfaces;

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
    public static CreditAdvisoryDto ToDto(CreditAdvisory advisory)
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

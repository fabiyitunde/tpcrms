using CRMS.Application.Common;
using CRMS.Application.Configuration.Commands;
using CRMS.Application.Configuration.DTOs;
using CRMS.Domain.Aggregates.Configuration;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Configuration.Queries;

public record GetScoringParameterByIdQuery(Guid Id) : IRequest<ApplicationResult<ScoringParameterDto>>;

public class GetScoringParameterByIdHandler : IRequestHandler<GetScoringParameterByIdQuery, ApplicationResult<ScoringParameterDto>>
{
    private readonly IScoringParameterRepository _repository;

    public GetScoringParameterByIdHandler(IScoringParameterRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<ScoringParameterDto>> Handle(GetScoringParameterByIdQuery request, CancellationToken ct = default)
    {
        var parameter = await _repository.GetByIdAsync(request.Id, ct);
        if (parameter == null)
            return ApplicationResult<ScoringParameterDto>.Failure("Scoring parameter not found");

        return ApplicationResult<ScoringParameterDto>.Success(ScoringParameterMapper.ToDto(parameter));
    }
}

public record GetAllScoringParametersQuery : IRequest<ApplicationResult<List<ScoringParameterDto>>>;

public class GetAllScoringParametersHandler : IRequestHandler<GetAllScoringParametersQuery, ApplicationResult<List<ScoringParameterDto>>>
{
    private readonly IScoringParameterRepository _repository;

    public GetAllScoringParametersHandler(IScoringParameterRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<List<ScoringParameterDto>>> Handle(GetAllScoringParametersQuery request, CancellationToken ct = default)
    {
        var parameters = await _repository.GetAllActiveAsync(ct);
        var dtos = parameters.Select(ScoringParameterMapper.ToDto).ToList();
        return ApplicationResult<List<ScoringParameterDto>>.Success(dtos);
    }
}

public record GetScoringParametersByCategoryQuery(string Category) : IRequest<ApplicationResult<List<ScoringParameterDto>>>;

public class GetScoringParametersByCategoryHandler : IRequestHandler<GetScoringParametersByCategoryQuery, ApplicationResult<List<ScoringParameterDto>>>
{
    private readonly IScoringParameterRepository _repository;

    public GetScoringParametersByCategoryHandler(IScoringParameterRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<List<ScoringParameterDto>>> Handle(GetScoringParametersByCategoryQuery request, CancellationToken ct = default)
    {
        var parameters = await _repository.GetByCategoryAsync(request.Category, ct);
        var dtos = parameters.Select(ScoringParameterMapper.ToDto).ToList();
        return ApplicationResult<List<ScoringParameterDto>>.Success(dtos);
    }
}

public record GetPendingChangesQuery : IRequest<ApplicationResult<List<PendingChangeDto>>>;

public class GetPendingChangesHandler : IRequestHandler<GetPendingChangesQuery, ApplicationResult<List<PendingChangeDto>>>
{
    private readonly IScoringParameterRepository _repository;

    public GetPendingChangesHandler(IScoringParameterRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<List<PendingChangeDto>>> Handle(GetPendingChangesQuery request, CancellationToken ct = default)
    {
        var parameters = await _repository.GetPendingChangesAsync(ct);
        
        var dtos = parameters.Select(p => new PendingChangeDto(
            p.Id,
            p.Category,
            p.ParameterKey,
            p.DisplayName,
            p.CurrentValue,
            p.PendingValue ?? 0,
            p.PendingChangeByUserId ?? Guid.Empty,
            null, // Would need to join with User table for name
            p.PendingChangeAt ?? DateTime.UtcNow,
            p.PendingChangeReason ?? ""
        )).ToList();

        return ApplicationResult<List<PendingChangeDto>>.Success(dtos);
    }
}

public record GetCategorySummariesQuery : IRequest<ApplicationResult<List<CategorySummaryDto>>>;

public class GetCategorySummariesHandler : IRequestHandler<GetCategorySummariesQuery, ApplicationResult<List<CategorySummaryDto>>>
{
    private readonly IScoringParameterRepository _repository;

    public GetCategorySummariesHandler(IScoringParameterRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<List<CategorySummaryDto>>> Handle(GetCategorySummariesQuery request, CancellationToken ct = default)
    {
        var parameters = await _repository.GetAllActiveAsync(ct);
        
        var summaries = parameters
            .GroupBy(p => p.Category)
            .Select(g => new CategorySummaryDto(
                g.Key,
                GetCategoryDisplayName(g.Key),
                g.Count(),
                g.Count(p => p.ChangeStatus == ParameterChangeStatus.Pending)
            ))
            .OrderBy(s => GetCategorySortOrder(s.Category))
            .ToList();

        return ApplicationResult<List<CategorySummaryDto>>.Success(summaries);
    }

    private static string GetCategoryDisplayName(string category) => category switch
    {
        "Weights" => "Category Weights",
        "CreditHistory" => "Credit History Scoring",
        "FinancialHealth" => "Financial Health Scoring",
        "Cashflow" => "Cashflow Analysis Scoring",
        "DSCR" => "Debt Service Capacity Scoring",
        "Collateral" => "Collateral Scoring",
        "Recommendations" => "Recommendation Thresholds",
        "LoanAdjustments" => "Loan Adjustment Parameters",
        "StatementTrust" => "Statement Trust Weights",
        _ => category
    };

    private static int GetCategorySortOrder(string category) => category switch
    {
        "Weights" => 1,
        "CreditHistory" => 2,
        "FinancialHealth" => 3,
        "Cashflow" => 4,
        "DSCR" => 5,
        "Collateral" => 6,
        "Recommendations" => 7,
        "LoanAdjustments" => 8,
        "StatementTrust" => 9,
        _ => 99
    };
}

public record GetParameterHistoryQuery(Guid ParameterId) : IRequest<ApplicationResult<List<ScoringParameterHistoryDto>>>;

public class GetParameterHistoryHandler : IRequestHandler<GetParameterHistoryQuery, ApplicationResult<List<ScoringParameterHistoryDto>>>
{
    private readonly IScoringParameterRepository _repository;

    public GetParameterHistoryHandler(IScoringParameterRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<List<ScoringParameterHistoryDto>>> Handle(GetParameterHistoryQuery request, CancellationToken ct = default)
    {
        var history = await _repository.GetHistoryByParameterIdAsync(request.ParameterId, ct);
        
        var dtos = history.Select(h => new ScoringParameterHistoryDto(
            h.Id,
            h.ScoringParameterId,
            h.Category,
            h.ParameterKey,
            h.PreviousValue,
            h.NewValue,
            h.ChangeType,
            h.ChangeReason,
            h.RequestedByUserId,
            h.RequestedAt,
            h.ApprovedByUserId,
            h.ApprovedAt,
            h.ApprovalNotes,
            h.VersionNumber
        )).ToList();

        return ApplicationResult<List<ScoringParameterHistoryDto>>.Success(dtos);
    }
}

public record GetRecentHistoryQuery(int Count = 50) : IRequest<ApplicationResult<List<ScoringParameterHistoryDto>>>;

public class GetRecentHistoryHandler : IRequestHandler<GetRecentHistoryQuery, ApplicationResult<List<ScoringParameterHistoryDto>>>
{
    private readonly IScoringParameterRepository _repository;

    public GetRecentHistoryHandler(IScoringParameterRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<List<ScoringParameterHistoryDto>>> Handle(GetRecentHistoryQuery request, CancellationToken ct = default)
    {
        var history = await _repository.GetRecentHistoryAsync(request.Count, ct);
        
        var dtos = history.Select(h => new ScoringParameterHistoryDto(
            h.Id,
            h.ScoringParameterId,
            h.Category,
            h.ParameterKey,
            h.PreviousValue,
            h.NewValue,
            h.ChangeType,
            h.ChangeReason,
            h.RequestedByUserId,
            h.RequestedAt,
            h.ApprovedByUserId,
            h.ApprovedAt,
            h.ApprovalNotes,
            h.VersionNumber
        )).ToList();

        return ApplicationResult<List<ScoringParameterHistoryDto>>.Success(dtos);
    }
}

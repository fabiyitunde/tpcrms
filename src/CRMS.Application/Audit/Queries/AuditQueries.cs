using CRMS.Application.Audit.DTOs;
using CRMS.Application.Common;
using CRMS.Domain.Aggregates.Audit;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Audit.Queries;

// Get audit log by ID
public record GetAuditLogByIdQuery(Guid Id) : IRequest<ApplicationResult<AuditLogDto>>;

public class GetAuditLogByIdHandler : IRequestHandler<GetAuditLogByIdQuery, ApplicationResult<AuditLogDto>>
{
    private readonly IAuditLogRepository _repository;

    public GetAuditLogByIdHandler(IAuditLogRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<AuditLogDto>> Handle(GetAuditLogByIdQuery request, CancellationToken ct = default)
    {
        var log = await _repository.GetByIdAsync(request.Id, ct);
        if (log == null)
            return ApplicationResult<AuditLogDto>.Failure("Audit log not found");

        return ApplicationResult<AuditLogDto>.Success(AuditMapper.ToDto(log));
    }
}

// Get audit logs by loan application
public record GetAuditLogsByLoanApplicationQuery(Guid LoanApplicationId) : IRequest<ApplicationResult<List<AuditLogSummaryDto>>>;

public class GetAuditLogsByLoanApplicationHandler : IRequestHandler<GetAuditLogsByLoanApplicationQuery, ApplicationResult<List<AuditLogSummaryDto>>>
{
    private readonly IAuditLogRepository _repository;

    public GetAuditLogsByLoanApplicationHandler(IAuditLogRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<List<AuditLogSummaryDto>>> Handle(GetAuditLogsByLoanApplicationQuery request, CancellationToken ct = default)
    {
        var logs = await _repository.GetByLoanApplicationAsync(request.LoanApplicationId, ct);
        var dtos = logs.Select(AuditMapper.ToSummaryDto).ToList();
        return ApplicationResult<List<AuditLogSummaryDto>>.Success(dtos);
    }
}

// Get audit logs by entity
public record GetAuditLogsByEntityQuery(string EntityType, Guid EntityId) : IRequest<ApplicationResult<List<AuditLogSummaryDto>>>;

public class GetAuditLogsByEntityHandler : IRequestHandler<GetAuditLogsByEntityQuery, ApplicationResult<List<AuditLogSummaryDto>>>
{
    private readonly IAuditLogRepository _repository;

    public GetAuditLogsByEntityHandler(IAuditLogRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<List<AuditLogSummaryDto>>> Handle(GetAuditLogsByEntityQuery request, CancellationToken ct = default)
    {
        var logs = await _repository.GetByEntityAsync(request.EntityType, request.EntityId, ct);
        var dtos = logs.Select(AuditMapper.ToSummaryDto).ToList();
        return ApplicationResult<List<AuditLogSummaryDto>>.Success(dtos);
    }
}

// Get audit logs by user
public record GetAuditLogsByUserQuery(Guid UserId, DateTime? From = null, DateTime? To = null) : IRequest<ApplicationResult<List<AuditLogSummaryDto>>>;

public class GetAuditLogsByUserHandler : IRequestHandler<GetAuditLogsByUserQuery, ApplicationResult<List<AuditLogSummaryDto>>>
{
    private readonly IAuditLogRepository _repository;

    public GetAuditLogsByUserHandler(IAuditLogRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<List<AuditLogSummaryDto>>> Handle(GetAuditLogsByUserQuery request, CancellationToken ct = default)
    {
        var logs = await _repository.GetByUserAsync(request.UserId, request.From, request.To, ct);
        var dtos = logs.Select(AuditMapper.ToSummaryDto).ToList();
        return ApplicationResult<List<AuditLogSummaryDto>>.Success(dtos);
    }
}

// Get recent audit logs
public record GetRecentAuditLogsQuery(int Count = 100) : IRequest<ApplicationResult<List<AuditLogSummaryDto>>>;

public class GetRecentAuditLogsHandler : IRequestHandler<GetRecentAuditLogsQuery, ApplicationResult<List<AuditLogSummaryDto>>>
{
    private readonly IAuditLogRepository _repository;

    public GetRecentAuditLogsHandler(IAuditLogRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<List<AuditLogSummaryDto>>> Handle(GetRecentAuditLogsQuery request, CancellationToken ct = default)
    {
        var logs = await _repository.GetRecentAsync(request.Count, ct);
        var dtos = logs.Select(AuditMapper.ToSummaryDto).ToList();
        return ApplicationResult<List<AuditLogSummaryDto>>.Success(dtos);
    }
}

// Get failed actions
public record GetFailedActionsQuery(DateTime? From = null, DateTime? To = null) : IRequest<ApplicationResult<List<AuditLogSummaryDto>>>;

public class GetFailedActionsHandler : IRequestHandler<GetFailedActionsQuery, ApplicationResult<List<AuditLogSummaryDto>>>
{
    private readonly IAuditLogRepository _repository;

    public GetFailedActionsHandler(IAuditLogRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<List<AuditLogSummaryDto>>> Handle(GetFailedActionsQuery request, CancellationToken ct = default)
    {
        var logs = await _repository.GetFailedActionsAsync(request.From, request.To, ct);
        var dtos = logs.Select(AuditMapper.ToSummaryDto).ToList();
        return ApplicationResult<List<AuditLogSummaryDto>>.Success(dtos);
    }
}

// Search audit logs
public record SearchAuditLogsQuery(
    AuditCategory? Category = null,
    AuditAction? Action = null,
    Guid? UserId = null,
    Guid? LoanApplicationId = null,
    string? EntityType = null,
    DateTime? From = null,
    DateTime? To = null,
    bool? IsSuccess = null,
    int PageNumber = 1,
    int PageSize = 50
) : IRequest<ApplicationResult<AuditSearchResultDto>>;

public class SearchAuditLogsHandler : IRequestHandler<SearchAuditLogsQuery, ApplicationResult<AuditSearchResultDto>>
{
    private readonly IAuditLogRepository _repository;

    public SearchAuditLogsHandler(IAuditLogRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<AuditSearchResultDto>> Handle(SearchAuditLogsQuery request, CancellationToken ct = default)
    {
        var logs = await _repository.SearchAsync(
            request.Category,
            request.Action,
            request.UserId,
            request.LoanApplicationId,
            request.EntityType,
            request.From,
            request.To,
            request.IsSuccess,
            request.PageNumber,
            request.PageSize,
            ct);

        var totalCount = await _repository.GetCountAsync(
            request.Category,
            request.Action,
            request.From,
            request.To,
            ct);

        var result = new AuditSearchResultDto(
            logs.Select(AuditMapper.ToSummaryDto).ToList(),
            totalCount,
            request.PageNumber,
            request.PageSize,
            (int)Math.Ceiling(totalCount / (double)request.PageSize)
        );

        return ApplicationResult<AuditSearchResultDto>.Success(result);
    }
}

// Get data access logs by user
public record GetDataAccessLogsByUserQuery(Guid UserId, DateTime? From = null, DateTime? To = null) : IRequest<ApplicationResult<List<DataAccessLogDto>>>;

public class GetDataAccessLogsByUserHandler : IRequestHandler<GetDataAccessLogsByUserQuery, ApplicationResult<List<DataAccessLogDto>>>
{
    private readonly IDataAccessLogRepository _repository;

    public GetDataAccessLogsByUserHandler(IDataAccessLogRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<List<DataAccessLogDto>>> Handle(GetDataAccessLogsByUserQuery request, CancellationToken ct = default)
    {
        var logs = await _repository.GetByUserAsync(request.UserId, request.From, request.To, ct);
        var dtos = logs.Select(AuditMapper.ToDataAccessDto).ToList();
        return ApplicationResult<List<DataAccessLogDto>>.Success(dtos);
    }
}

// Get data access logs by loan application
public record GetDataAccessLogsByLoanApplicationQuery(Guid LoanApplicationId) : IRequest<ApplicationResult<List<DataAccessLogDto>>>;

public class GetDataAccessLogsByLoanApplicationHandler : IRequestHandler<GetDataAccessLogsByLoanApplicationQuery, ApplicationResult<List<DataAccessLogDto>>>
{
    private readonly IDataAccessLogRepository _repository;

    public GetDataAccessLogsByLoanApplicationHandler(IDataAccessLogRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<List<DataAccessLogDto>>> Handle(GetDataAccessLogsByLoanApplicationQuery request, CancellationToken ct = default)
    {
        var logs = await _repository.GetByLoanApplicationAsync(request.LoanApplicationId, ct);
        var dtos = logs.Select(AuditMapper.ToDataAccessDto).ToList();
        return ApplicationResult<List<DataAccessLogDto>>.Success(dtos);
    }
}

// Mapper
internal static class AuditMapper
{
    public static AuditLogDto ToDto(AuditLog log) => new(
        log.Id,
        log.Action.ToString(),
        log.Category.ToString(),
        log.Description,
        log.UserId,
        log.UserName,
        log.UserRole,
        log.IpAddress,
        log.EntityType,
        log.EntityId,
        log.EntityReference,
        log.LoanApplicationId,
        log.LoanApplicationNumber,
        log.OldValues,
        log.NewValues,
        log.AdditionalData,
        log.IsSuccess,
        log.ErrorMessage,
        log.Timestamp
    );

    public static AuditLogSummaryDto ToSummaryDto(AuditLog log) => new(
        log.Id,
        log.Action.ToString(),
        log.Category.ToString(),
        log.Description,
        log.UserName,
        log.EntityType,
        log.EntityReference,
        log.IsSuccess,
        log.Timestamp
    );

    public static DataAccessLogDto ToDataAccessDto(DataAccessLog log) => new(
        log.Id,
        log.UserId,
        log.UserName,
        log.UserRole,
        log.DataType.ToString(),
        log.EntityType,
        log.EntityId,
        log.EntityReference,
        log.LoanApplicationId,
        log.LoanApplicationNumber,
        log.AccessType.ToString(),
        log.AccessReason,
        log.IpAddress,
        log.AccessedAt
    );
}

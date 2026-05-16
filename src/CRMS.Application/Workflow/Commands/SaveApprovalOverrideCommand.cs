using CRMS.Application.Common;
using CRMS.Domain.Aggregates.LoanApplication;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Workflow.Commands;

public record ApprovalOverrideDto(
    Guid Id,
    Guid LoanApplicationId,
    string Stage,
    string ActorName,
    string NoteText,
    bool IsResolved,
    DateTime? ResolvedAt,
    string? ResolvedByName,
    DateTime CreatedAt
);

public record SaveApprovalOverrideCommand(
    Guid LoanApplicationId,
    string Stage,
    Guid ActorId,
    string ActorName,
    string NoteText
) : IRequest<ApplicationResult<ApprovalOverrideDto>>;

public class SaveApprovalOverrideHandler : IRequestHandler<SaveApprovalOverrideCommand, ApplicationResult<ApprovalOverrideDto>>
{
    private readonly IApprovalOverrideRepository _overrideRepo;
    private readonly IUnitOfWork _unitOfWork;

    public SaveApprovalOverrideHandler(IApprovalOverrideRepository overrideRepo, IUnitOfWork unitOfWork)
    {
        _overrideRepo = overrideRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<ApprovalOverrideDto>> Handle(SaveApprovalOverrideCommand request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.NoteText))
            return ApplicationResult<ApprovalOverrideDto>.Failure("Override note is required");

        var record = ApprovalOverrideRecord.Create(
            request.LoanApplicationId,
            request.Stage,
            request.ActorId,
            request.ActorName,
            request.NoteText);

        record.SetAuditInfo(request.ActorId.ToString(), isNew: true);

        await _overrideRepo.AddAsync(record, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<ApprovalOverrideDto>.Success(ToDto(record));
    }

    private static ApprovalOverrideDto ToDto(ApprovalOverrideRecord r) => new(
        r.Id, r.LoanApplicationId, r.Stage, r.ActorName, r.NoteText,
        r.IsResolved, r.ResolvedAt, r.ResolvedByName, r.CreatedAt);
}

public record GetApprovalOverridesQuery(Guid LoanApplicationId)
    : IRequest<ApplicationResult<List<ApprovalOverrideDto>>>;

public class GetApprovalOverridesHandler : IRequestHandler<GetApprovalOverridesQuery, ApplicationResult<List<ApprovalOverrideDto>>>
{
    private readonly IApprovalOverrideRepository _overrideRepo;

    public GetApprovalOverridesHandler(IApprovalOverrideRepository overrideRepo)
    {
        _overrideRepo = overrideRepo;
    }

    public async Task<ApplicationResult<List<ApprovalOverrideDto>>> Handle(GetApprovalOverridesQuery request, CancellationToken ct = default)
    {
        var records = await _overrideRepo.GetByLoanApplicationIdAsync(request.LoanApplicationId, ct);
        var dtos = records.Select(r => new ApprovalOverrideDto(
            r.Id, r.LoanApplicationId, r.Stage, r.ActorName, r.NoteText,
            r.IsResolved, r.ResolvedAt, r.ResolvedByName, r.CreatedAt)).ToList();

        return ApplicationResult<List<ApprovalOverrideDto>>.Success(dtos);
    }
}

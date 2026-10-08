using CRMS.Application.Common;
using CRMS.Application.Rhshf.DTOs;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Queries;

/// <summary>Appraisal or Risk Review queue (design doc Phase 4 §5/§6), branch-scoped like every
/// other CRMS queue — BranchId null means global/HO visibility, resolved by the caller from the
/// current user's role (Roles.HasGlobalVisibility).</summary>
public record GetRhshfStaffQueueQuery(RhshfInternalStage Stage, Guid? BranchId) : IRequest<ApplicationResult<List<RhshfQueueItemDto>>>;

public class GetRhshfStaffQueueHandler : IRequestHandler<GetRhshfStaffQueueQuery, ApplicationResult<List<RhshfQueueItemDto>>>
{
    private readonly IRhshfCreditProfileRepository _repo;

    public GetRhshfStaffQueueHandler(IRhshfCreditProfileRepository repo) => _repo = repo;

    public async Task<ApplicationResult<List<RhshfQueueItemDto>>> Handle(GetRhshfStaffQueueQuery request, CancellationToken ct = default)
    {
        var cases = await _repo.GetQueueAsync(request.Stage, request.BranchId, ct);
        var dtos = cases.Select(c => new RhshfQueueItemDto(
            c.Reference, c.CompanyName, c.TotalEopValue, c.Currency, c.CurrentCycleNumber, c.UpdatedAt,
            c.Status, c.InternalStage)).ToList();

        return ApplicationResult<List<RhshfQueueItemDto>>.Success(dtos);
    }
}

/// <summary>Admin/HO "all cases" view — every RH-SHF case regardless of status or stage, branch-scoped
/// (BranchId null = everything). Deliberately a light query (no collection graph) since it can return
/// the full book.</summary>
public record GetRhshfAllCasesQuery(Guid? BranchId) : IRequest<ApplicationResult<List<RhshfQueueItemDto>>>;

public class GetRhshfAllCasesHandler : IRequestHandler<GetRhshfAllCasesQuery, ApplicationResult<List<RhshfQueueItemDto>>>
{
    private readonly IRhshfCreditProfileRepository _repo;

    public GetRhshfAllCasesHandler(IRhshfCreditProfileRepository repo) => _repo = repo;

    public async Task<ApplicationResult<List<RhshfQueueItemDto>>> Handle(GetRhshfAllCasesQuery request, CancellationToken ct = default)
    {
        var cases = await _repo.GetAllForListAsync(request.BranchId, ct);
        var dtos = cases.Select(c => new RhshfQueueItemDto(
            c.Reference, c.CompanyName, c.TotalEopValue, c.Currency, c.CurrentCycleNumber, c.UpdatedAt,
            c.Status, c.InternalStage)).ToList();

        return ApplicationResult<List<RhshfQueueItemDto>>.Success(dtos);
    }
}

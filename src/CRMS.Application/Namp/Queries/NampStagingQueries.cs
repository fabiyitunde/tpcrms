using CRMS.Application.Common;
using CRMS.Application.Namp.DTOs;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Namp.Queries;

public record GetNampStagingQueueQuery(Guid? BranchId = null)
    : IRequest<ApplicationResult<List<NampStagingRecordSummaryDto>>>;

public class GetNampStagingQueueHandler
    : IRequestHandler<GetNampStagingQueueQuery, ApplicationResult<List<NampStagingRecordSummaryDto>>>
{
    private readonly INampStagingRepository _repo;

    public GetNampStagingQueueHandler(INampStagingRepository repo) => _repo = repo;

    public async Task<ApplicationResult<List<NampStagingRecordSummaryDto>>> Handle(
        GetNampStagingQueueQuery request, CancellationToken ct = default)
    {
        var records = await _repo.GetPendingRecallAsync(request.BranchId, ct);
        var dtos = records.Select(r => new NampStagingRecordSummaryDto(
            r.Id,
            r.ApplicationReference,
            r.ApplicantName,
            r.BoaAccountNumber,
            r.ApplicantCategory.ToString(),
            r.EquipmentDescription,
            r.EquipmentValue,
            r.ResolvedBranchId,
            r.IsRecalled,
            r.ReceivedAt
        )).ToList();

        return ApplicationResult<List<NampStagingRecordSummaryDto>>.Success(dtos);
    }
}

public record GetNampStagingRecordByIdQuery(Guid Id)
    : IRequest<ApplicationResult<NampStagingRecordDto>>;

public class GetNampStagingRecordByIdHandler
    : IRequestHandler<GetNampStagingRecordByIdQuery, ApplicationResult<NampStagingRecordDto>>
{
    private readonly INampStagingRepository _repo;

    public GetNampStagingRecordByIdHandler(INampStagingRepository repo) => _repo = repo;

    public async Task<ApplicationResult<NampStagingRecordDto>> Handle(
        GetNampStagingRecordByIdQuery request, CancellationToken ct = default)
    {
        var r = await _repo.GetByIdAsync(request.Id, ct);
        if (r is null)
            return ApplicationResult<NampStagingRecordDto>.Failure("Staging record not found.");

        return ApplicationResult<NampStagingRecordDto>.Success(new NampStagingRecordDto(
            r.Id,
            r.ApplicationReference,
            r.CrmsApplicationNumber,
            r.ApplicantName,
            r.BoaAccountNumber,
            r.ApplicantPhone,
            r.ApplicantEmail,
            r.ApplicantCategory.ToString(),
            r.EquipmentDescription,
            r.EquipmentValue,
            r.ResolvedBranchId,
            r.ResolvedOfficeId,
            r.ResolvedLocationId,
            r.BranchResolutionNote,
            r.IsRecalled,
            r.RecalledAt,
            r.RecalledByUserId,
            r.ReceivedAt
        ));
    }
}

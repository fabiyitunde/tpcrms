using CRMS.Application.Common;
using CRMS.Application.Rhshf.Services;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

public record RhshfReasonCodeDto(string Code, RhshfReasonCodeType Type, string Label, int SortOrder, bool IsActive);

/// <summary>Every reason code, for Admin > RH-SHF Reason Codes; or, with <see cref="PickFor"/>, the
/// active ones staff can pick for a return or a decline (OTHER last).</summary>
public record GetRhshfReasonCodesQuery(RhshfReasonCodeType? PickFor = null) : IRequest<ApplicationResult<List<RhshfReasonCodeDto>>>;

public class GetRhshfReasonCodesHandler : IRequestHandler<GetRhshfReasonCodesQuery, ApplicationResult<List<RhshfReasonCodeDto>>>
{
    private readonly IRhshfReasonCodeRepository _repo;

    public GetRhshfReasonCodesHandler(IRhshfReasonCodeRepository repo)
    {
        _repo = repo;
    }

    public async Task<ApplicationResult<List<RhshfReasonCodeDto>>> Handle(GetRhshfReasonCodesQuery request, CancellationToken ct = default)
    {
        var all = await _repo.GetAllAsync(ct);
        var codes = request.PickFor is { } type ? RhshfReasonCodeRules.PickList(all, type) : all;
        return ApplicationResult<List<RhshfReasonCodeDto>>.Success(
            codes.Select(c => new RhshfReasonCodeDto(c.Code, c.Type, c.Label, c.SortOrder, c.IsActive)).ToList());
    }
}

/// <summary>
/// Adds a reason code, or changes an existing one's wording, order and switch. The code itself and
/// its type never change once created: the portal stores the code, and past cases carry it.
/// </summary>
public record SaveRhshfReasonCodeCommand(string Code, RhshfReasonCodeType Type, string Label, int SortOrder, bool IsActive, string ActorName)
    : IRequest<ApplicationResult>;

public class SaveRhshfReasonCodeHandler : IRequestHandler<SaveRhshfReasonCodeCommand, ApplicationResult>
{
    private readonly IRhshfReasonCodeRepository _repo;
    private readonly IUnitOfWork _uow;

    public SaveRhshfReasonCodeHandler(IRhshfReasonCodeRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(SaveRhshfReasonCodeCommand request, CancellationToken ct = default)
    {
        var existing = await _repo.GetByCodeAsync(request.Code, ct);
        if (existing is null)
        {
            var created = RhshfReasonCode.Create(request.Code, request.Type, request.Label, request.SortOrder);
            if (created.IsFailure)
                return ApplicationResult.Failure(created.Error);
            if (!request.IsActive)
                created.Value.Update(request.Label, request.SortOrder, isActive: false);
            created.Value.SetAuditInfo(request.ActorName, isNew: true);
            await _repo.AddAsync(created.Value, ct);
        }
        else
        {
            if (existing.Type != request.Type)
                return ApplicationResult.Failure($"{existing.Code} is a {existing.Type.ToString().ToLowerInvariant()} reason; a code's type cannot change. Add a new code instead.");
            var updated = existing.Update(request.Label, request.SortOrder, request.IsActive);
            if (updated.IsFailure)
                return ApplicationResult.Failure(updated.Error);
            existing.SetAuditInfo(request.ActorName);
        }

        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}

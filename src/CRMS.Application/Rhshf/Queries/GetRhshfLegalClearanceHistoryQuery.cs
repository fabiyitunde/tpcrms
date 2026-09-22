using CRMS.Application.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Queries;

public record GetRhshfLegalClearanceHistoryQuery(string Reference) : IRequest<ApplicationResult<List<RhshfLegalClearanceDto>>>;

public record RhshfLegalClearanceDto(int CycleNumber, Guid LegalOfficerId, string LegalOfficerName, DateTime ClearedAt, RhshfLegalClearanceOutcome Outcome, string? Comments);

public class GetRhshfLegalClearanceHistoryHandler : IRequestHandler<GetRhshfLegalClearanceHistoryQuery, ApplicationResult<List<RhshfLegalClearanceDto>>>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfLegalClearanceRepository _legalRepo;
    private readonly IUserNameResolver _names;

    public GetRhshfLegalClearanceHistoryHandler(
        IRhshfCreditProfileRepository repo, IRhshfLegalClearanceRepository legalRepo, IUserNameResolver names)
    {
        _repo = repo;
        _legalRepo = legalRepo;
        _names = names;
    }

    public async Task<ApplicationResult<List<RhshfLegalClearanceDto>>> Handle(GetRhshfLegalClearanceHistoryQuery request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult<List<RhshfLegalClearanceDto>>.Failure("Case not found.");

        var history = await _legalRepo.GetByProfileAndCycleAsync(profile.Id, profile.CurrentCycleNumber, ct);
        var names = await _names.ResolveManyAsync(history.Select(c => c.LegalOfficerId), ct);
        var dto = history.Select(c => new RhshfLegalClearanceDto(
            c.CycleNumber, c.LegalOfficerId, names.TryGetValue(c.LegalOfficerId, out var n) ? n : "—",
            c.ClearedAt, c.Outcome, c.Comments)).ToList();

        return ApplicationResult<List<RhshfLegalClearanceDto>>.Success(dto);
    }
}

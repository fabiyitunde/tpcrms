using CRMS.Application.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Queries;

public record GetRhshfLegalClearanceHistoryQuery(string Reference) : IRequest<ApplicationResult<List<RhshfLegalClearanceDto>>>;

public record RhshfLegalClearanceDto(int CycleNumber, Guid LegalOfficerId, DateTime ClearedAt, RhshfLegalClearanceOutcome Outcome, string? Comments);

public class GetRhshfLegalClearanceHistoryHandler : IRequestHandler<GetRhshfLegalClearanceHistoryQuery, ApplicationResult<List<RhshfLegalClearanceDto>>>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfLegalClearanceRepository _legalRepo;

    public GetRhshfLegalClearanceHistoryHandler(IRhshfCreditProfileRepository repo, IRhshfLegalClearanceRepository legalRepo)
    {
        _repo = repo;
        _legalRepo = legalRepo;
    }

    public async Task<ApplicationResult<List<RhshfLegalClearanceDto>>> Handle(GetRhshfLegalClearanceHistoryQuery request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult<List<RhshfLegalClearanceDto>>.Failure("Case not found.");

        var history = await _legalRepo.GetByProfileAndCycleAsync(profile.Id, profile.CurrentCycleNumber, ct);
        var dto = history.Select(c => new RhshfLegalClearanceDto(c.CycleNumber, c.LegalOfficerId, c.ClearedAt, c.Outcome, c.Comments)).ToList();

        return ApplicationResult<List<RhshfLegalClearanceDto>>.Success(dto);
    }
}

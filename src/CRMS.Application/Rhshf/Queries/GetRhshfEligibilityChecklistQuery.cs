using CRMS.Application.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Queries;

public record GetRhshfEligibilityChecklistQuery(string Reference) : IRequest<ApplicationResult<List<RhshfEligibilityCheckDto>>>;

public record RhshfEligibilityCheckDto(RhshfEligibilityCriterion Criterion, bool IsSatisfied, string? Notes, Guid VerifiedBy, DateTime VerifiedAt);

public class GetRhshfEligibilityChecklistHandler : IRequestHandler<GetRhshfEligibilityChecklistQuery, ApplicationResult<List<RhshfEligibilityCheckDto>>>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfEligibilityCheckRepository _eligibilityRepo;

    public GetRhshfEligibilityChecklistHandler(IRhshfCreditProfileRepository repo, IRhshfEligibilityCheckRepository eligibilityRepo)
    {
        _repo = repo;
        _eligibilityRepo = eligibilityRepo;
    }

    public async Task<ApplicationResult<List<RhshfEligibilityCheckDto>>> Handle(GetRhshfEligibilityChecklistQuery request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult<List<RhshfEligibilityCheckDto>>.Failure("Case not found.");

        var checks = await _eligibilityRepo.GetByProfileAndCycleAsync(profile.Id, profile.CurrentCycleNumber, ct);
        var dto = checks.Select(c => new RhshfEligibilityCheckDto(c.Criterion, c.IsSatisfied, c.Notes, c.VerifiedBy, c.VerifiedAt)).ToList();

        return ApplicationResult<List<RhshfEligibilityCheckDto>>.Success(dto);
    }
}

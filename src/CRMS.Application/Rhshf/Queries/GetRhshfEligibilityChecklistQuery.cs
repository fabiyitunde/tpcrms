using CRMS.Application.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Queries;

public record GetRhshfEligibilityChecklistQuery(string Reference) : IRequest<ApplicationResult<List<RhshfEligibilityCheckDto>>>;

public record RhshfEligibilityCheckDto(RhshfEligibilityCriterion Criterion, bool IsSatisfied, string? Notes, Guid VerifiedBy, string VerifiedByName, DateTime VerifiedAt);

public class GetRhshfEligibilityChecklistHandler : IRequestHandler<GetRhshfEligibilityChecklistQuery, ApplicationResult<List<RhshfEligibilityCheckDto>>>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfEligibilityCheckRepository _eligibilityRepo;
    private readonly IUserNameResolver _names;

    public GetRhshfEligibilityChecklistHandler(
        IRhshfCreditProfileRepository repo, IRhshfEligibilityCheckRepository eligibilityRepo, IUserNameResolver names)
    {
        _repo = repo;
        _eligibilityRepo = eligibilityRepo;
        _names = names;
    }

    public async Task<ApplicationResult<List<RhshfEligibilityCheckDto>>> Handle(GetRhshfEligibilityChecklistQuery request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult<List<RhshfEligibilityCheckDto>>.Failure("Case not found.");

        var checks = await _eligibilityRepo.GetByProfileAndCycleAsync(profile.Id, profile.CurrentCycleNumber, ct);
        // Resolved per-criterion, not once for the whole checklist — nothing guarantees a single
        // verifier recorded every item.
        var names = await _names.ResolveManyAsync(checks.Select(c => c.VerifiedBy), ct);
        var dto = checks.Select(c => new RhshfEligibilityCheckDto(
            c.Criterion, c.IsSatisfied, c.Notes, c.VerifiedBy,
            names.TryGetValue(c.VerifiedBy, out var n) ? n : "—", c.VerifiedAt)).ToList();

        return ApplicationResult<List<RhshfEligibilityCheckDto>>.Success(dto);
    }
}

using CRMS.Application.Common;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>
/// Pre-fills the FAC's director list from core banking during profiling — the BOA account's registered
/// directors and mandate signatories, WITH their BVNs (which CAC does not return). An assist, not an
/// authority: it adds anyone carrying a valid BVN who isn't already on the case (deduped by BVN), and
/// the FAC then confirms, edits or removes. Returns how many were added.
/// </summary>
public record PullRhshfDirectorsFromCbsCommand(string Reference) : IRequest<ApplicationResult<int>>;

public class PullRhshfDirectorsFromCbsHandler : IRequestHandler<PullRhshfDirectorsFromCbsCommand, ApplicationResult<int>>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly ICoreBankingService _cbs;
    private readonly IUnitOfWork _uow;

    public PullRhshfDirectorsFromCbsHandler(IRhshfCreditProfileRepository repo, ICoreBankingService cbs, IUnitOfWork uow)
    {
        _repo = repo;
        _cbs = cbs;
        _uow = uow;
    }

    public async Task<ApplicationResult<int>> Handle(PullRhshfDirectorsFromCbsCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult<int>.Failure("Case not found.");
        if (string.IsNullOrWhiteSpace(profile.BoaAccountNumber))
            return ApplicationResult<int>.Failure("No BOA account number is on file for this case.");

        // Candidates = the corporate's registered directors + the account's mandate signatories.
        var candidates = new List<(string Name, string? Bvn, decimal? Shareholding)>();

        var corp = await _cbs.GetCorporateInfoAsync(profile.BoaAccountNumber, ct);
        if (corp.IsSuccess)
        {
            var directors = await _cbs.GetDirectorsAsync(corp.Value.CorporateId, ct);
            if (directors.IsSuccess)
                candidates.AddRange(directors.Value.Select(d => (d.FullName, d.BVN, d.ShareholdingPercent)));
        }

        var signatories = await _cbs.GetSignatoriesAsync(profile.BoaAccountNumber, ct);
        if (signatories.IsSuccess)
            candidates.AddRange(signatories.Value.Select(s => (s.FullName, s.BVN, (decimal?)null)));

        var existingBvns = profile.Directors
            .Where(d => !string.IsNullOrWhiteSpace(d.Bvn))
            .Select(d => d.Bvn!.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var c in candidates
                     .Where(c => !string.IsNullOrWhiteSpace(c.Bvn) && c.Bvn!.Trim().Length == 11 && c.Bvn.Trim().All(char.IsDigit))
                     .GroupBy(c => c.Bvn!.Trim(), StringComparer.OrdinalIgnoreCase)
                     .Select(g => g.First()))
        {
            var bvn = c.Bvn!.Trim();
            if (existingBvns.Contains(bvn))
                continue;

            var result = RhshfDirector.CreateManual(profile.Id, c.Name, bvn, c.Shareholding, isChairman: false, email: null, phoneNumber: null);
            if (result.IsFailure)
                continue;

            profile.AddDirector(result.Value);
            existingBvns.Add(bvn);
            added++;
        }

        if (added > 0)
        {
            profile.SetAuditInfo("CBS-PULL");
            await _uow.SaveChangesAsync(ct);
        }

        return ApplicationResult<int>.Success(added);
    }
}

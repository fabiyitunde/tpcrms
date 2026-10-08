using CRMS.Application.Common;
using CRMS.Application.Rhshf.Queries;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Interfaces;
using System.Text.Json;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>
/// Runs (or re-runs) the directors cross-check: fetches the CAC registry and the core-banking record
/// live, matches each against the FAC-declared directors server-side (BVN, else normalised name), and
/// SAVES the result as a snapshot so the tab can show it later without re-billing CAC. This is the
/// only path that hits the billed CAC lookup — it is officer-triggered, never automatic. Matching is
/// done server-side so raw BVNs never reach the browser. CAC/CBS parties are never merged into the
/// case director list (that merge caused duplicate rows and duplicate billable bureau checks).
/// </summary>
public record RunRhshfDirectorCrossCheckCommand(string Reference, Guid ActorUserId)
    : IRequest<ApplicationResult<RhshfDirectorCrossCheckDto?>>;

public class RunRhshfDirectorCrossCheckHandler
    : IRequestHandler<RunRhshfDirectorCrossCheckCommand, ApplicationResult<RhshfDirectorCrossCheckDto?>>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly ISmartComplyProvider _smartComply;
    private readonly ICoreBankingService _cbs;
    private readonly IRhshfDirectorCrossCheckRepository _snapshotRepo;
    private readonly IUnitOfWork _uow;

    public RunRhshfDirectorCrossCheckHandler(
        IRhshfCreditProfileRepository repo, ISmartComplyProvider smartComply, ICoreBankingService cbs,
        IRhshfDirectorCrossCheckRepository snapshotRepo, IUnitOfWork uow)
    {
        _repo = repo;
        _smartComply = smartComply;
        _cbs = cbs;
        _snapshotRepo = snapshotRepo;
        _uow = uow;
    }

    public async Task<ApplicationResult<RhshfDirectorCrossCheckDto?>> Handle(
        RunRhshfDirectorCrossCheckCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult<RhshfDirectorCrossCheckDto?>.Failure("Case not found.");

        // Reference people as (name, bvn) tuples — BVN kept server-side for matching only.
        var cacPeople = new List<(string Name, string? Bvn, string? Detail)>();
        var cbsDirectors = new List<(string Name, string? Bvn, string? Detail)>();
        var cbsSignatories = new List<(string Name, string? Bvn, string? Detail)>();

        // ── CAC (billed) ────────────────────────────────────────────────────────────────────────
        bool cacOk = false;
        string? cacStatus = null, cacError = null;
        if (string.IsNullOrWhiteSpace(profile.RcNumber))
        {
            cacError = "No RC number on the case to look up.";
        }
        else
        {
            var cac = await _smartComply.VerifyCacAdvancedAsync(
                profile.RcNumber.Trim(), profile.CompanyName, DeriveCompanyType(profile.RcNumber), ct);
            if (cac.IsFailure)
            {
                cacError = cac.Error ?? "CAC lookup failed.";
            }
            else
            {
                cacOk = true;
                cacStatus = cac.Value.Status;
                foreach (var d in cac.Value.Directors)
                {
                    var name = !string.IsNullOrWhiteSpace(d.FullName)
                        ? d.FullName!
                        : $"{d.FirstName} {d.OtherName} {d.Surname}".Replace("  ", " ").Trim();
                    var role = (d.IsChairman ?? false) ? "Chairman" : (d.AffiliateType ?? "Director");
                    cacPeople.Add((name, Blank(d.IdentityNumber), role));
                }
            }
        }

        // ── Core banking ──────────────────────────────────────────────────────────────────────
        bool cbsOk = false;
        string? cbsError = null;
        var corp = await _cbs.GetCorporateInfoAsync(profile.BoaAccountNumber, ct);
        if (corp.IsFailure || corp.Value is null)
        {
            cbsError = corp.Error ?? "No core-banking corporate record for this account.";
        }
        else
        {
            cbsOk = true;
            var dir = await _cbs.GetDirectorsAsync(corp.Value.CorporateId, ct);
            if (dir.IsSuccess && dir.Value is not null)
                cbsDirectors.AddRange(dir.Value.Select(d =>
                    (d.FullName, Blank(d.BVN), d.ShareholdingPercent.HasValue ? $"{d.ShareholdingPercent:0.##}%" : "Director")));

            var sig = await _cbs.GetSignatoriesAsync(profile.BoaAccountNumber, ct);
            if (sig.IsSuccess && sig.Value is not null)
                cbsSignatories.AddRange(sig.Value.Select(s =>
                    (s.FullName, Blank(s.BVN), string.IsNullOrWhiteSpace(s.Designation) ? s.MandateType : s.Designation)));
        }

        var cbsAll = cbsDirectors.Concat(cbsSignatories).ToList();

        // ── Match (server-side) ─────────────────────────────────────────────────────────────────
        var fac = profile.Directors
            .OrderByDescending(d => d.IsChairman).ThenBy(d => d.FullName)
            .Select(d => new RhshfCrossCheckFacDto(
                d.FullName,
                !string.IsNullOrWhiteSpace(d.Bvn),
                d.ShareholdingPercent,
                d.IsChairman,
                InCac: cacOk ? cacPeople.Any(p => NameMatch(p.Name, d.FullName)) : null,
                InCbs: cbsOk ? cbsAll.Any(p => Matches(p.Bvn, p.Name, d.Bvn, d.FullName)) : null))
            .ToList();

        RhshfCrossCheckRefDto ToRef((string Name, string? Bvn, string? Detail) p) => new(
            p.Name, p.Detail, !string.IsNullOrWhiteSpace(p.Bvn),
            DeclaredByFac: profile.Directors.Any(d => Matches(p.Bvn, p.Name, d.Bvn, d.FullName)));

        var dto = new RhshfDirectorCrossCheckDto(
            fac,
            cacOk, cacStatus, cacError, cacPeople.Select(ToRef).ToList(),
            cbsOk, cbsError, cbsDirectors.Select(ToRef).ToList(), cbsSignatories.Select(ToRef).ToList(),
            FetchedAt: DateTime.UtcNow);

        // Persist as the case's saved snapshot (one per profile) so the next view is free.
        var json = JsonSerializer.Serialize(dto);
        var existing = await _snapshotRepo.GetByProfileIdAsync(profile.Id, ct);
        if (existing is null)
            await _snapshotRepo.AddAsync(new RhshfDirectorCrossCheck(profile.Id, json, request.ActorUserId), ct);
        else
        {
            existing.Replace(json, request.ActorUserId);
            _snapshotRepo.Update(existing);
        }
        await _uow.SaveChangesAsync(ct);

        return ApplicationResult<RhshfDirectorCrossCheckDto?>.Success(dto);
    }

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string Norm(string? name) =>
        string.Join(' ', (name ?? "").Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static bool NameMatch(string? a, string? b) => Norm(a).Length > 0 && Norm(a) == Norm(b);

    /// <summary>BVN is the strong key when both sides have one; otherwise fall back to a normalised
    /// name match.</summary>
    private static bool Matches(string? bvnA, string? nameA, string? bvnB, string? nameB)
        => (!string.IsNullOrWhiteSpace(bvnA) && !string.IsNullOrWhiteSpace(bvnB) && bvnA.Trim() == bvnB.Trim())
           || NameMatch(nameA, nameB);

    private static string DeriveCompanyType(string rcNumber)
    {
        var rc = rcNumber.Trim().ToUpperInvariant();
        if (rc.StartsWith("BN")) return "BN";
        if (rc.StartsWith("IT")) return "IT";
        return "RC";
    }
}

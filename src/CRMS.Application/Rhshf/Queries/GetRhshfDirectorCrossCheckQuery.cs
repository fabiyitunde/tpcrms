using CRMS.Application.Common;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Queries;

/// <summary>
/// Read-only verification view: the directors the FAC declared, set beside what the CAC registry and
/// the core-banking record independently say, so an officer can spot discrepancies. Nothing here is
/// written back — in particular CAC/CBS parties are NOT merged into the case director list (that
/// merge was the source of duplicate rows and, worse, duplicate billable bureau checks). Matching
/// (by BVN, else normalised name) is done server-side so raw BVNs never reach the browser.
///
/// CAC is a billed SmartComply lookup, so this runs on demand (officer-triggered), never on load.
/// </summary>
public record GetRhshfDirectorCrossCheckQuery(string Reference) : IRequest<ApplicationResult<RhshfDirectorCrossCheckDto>>;

public record RhshfDirectorCrossCheckDto(
    IReadOnlyList<RhshfCrossCheckFacDto> Fac,
    bool CacOk, string? CacStatus, string? CacError, IReadOnlyList<RhshfCrossCheckRefDto> Cac,
    bool CbsOk, string? CbsError, IReadOnlyList<RhshfCrossCheckRefDto> CbsDirectors, IReadOnlyList<RhshfCrossCheckRefDto> CbsSignatories);

/// <summary>A director the FAC declared, with whether the two reference sources corroborate them.
/// Null flags mean that source could not be reached this run.</summary>
public record RhshfCrossCheckFacDto(
    string FullName, bool HasBvn, decimal? ShareholdingPercent, bool IsChairman, bool? InCac, bool? InCbs);

/// <summary>A party from a reference source (CAC or CBS), with whether the FAC declared them.</summary>
public record RhshfCrossCheckRefDto(string FullName, string? Detail, bool HasBvn, bool DeclaredByFac);

public class GetRhshfDirectorCrossCheckHandler
    : IRequestHandler<GetRhshfDirectorCrossCheckQuery, ApplicationResult<RhshfDirectorCrossCheckDto>>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly ISmartComplyProvider _smartComply;
    private readonly ICoreBankingService _cbs;

    public GetRhshfDirectorCrossCheckHandler(
        IRhshfCreditProfileRepository repo, ISmartComplyProvider smartComply, ICoreBankingService cbs)
    {
        _repo = repo;
        _smartComply = smartComply;
        _cbs = cbs;
    }

    public async Task<ApplicationResult<RhshfDirectorCrossCheckDto>> Handle(
        GetRhshfDirectorCrossCheckQuery request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult<RhshfDirectorCrossCheckDto>.Failure("Case not found.");

        // Reference people as (name, bvn) tuples — BVN kept server-side for matching only.
        var cacPeople = new List<(string Name, string? Bvn, string? Detail)>();
        var cbsDirectors = new List<(string Name, string? Bvn, string? Detail)>();
        var cbsSignatories = new List<(string Name, string? Bvn, string? Detail)>();

        // ── CAC ───────────────────────────────────────────────────────────────────────────────
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
            cbsOk, cbsError, cbsDirectors.Select(ToRef).ToList(), cbsSignatories.Select(ToRef).ToList());

        return ApplicationResult<RhshfDirectorCrossCheckDto>.Success(dto);
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

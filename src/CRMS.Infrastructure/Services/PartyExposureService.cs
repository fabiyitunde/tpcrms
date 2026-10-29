using System.Reflection;
using CRMS.Application.PartyExposure;
using CRMS.Application.Rhshf.Webhooks;
using CRMS.Domain.Aggregates.PartyExposure;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using CRMS.Infrastructure.ExternalServices.Rhshf;
using CRMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CRMS.Infrastructure.Services;

/// <summary>
/// The party-exposure lookup (CRMSQA#4). Reads across all three loan tracks directly, as the
/// reporting service does, because no single aggregate owns "everything this person is involved in".
/// <para>
/// Every search compares an exact, already-normalised value against an indexed column (see
/// AddPartyExposureLookup migration) — no LIKE, no function on the column, no OR across columns — so
/// each one is an index lookup however large the tables grow. Each identifier is its own query for
/// the same reason.
/// </para>
/// </summary>
public class PartyExposureService : IPartyExposureService
{
    public const string TrackCorporate = "CORPORATE";
    public const string TrackNamp = "NAMP";
    public const string TrackRhshf = "RHSHF";

    private static readonly string CrmsVersion =
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";

    private readonly CRMSDbContext _db;
    private readonly IRhshfCreditProfileRepository _rhshfProfiles;
    private readonly IRhshfOfferRepository _rhshfOffers;
    private readonly IFineractDirectService _coreBanking;
    private readonly PartyExposureSettings _settings;
    private readonly string _hashKey;
    private readonly ILogger<PartyExposureService> _logger;

    public PartyExposureService(
        CRMSDbContext db,
        IRhshfCreditProfileRepository rhshfProfiles,
        IRhshfOfferRepository rhshfOffers,
        IFineractDirectService coreBanking,
        IOptions<PartyExposureSettings> settings,
        IOptions<RhshfSettings> rhshfSettings,
        ILogger<PartyExposureService> logger)
    {
        _db = db;
        _rhshfProfiles = rhshfProfiles;
        _rhshfOffers = rhshfOffers;
        _coreBanking = coreBanking;
        _settings = settings.Value;
        _hashKey = ResolveHashKey(_settings, rhshfSettings.Value);
        _logger = logger;
    }

    public static string ResolveHashKey(PartyExposureSettings settings, RhshfSettings rhshf)
        => !string.IsNullOrWhiteSpace(settings.HashKey) ? settings.HashKey
            : !string.IsNullOrWhiteSpace(rhshf.CallbackSigningSecret) ? rhshf.CallbackSigningSecret
            : "crms-party-exposure-local";

    public string HashIdentifier(string value) => PartyExposureRules.KeyedHash(value, _hashKey);

    public async Task<PartyExposureResponse> LookupAsync(string requestId, ExposureQuery query, CancellationToken ct = default)
    {
        var candidates = new List<ExposureCandidate>();
        candidates.AddRange(await CorporateAsync(query, ct));
        candidates.AddRange(await NampAsync(query, ct));
        candidates.AddRange(await RhshfAsync(query, ct));

        var now = DateTime.UtcNow;
        var kept = PartyExposureRules.ApplyFilters(candidates, query, now);
        var balances = query.FetchLiveBalances
            ? await LiveBalancesAsync(kept, ct)
            : new Dictionary<long, LiveBalance?>();

        return PartyExposureRules.BuildResponse(
            requestId, query, candidates, balances, _settings.MaxRelationships, _hashKey, CrmsVersion, now);
    }

    // ── Corporate ────────────────────────────────────────────────────────────────────────────────

    private record CorporateCase(
        Guid Id, string Number, string ProductCode, LoanApplicationType Type, LoanApplicationStatus Status,
        string CustomerName, string AccountNumber, decimal? Approved, DateTime Created, DateTime? ApprovedAt,
        DateTime? DisbursedAt, long? LoanId, string? CoreBankingLoanId);

    private async Task<List<ExposureCandidate>> CorporateAsync(ExposureQuery q, CancellationToken ct)
    {
        var found = new List<(Guid CaseId, string Type, string? Role, string? Bvn, string? Nin, string? Nuban,
            string Matched, decimal? Guarantee, DateTime Since, DateTime? Ended)>();

        if (q.Nubans.Count > 0)
        {
            var byAccount = await _db.LoanApplications.AsNoTracking()
                .Where(a => q.Nubans.Contains(a.AccountNumber))
                .Select(a => new { a.Id, a.AccountNumber, a.CreatedAt })
                .ToListAsync(ct);
            found.AddRange(byAccount.Select(a => (a.Id, RelationshipTypes.Borrower, (string?)null, (string?)null,
                (string?)null, (string?)a.AccountNumber, IdentifierTypes.Nuban, (decimal?)null, a.CreatedAt, (DateTime?)null)));
        }

        if (q.Bvn is not null)
        {
            var parties = await _db.LoanApplicationParties.AsNoTracking()
                .Where(p => p.BVN == q.Bvn)
                .Select(p => new { p.LoanApplicationId, p.PartyType, p.Designation, p.BVN, p.CreatedAt })
                .ToListAsync(ct);
            foreach (var p in parties)
            {
                var (type, role) = p.PartyType switch
                {
                    PartyType.Director => (RelationshipTypes.DirectorOrOwner, p.Designation ?? "Director"),
                    PartyType.BeneficialOwner => (RelationshipTypes.DirectorOrOwner, "Beneficial owner"),
                    PartyType.Signatory => (RelationshipTypes.AccountSignatory, p.Designation ?? "Signatory"),
                    _ => (RelationshipTypes.Guarantor, "Guarantor named on the application"),
                };
                found.Add((p.LoanApplicationId, type, role, p.BVN, null, null, IdentifierTypes.Bvn, null, p.CreatedAt, null));
            }
        }

        // Two queries rather than one OR, so each uses its own index.
        var guarantors = new List<(Guid, string?, string?, string, decimal?, DateTime, GuarantorStatus)>();
        if (q.Bvn is not null)
            guarantors.AddRange((await _db.Guarantors.AsNoTracking().Where(g => g.BVN == q.Bvn)
                    .Select(g => new { g.LoanApplicationId, g.BVN, g.NIN, Limit = (decimal?)g.GuaranteeLimit!.Amount, g.CreatedAt, g.Status })
                    .ToListAsync(ct))
                .Select(g => (g.LoanApplicationId, g.BVN, g.NIN, IdentifierTypes.Bvn, g.Limit, g.CreatedAt, g.Status)));
        if (q.Nin is not null)
            guarantors.AddRange((await _db.Guarantors.AsNoTracking().Where(g => g.NIN == q.Nin)
                    .Select(g => new { g.LoanApplicationId, g.BVN, g.NIN, Limit = (decimal?)g.GuaranteeLimit!.Amount, g.CreatedAt, g.Status })
                    .ToListAsync(ct))
                .Select(g => (g.LoanApplicationId, g.BVN, g.NIN, IdentifierTypes.Nin, g.Limit, g.CreatedAt, g.Status)));
        foreach (var g in guarantors)
        {
            DateTime? ended = g.Item7 is GuarantorStatus.Released or GuarantorStatus.Rejected ? g.Item6 : null;
            found.Add((g.Item1, RelationshipTypes.Guarantor, "Personal guarantor", g.Item2, g.Item3, null, g.Item4, g.Item5, g.Item6, ended));
        }

        if (found.Count == 0)
            return [];

        var ids = found.Select(f => f.CaseId).Distinct().ToList();
        var cases = await _db.LoanApplications.AsNoTracking()
            .Where(a => ids.Contains(a.Id))
            .Select(a => new CorporateCase(
                a.Id, a.ApplicationNumber, a.ProductCode, a.Type, a.Status, a.CustomerName, a.AccountNumber,
                a.ApprovedAmount == null ? null : a.ApprovedAmount.Amount,
                a.CreatedAt, a.FinalApprovedAt, a.DisbursedAt, a.FineractLoanId, a.CoreBankingLoanId))
            .ToDictionaryAsync(a => a.Id, ct);
        var lastChanged = await _db.LoanApplicationStatusHistory.AsNoTracking()
            .Where(h => ids.Contains(h.LoanApplicationId))
            .GroupBy(h => h.LoanApplicationId)
            .Select(g => new { g.Key, Last = g.Max(h => h.ChangedAt) })
            .ToDictionaryAsync(x => x.Key, x => x.Last, ct);

        return Merge(found.Select(f => (Key: (f.CaseId, f.Type, f.Role, f.Bvn, f.Nin, f.Nuban), f.Matched, f.Guarantee, f.Since, f.Ended)))
            .Where(m => cases.ContainsKey(m.Key.CaseId))
            .Select(m =>
            {
                var c = cases[m.Key.CaseId];
                var individual = c.Type == LoanApplicationType.Retail;
                return new ExposureCandidate(
                    m.Key.Type, m.Key.Role, TrackCorporate, c.Number, c.ProductCode,
                    individual ? "RETAIL_LOAN" : "CORPORATE_LOAN",
                    individual ? ObligorTypes.Individual : ObligorTypes.Corporate, c.CustomerName,
                    PartyExposureRules.CorporateStatus(c.Status), null,
                    c.Approved, null, m.Guarantee,
                    c.Created, c.ApprovedAt, c.DisbursedAt,
                    lastChanged.TryGetValue(c.Id, out var last) ? last : c.Created,
                    c.LoanId, c.CoreBankingLoanId,
                    m.Key.Bvn, m.Key.Nin, m.Key.Nuban ?? (m.Key.Type == RelationshipTypes.Borrower ? c.AccountNumber : null),
                    m.Matched, m.Since, m.Ended);
            })
            .ToList();
    }

    // ── NAMP ─────────────────────────────────────────────────────────────────────────────────────

    private async Task<List<ExposureCandidate>> NampAsync(ExposureQuery q, CancellationToken ct)
    {
        var found = new List<(Guid CaseId, string Type, string? Role, string? Bvn, string? Nin, string? Nuban,
            string Matched, decimal? Guarantee, DateTime Since, DateTime? Ended)>();

        async Task Applicants(IQueryable<Domain.Aggregates.Namp.NampApplication> source, string matched)
        {
            var rows = await source.AsNoTracking()
                .Select(a => new { a.Id, a.Bvn, a.Nin, a.BoaAccountNumber, a.ApplicantCategory, a.CreatedAt })
                .ToListAsync(ct);
            foreach (var a in rows)
            {
                // A company's application names a contact person; that person is not the borrower.
                var company = a.ApplicantCategory == NampApplicantCategory.AgroServiceCompany;
                var (type, role) = company && matched != IdentifierTypes.Nuban
                    ? (RelationshipTypes.Other, "Named as the contact on a company's application")
                    : (RelationshipTypes.Borrower, (string?)null);
                found.Add((a.Id, type, role, a.Bvn, a.Nin, a.BoaAccountNumber, matched, null, a.CreatedAt, null));
            }
        }

        if (q.Bvn is not null) await Applicants(_db.NampApplications.Where(a => a.Bvn == q.Bvn), IdentifierTypes.Bvn);
        if (q.Nin is not null) await Applicants(_db.NampApplications.Where(a => a.Nin == q.Nin), IdentifierTypes.Nin);
        if (q.Nubans.Count > 0)
            await Applicants(_db.NampApplications.Where(a => q.Nubans.Contains(a.BoaAccountNumber)), IdentifierTypes.Nuban);

        async Task Guarantors(IQueryable<Domain.Aggregates.Namp.NampGuarantor> source, string matched)
        {
            var rows = await source.AsNoTracking()
                .Select(g => new { g.NampApplicationId, g.Bvn, g.Nin, g.GuaranteeLimit, g.CreatedAt })
                .ToListAsync(ct);
            found.AddRange(rows.Select(g => (g.NampApplicationId, RelationshipTypes.Guarantor, (string?)"Personal guarantor",
                g.Bvn, g.Nin, (string?)null, matched, g.GuaranteeLimit, g.CreatedAt, (DateTime?)null)));
        }

        if (q.Bvn is not null) await Guarantors(_db.NampGuarantors.Where(g => g.Bvn == q.Bvn), IdentifierTypes.Bvn);
        if (q.Nin is not null) await Guarantors(_db.NampGuarantors.Where(g => g.Nin == q.Nin), IdentifierTypes.Nin);

        if (q.Bvn is not null)
        {
            var directors = await _db.NampDirectors.AsNoTracking()
                .Where(d => d.Bvn == q.Bvn)
                .Select(d => new { d.NampApplicationId, d.Bvn, d.CreatedAt })
                .ToListAsync(ct);
            found.AddRange(directors.Select(d => (d.NampApplicationId, RelationshipTypes.DirectorOrOwner, (string?)"Director",
                d.Bvn, (string?)null, (string?)null, IdentifierTypes.Bvn, (decimal?)null, d.CreatedAt, (DateTime?)null)));
        }

        if (found.Count == 0)
            return [];

        var ids = found.Select(f => f.CaseId).Distinct().ToList();
        var cases = await _db.NampApplications.AsNoTracking()
            .Where(a => ids.Contains(a.Id))
            .Select(a => new
            {
                a.Id, a.ApplicationNumber, a.Status, a.ApplicantCategory, a.ApplicantName, a.CompanyName,
                a.BoaAccountNumber, a.LoanAmount, a.CreatedAt, a.RatifiedAt, a.FineractDisbursedOn,
                a.FineractLoanId, a.FineractLoanAccountNumber,
            })
            .ToDictionaryAsync(a => a.Id, ct);
        var lastChanged = await _db.NampStatusHistory.AsNoTracking()
            .Where(h => ids.Contains(h.NampApplicationId))
            .GroupBy(h => h.NampApplicationId)
            .Select(g => new { g.Key, Last = g.Max(h => h.ChangedAt) })
            .ToDictionaryAsync(x => x.Key, x => x.Last, ct);

        return Merge(found.Select(f => (Key: (f.CaseId, f.Type, f.Role, f.Bvn, f.Nin, f.Nuban), f.Matched, f.Guarantee, f.Since, f.Ended)))
            .Where(m => cases.ContainsKey(m.Key.CaseId))
            .Select(m =>
            {
                var c = cases[m.Key.CaseId];
                var company = c.ApplicantCategory == NampApplicantCategory.AgroServiceCompany;
                return new ExposureCandidate(
                    m.Key.Type, m.Key.Role, TrackNamp, c.ApplicationNumber, "NAMP", "EQUIPMENT_FINANCE",
                    company ? ObligorTypes.Corporate : ObligorTypes.Individual,
                    company ? c.CompanyName ?? c.ApplicantName : c.ApplicantName,
                    PartyExposureRules.NampStatus(c.Status, c.FineractDisbursedOn), null,
                    c.LoanAmount, null, m.Guarantee,
                    c.CreatedAt, c.RatifiedAt, c.FineractDisbursedOn,
                    lastChanged.TryGetValue(c.Id, out var last) ? last : c.CreatedAt,
                    c.FineractLoanId, c.FineractLoanAccountNumber,
                    m.Key.Bvn, m.Key.Nin, m.Key.Nuban,
                    m.Matched, m.Since, m.Ended);
            })
            .ToList();
    }

    // ── RH-SHF ───────────────────────────────────────────────────────────────────────────────────

    private async Task<List<ExposureCandidate>> RhshfAsync(ExposureQuery q, CancellationToken ct)
    {
        var found = new List<(Guid CaseId, string Type, string? Role, string? Bvn, string? Nin, string? Nuban,
            string Matched, decimal? Guarantee, DateTime Since, DateTime? Ended)>();

        if (q.Nubans.Count > 0)
        {
            var facs = await _db.RhshfCreditProfiles.AsNoTracking()
                .Where(p => q.Nubans.Contains(p.BoaAccountNumber))
                .Select(p => new { p.Id, p.BoaAccountNumber, p.CreatedAt })
                .ToListAsync(ct);
            found.AddRange(facs.Select(p => (p.Id, RelationshipTypes.Borrower, (string?)null, (string?)null, (string?)null,
                (string?)p.BoaAccountNumber, IdentifierTypes.Nuban, (decimal?)null, p.CreatedAt, (DateTime?)null)));
        }

        if (q.Bvn is not null)
        {
            var directors = await _db.RhshfDirectors.AsNoTracking()
                .Where(d => d.Bvn == q.Bvn)
                .Select(d => new { d.RhshfCreditProfileId, d.Bvn, d.CreatedAt })
                .ToListAsync(ct);
            found.AddRange(directors.Select(d => (d.RhshfCreditProfileId, RelationshipTypes.DirectorOrOwner, (string?)"Director",
                d.Bvn, (string?)null, (string?)null, IdentifierTypes.Bvn, (decimal?)null, d.CreatedAt, (DateTime?)null)));

            var guarantors = await _db.RhshfGuarantors.AsNoTracking()
                .Where(g => g.Bvn == q.Bvn)
                .Select(g => new { g.RhshfCreditProfileId, g.Bvn, g.GuaranteeAmount, g.CreatedAt })
                .ToListAsync(ct);
            found.AddRange(guarantors.Select(g => (g.RhshfCreditProfileId, RelationshipTypes.Guarantor, (string?)"Personal guarantor",
                g.Bvn, (string?)null, (string?)null, IdentifierTypes.Bvn, g.GuaranteeAmount, g.CreatedAt, (DateTime?)null)));
        }

        if (found.Count == 0)
            return [];

        var ids = found.Select(f => f.CaseId).Distinct().ToList();
        var cases = await _db.RhshfCreditProfiles.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new
            {
                p.Id, p.Reference, p.ProgrammeCode, p.CompanyName, p.BoaAccountNumber, p.Status, p.InternalStage,
                p.ApprovedAmount, p.CreatedAt, p.DecidedAt, p.UpdatedAt,
            })
            .ToDictionaryAsync(p => p.Id, ct);
        var booked = (await _db.RhshfDisbursements.AsNoTracking()
                .Where(d => ids.Contains(d.RhshfCreditProfileId) && d.Status == RhshfDisbursementStatus.Booked)
                .Select(d => new { d.RhshfCreditProfileId, d.FineractLoanId, d.FineractLoanAccountNumber, d.DisbursedAmount, d.BookedAt })
                .ToListAsync(ct))
            .GroupBy(d => d.RhshfCreditProfileId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(d => d.BookedAt).First());

        // The credit status the portal already receives for these cases, built the same way, so the
        // two never disagree. Matches are few per lookup, so loading each case is affordable.
        var creditStatus = new Dictionary<Guid, string>();
        foreach (var id in ids)
        {
            var profile = await _rhshfProfiles.GetByIdAsync(id, ct);
            if (profile is null) continue;
            var offer = await _rhshfOffers.GetByProfileAndCycleAsync(profile.Id, profile.CurrentCycleNumber, ct);
            creditStatus[id] = RhshfPortalStateBuilder.Build(profile, offer, null, null).CreditStatus;
        }

        return Merge(found.Select(f => (Key: (f.CaseId, f.Type, f.Role, f.Bvn, f.Nin, f.Nuban), f.Matched, f.Guarantee, f.Since, f.Ended)))
            .Where(m => cases.ContainsKey(m.Key.CaseId))
            .Select(m =>
            {
                var c = cases[m.Key.CaseId];
                booked.TryGetValue(c.Id, out var loan);
                return new ExposureCandidate(
                    m.Key.Type, m.Key.Role, TrackRhshf, c.Reference, c.ProgrammeCode, "INPUT_FINANCE",
                    ObligorTypes.Corporate, c.CompanyName,
                    PartyExposureRules.RhshfStatus(c.Status, c.InternalStage),
                    creditStatus.GetValueOrDefault(c.Id),
                    c.ApprovedAmount, loan?.DisbursedAmount, m.Guarantee,
                    c.CreatedAt, c.DecidedAt, loan?.BookedAt, c.UpdatedAt,
                    loan?.FineractLoanId, loan?.FineractLoanAccountNumber,
                    m.Key.Bvn, m.Key.Nin, m.Key.Nuban ?? (m.Key.Type == RelationshipTypes.Borrower ? c.BoaAccountNumber : null),
                    m.Matched, m.Since, m.Ended);
            })
            .ToList();
    }

    /// <summary>
    /// One record found by several identifiers (BVN and NIN on the same guarantor, say) is one
    /// relationship matched on both, not two.
    /// </summary>
    private static IEnumerable<(
        (Guid CaseId, string Type, string? Role, string? Bvn, string? Nin, string? Nuban) Key,
        IReadOnlyList<string> Matched, decimal? Guarantee, DateTime Since, DateTime? Ended)> Merge(
        IEnumerable<((Guid CaseId, string Type, string? Role, string? Bvn, string? Nin, string? Nuban) Key,
            string Matched, decimal? Guarantee, DateTime Since, DateTime? Ended)> rows)
        => rows
            .GroupBy(r => r.Key)
            .Select(g => (
                g.Key,
                (IReadOnlyList<string>)g.Select(r => r.Matched).Distinct()
                    .OrderBy(t => t switch { IdentifierTypes.Bvn => 0, IdentifierTypes.Nin => 1, _ => 2 }).ToList(),
                g.First().Guarantee,
                g.Min(r => r.Since),
                g.First().Ended));

    // ── Core banking balances ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads every active booked loan's balance at once, within the configured budget. Whatever has
    /// not answered by then is left out — the reply marks it UNAVAILABLE rather than waiting, and
    /// never shows a missing balance as zero.
    /// </summary>
    private async Task<Dictionary<long, LiveBalance?>> LiveBalancesAsync(
        IReadOnlyList<ExposureCandidate> kept, CancellationToken ct)
    {
        var loanIds = kept
            .Where(c => c.Status == FacilityStatuses.Active && c.CoreBankingLoanId is not null)
            .Select(c => c.CoreBankingLoanId!.Value)
            .Distinct()
            .ToList();
        var result = new Dictionary<long, LiveBalance?>();
        if (loanIds.Count == 0)
            return result;

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(_settings.LiveBalanceBudgetMs);

        var reads = loanIds.Select(async id =>
        {
            try
            {
                var detail = await _coreBanking.GetLoanDetailAsync(id, budget.Token);
                if (detail.IsFailure)
                    return (id, (LiveBalance?)null);
                var d = detail.Value;
                return (id, (LiveBalance?)PartyExposureRules.BalanceFrom(
                    d.Summary.PrincipalDisbursed, d.Summary.TotalOutstanding, d.MaturityDate,
                    d.RepaymentSchedule.Where(p => p.Period > 0).Select(p => (p.DueDate, p.TotalOutstanding)),
                    DateTime.UtcNow));
            }
            catch (Exception ex) when (ex is OperationCanceledException or HttpRequestException or TaskCanceledException)
            {
                return (id, (LiveBalance?)null);
            }
        }).ToList();

        try
        {
            await Task.WhenAll(reads);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Party exposure: a core banking balance read failed.");
        }

        foreach (var read in reads.Where(r => r.IsCompletedSuccessfully))
            result[read.Result.id] = read.Result.Item2;
        var missing = loanIds.Count - result.Values.Count(v => v is not null);
        if (missing > 0)
            _logger.LogInformation("Party exposure: {Missing} of {Total} core banking balances unavailable within {Budget}ms.",
                missing, loanIds.Count, _settings.LiveBalanceBudgetMs);
        return result;
    }

    // ── Audit and batches ────────────────────────────────────────────────────────────────────────

    public async Task RecordLookupAsync(PartyExposureLookupLog log, CancellationToken ct = default)
    {
        _db.PartyExposureLookupLogs.Add(log);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AddBatchAsync(PartyExposureBatch batch, CancellationToken ct = default)
    {
        _db.PartyExposureBatches.Add(batch);
        await _db.SaveChangesAsync(ct);
    }

    public Task<PartyExposureBatch?> GetBatchAsync(Guid batchId, CancellationToken ct = default)
        => _db.PartyExposureBatches.FirstOrDefaultAsync(b => b.Id == batchId, ct);

    public Task<PartyExposureBatch?> FindBatchByRequestIdAsync(string requestId, CancellationToken ct = default)
        => _db.PartyExposureBatches.AsNoTracking().FirstOrDefaultAsync(b => b.RequestId == requestId, ct);

    public async Task SaveBatchAsync(PartyExposureBatch batch, CancellationToken ct = default)
    {
        if (_db.Entry(batch).State == EntityState.Detached)
            _db.PartyExposureBatches.Update(batch);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> FailInterruptedBatchesAsync(CancellationToken ct = default)
    {
        var open = await _db.PartyExposureBatches
            .Where(b => b.Status == PartyExposureBatch.Queued || b.Status == PartyExposureBatch.Processing)
            .ToListAsync(ct);
        foreach (var b in open)
            b.Fail("The CRMS restarted before this batch finished. Its identifiers are not kept, so please send it again.");
        if (open.Count > 0)
            await _db.SaveChangesAsync(ct);
        return open.Count;
    }
}

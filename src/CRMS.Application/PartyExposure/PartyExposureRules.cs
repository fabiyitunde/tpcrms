using System.Security.Cryptography;
using System.Text;
using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Application.PartyExposure;

/// <summary>One CRMS record that matched, before it is filtered, masked and shaped for the reply.</summary>
public record ExposureCandidate(
    string RelationshipType,
    string? RoleDescription,
    string Track,
    string CaseReference,
    string? Programme,
    string FacilityType,
    string ObligorType,
    string ObligorName,
    string Status,
    string? CreditStatus,
    decimal? Approved,
    decimal? DisbursedLocal,
    decimal? GuaranteeAmount,
    DateTime Applied,
    DateTime? ApprovedAt,
    DateTime? DisbursedAt,
    DateTime LastChangedAt,
    long? CoreBankingLoanId,
    string? FacilityReference,
    string? RowBvn,
    string? RowNin,
    string? RowNuban,
    IReadOnlyList<string> MatchedOn,
    DateTime RoleSince,
    DateTime? RoleEnded);

/// <summary>A booked loan's position as core banking reports it.</summary>
public record LiveBalance(
    decimal? Disbursed, decimal Outstanding, decimal Overdue, int DaysPastDue, DateTime? Maturity, DateTime AsOf);

public static class ObligorTypes
{
    public const string Individual = "INDIVIDUAL";
    public const string Corporate = "CORPORATE";
}

public static class IdentifierTypes
{
    public const string Bvn = "BVN";
    public const string Nin = "NIN";
    public const string Nuban = "NUBAN";
}

public static class PartyExposureRules
{
    public const int DefaultHistoryMonths = 36;
    public const int MaxHistoryMonths = 120;
    public const int MaxAccounts = 10;
    public const string Currency = "NGN";

    // ── Request validation ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Checks a request and turns its identifiers into the exact form they are stored in. Errors name
    /// the field, never echo its value.
    /// </summary>
    public static (ExposureQuery? Query, List<string> Errors) Validate(
        string? requestId, string? purpose, ExposureIdentifiers? ids, ExposureInclude? include, bool fetchLiveBalances,
        bool requireRequestEnvelope = true)
    {
        var errors = new List<string>();

        if (requireRequestEnvelope)
        {
            if (string.IsNullOrWhiteSpace(requestId))
                errors.Add("requestId is required.");
            else if (requestId.Length > 100)
                errors.Add("requestId must be at most 100 characters.");

            if (string.IsNullOrWhiteSpace(purpose) || !ExposurePurposes.All.Contains(purpose))
                errors.Add($"purpose must be one of {string.Join(", ", ExposurePurposes.All)}.");
        }

        var bvn = Identifiers.Digits(ids?.Bvn);
        var nin = Identifiers.Digits(ids?.Nin);
        if (bvn is not null && !IsDigits(bvn, 11)) errors.Add("identifiers.bvn must be 11 digits.");
        if (nin is not null && !IsDigits(nin, 11)) errors.Add("identifiers.nin must be 11 digits.");

        var nubans = new List<string>();
        var accounts = ids?.Accounts ?? [];
        if (accounts.Count > MaxAccounts)
            errors.Add($"identifiers.accounts may hold at most {MaxAccounts} accounts.");
        for (var i = 0; i < Math.Min(accounts.Count, MaxAccounts); i++)
        {
            var nuban = Identifiers.Digits(accounts[i]?.Nuban);
            if (nuban is null) continue;
            if (!IsDigits(nuban, 10)) errors.Add($"identifiers.accounts[{i}].nuban must be 10 digits.");
            else if (!nubans.Contains(nuban)) nubans.Add(nuban);
        }

        if (bvn is null && nin is null && nubans.Count == 0 && errors.Count == 0)
            errors.Add("At least one identifier (bvn, nin or an account nuban) is required.");

        var months = include?.HistoryMonths ?? DefaultHistoryMonths;
        if (months < 1 || months > MaxHistoryMonths)
            errors.Add($"include.historyMonths must be between 1 and {MaxHistoryMonths}.");

        if (errors.Count > 0)
            return (null, errors);

        return (new ExposureQuery(bvn, nin, nubans, include?.ClosedFacilities ?? true, months, fetchLiveBalances), errors);
    }

    private static bool IsDigits(string value, int length) => value.Length == length && value.All(char.IsAsciiDigit);

    // ── Masking ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>Last four digits only, the rest starred. Never the whole number.</summary>
    public static string MaskIdentifier(string value)
        => value.Length <= 4 ? new string('*', value.Length) : new string('*', value.Length - 4) + value[^4..];

    /// <summary>"Adamu Bello" → "A**** B****". Individuals' names are never returned in full.</summary>
    public static string MaskName(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? "****" : string.Join(' ', parts.Select(p => $"{char.ToUpperInvariant(p[0])}****"));
    }

    // ── Facility status per track ────────────────────────────────────────────────────────────────

    public static string CorporateStatus(LoanApplicationStatus status) => status switch
    {
        LoanApplicationStatus.Disbursed => FacilityStatuses.Active,
        LoanApplicationStatus.Closed => FacilityStatuses.Closed,
        LoanApplicationStatus.Rejected or LoanApplicationStatus.BranchRejected
            or LoanApplicationStatus.CommitteeRejected or LoanApplicationStatus.RatificationDeclined
            => FacilityStatuses.Declined,
        LoanApplicationStatus.Cancelled => FacilityStatuses.Cancelled,
        _ => FacilityStatuses.InProcess,
    };

    /// <summary>NAMP counts as active from the moment core banking actually disbursed, whatever the
    /// workflow stage — deployment and GPS activation come after the money has moved.</summary>
    public static string NampStatus(NampApplicationStatus status, DateTime? coreBankingDisbursedOn) => status switch
    {
        NampApplicationStatus.Closed => FacilityStatuses.Closed,
        NampApplicationStatus.FinancialDeclined or NampApplicationStatus.RiskDeclined
            or NampApplicationStatus.BranchCommitteeDeclined or NampApplicationStatus.ZonalCommitteeDeclined
            or NampApplicationStatus.RegionalCommitteeDeclined or NampApplicationStatus.HOCommitteeDeclined
            or NampApplicationStatus.RatificationDeclined or NampApplicationStatus.LegalDeclined
            or NampApplicationStatus.Declined
            => FacilityStatuses.Declined,
        NampApplicationStatus.OfferLapsed => FacilityStatuses.Cancelled,
        NampApplicationStatus.Active => FacilityStatuses.Active,
        _ when coreBankingDisbursedOn is not null => FacilityStatuses.Active,
        _ => FacilityStatuses.InProcess,
    };

    public static string RhshfStatus(RhshfCaseStatus status, RhshfInternalStage? stage) => status switch
    {
        RhshfCaseStatus.Approved when stage == RhshfInternalStage.Closed => FacilityStatuses.Closed,
        RhshfCaseStatus.Approved => FacilityStatuses.Active,
        RhshfCaseStatus.Declined => FacilityStatuses.Declined,
        RhshfCaseStatus.Cancelled or RhshfCaseStatus.Expired => FacilityStatuses.Cancelled,
        _ => FacilityStatuses.InProcess,
    };

    // ── Core banking balance ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Overdue is what is still owed on instalments already due; days past due run from the oldest
    /// of those. Computed from the schedule because the loan summary carries no arrears figure.
    /// </summary>
    public static LiveBalance BalanceFrom(
        decimal principalDisbursed, decimal totalOutstanding, DateTime? maturity,
        IEnumerable<(DateTime DueDate, decimal Outstanding)> instalments, DateTime now)
    {
        var due = instalments.Where(i => i.DueDate.Date < now.Date && i.Outstanding > 0).ToList();
        var overdue = due.Sum(i => i.Outstanding);
        var dpd = due.Count == 0 ? 0 : (int)(now.Date - due.Min(i => i.DueDate).Date).TotalDays;
        return new LiveBalance(principalDisbursed, totalOutstanding, overdue, dpd, maturity, now);
    }

    // ── Party grouping and identity conflicts ────────────────────────────────────────────────────

    /// <summary>
    /// Which CRMS record a relationship came from, as an opaque label. The CRMS has no party master,
    /// so two relationships share a label only when their records carry the same identifier: the
    /// same BVN, or failing that the same NIN, or for a company borrower found by account, the same
    /// account. The label is keyed, so it cannot be turned back into the identifier.
    /// </summary>
    public static string PartyRef(ExposureCandidate c, string hashKey)
    {
        var signature = c.RelationshipType == RelationshipTypes.Borrower && c.ObligorType == ObligorTypes.Corporate
            ? $"ACCT:{c.RowNuban}"
            : c.RowBvn is not null ? $"BVN:{c.RowBvn}"
            : c.RowNin is not null ? $"NIN:{c.RowNin}"
            : $"ACCT:{c.RowNuban}";
        return "P-" + KeyedHash(signature, hashKey)[..10];
    }

    /// <summary>HMAC-SHA256, hex. Used for audit hashes and party labels — a plain SHA-256 of an
    /// 11-digit number can be reversed by trying every number.</summary>
    public static string KeyedHash(string value, string key)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    /// <summary>
    /// Where the identifiers given point at different people. A record found by one identifier that
    /// holds a different value for another, or an account that belongs to a company when a person's
    /// BVN or NIN was given. Notes name the record, never an identifier.
    /// </summary>
    public static List<string> ConflictNotes(ExposureQuery q, IReadOnlyList<ExposureCandidate> candidates)
    {
        var notes = new List<string>();
        foreach (var c in candidates)
        {
            var where = $"{Describe(c)} on {c.CaseReference}";
            if (q.Bvn is not null && c.RowBvn is not null && c.RowBvn != q.Bvn)
                notes.Add($"{where} was found by {string.Join(" and ", c.MatchedOn)} but holds a different BVN.");
            if (q.Nin is not null && c.RowNin is not null && c.RowNin != q.Nin)
                notes.Add($"{where} was found by {string.Join(" and ", c.MatchedOn)} but holds a different NIN.");
            if (c.MatchedOn.Contains(IdentifierTypes.Nuban) && c.ObligorType == ObligorTypes.Corporate
                && c.RelationshipType == RelationshipTypes.Borrower && (q.Bvn is not null || q.Nin is not null))
                notes.Add($"The account given belongs to a company borrower ({where}), not to an individual.");
        }
        return notes.Distinct().ToList();
    }

    private static string Describe(ExposureCandidate c) => c.RelationshipType switch
    {
        RelationshipTypes.Borrower => $"The {c.Track} borrower record",
        RelationshipTypes.Guarantor => $"A {c.Track} guarantor record",
        RelationshipTypes.DirectorOrOwner => $"A {c.Track} director record",
        RelationshipTypes.AccountSignatory => $"A {c.Track} signatory record",
        _ => $"A {c.Track} record",
    };

    // ── Shaping the reply ────────────────────────────────────────────────────────────────────────

    /// <summary>Drops what the caller asked to leave out. A facility still open is always kept; an
    /// ended one only while it last changed inside the history window.</summary>
    public static List<ExposureCandidate> ApplyFilters(IEnumerable<ExposureCandidate> candidates, ExposureQuery q, DateTime now)
    {
        var cutoff = now.AddMonths(-q.HistoryMonths);
        return candidates
            .Where(c => !FacilityStatuses.IsEnded(c.Status) || (q.IncludeClosed && c.LastChangedAt >= cutoff))
            .ToList();
    }

    public static PartyExposureResponse BuildResponse(
        string requestId,
        ExposureQuery q,
        IReadOnlyList<ExposureCandidate> matched,
        IReadOnlyDictionary<long, LiveBalance?> balances,
        int maxRelationships,
        string hashKey,
        string crmsVersion,
        DateTime now)
    {
        var kept = ApplyFilters(matched, q, now);

        var ordered = kept
            .OrderBy(c => c.Status switch
            {
                FacilityStatuses.Active => 0,
                FacilityStatuses.InProcess => 1,
                _ => 2,
            })
            .ThenByDescending(c => c.Applied)
            .ToList();

        var relationships = ordered.Select(c => Shape(c, q, balances, hashKey)).ToList();

        var conflicts = ConflictNotes(q, kept);
        var matchStatus = kept.Count == 0 ? MatchStatuses.NoMatch
            : conflicts.Count > 0 ? MatchStatuses.IdentityConflict
            : MatchStatuses.Match;

        var summary = Summarise(relationships, q.FetchLiveBalances);
        var truncated = relationships.Count > maxRelationships;

        return new PartyExposureResponse(
            requestId,
            now,
            crmsVersion,
            matchStatus,
            IdentifierResults(q, kept, hashKey),
            new IdentityConsistency(conflicts.Count == 0, conflicts),
            Coverage,
            summary,
            truncated ? relationships.Take(maxRelationships).ToList() : relationships,
            truncated);
    }

    private static ExposureRelationship Shape(
        ExposureCandidate c, ExposureQuery q, IReadOnlyDictionary<long, LiveBalance?> balances, string hashKey)
    {
        LiveBalance? live = null;
        string source;
        if (c.CoreBankingLoanId is null || c.Status != FacilityStatuses.Active)
            source = BalanceSources.NotBooked;
        else if (!q.FetchLiveBalances)
            source = BalanceSources.NotRequested;
        else if (balances.TryGetValue(c.CoreBankingLoanId.Value, out live) && live is not null)
            source = BalanceSources.CoreBankingLive;
        else
            source = BalanceSources.Unavailable;

        // A loan that is not active owes nothing; one whose balance could not be read is unknown,
        // and stays null rather than reading as zero.
        decimal? outstanding = source switch
        {
            BalanceSources.CoreBankingLive => live!.Outstanding,
            BalanceSources.NotBooked => 0m,
            _ => null,
        };
        decimal? overdue = source switch
        {
            BalanceSources.CoreBankingLive => live!.Overdue,
            BalanceSources.NotBooked => 0m,
            _ => null,
        };

        var obligorName = c.ObligorType == ObligorTypes.Individual ? MaskName(c.ObligorName) : c.ObligorName;
        var closed = FacilityStatuses.IsEnded(c.Status) ? c.LastChangedAt : (DateTime?)null;

        return new ExposureRelationship(
            c.RelationshipType,
            c.RoleDescription,
            c.MatchedOn,
            PartyRef(c, hashKey),
            c.Track,
            c.CaseReference,
            c.FacilityReference,
            c.Programme,
            c.FacilityType,
            new ExposureObligor(c.ObligorType, obligorName),
            c.Status,
            c.CreditStatus,
            new ExposureAmounts(c.Approved, live?.Disbursed ?? c.DisbursedLocal, outstanding, overdue),
            c.GuaranteeAmount,
            source == BalanceSources.CoreBankingLive ? live!.DaysPastDue : null,
            Classification: null,
            new ExposureDates(c.Applied, c.ApprovedAt, c.DisbursedAt, live?.Maturity, closed),
            source,
            live?.AsOf,
            c.RoleSince,
            c.RoleEnded ?? closed);
    }

    /// <summary>
    /// Direct facilities are the ones the person borrowed; indirect are every other role. Contingent
    /// exposure counts guarantees only — a director or signatory is not liable for the debt. A
    /// guarantee with no stated amount counts at the loan's outstanding balance.
    /// </summary>
    private static ExposureSummary Summarise(IReadOnlyList<ExposureRelationship> rels, bool balancesRequested)
    {
        var direct = rels.Where(r => r.RelationshipType == RelationshipTypes.Borrower).ToList();
        var indirect = rels.Where(r => r.RelationshipType != RelationshipTypes.Borrower).ToList();

        var activeDirect = direct.Where(r => r.Status == FacilityStatuses.Active).ToList();
        var activeGuarantees = indirect
            .Where(r => r.RelationshipType == RelationshipTypes.Guarantor && r.Status == FacilityStatuses.Active && r.RoleEnded is null)
            .ToList();

        var complete = balancesRequested
            && activeDirect.All(r => r.Amounts.Outstanding is not null)
            && activeGuarantees.All(r => r.GuaranteeAmount is not null || r.Amounts.Outstanding is not null);

        decimal? directOutstanding = balancesRequested
            ? activeDirect.Sum(r => r.Amounts.Outstanding ?? 0m)
            : null;
        decimal? contingent = activeGuarantees.Count == 0 ? 0m
            : activeGuarantees.All(r => r.GuaranteeAmount is not null) || balancesRequested
                ? activeGuarantees.Sum(r => r.GuaranteeAmount ?? r.Amounts.Outstanding ?? 0m)
                : null;

        var dpd = rels.Where(r => r.DaysPastDue is not null).Select(r => r.DaysPastDue!.Value).ToList();

        return new ExposureSummary(
            direct.Select(r => r.CaseReference).Distinct().Count(),
            indirect.Select(r => r.CaseReference).Distinct().Count(),
            directOutstanding,
            contingent,
            WorstClassification: null,
            dpd.Count == 0 ? null : dpd.Max(),
            Currency,
            complete);
    }

    /// <summary>Counted on everything kept, not just the relationships returned after truncation, so
    /// the counts describe the whole match.</summary>
    private static List<IdentifierResult> IdentifierResults(
        ExposureQuery q, IReadOnlyList<ExposureCandidate> kept, string hashKey)
    {
        var results = new List<IdentifierResult>();
        void Add(string type, string value, Func<ExposureCandidate, bool> hit)
        {
            var parties = kept.Where(hit).Select(c => PartyRef(c, hashKey)).Distinct().Count();
            results.Add(new IdentifierResult(type, MaskIdentifier(value), parties > 0 ? "MATCHED" : "NOT_FOUND", parties));
        }

        if (q.Bvn is not null) Add(IdentifierTypes.Bvn, q.Bvn, c => c.MatchedOn.Contains(IdentifierTypes.Bvn));
        if (q.Nin is not null) Add(IdentifierTypes.Nin, q.Nin, c => c.MatchedOn.Contains(IdentifierTypes.Nin));
        foreach (var nuban in q.Nubans)
            Add(IdentifierTypes.Nuban, nuban, c => c.MatchedOn.Contains(IdentifierTypes.Nuban) && c.RowNuban == nuban);
        return results;
    }

    /// <summary>
    /// What the CRMS can and cannot see, stated plainly so a NO_MATCH is never read as "no exposure".
    /// FULL: every record of that kind carries the identifiers. PARTIAL: some do. NONE: not held.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, object> Coverage = new Dictionary<string, object>
    {
        [RelationshipTypes.Borrower] = "PARTIAL",
        [RelationshipTypes.CoBorrower] = "NONE",
        [RelationshipTypes.ScheduleBeneficiary] = "NONE",
        [RelationshipTypes.Guarantor] = "PARTIAL",
        [RelationshipTypes.AccountSignatory] = "PARTIAL",
        [RelationshipTypes.DirectorOrOwner] = "PARTIAL",
        [RelationshipTypes.NextOfKinOrReferee] = "NONE",
        [RelationshipTypes.Other] = "NONE",
        ["classification"] = "NONE",
        ["notes"] =
            "Borrower: individual borrowers exist only on NAMP (BVN, NIN, account number, held since May 2026); " +
            "corporate and RH-SHF borrowers are companies, found by account number only. " +
            "Schedule beneficiary: the CRMS holds farmer counts per EOP, not the farmers themselves. " +
            "Guarantor: BVN on all three tracks (corporate since Feb 2026, NAMP since May 2026, RH-SHF since Oct 2026); " +
            "NIN on NAMP guarantors only. " +
            "Account signatory: corporate applications only (BVN, since Feb 2026); NAMP and RH-SHF signatories are read " +
            "from core banking when needed and not stored. " +
            "Director or owner: BVN only (corporate since Feb 2026, NAMP since Jun 2026, RH-SHF since Sep 2026), " +
            "often blank where it is entered by hand. " +
            "Classification: the CRMS holds no prudential (CBN) classification; outstanding, overdue and days past due " +
            "are read live from core banking for booked loans.",
    };
}

namespace CRMS.Application.PartyExposure;

// The party-exposure lookup asked for in CRMSQA#4: given a person's BVN, NIN or account number,
// every CRMS facility that person is involved in, in any capacity. Read-only. Field names follow the
// portal team's proposed contract; the values the CRMS cannot honestly supply (prudential
// classification, a farmer schedule) are returned as null or NONE and said so in `coverage`, never
// left out.

public static class ExposurePurposes
{
    public const string Farmer360ProfileView = "FARMER360_PROFILE_VIEW";
    public const string FinancingEligibilityCheck = "FINANCING_ELIGIBILITY_CHECK";
    public const string RegisterKpiRefresh = "REGISTER_KPI_REFRESH";

    public static readonly IReadOnlySet<string> All =
        new HashSet<string> { Farmer360ProfileView, FinancingEligibilityCheck, RegisterKpiRefresh };
}

public static class RelationshipTypes
{
    public const string Borrower = "BORROWER";
    public const string CoBorrower = "CO_BORROWER";
    public const string ScheduleBeneficiary = "SCHEDULE_BENEFICIARY";
    public const string Guarantor = "GUARANTOR";
    public const string AccountSignatory = "ACCOUNT_SIGNATORY";
    public const string DirectorOrOwner = "DIRECTOR_OR_OWNER";
    public const string NextOfKinOrReferee = "NEXT_OF_KIN_OR_REFEREE";
    public const string Other = "OTHER";
}

public static class MatchStatuses
{
    public const string Match = "MATCH";
    public const string NoMatch = "NO_MATCH";
    public const string IdentityConflict = "IDENTITY_CONFLICT";
    public const string Partial = "PARTIAL";
    public const string Error = "ERROR";
}

public static class FacilityStatuses
{
    /// <summary>An application still being assessed, or approved but not yet booked.</summary>
    public const string InProcess = "IN_PROCESS";
    public const string Active = "ACTIVE";
    public const string Closed = "CLOSED";
    public const string Declined = "DECLINED";
    public const string Cancelled = "CANCELLED";

    public static bool IsEnded(string status) => status is Closed or Declined or Cancelled;
}

// ── Request ──────────────────────────────────────────────────────────────────────────────────────

public record PartyExposureLookupRequest(
    string? RequestId,
    string? Purpose,
    ExposureRequester? RequestedBy,
    string? SubjectRef,
    ExposureIdentifiers? Identifiers,
    ExposureInclude? Include);

public record ExposureRequester(string? PortalUserId, string? Role);

public record ExposureIdentifiers(string? Bvn, string? Nin, List<ExposureAccount>? Accounts);

public record ExposureAccount(string? Nuban, string? BankCode);

public record ExposureInclude(bool? ClosedFacilities, int? HistoryMonths);

public record PartyExposureBatchRequest(
    string? RequestId,
    string? Purpose,
    ExposureRequester? RequestedBy,
    /// <summary>Optional. Where to post PARTY_EXPOSURE_BATCH_READY; polling works either way. Only
    /// hosts on the configured allow-list are accepted.</summary>
    string? CallbackUrl,
    ExposureInclude? Include,
    List<ExposureBatchSubject>? Subjects);

public record ExposureBatchSubject(string? SubjectRef, ExposureIdentifiers? Identifiers);

// ── Validated query (what the service actually runs) ─────────────────────────────────────────────

/// <summary>A request after validation: identifiers are digits only and the right length.</summary>
public record ExposureQuery(
    string? Bvn,
    string? Nin,
    IReadOnlyList<string> Nubans,
    bool IncludeClosed,
    int HistoryMonths,
    bool FetchLiveBalances);

// ── Response ─────────────────────────────────────────────────────────────────────────────────────

public record PartyExposureResponse(
    string RequestId,
    DateTime DataAsOf,
    string CrmsVersion,
    string MatchStatus,
    IReadOnlyList<IdentifierResult> IdentifierResults,
    IdentityConsistency IdentityConsistency,
    IReadOnlyDictionary<string, object> Coverage,
    ExposureSummary Summary,
    IReadOnlyList<ExposureRelationship> Relationships,
    bool Truncated);

public record IdentifierResult(string Type, string Value, string Result, int PartyCount);

public record IdentityConsistency(bool Consistent, IReadOnlyList<string> Notes);

public record ExposureSummary(
    int DirectFacilities,
    int IndirectFacilities,
    decimal? ActiveDirectOutstanding,
    decimal? ActiveContingentExposure,
    /// <summary>Always null: the CRMS holds no prudential classification. See coverage.</summary>
    string? WorstClassification,
    int? MaxDaysPastDue,
    string Currency,
    /// <summary>False when any active facility's balance could not be read from core banking in
    /// time, so the outstanding totals above leave that facility out.</summary>
    bool BalancesComplete);

public record ExposureRelationship(
    string RelationshipType,
    string? RoleDescription,
    IReadOnlyList<string> MatchedOn,
    /// <summary>Opaque label for the CRMS record that matched. Relationships with different labels
    /// came from records the CRMS cannot show are the same person.</summary>
    string PartyRef,
    string Track,
    string CaseReference,
    string? FacilityReference,
    string? Programme,
    string FacilityType,
    ExposureObligor Obligor,
    string Status,
    string? CreditStatus,
    ExposureAmounts Amounts,
    decimal? GuaranteeAmount,
    int? DaysPastDue,
    string? Classification,
    ExposureDates Dates,
    string BalanceSource,
    DateTime? BalanceAsOf,
    DateTime RoleSince,
    DateTime? RoleEnded);

public record ExposureObligor(string Type, string DisplayName);

public record ExposureAmounts(decimal? Approved, decimal? Disbursed, decimal? Outstanding, decimal? Overdue);

public record ExposureDates(DateTime? Applied, DateTime? Approved, DateTime? Disbursed, DateTime? Maturity, DateTime? Closed);

public static class BalanceSources
{
    /// <summary>Read from core banking during this lookup.</summary>
    public const string CoreBankingLive = "CORE_BANKING_LIVE";
    /// <summary>Booked, but core banking did not answer within the time allowed.</summary>
    public const string Unavailable = "UNAVAILABLE";
    /// <summary>No loan exists in core banking yet (or any more): nothing is outstanding.</summary>
    public const string NotBooked = "NOT_BOOKED";
    /// <summary>Batch mode, or a request that did not ask for balances.</summary>
    public const string NotRequested = "NOT_REQUESTED";
}

// ── Batch ────────────────────────────────────────────────────────────────────────────────────────

public record PartyExposureBatchAccepted(string BatchId, string Status, int SubjectCount, string PollUrl);

public record PartyExposureBatchStatus(
    string BatchId,
    string RequestId,
    string Status,
    int SubjectCount,
    int ProcessedCount,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    string? Failure,
    IReadOnlyList<ExposureBatchResult>? Results);

/// <summary>Batch results carry summaries only, never facility detail.</summary>
public record ExposureBatchResult(
    string? SubjectRef,
    string MatchStatus,
    IReadOnlyList<IdentifierResult> IdentifierResults,
    bool IdentityConsistent,
    ExposureSummary? Summary,
    string? Error);

public static class BatchStatuses
{
    public const string Queued = "QUEUED";
    public const string Processing = "PROCESSING";
    public const string Completed = "COMPLETED";
    public const string Failed = "FAILED";
}

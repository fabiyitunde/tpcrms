using CRMS.Application.PartyExposure;
using CRMS.Domain.Common;
using CRMS.Domain.Enums;
using Xunit;

namespace CRMS.Application.Tests.PartyExposure;

public class PartyExposureRulesTests
{
    private const string Key = "test-key";
    private static readonly DateTime Now = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);

    private static ExposureIdentifiers Ids(string? bvn = null, string? nin = null, params string[] nubans)
        => new(bvn, nin, nubans.Select(n => new ExposureAccount(n, "BOA")).ToList());

    private static ExposureQuery Query(string? bvn = "22212345678", string? nin = null, bool closed = true, int months = 36, bool live = true, params string[] nubans)
        => new(bvn, nin, nubans, closed, months, live);

    private static ExposureCandidate Candidate(
        string type = RelationshipTypes.Borrower, string status = FacilityStatuses.Active, string obligorType = ObligorTypes.Individual,
        string? rowBvn = "22212345678", string? rowNin = null, string? rowNuban = null, long? loanId = null,
        decimal? guarantee = null, DateTime? lastChanged = null, string reference = "NAMP-1", params string[] matched)
        => new(type, null, "NAMP", reference, "NAMP", "EQUIPMENT_FINANCE", obligorType, "Adamu Bello",
            status, null, 1_000_000m, null, guarantee, Now.AddMonths(-6), null, null, lastChanged ?? Now.AddDays(-1),
            loanId, null, rowBvn, rowNin, rowNuban, matched.Length == 0 ? [IdentifierTypes.Bvn] : matched,
            Now.AddMonths(-6), null);

    // ── Identifiers ──────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(" 222 1234 5678 ", "22212345678")]
    [InlineData("222-123-45678", "22212345678")]
    [InlineData("0123.456.789", "0123456789")]
    [InlineData("A12345", "A12345")]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void Digits_strips_separators_but_keeps_a_value_it_cannot_clean(string? input, string? expected)
        => Assert.Equal(expected, Identifiers.Digits(input));

    // ── Validation ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_normalises_identifiers_and_drops_repeated_accounts()
    {
        var (q, errors) = PartyExposureRules.Validate("r1", ExposurePurposes.Farmer360ProfileView,
            Ids("222 1234 5678", null, "0123456789", "0123-456-789"), null, true);

        Assert.Empty(errors);
        Assert.Equal("22212345678", q!.Bvn);
        Assert.Equal(["0123456789"], q.Nubans);
        Assert.True(q.IncludeClosed);
        Assert.Equal(36, q.HistoryMonths);
    }

    [Theory]
    [InlineData("1234", null, null, "identifiers.bvn must be 11 digits.")]
    [InlineData(null, "123", null, "identifiers.nin must be 11 digits.")]
    [InlineData(null, null, "12345", "identifiers.accounts[0].nuban must be 10 digits.")]
    [InlineData(null, null, null, "At least one identifier (bvn, nin or an account nuban) is required.")]
    public void Validate_refuses_bad_identifiers_without_echoing_them(string? bvn, string? nin, string? nuban, string expected)
    {
        var (q, errors) = PartyExposureRules.Validate("r1", ExposurePurposes.Farmer360ProfileView,
            nuban is null ? Ids(bvn, nin) : Ids(bvn, nin, nuban), null, true);

        Assert.Null(q);
        Assert.Contains(expected, errors);
    }

    [Fact]
    public void Validate_requires_a_known_purpose_and_a_request_id()
    {
        var (_, errors) = PartyExposureRules.Validate(null, "MARKETING", Ids("22212345678"), null, true);
        Assert.Contains("requestId is required.", errors);
        Assert.Contains(errors, e => e.StartsWith("purpose must be one of"));
    }

    [Fact]
    public void Validate_bounds_the_history_window()
    {
        var (_, errors) = PartyExposureRules.Validate("r1", ExposurePurposes.Farmer360ProfileView,
            Ids("22212345678"), new ExposureInclude(true, 500), true);
        Assert.Contains("include.historyMonths must be between 1 and 120.", errors);
    }

    // ── Masking ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Masking_keeps_only_the_last_four_digits_and_initials()
    {
        Assert.Equal("*******5678", PartyExposureRules.MaskIdentifier("22212345678"));
        Assert.Equal("A**** B****", PartyExposureRules.MaskName("adamu  Bello"));
    }

    // ── Status per track ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Namp_counts_as_active_once_core_banking_disbursed()
    {
        Assert.Equal(FacilityStatuses.InProcess, PartyExposureRules.NampStatus(NampApplicationStatus.PreDeploymentVerification, null));
        Assert.Equal(FacilityStatuses.Active, PartyExposureRules.NampStatus(NampApplicationStatus.Deployment, Now));
        Assert.Equal(FacilityStatuses.Declined, PartyExposureRules.NampStatus(NampApplicationStatus.HOCommitteeDeclined, null));
        Assert.Equal(FacilityStatuses.Cancelled, PartyExposureRules.NampStatus(NampApplicationStatus.OfferLapsed, null));
    }

    [Fact]
    public void Rhshf_offer_and_acceptance_stay_in_process_until_booked()
    {
        Assert.Equal(FacilityStatuses.InProcess, PartyExposureRules.RhshfStatus(RhshfCaseStatus.UnderReview, RhshfInternalStage.PreDeploymentVerification));
        Assert.Equal(FacilityStatuses.Active, PartyExposureRules.RhshfStatus(RhshfCaseStatus.Approved, RhshfInternalStage.Active));
        Assert.Equal(FacilityStatuses.Closed, PartyExposureRules.RhshfStatus(RhshfCaseStatus.Approved, RhshfInternalStage.Closed));
    }

    // ── Balances ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Balance_counts_overdue_and_days_past_due_from_the_oldest_unpaid_instalment()
    {
        var balance = PartyExposureRules.BalanceFrom(900_000m, 600_000m, Now.AddMonths(6),
        [
            (Now.AddDays(-40), 100_000m),
            (Now.AddDays(-10), 100_000m),
            (Now.AddDays(-70), 0m),
            (Now.AddDays(20), 100_000m),
        ], Now);

        Assert.Equal(200_000m, balance.Overdue);
        Assert.Equal(40, balance.DaysPastDue);
        Assert.Equal(600_000m, balance.Outstanding);
    }

    // ── Reply ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void No_record_is_no_match_with_coverage_never_no_exposure()
    {
        var reply = PartyExposureRules.BuildResponse("r1", Query(), [], new Dictionary<long, LiveBalance?>(), 200, Key, "1", Now);

        Assert.Equal(MatchStatuses.NoMatch, reply.MatchStatus);
        Assert.Equal("NOT_FOUND", reply.IdentifierResults.Single().Result);
        Assert.Equal("NONE", reply.Coverage[RelationshipTypes.ScheduleBeneficiary]);
        Assert.Equal("NONE", reply.Coverage["classification"]);
    }

    [Fact]
    public void Live_balance_fills_the_amounts_and_an_unreadable_one_stays_unknown()
    {
        var candidates = new[]
        {
            Candidate(loanId: 1, reference: "NAMP-1"),
            Candidate(loanId: 2, reference: "NAMP-2"),
        };
        var balances = new Dictionary<long, LiveBalance?>
        {
            [1] = new(1_000_000m, 750_000m, 50_000m, 12, Now.AddMonths(3), Now),
            [2] = null,
        };

        var reply = PartyExposureRules.BuildResponse("r1", Query(), candidates, balances, 200, Key, "1", Now);

        var known = reply.Relationships.Single(r => r.CaseReference == "NAMP-1");
        Assert.Equal(BalanceSources.CoreBankingLive, known.BalanceSource);
        Assert.Equal(750_000m, known.Amounts.Outstanding);
        Assert.Equal(12, known.DaysPastDue);

        var unknown = reply.Relationships.Single(r => r.CaseReference == "NAMP-2");
        Assert.Equal(BalanceSources.Unavailable, unknown.BalanceSource);
        Assert.Null(unknown.Amounts.Outstanding);

        Assert.False(reply.Summary.BalancesComplete);
        Assert.Equal(750_000m, reply.Summary.ActiveDirectOutstanding);
        Assert.Equal(12, reply.Summary.MaxDaysPastDue);
        Assert.Null(reply.Summary.WorstClassification);
    }

    [Fact]
    public void Contingent_exposure_counts_guarantees_only()
    {
        var candidates = new[]
        {
            Candidate(type: RelationshipTypes.Guarantor, obligorType: ObligorTypes.Corporate, guarantee: 3_400_000m, reference: "RHSHF-1"),
            Candidate(type: RelationshipTypes.DirectorOrOwner, obligorType: ObligorTypes.Corporate, reference: "RHSHF-2"),
        };

        var reply = PartyExposureRules.BuildResponse("r1", Query(live: false), candidates, new Dictionary<long, LiveBalance?>(), 200, Key, "1", Now);

        Assert.Equal(0, reply.Summary.DirectFacilities);
        Assert.Equal(2, reply.Summary.IndirectFacilities);
        Assert.Equal(3_400_000m, reply.Summary.ActiveContingentExposure);
    }

    [Fact]
    public void Individual_obligors_are_masked_and_companies_are_not()
    {
        var reply = PartyExposureRules.BuildResponse("r1", Query(), [
            Candidate(reference: "NAMP-1"),
            Candidate(type: RelationshipTypes.Guarantor, obligorType: ObligorTypes.Corporate, reference: "RHSHF-1"),
        ], new Dictionary<long, LiveBalance?>(), 200, Key, "1", Now);

        Assert.Equal("A**** B****", reply.Relationships.Single(r => r.CaseReference == "NAMP-1").Obligor.DisplayName);
        Assert.Equal("Adamu Bello", reply.Relationships.Single(r => r.CaseReference == "RHSHF-1").Obligor.DisplayName);
    }

    [Fact]
    public void A_record_holding_a_different_nin_is_an_identity_conflict()
    {
        var q = Query(nin: "11111111111");
        var reply = PartyExposureRules.BuildResponse("r1", q, [
            Candidate(type: RelationshipTypes.Guarantor, rowNin: "99999999999", reference: "NAMP-7"),
        ], new Dictionary<long, LiveBalance?>(), 200, Key, "1", Now);

        Assert.Equal(MatchStatuses.IdentityConflict, reply.MatchStatus);
        Assert.False(reply.IdentityConsistency.Consistent);
        var note = Assert.Single(reply.IdentityConsistency.Notes);
        Assert.Contains("NAMP-7", note);
        Assert.DoesNotContain("99999999999", note);
        Assert.DoesNotContain("11111111111", note);
    }

    [Fact]
    public void An_account_belonging_to_a_company_is_a_conflict_when_a_person_is_named()
    {
        var q = Query(nubans: "0123456789");
        var reply = PartyExposureRules.BuildResponse("r1", q, [
            Candidate(obligorType: ObligorTypes.Corporate, rowBvn: null, rowNuban: "0123456789", reference: "RHSHF-9",
                matched: IdentifierTypes.Nuban),
        ], new Dictionary<long, LiveBalance?>(), 200, Key, "1", Now);

        Assert.Equal(MatchStatuses.IdentityConflict, reply.MatchStatus);
        Assert.Equal("MATCHED", reply.IdentifierResults.Single(r => r.Type == IdentifierTypes.Nuban).Result);
        Assert.Equal("NOT_FOUND", reply.IdentifierResults.Single(r => r.Type == IdentifierTypes.Bvn).Result);
    }

    [Fact]
    public void Ended_facilities_follow_the_closed_flag_and_history_window()
    {
        var candidates = new[]
        {
            Candidate(status: FacilityStatuses.Closed, lastChanged: Now.AddMonths(-2), reference: "RECENT"),
            Candidate(status: FacilityStatuses.Closed, lastChanged: Now.AddMonths(-40), reference: "OLD"),
            Candidate(status: FacilityStatuses.InProcess, lastChanged: Now.AddMonths(-40), reference: "OPEN"),
        };

        var all = PartyExposureRules.BuildResponse("r1", Query(), candidates, new Dictionary<long, LiveBalance?>(), 200, Key, "1", Now);
        Assert.Equal(["OPEN", "RECENT"], all.Relationships.Select(r => r.CaseReference).Order().ToArray());
        Assert.NotNull(all.Relationships.Single(r => r.CaseReference == "RECENT").RoleEnded);

        var openOnly = PartyExposureRules.BuildResponse("r1", Query(closed: false), candidates, new Dictionary<long, LiveBalance?>(), 200, Key, "1", Now);
        Assert.Equal(["OPEN"], openOnly.Relationships.Select(r => r.CaseReference).ToArray());
    }

    [Fact]
    public void Truncation_caps_the_list_but_the_summary_counts_everything()
    {
        var candidates = Enumerable.Range(1, 5).Select(i => Candidate(reference: $"NAMP-{i}")).ToArray();

        var reply = PartyExposureRules.BuildResponse("r1", Query(live: false), candidates, new Dictionary<long, LiveBalance?>(), 3, Key, "1", Now);

        Assert.True(reply.Truncated);
        Assert.Equal(3, reply.Relationships.Count);
        Assert.Equal(5, reply.Summary.DirectFacilities);
    }

    [Fact]
    public void Party_labels_are_the_same_for_the_same_bvn_and_reveal_nothing()
    {
        var a = PartyExposureRules.PartyRef(Candidate(reference: "A"), Key);
        var b = PartyExposureRules.PartyRef(Candidate(type: RelationshipTypes.Guarantor, reference: "B"), Key);
        var other = PartyExposureRules.PartyRef(Candidate(rowBvn: "33312345678", reference: "C"), Key);

        Assert.Equal(a, b);
        Assert.NotEqual(a, other);
        Assert.Matches("^P-[0-9a-f]{10}$", a);
    }
}

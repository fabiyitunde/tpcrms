using CRMS.Application.PartyExposure;
using CRMS.Domain.Aggregates.LoanApplication;
using CRMS.Domain.Aggregates.Namp;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using CRMS.Domain.ValueObjects;
using CRMS.Infrastructure.ExternalServices.Rhshf;
using CRMS.Infrastructure.Persistence;
using CRMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CRMS.Infrastructure.Tests;

/// <summary>The party-exposure lookup (CRMSQA#4) against real records from all three tracks.</summary>
public class PartyExposureServiceTests
{
    private const string Bvn = "22212345678";
    private const string Nin = "12345678901";
    private const string FacAccount = "0123456789";
    private const string CompanyAccount = "0987654321";

    private readonly CRMSDbContext _db = new(new DbContextOptionsBuilder<CRMSDbContext>()
        .UseInMemoryDatabase("PartyExposure_" + Guid.NewGuid()).Options);
    private readonly Mock<IRhshfCreditProfileRepository> _profiles = new();
    private readonly Mock<IRhshfOfferRepository> _offers = new();
    private readonly Mock<IFineractDirectService> _coreBanking = new();

    private PartyExposureService Service() => new(
        _db, _profiles.Object, _offers.Object, _coreBanking.Object,
        Options.Create(new PartyExposureSettings { LiveBalanceBudgetMs = 2000 }),
        Options.Create(new RhshfSettings { CallbackSigningSecret = "secret" }),
        NullLogger<PartyExposureService>.Instance);

    private static ExposureQuery Query(string? bvn = Bvn, string? nin = null, bool live = true, params string[] nubans)
        => new(bvn, nin, nubans, true, 36, live);

    private async Task<RhshfCreditProfile> SeedFacAsync()
    {
        var profile = RhshfCreditProfile.Create(
            submissionId: Guid.NewGuid(), programmeCode: "FYP-NAG2026", programmeName: "Renewed Hope",
            sessionCode: "2026-DRY", sessionName: "Dry Season 2026", facId: Guid.NewGuid(),
            companyName: "Grandbai Limited", rcNumber: "RC123456", tin: null,
            boaAccountNumber: FacAccount, contactEmail: "fac@example.com", contactPhone: "+2348012345678",
            state: "Kano", lga: "Nassarawa", totalEopValue: 12_884_464m, currency: "NGN", farmerCount: 220,
            callbackUrl: "https://portal.example/api/integrations/crms/webhook",
            certifiedByAdmin: "admin@boa.gov.ng", certifiedAt: DateTime.UtcNow, rawSubmissionPayload: "{}",
            eopLines: null, resolvedBranchId: null, resolvedOfficeId: null).Value;
        _db.RhshfCreditProfiles.Add(profile);
        _db.RhshfDirectors.Add(RhshfDirector.CreateManual(profile.Id, "Adamu Bello", Bvn, 40m, true, null, null).Value);
        _db.RhshfGuarantors.Add(RhshfGuarantor.Create(profile.Id, RhshfGuarantorType.Individual, "Adamu Bello", Bvn, null,
            RhshfGuarantorRelationship.Other, "Director", null, null, null, 3_400_000m, null).Value);
        await _db.SaveChangesAsync();
        _profiles.Setup(r => r.GetByIdAsync(profile.Id, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        return profile;
    }

    private async Task<NampApplication> SeedNampApplicantAsync(long? loanId = null)
    {
        var app = NampApplication.Create(
            "NAMP-2026-0001", Guid.NewGuid(), "PAYS-1", "Adamu Bello", "0111111111",
            NampApplicantCategory.YouthAgripreneur, "Tractor", 5_000_000m, Guid.NewGuid(),
            NampCommitteeTier.Branch, Guid.NewGuid()).Value;
        app.SetExtendedApplicantInfo(Nin, "222-1234-5678", null, null, null, null, null, null, null, null, null);
        if (loanId is not null)
            app.SetFineractLoanResult(loanId.Value, "LN-0001");
        _db.NampApplications.Add(app);
        await _db.SaveChangesAsync();
        return app;
    }

    [Fact]
    public async Task Finds_a_person_as_director_and_guarantor_of_a_fac()
    {
        await SeedFacAsync();

        var reply = await Service().LookupAsync("r1", Query());

        Assert.Equal(MatchStatuses.Match, reply.MatchStatus);
        Assert.Equal(2, reply.Relationships.Count);
        var guarantee = reply.Relationships.Single(r => r.RelationshipType == RelationshipTypes.Guarantor);
        Assert.Equal("RHSHF", guarantee.Track);
        Assert.Equal("Grandbai Limited", guarantee.Obligor.DisplayName);
        Assert.Equal(3_400_000m, guarantee.GuaranteeAmount);
        Assert.Equal(FacilityStatuses.InProcess, guarantee.Status);
        Assert.Contains(reply.Relationships, r => r.RelationshipType == RelationshipTypes.DirectorOrOwner);
        // Same BVN on both records, so the CRMS reports them as the same party.
        Assert.Single(reply.Relationships.Select(r => r.PartyRef).Distinct());
        Assert.Equal(0m, reply.Summary.ActiveContingentExposure);
    }

    [Fact]
    public async Task Finds_a_fac_by_its_account_and_never_echoes_the_number()
    {
        await SeedFacAsync();

        var reply = await Service().LookupAsync("r1", Query(bvn: null, nubans: FacAccount));

        var borrower = Assert.Single(reply.Relationships);
        Assert.Equal(RelationshipTypes.Borrower, borrower.RelationshipType);
        Assert.Equal(ObligorTypes.Corporate, borrower.Obligor.Type);
        Assert.Equal("******6789", reply.IdentifierResults.Single().Value);
        Assert.Equal(MatchStatuses.Match, reply.MatchStatus);
    }

    [Fact]
    public async Task One_applicant_found_by_bvn_and_nin_is_one_relationship_matched_on_both()
    {
        await SeedNampApplicantAsync();

        var reply = await Service().LookupAsync("r1", Query(nin: Nin));

        var borrower = Assert.Single(reply.Relationships);
        Assert.Equal([IdentifierTypes.Bvn, IdentifierTypes.Nin], borrower.MatchedOn);
        Assert.Equal("A**** B****", borrower.Obligor.DisplayName);
        Assert.Equal(1, reply.Summary.DirectFacilities);
    }

    [Fact]
    public async Task A_different_nin_on_the_matched_record_is_an_identity_conflict()
    {
        await SeedNampApplicantAsync();

        var reply = await Service().LookupAsync("r1", Query(nin: "99999999999"));

        Assert.Equal(MatchStatuses.IdentityConflict, reply.MatchStatus);
        Assert.Contains("NAMP-2026-0001", Assert.Single(reply.IdentityConsistency.Notes));
    }

    [Fact]
    public async Task Reads_a_booked_loan_balance_live_from_core_banking()
    {
        await SeedNampApplicantAsync(loanId: 77);
        var due = DateTime.UtcNow.AddDays(-15);
        _coreBanking.Setup(c => c.GetLoanDetailAsync(77, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success(
            new FineractLoanDetail(77, "LN-0001", "NAMP", "Active", 300, 5_000_000m, 5_000_000m, 9m, 12,
                DateTime.UtcNow.AddMonths(-2), DateTime.UtcNow.AddMonths(10),
                new FineractLoanSummary(5_400_000m, 400_000m, 5_000_000m, 5_000_000m, 0, 5_000_000m, 0, 0, 0, 0, 0, 0, 0, 0, 0),
                [new FineractSchedulePeriod(1, due.AddMonths(-1), due, 400_000m, 0, 400_000m, 50_000m, 0, 50_000m, 0, 0, 450_000m, 0, 450_000m, false)])));

        var reply = await Service().LookupAsync("r1", Query());

        var loan = Assert.Single(reply.Relationships);
        Assert.Equal(FacilityStatuses.Active, loan.Status);
        Assert.Equal(BalanceSources.CoreBankingLive, loan.BalanceSource);
        Assert.Equal(5_000_000m, loan.Amounts.Outstanding);
        Assert.Equal(450_000m, loan.Amounts.Overdue);
        Assert.Equal(15, loan.DaysPastDue);
        Assert.True(reply.Summary.BalancesComplete);
    }

    [Fact]
    public async Task A_core_banking_failure_leaves_the_balance_unknown_not_zero()
    {
        await SeedNampApplicantAsync(loanId: 78);
        _coreBanking.Setup(c => c.GetLoanDetailAsync(78, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<FineractLoanDetail>("timeout"));

        var reply = await Service().LookupAsync("r1", Query());

        var loan = Assert.Single(reply.Relationships);
        Assert.Equal(BalanceSources.Unavailable, loan.BalanceSource);
        Assert.Null(loan.Amounts.Outstanding);
        Assert.False(reply.Summary.BalancesComplete);
    }

    [Fact]
    public async Task Batch_mode_does_not_call_core_banking()
    {
        await SeedNampApplicantAsync(loanId: 79);

        var reply = await Service().LookupAsync("r1", Query(live: false));

        Assert.Equal(BalanceSources.NotRequested, Assert.Single(reply.Relationships).BalanceSource);
        _coreBanking.Verify(c => c.GetLoanDetailAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Corporate_signatory_and_company_account_are_found()
    {
        var loan = LoanApplication.CreateCorporate(
            Guid.NewGuid(), "CORP-TL", CompanyAccount, "C001", "Acme Farms Ltd", Money.Create(10_000_000m),
            12, 18m, InterestRateType.Flat, Guid.NewGuid(), null).Value;
        loan.AddParty(PartyType.Signatory, "Adamu Bello", Bvn, null, null, "Managing Director", null);
        _db.LoanApplications.Add(loan);
        await _db.SaveChangesAsync();

        var reply = await Service().LookupAsync("r1", Query(nubans: CompanyAccount));

        Assert.Contains(reply.Relationships, r => r.RelationshipType == RelationshipTypes.AccountSignatory && r.Obligor.DisplayName == "Acme Farms Ltd");
        Assert.Contains(reply.Relationships, r => r.RelationshipType == RelationshipTypes.Borrower);
        // A person's BVN alongside a company's account: flagged, not merged.
        Assert.Equal(MatchStatuses.IdentityConflict, reply.MatchStatus);
    }

    [Fact]
    public async Task Finds_a_farmer_on_a_facs_schedule_by_account()
    {
        var profile = await SeedFacAsync();
        var farmers = RhshfScheduleFarmer.BuildSchedule(profile.Id,
            [new RhshfScheduleFarmerInput(null, "F-1", null, null, "0222222222")], null).Value;
        _db.RhshfScheduleFarmers.AddRange(farmers);
        await _db.SaveChangesAsync();

        var reply = await Service().LookupAsync("r1", Query(bvn: null, nubans: "0222222222"));

        var row = Assert.Single(reply.Relationships);
        Assert.Equal(RelationshipTypes.ScheduleBeneficiary, row.RelationshipType);
        Assert.Equal("Grandbai Limited", row.Obligor.DisplayName);
        Assert.Equal(1, reply.Summary.IndirectFacilities);
        Assert.Equal(MatchStatuses.Match, reply.MatchStatus);
    }

    [Fact]
    public async Task Nothing_found_is_no_match()
    {
        var reply = await Service().LookupAsync("r1", Query(bvn: "33312345678"));

        Assert.Equal(MatchStatuses.NoMatch, reply.MatchStatus);
        Assert.Empty(reply.Relationships);
    }

    [Fact]
    public async Task Interrupted_batches_are_failed_on_restart()
    {
        var batch = Domain.Aggregates.PartyExposure.PartyExposureBatch.Create("b1", ExposurePurposes.RegisterKpiRefresh, "u1", "BOA_ADMIN", null, 3);
        batch.Start();
        _db.PartyExposureBatches.Add(batch);
        await _db.SaveChangesAsync();

        Assert.Equal(1, await Service().FailInterruptedBatchesAsync());
        Assert.Equal(Domain.Aggregates.PartyExposure.PartyExposureBatch.Failed, (await _db.PartyExposureBatches.SingleAsync()).Status);
    }
}

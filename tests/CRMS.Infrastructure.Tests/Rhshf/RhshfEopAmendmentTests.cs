using CRMS.Application.Rhshf.Commands;
using CRMS.Application.Rhshf.DTOs;
using CRMS.Application.Rhshf.Services;
using CRMS.Application.Rhshf.Webhooks;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Infrastructure.Persistence;
using CRMS.Infrastructure.Persistence.Repositories.Rhshf;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CRMS.Infrastructure.Tests.Rhshf;

/// <summary>
/// The portal amending a case's EOPs with amendsReference (CRMSQA#3), run against a real
/// CRMSDbContext: the breakdown is replaced in the database, the open offer is superseded, the
/// collateral is carried into the new review cycle, and the replaced version is kept.
/// </summary>
public class RhshfEopAmendmentTests
{
    private static readonly Guid FacId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid EopId = Guid.Parse("3cc02579-d786-40fb-b87d-17d6195c5847");

    internal static (CRMSDbContext Db, SubmitConsolidatedEopHandler Handler) MakeHarness()
    {
        var options = new DbContextOptionsBuilder<CRMSDbContext>()
            .UseInMemoryDatabase("RhshfEopAmendment_" + Guid.NewGuid())
            .Options;
        var db = new CRMSDbContext(options);
        var handler = new SubmitConsolidatedEopHandler(
            new RhshfCreditProfileRepository(db), new SubmitConsolidatedEopIdempotencyTests.FakeTokenService(),
            new SubmitConsolidatedEopIdempotencyTests.FakeFineractDirectService(),
            new SubmitConsolidatedEopIdempotencyTests.FakeLocationRepository(), db, new RhshfScheduleFarmerRepository(db),
            new RhshfEopHolderLookup(db), new RhshfOfferRepository(db), new RhshfCollateralRepository(db));
        return (db, handler);
    }

    /// <summary>One EOP of 30 farmers on one input line, with its v1 hash worked out.</summary>
    internal static SubmitConsolidatedEopRequest Request(
        decimal unitPrice, int revision, string? amendsReference = null, Guid? submissionId = null, Guid? facId = null)
    {
        var amount = 30m * unitPrice;
        var json = System.Text.Json.Nodes.JsonNode.Parse($$"""
        {
          "submissionId": "{{submissionId ?? Guid.NewGuid()}}",
          "programme": { "code": "RH-SHF-DRY-2026", "name": "Renewed Hope" },
          "session": { "code": "2026-DRY", "name": "Dry Season 2026" },
          "fac": { "facId": "{{facId ?? FacId}}", "companyName": "Amended Farms Ltd", "rcNumber": "RC7654321",
                   "tin": "10293847-0001", "boaAccountNumber": "0099887766",
                   "contact": { "email": "test@example.com", "phone": "+2348099998888" }, "state": "Kano", "lga": "Bunkure" },
          "totalEopValue": {{amount}},
          "currency": "NGN",
          "farmerCount": 30,
          "eopLines": [ { "commodity": "NPK Fertilizer", "quantityKg": 30, "unitPricePerKg": {{unitPrice}}, "lineValue": {{amount}} } ],
          "callbackUrl": "https://portal.example.gov.ng/api/integrations/crms/webhook",
          "amendsReference": {{(amendsReference is null ? "null" : $"\"{amendsReference}\"")}},
          "financing": {
            "currency": "NGN",
            "inputLines": [
              { "eopId": "{{EopId}}", "crop": "Maize", "item": "NPK Fertilizer", "category": "Fertilizer",
                "supplier": "Agro Supplies Ltd", "quantityPerFarmer": 1, "unit": "Kg", "unitPrice": {{unitPrice}},
                "basis": "perFarmer", "farmers": 30, "amount": {{amount}}, "packagingExcess": 0, "inLoan": true,
                "lineId": "00000000-0000-0000-0000-0000000000a1" }
            ],
            "charges": [],
            "inputsTotal": {{amount}}, "packagingExcessTotal": 0, "chargesTotal": 0,
            "approvedEopTotal": {{amount}}, "loanAmount": {{amount}}, "excludedFromLoan": 0,
            "eops": [
              { "eopId": "{{EopId}}", "crop": "Maize", "state": "Kano", "farmers": 30, "inputs": {{amount}},
                "packagingExcess": 0, "insurance": 0, "workingCapital": 0, "techServicesFee": 0,
                "approvedTotal": {{amount}}, "loanAmount": {{amount}}, "eopReference": "EOP-3CC02579",
                "revision": {{revision}}, "snapshotHashScheme": "rhshf-eop-snapshot-v1" }
            ]
          }
        }
        """);
        var request = System.Text.Json.JsonSerializer.Deserialize<SubmitConsolidatedEopRequest>(json, System.Text.Json.JsonSerializerOptions.Web)!;
        var eop = request.Financing!.Eops!.Single();
        var hashed = eop with { SnapshotHash = RhshfEopSnapshotHash.ComputeV1(request.Financing, eop) };
        return request with { Financing = request.Financing with { Eops = [hashed] } };
    }

    /// <summary>A case submitted at ₦4,000 a farmer, then taken to an offer awaiting the FAC in cycle 1,
    /// with one collateral record declared.</summary>
    private static async Task<RhshfCreditProfile> CaseWithOfferAsync(CRMSDbContext db, SubmitConsolidatedEopHandler handler)
    {
        var created = await handler.Handle(new SubmitConsolidatedEopCommand(Request(4_000m, revision: 1), "{}"));
        Assert.True(created.IsSuccess, created.Error);

        var profile = await db.RhshfCreditProfiles.SingleAsync();
        db.Entry(profile).Property(p => p.Status).CurrentValue = RhshfCaseStatus.UnderReview;
        db.Entry(profile).Property(p => p.CurrentCycleNumber).CurrentValue = 1;
        db.Entry(profile).Property(p => p.InternalStage).CurrentValue = RhshfInternalStage.AwaitingOfferAcceptance;
        db.Entry(profile).Property(p => p.ApprovedAmount).CurrentValue = 120_000m;
        await db.RhshfOffers.AddAsync(RhshfOffer.Create(profile.Id, 1, "offers/letter.pdf").Value);
        var collateral = RhshfCollateral.Create(profile.Id, 1, RhshfCollateralType.NirsalCrg, Guid.NewGuid(), "Declared by the FAC",
            null, null, null, null, null, null, 50m, null, null, null, null).Value;
        collateral.AddDocument("crg.pdf", "application/pdf", "collateral/crg.pdf", 100);
        await db.RhshfCollaterals.AddAsync(collateral);
        await db.SaveChangesAsync();
        return profile;
    }

    [Fact]
    public async Task An_amendment_replaces_the_breakdown_and_restarts_the_review_at_appraisal()
    {
        var (db, handler) = MakeHarness();
        var profile = await CaseWithOfferAsync(db, handler);

        var amended = await handler.Handle(new SubmitConsolidatedEopCommand(
            Request(4_200m, revision: 2, amendsReference: profile.Reference), "{\"amended\":true}"));

        Assert.True(amended.IsSuccess, amended.Error);
        Assert.Equal(profile.Reference, amended.Data!.Reference);
        Assert.Equal(1, await db.RhshfCreditProfiles.CountAsync());

        db.ChangeTracker.Clear();
        var reloaded = (await new RhshfCreditProfileRepository(db).GetByReferenceAsync(profile.Reference))!;
        Assert.Equal((RhshfCaseStatus.UnderReview, RhshfInternalStage.Appraisal, 2),
            (reloaded.Status, reloaded.InternalStage!.Value, reloaded.CurrentCycleNumber));
        Assert.Null(reloaded.ApprovedAmount);
        Assert.Equal(126_000m, reloaded.TotalEopValue);
        Assert.Equal(4_200m, Assert.Single(reloaded.FinancingLines).UnitPrice);
        Assert.Equal(2, Assert.Single(reloaded.FinancingEops).EopRevision);
        Assert.Single(await db.Set<RhshfFinancingLine>().ToListAsync()); // the old row is gone, not orphaned

        var amendment = Assert.Single(reloaded.Amendments);
        Assert.Equal((2, 1, 120_000m, 126_000m), (amendment.CycleNumber, amendment.ReplacedCycleNumber, amendment.PreviousTotalEopValue, amendment.NewTotalEopValue));
        Assert.Equal(new RhshfEopRevisionChange(EopId, "EOP-3CC02579", 1, 2, Added: false), Assert.Single(amendment.Changes));
        Assert.Contains("\"UnitPrice\":4000", amendment.PreviousBreakdownJson);
        Assert.Equal("{\"amended\":true}", amendment.RawPayload);
        Assert.Contains(reloaded.StatusHistory, h => h.Action.StartsWith("EOP amended by the portal: new review from appraisal"));

        Assert.Equal(RhshfOfferStatus.Superseded, (await db.RhshfOffers.SingleAsync()).Status);
        var carried = await db.RhshfCollaterals.Include(c => c.Documents).SingleAsync(c => c.CycleNumber == 2);
        Assert.Equal((RhshfCollateralType.NirsalCrg, 50m), (carried.Type, carried.CrgCoveragePercentage!.Value));
        Assert.Equal("collateral/crg.pdf", Assert.Single(carried.Documents).StoragePath);
        Assert.Null(carried.PerfectionStatus);

        var state = RhshfPortalStateBuilder.Build(reloaded, currentOffer: null, offerUrl: null, profilingUrl: null);
        Assert.Equal("UNDER_CREDIT_REVIEW", state.CreditStatus);
        Assert.Null(state.DecisionBasis);
        var block = Assert.IsType<RhshfPortalAmendment>(state.Amendment);
        Assert.Equal((EopId, 1, 2), (block.Eops.Single().EopId, block.Eops.Single().FromRevision!.Value, block.Eops.Single().ToRevision!.Value));
    }

    [Fact]
    public async Task A_repeat_of_the_same_amendment_is_a_retry()
    {
        var (db, handler) = MakeHarness();
        var profile = await CaseWithOfferAsync(db, handler);
        var amendment = Request(4_200m, revision: 2, amendsReference: profile.Reference);

        await handler.Handle(new SubmitConsolidatedEopCommand(amendment, "{}"));
        var again = await handler.Handle(new SubmitConsolidatedEopCommand(amendment, "{}"));

        Assert.True(again.IsSuccess, again.Error);
        Assert.Single(await db.Set<RhshfEopAmendment>().ToListAsync());
        Assert.Equal(2, (await db.RhshfCreditProfiles.SingleAsync()).CurrentCycleNumber);
    }

    [Theory]
    [InlineData("same revision", "has changed, so its revision must be higher than 1")]
    [InlineData("nothing changed", "No EOP in the amendment differs")]
    [InlineData("other FAC", "belongs to a different FAC")]
    [InlineData("booked", "has been booked")]
    [InlineData("declined", "is closed")]
    public async Task An_amendment_that_cannot_be_taken_is_refused_and_changes_nothing(string scenario, string message)
    {
        var (db, handler) = MakeHarness();
        var profile = await CaseWithOfferAsync(db, handler);
        if (scenario == "booked")
            db.Entry(profile).Property(p => p.InternalStage).CurrentValue = RhshfInternalStage.Active;
        if (scenario == "declined")
            db.Entry(profile).Property(p => p.Status).CurrentValue = RhshfCaseStatus.Declined;
        await db.SaveChangesAsync();

        var request = scenario switch
        {
            "same revision" => Request(4_200m, revision: 1, amendsReference: profile.Reference),
            "nothing changed" => Request(4_000m, revision: 1, amendsReference: profile.Reference),
            "other FAC" => Request(4_200m, revision: 2, amendsReference: profile.Reference, facId: Guid.NewGuid()),
            _ => Request(4_200m, revision: 2, amendsReference: profile.Reference),
        };
        var result = await handler.Handle(new SubmitConsolidatedEopCommand(request, "{}"));

        Assert.False(result.IsSuccess);
        Assert.StartsWith(SubmitConsolidatedEopHandler.AmendmentRefusedError, result.Error);
        Assert.Contains(message, result.Error);

        db.ChangeTracker.Clear();
        var unchanged = await db.RhshfCreditProfiles.Include(p => p.FinancingLines).SingleAsync();
        Assert.Equal((1, 120_000m, 4_000m), (unchanged.CurrentCycleNumber, unchanged.TotalEopValue, unchanged.FinancingLines.Single().UnitPrice!.Value));
        Assert.Empty(await db.Set<RhshfEopAmendment>().ToListAsync());
    }

    [Fact]
    public async Task An_amendment_naming_an_unknown_case_is_not_found()
    {
        var (_, handler) = MakeHarness();

        var result = await handler.Handle(new SubmitConsolidatedEopCommand(
            Request(4_200m, revision: 2, amendsReference: "RHSHF-2026-999999"), "{}"));

        Assert.StartsWith(SubmitConsolidatedEopHandler.AmendedCaseNotFoundError, result.Error);
    }

    [Fact]
    public async Task A_case_with_the_FAC_takes_the_amendment_without_a_new_cycle()
    {
        var (db, handler) = MakeHarness();
        await handler.Handle(new SubmitConsolidatedEopCommand(Request(4_000m, revision: 1), "{}"));
        var profile = await db.RhshfCreditProfiles.SingleAsync();

        var amended = await handler.Handle(new SubmitConsolidatedEopCommand(
            Request(4_200m, revision: 2, amendsReference: profile.Reference), "{}"));

        Assert.True(amended.IsSuccess, amended.Error);
        Assert.Equal((0, 126_000m), (profile.CurrentCycleNumber, profile.TotalEopValue));
        Assert.Equal(1, Assert.Single(profile.Amendments).CycleNumber); // reviewed when the FAC submits
        Assert.Contains(profile.StatusHistory, h => h.Action == "EOP amended by the portal while the case is with the FAC");
    }

    [Fact]
    public async Task An_administrator_can_have_an_amended_case_restart_at_legal_clearance()
    {
        var (db, handler) = MakeHarness();
        var profile = await CaseWithOfferAsync(db, handler);
        var request = Request(4_200m, revision: 2, amendsReference: profile.Reference);

        var amended = profile.AmendEops(Guid.NewGuid(), SubmitConsolidatedEopHandler.MapFinancing(request.Financing)!,
            request.TotalEopValue, request.FarmerCount, null, RhshfInternalStage.LegalClearance, "{}");

        Assert.True(amended.IsSuccess, amended.Error);
        Assert.Equal(RhshfInternalStage.LegalClearance, profile.InternalStage);
        Assert.Contains(profile.StatusHistory, h => h.Action == "EOP amended by the portal: new review from legal clearance");
        Assert.Contains(profile.DomainEvents, e => e is RhshfPortalUpdateEvent { EventType: RhshfCallbackEventType.CorrectionSubmitted });
        Assert.Contains(profile.DomainEvents, e => e is RhshfEopsAmendedEvent);
    }

    [Fact]
    public async Task A_later_stage_is_not_a_restart_point()
    {
        var (db, handler) = MakeHarness();
        var profile = await CaseWithOfferAsync(db, handler);
        var request = Request(4_200m, revision: 2, amendsReference: profile.Reference);

        var amended = profile.AmendEops(Guid.NewGuid(), SubmitConsolidatedEopHandler.MapFinancing(request.Financing)!,
            request.TotalEopValue, request.FarmerCount, null, RhshfInternalStage.RiskReview, "{}");

        Assert.False(amended.IsSuccess);
        Assert.Equal(1, profile.CurrentCycleNumber);
    }

    [Fact]
    public async Task Every_EOP_on_the_case_must_be_listed()
    {
        var (db, handler) = MakeHarness();
        var profile = await CaseWithOfferAsync(db, handler);
        var request = Request(4_200m, revision: 2, amendsReference: profile.Reference);
        var otherEop = Guid.NewGuid();
        var financing = request.Financing! with
        {
            InputLines = request.Financing!.InputLines!.Select(l => l with { EopId = otherEop }).ToList(),
            Eops = request.Financing.Eops!.Select(e => e with { EopId = otherEop, EopReference = "EOP-OTHER" }).ToList(),
        };

        var result = await handler.Handle(new SubmitConsolidatedEopCommand(request with { Financing = financing }, "{}"));

        Assert.False(result.IsSuccess);
        Assert.Contains("EOP-3CC02579 is on the case but not in the amendment", result.Error);
    }

    [Fact]
    public async Task The_portal_update_names_the_amendment()
    {
        var (db, handler) = MakeHarness();
        var profile = await CaseWithOfferAsync(db, handler);
        await handler.Handle(new SubmitConsolidatedEopCommand(Request(4_200m, revision: 2, amendsReference: profile.Reference), "{}"));

        var payload = RhshfWebhookPayloadBuilder.Build(profile, RhshfCallbackEventType.CorrectionSubmitted, "evt_1", DateTime.UtcNow);
        var json = System.Text.Json.JsonSerializer.Serialize(payload, System.Text.Json.JsonSerializerOptions.Web);

        Assert.Equal("CORRECTION_SUBMITTED", payload.EventType);
        Assert.Contains("\"amendment\":{", json);
        Assert.Contains("\"fromRevision\":1,\"toRevision\":2", json);
    }
}

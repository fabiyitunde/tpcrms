using CRMS.Application.Rhshf.Commands;
using CRMS.Application.Rhshf.DTOs;
using CRMS.Domain.Enums;
using CRMS.Infrastructure.Persistence;
using CRMS.Infrastructure.Persistence.Repositories.Rhshf;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CRMS.Infrastructure.Tests.Rhshf;

/// <summary>
/// The same EOP on two live cases would finance the same farmers twice (CRMSQA#3). Run against a
/// real CRMSDbContext, since the lookup is a query across the case, its EOP summaries and its lines.
/// </summary>
public class RhshfEopOnAnotherCaseTests
{
    private static (CRMSDbContext Db, SubmitConsolidatedEopHandler Handler) MakeHarness()
    {
        var options = new DbContextOptionsBuilder<CRMSDbContext>()
            .UseInMemoryDatabase("RhshfEopOnAnotherCase_" + Guid.NewGuid())
            .Options;
        var db = new CRMSDbContext(options);
        var handler = new SubmitConsolidatedEopHandler(
            new RhshfCreditProfileRepository(db), new SubmitConsolidatedEopIdempotencyTests.FakeTokenService(), new SubmitConsolidatedEopIdempotencyTests.FakeFineractDirectService(),
            new SubmitConsolidatedEopIdempotencyTests.FakeLocationRepository(), db, new RhshfScheduleFarmerRepository(db), new RhshfEopHolderLookup(db));
        return (db, handler);
    }

    private static SubmitConsolidatedEopRequest WithEop(Guid eopId)
    {
        var json = System.Text.Json.Nodes.JsonNode.Parse($$"""
        {
          "submissionId": "{{Guid.NewGuid()}}",
          "programme": { "code": "RH-SHF-DRY-2026", "name": "Renewed Hope" },
          "session": { "code": "2026-DRY", "name": "Dry Season 2026" },
          "fac": { "facId": "{{Guid.NewGuid()}}", "companyName": "Twice Farms Ltd", "rcNumber": "RC7654321",
                   "tin": "10293847-0001", "boaAccountNumber": "0099887766",
                   "contact": { "email": "test@example.com", "phone": "+2348099998888" }, "state": "Kano", "lga": "Bunkure" },
          "totalEopValue": 120000,
          "currency": "NGN",
          "farmerCount": 30,
          "eopLines": [ { "commodity": "NPK Fertilizer", "quantityKg": 30, "unitPricePerKg": 4000, "lineValue": 120000 } ],
          "callbackUrl": "https://portal.example.gov.ng/api/integrations/crms/webhook",
          "financing": {
            "currency": "NGN",
            "inputLines": [
              { "eopId": "{{eopId}}", "crop": "Maize", "item": "NPK Fertilizer", "category": "Fertilizer",
                "supplier": "Agro Supplies Ltd", "quantityPerFarmer": 1, "unit": "Kg", "unitPrice": 4000,
                "basis": "perFarmer", "farmers": 30, "amount": 120000, "packagingExcess": 0, "inLoan": true }
            ],
            "charges": [],
            "inputsTotal": 120000, "packagingExcessTotal": 0, "chargesTotal": 0,
            "approvedEopTotal": 120000, "loanAmount": 120000, "excludedFromLoan": 0,
            "eops": [
              { "eopId": "{{eopId}}", "crop": "Maize", "state": "Kano", "farmers": 30, "inputs": 120000,
                "packagingExcess": 0, "insurance": 0, "workingCapital": 0, "techServicesFee": 0,
                "approvedTotal": 120000, "loanAmount": 120000, "eopReference": "EOP-TWICE", "revision": 1 }
            ]
          }
        }
        """);
        return System.Text.Json.JsonSerializer.Deserialize<SubmitConsolidatedEopRequest>(json, System.Text.Json.JsonSerializerOptions.Web)!;
    }

    [Fact]
    public async Task A_second_case_for_an_EOP_on_a_live_case_is_refused_naming_that_case()
    {
        var (db, handler) = MakeHarness();
        var eopId = Guid.NewGuid();

        var first = await handler.Handle(new SubmitConsolidatedEopCommand(WithEop(eopId), "{}"));
        var second = await handler.Handle(new SubmitConsolidatedEopCommand(WithEop(eopId), "{}"));

        Assert.True(first.IsSuccess, first.Error);
        Assert.False(second.IsSuccess);
        Assert.StartsWith(SubmitConsolidatedEopHandler.EopOnAnotherCaseError, second.Error);
        var holder = SubmitConsolidatedEopHandler.ParseHolder(Assert.Single(second.Errors.Skip(1)));
        Assert.Equal((eopId, "EOP-TWICE", first.Data!.Reference), (holder.EopId, holder.EopReference, holder.CaseReference));
        Assert.Equal(1, await db.RhshfCreditProfiles.CountAsync());
    }

    [Theory]
    [InlineData(RhshfCaseStatus.Declined)]
    [InlineData(RhshfCaseStatus.Cancelled)]
    [InlineData(RhshfCaseStatus.Expired)]
    public async Task A_closed_case_does_not_hold_its_EOP(RhshfCaseStatus closed)
    {
        var (db, handler) = MakeHarness();
        var eopId = Guid.NewGuid();
        await handler.Handle(new SubmitConsolidatedEopCommand(WithEop(eopId), "{}"));
        var held = await db.RhshfCreditProfiles.SingleAsync();
        db.Entry(held).Property(p => p.Status).CurrentValue = closed;
        await db.SaveChangesAsync();

        var again = await handler.Handle(new SubmitConsolidatedEopCommand(WithEop(eopId), "{}"));

        Assert.True(again.IsSuccess, again.Error);
        Assert.Equal(2, await db.RhshfCreditProfiles.CountAsync());
    }

    [Fact]
    public async Task An_EOP_held_only_on_lines_is_still_found()
    {
        var (db, handler) = MakeHarness();
        var eopId = Guid.NewGuid();
        await handler.Handle(new SubmitConsolidatedEopCommand(WithEop(eopId), "{}"));
        // A case from before the portal sent the per-EOP summary.
        db.RhshfFinancingEops.RemoveRange(db.RhshfFinancingEops);
        await db.SaveChangesAsync();

        var holders = await new RhshfEopHolderLookup(db).GetLiveHoldersAsync([eopId]);

        Assert.Null(Assert.Single(holders).EopReference);
    }
}

using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Infrastructure.Persistence;
using CRMS.Infrastructure.Persistence.Repositories.Rhshf;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CRMS.Infrastructure.Tests.Rhshf;

/// <summary>
/// The trail is appended to an already-tracked aggregate on nearly every save, which is the exact
/// shape of the recurring EF bug in this codebase: a new child with a non-default key is inferred
/// as Modified rather than Added, and the UPDATE hits zero rows. Domain tests cannot see that —
/// they never touch a DbContext. These use a real one.
/// </summary>
public class RhshfStatusHistoryPersistenceTests
{
    private const decimal Eop = 5_000_000.00m;

    private static CRMSDbContext MakeDb() => new(
        new DbContextOptionsBuilder<CRMSDbContext>()
            .UseInMemoryDatabase("RhshfStatusHistory_" + Guid.NewGuid())
            .Options);

    private static RhshfCreditProfile NewProfile() => RhshfCreditProfile.Create(
        submissionId: Guid.NewGuid(), programmeCode: "RH-SHF-DRY-2026", programmeName: "Renewed Hope",
        sessionCode: "2026-DRY", sessionName: "Dry Season 2026", facId: Guid.NewGuid(),
        companyName: "Trail Test Farms Ltd", rcNumber: "RC1122334", tin: "55667788-0001",
        boaAccountNumber: "0123456789", contactEmail: "fac@example.com", contactPhone: "+2348012345678",
        state: "Kano", lga: "Bunkure", totalEopValue: Eop, currency: "NGN", farmerCount: 100,
        callbackUrl: "https://portal.example.gov.ng/api/integrations/crms/webhook",
        certifiedByAdmin: "admin@boa.gov.ng", certifiedAt: DateTime.UtcNow, rawSubmissionPayload: "{}",
        eopLines: null, resolvedBranchId: null, resolvedOfficeId: null).Value;

    [Fact]
    public async Task EntriesAppendedAcrossSeparateSaves_AreAllInserted()
    {
        var db = MakeDb();
        var repo = new RhshfCreditProfileRepository(db);
        var profile = NewProfile();
        await repo.AddAsync(profile);
        await db.SaveChangesAsync();

        // Each of these is a separate save against an aggregate EF is already tracking — the
        // condition that produced the "0 rows affected" bug on RhshfIssuedToken.
        foreach (var stage in new[]
        {
            RhshfProfilingStage.CompanyVerification,
            RhshfProfilingStage.CreditBureauCheck,
            RhshfProfilingStage.EopReview,
        })
        {
            if (stage == RhshfProfilingStage.EopReview)
                profile.AddFarmPlanDuringProfiling("Maize", 100m, 3_000m, 150m);

            profile.AdvanceStage(stage);
            await db.SaveChangesAsync();
        }

        var reloaded = await repo.GetByReferenceAsync(profile.Reference);

        Assert.NotNull(reloaded);
        Assert.Equal(4, reloaded!.StatusHistory.Count); // creation + 3 stage confirmations
        Assert.Equal(4, await db.RhshfStatusHistory.CountAsync());
    }

    [Fact]
    public async Task EntriesSurviveAReload_WithTheirEnumsAndActorIntact()
    {
        var db = MakeDb();
        var repo = new RhshfCreditProfileRepository(db);
        var profile = NewProfile();
        profile.AddFarmPlanDuringProfilingViaStages();
        var creditOfficer = Guid.NewGuid();
        profile.SeedViableAppraisal(creditOfficer);
        profile.Appraise(creditOfficer, RhshfAppraisalOutcome.Proceed, "Viable off-take");

        await repo.AddAsync(profile);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var reloaded = await repo.GetByReferenceAsync(profile.Reference);
        var entry = reloaded!.StatusHistory.Single(e => e.InternalStage == RhshfInternalStage.RiskReview);

        Assert.Equal("Appraisal completed — proceeding to risk review", entry.Action);
        Assert.Equal(creditOfficer, entry.ActorUserId);
        Assert.Null(entry.ActorLabel);
        Assert.Equal(RhshfCaseStatus.UnderReview, entry.Status);
        Assert.Equal("Viable off-take", entry.Note);
    }

    [Fact]
    public void EnumsArePersistedByName_NotOrdinal()
    {
        // An ordinal column would silently reinterpret every historical row the moment a value is
        // inserted into the middle of one of these enums. For an audit trail that is unacceptable,
        // so assert the storage shape rather than trusting the configuration to stay put.
        var db = MakeDb();
        var entityType = db.Model.FindEntityType(typeof(RhshfStatusHistory))!;

        foreach (var property in new[] { "Status", "InternalStage", "ProfilingStage" })
        {
            var clrType = entityType.FindProperty(property)!.GetProviderClrType();
            Assert.Equal(typeof(string), clrType);
        }
    }

    [Fact]
    public async Task ExistingEntriesAreNeverRewritten_OnASubsequentSave()
    {
        var db = MakeDb();
        var repo = new RhshfCreditProfileRepository(db);
        var profile = NewProfile();
        await repo.AddAsync(profile);
        await db.SaveChangesAsync();

        var originalId = profile.StatusHistory.Single().Id;
        var originalChangedAt = profile.StatusHistory.Single().ChangedAt;

        profile.AdvanceStage(RhshfProfilingStage.CompanyVerification);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var reloaded = await repo.GetByReferenceAsync(profile.Reference);
        var first = reloaded!.StatusHistory.Single(e => e.Id == originalId);

        Assert.Equal(originalChangedAt, first.ChangedAt);
        Assert.Equal("Submission received from the portal", first.Action);
    }
}

internal static class RhshfStatusHistoryPersistenceExtensions
{
    /// <summary>Walks the profile to UnderReview so an appraisal can be recorded against it.</summary>
    internal static void AddFarmPlanDuringProfilingViaStages(this RhshfCreditProfile profile)
    {
        foreach (var stage in new[]
        {
            RhshfProfilingStage.CompanyVerification, RhshfProfilingStage.CreditBureauCheck,
            RhshfProfilingStage.EopReview, RhshfProfilingStage.SupportingDocuments,
            RhshfProfilingStage.ReviewAndSubmit,
        })
        {
            if (stage == RhshfProfilingStage.EopReview)
                profile.AddFarmPlanDuringProfiling("Maize", 100m, 3_000m, 150m);

            profile.AdvanceStage(stage);
        }
    }
}

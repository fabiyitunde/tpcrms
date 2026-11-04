using CRMS.Application.Rhshf.Commands;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Infrastructure.Persistence;
using CRMS.Infrastructure.Persistence.Repositories.Rhshf;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CRMS.Infrastructure.Tests.Rhshf;

/// <summary>
/// A case returned to the FAC used to reopen the profiling form with no security declared, so the FAC
/// re-declared every instrument and re-uploaded its documents before it could resubmit. The next
/// cycle now starts with the collateral as declared.
/// </summary>
public class RhshfCollateralCarryForwardTests
{
    /// <summary>A submitted case at appraisal in cycle 1, with one perfected instrument and its document.</summary>
    private static async Task<RhshfCreditProfile> CaseAtAppraisalAsync(CRMSDbContext db, SubmitConsolidatedEopHandler handler,
        RhshfCaseStatus status = RhshfCaseStatus.UnderReview)
    {
        await handler.Handle(new SubmitConsolidatedEopCommand(RhshfEopAmendmentTests.Request(4_000m, revision: 1), "{}"));
        var profile = await db.RhshfCreditProfiles.SingleAsync();
        db.Entry(profile).Property(p => p.Status).CurrentValue = status;
        db.Entry(profile).Property(p => p.CurrentCycleNumber).CurrentValue = 1;
        db.Entry(profile).Property(p => p.InternalStage).CurrentValue =
            status == RhshfCaseStatus.UnderReview ? RhshfInternalStage.Appraisal : null;
        if (status == RhshfCaseStatus.ProfilingInProgress)
            db.Entry(profile).Property(p => p.CurrentStage).CurrentValue = RhshfProfilingStage.ReviewAndSubmit;

        var collateral = RhshfCollateral.Create(profile.Id, 1, RhshfCollateralType.BankGuarantee, Guid.NewGuid(), "From the FAC",
            "BG-001", null, null, "Union Bank", 120_000m, true, null, null, null, null, null).Value;
        collateral.AddDocument("guarantee.pdf", "application/pdf", "collateral/guarantee.pdf", 100);
        collateral.AssertPerfection(Guid.NewGuid(), RhshfCollateralPerfectionStatus.Perfected);
        await db.RhshfCollaterals.AddAsync(collateral);
        await db.SaveChangesAsync();
        return profile;
    }

    private static AppraiseRhshfCaseHandler Appraisal(CRMSDbContext db)
        => new(new RhshfCreditProfileRepository(db), db, reasonCodes: null, collateral: new RhshfCollateralRepository(db));

    [Fact]
    public async Task A_case_returned_to_the_FAC_keeps_its_collateral_for_the_resubmission()
    {
        var (db, handler) = RhshfEopAmendmentTests.MakeHarness();
        var profile = await CaseAtAppraisalAsync(db, handler);

        var returned = await Appraisal(db).Handle(new AppraiseRhshfCaseCommand(
            profile.Reference, Guid.NewGuid(), RhshfAppraisalOutcome.ReturnToFac, "Upload the CAC certificate",
            RhshfProfilingStage.SupportingDocuments));

        Assert.True(returned.IsSuccess, returned.Error);
        Assert.Equal(2, profile.ProfilingTargetCycleNumber);
        var carried = await db.RhshfCollaterals.Include(c => c.Documents).SingleAsync(c => c.CycleNumber == 2);
        Assert.Equal(("Union Bank", 120_000m), (carried.GuarantorBankName, carried.GuaranteeAmount!.Value));
        Assert.Equal("collateral/guarantee.pdf", Assert.Single(carried.Documents).StoragePath);
        Assert.Null(carried.PerfectionStatus); // Legal confirms it again in the new review
        Assert.Equal(2, await db.RhshfCollaterals.CountAsync()); // the cycle 1 record stays as it was
    }

    [Fact]
    public async Task What_the_FAC_has_already_declared_for_the_next_cycle_is_left_alone()
    {
        var (db, handler) = RhshfEopAmendmentTests.MakeHarness();
        var profile = await CaseAtAppraisalAsync(db, handler);
        await db.RhshfCollaterals.AddAsync(RhshfCollateral.Create(profile.Id, 2, RhshfCollateralType.NirsalCrg, Guid.NewGuid(), null,
            null, null, null, null, null, null, 50m, null, null, null, null).Value);
        await db.SaveChangesAsync();

        await Appraisal(db).Handle(new AppraiseRhshfCaseCommand(
            profile.Reference, Guid.NewGuid(), RhshfAppraisalOutcome.ReturnToFac, "Fix the farm plan", RhshfProfilingStage.SupportingDocuments));

        Assert.Equal(RhshfCollateralType.NirsalCrg, (await db.RhshfCollaterals.SingleAsync(c => c.CycleNumber == 2)).Type);
    }

    [Fact]
    public async Task A_decline_carries_nothing()
    {
        var (db, handler) = RhshfEopAmendmentTests.MakeHarness();
        var profile = await CaseAtAppraisalAsync(db, handler);

        await Appraisal(db).Handle(new AppraiseRhshfCaseCommand(
            profile.Reference, Guid.NewGuid(), RhshfAppraisalOutcome.Decline, "Not eligible", null));

        Assert.Equal(1, await db.RhshfCollaterals.CountAsync());
    }

    [Fact]
    public async Task Cases_already_with_the_FAC_are_back_filled_at_startup_once()
    {
        var (db, handler) = RhshfEopAmendmentTests.MakeHarness();
        await CaseAtAppraisalAsync(db, handler, RhshfCaseStatus.ProfilingInProgress);

        await RhshfWorkflowSeeder.SeedAsync(db, NullLogger.Instance);
        await RhshfWorkflowSeeder.SeedAsync(db, NullLogger.Instance);

        var carried = await db.RhshfCollaterals.Include(c => c.Documents).SingleAsync(c => c.CycleNumber == 2);
        Assert.Single(carried.Documents);
    }
}

using CRMS.Application.Rhshf.Commands;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Tests.Rhshf;

/// <summary>
/// The submit-time gates that live in the handler rather than the domain, because guarantors and
/// collateral are separate aggregates the profile doesn't own: every individual guarantor must carry
/// a BVN (companies are checked by RC), and at least one security instrument must be declared, since
/// the Legal Officer sights and perfects it before the case is circulated for approval.
/// </summary>
public class AdvanceRhshfProfilingStageGuarantorGateTests
{
    /// <summary>A profile walked to ReviewAndSubmit with a farm plan and a BVN-complete director, so
    /// the only thing that can block submission is the guarantor gate under test.</summary>
    private static RhshfCreditProfile ProfileReadyToSubmit()
    {
        var profile = RhshfCreditProfile.Create(
            submissionId: Guid.NewGuid(), programmeCode: "RH-SHF-DRY-2026", programmeName: "Renewed Hope",
            sessionCode: "2026-DRY", sessionName: "Dry Season 2026", facId: Guid.NewGuid(),
            companyName: "Alliedsoft Limited", rcNumber: "RC123456", tin: "01234567-0001",
            boaAccountNumber: "0123456789", contactEmail: "fac@company.com", contactPhone: "+2348012345678",
            state: "Kano", lga: "Nassarawa", totalEopValue: 51_500_000m, currency: "NGN", farmerCount: 1200,
            callbackUrl: "https://portal.example.gov.ng/api/integrations/crms/webhook",
            certifiedByAdmin: "admin@boa.gov.ng", certifiedAt: DateTime.UtcNow, rawSubmissionPayload: "{}",
            eopLines: null, resolvedBranchId: null, resolvedOfficeId: null).Value;

        profile.AdvanceStage(RhshfProfilingStage.CompanyVerification);
        profile.AddDirector(RhshfDirector.CreateManual(
            profile.Id, "Jane Doe", "12345678901", null, false, null, null).Value);
        profile.AdvanceStage(RhshfProfilingStage.CreditBureauCheck);
        profile.AddFarmPlanDuringProfiling("Maize", 100m, 3_000m, 515m);
        profile.AdvanceStage(RhshfProfilingStage.EopReview);
        profile.AdvanceStage(RhshfProfilingStage.SupportingDocuments);
        return profile; // now on ReviewAndSubmit
    }

    /// <summary>Security declared by default, so these calls stay about the guarantor gate.</summary>
    private static AdvanceRhshfProfilingStageHandler Handler(RhshfCreditProfile profile, params RhshfGuarantor[] guarantors)
        => new(new FakeProfileRepo(profile), new FakeRequirementRepo(), new FakeGuarantorRepo(guarantors),
            new FakeCollateralRepo([DeclaredSecurity(profile)]), new FakeUow());

    private static AdvanceRhshfProfilingStageHandler HandlerWithSecurity(RhshfCreditProfile profile, params RhshfCollateral[] collateral)
        => new(new FakeProfileRepo(profile), new FakeRequirementRepo(), new FakeGuarantorRepo([]),
            new FakeCollateralRepo(collateral), new FakeUow());

    private static RhshfGuarantor Individual(string? bvn) => RhshfGuarantor.Create(
        Guid.NewGuid(), RhshfGuarantorType.Individual, "Ada Guarantor", bvn, null, "Friend", null, null, null, 1_000_000m, null).Value;

    /// <summary>A bank guarantee filed under the cycle this profiling pass will be reviewed as, with
    /// its guarantee letter attached. PerfectionStatus is left unset — the FAC declares, the Legal
    /// Officer asserts perfection.</summary>
    private static RhshfCollateral DeclaredSecurity(RhshfCreditProfile profile, bool withDocument = true)
    {
        var collateral = RhshfCollateral.Create(
            profile.Id, profile.ProfilingTargetCycleNumber, RhshfCollateralType.BankGuarantee, Guid.NewGuid(), null,
            "BG-2026-01", null, null, "First Bank", 52_000_000m, true, null, null, null, null, null, null).Value;
        if (withDocument)
            collateral.AddDocument("guarantee-letter.pdf", "application/pdf", "path/bg.pdf", 2048);
        return collateral;
    }

    [Fact]
    public async Task Submit_IsBlocked_WhenAnIndividualGuarantorHasNoBvn()
    {
        var profile = ProfileReadyToSubmit();

        var result = await Handler(profile, Individual(bvn: null))
            .Handle(new AdvanceRhshfProfilingStageCommand(profile.Reference, RhshfProfilingStage.ReviewAndSubmit));

        Assert.False(result.IsSuccess);
        Assert.Contains("BVN", result.Error);
        Assert.Equal(RhshfProfilingStage.ReviewAndSubmit, profile.CurrentStage); // not advanced
    }

    [Fact]
    public async Task Submit_Proceeds_WhenEveryIndividualGuarantorHasABvn()
    {
        var profile = ProfileReadyToSubmit();

        var result = await Handler(profile, Individual(bvn: "22222222222"))
            .Handle(new AdvanceRhshfProfilingStageCommand(profile.Reference, RhshfProfilingStage.ReviewAndSubmit));

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfCaseStatus.UnderReview, profile.Status);
    }

    [Fact]
    public async Task Submit_IsBlocked_WhenNoSecurityHasBeenDeclared()
    {
        var profile = ProfileReadyToSubmit();

        var result = await HandlerWithSecurity(profile)
            .Handle(new AdvanceRhshfProfilingStageCommand(profile.Reference, RhshfProfilingStage.ReviewAndSubmit));

        Assert.False(result.IsSuccess);
        Assert.Contains("security instrument", result.Error);
        Assert.Equal(RhshfProfilingStage.ReviewAndSubmit, profile.CurrentStage); // not advanced
        Assert.Equal(RhshfCaseStatus.ProfilingInProgress, profile.Status);
    }

    [Fact]
    public async Task Submit_Proceeds_WhenSecurityHasBeenDeclared()
    {
        var profile = ProfileReadyToSubmit();

        var result = await HandlerWithSecurity(profile, DeclaredSecurity(profile))
            .Handle(new AdvanceRhshfProfilingStageCommand(profile.Reference, RhshfProfilingStage.ReviewAndSubmit));

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfCaseStatus.UnderReview, profile.Status);
        // Lands at Appraisal; legal clearance (where this security gets sighted) is the next stage.
        Assert.Equal(RhshfInternalStage.Appraisal, profile.InternalStage);
    }

    [Fact]
    public async Task Submit_IsBlocked_WhenADeclaredInstrumentHasNoDocument()
    {
        var profile = ProfileReadyToSubmit();

        var result = await HandlerWithSecurity(profile, DeclaredSecurity(profile, withDocument: false))
            .Handle(new AdvanceRhshfProfilingStageCommand(profile.Reference, RhshfProfilingStage.ReviewAndSubmit));

        // Legal has to sight the instrument itself; a bare declaration would bounce back from legal
        // clearance and cost the FAC a cycle.
        Assert.False(result.IsSuccess);
        Assert.Contains("supporting document", result.Error);
        Assert.Equal(RhshfProfilingStage.ReviewAndSubmit, profile.CurrentStage);
    }

    [Fact]
    public async Task Submit_IsBlocked_WhenOnlySomeInstrumentsHaveDocuments()
    {
        var profile = ProfileReadyToSubmit();

        var result = await HandlerWithSecurity(profile, DeclaredSecurity(profile), DeclaredSecurity(profile, withDocument: false))
            .Handle(new AdvanceRhshfProfilingStageCommand(profile.Reference, RhshfProfilingStage.ReviewAndSubmit));

        Assert.False(result.IsSuccess);
        Assert.Contains("1 of 2", result.Error);
    }

    [Fact]
    public async Task Submit_IsNotBlocked_ByAnInstrumentFiledUnderAnEarlierCycle()
    {
        var profile = ProfileReadyToSubmit();
        var stale = RhshfCollateral.Create(
            profile.Id, profile.ProfilingTargetCycleNumber - 1, RhshfCollateralType.NirsalCrg, Guid.NewGuid(), null,
            "CRG-OLD", null, null, null, null, null, 75m, null, null, null, null, null).Value;

        var result = await HandlerWithSecurity(profile, stale)
            .Handle(new AdvanceRhshfProfilingStageCommand(profile.Reference, RhshfProfilingStage.ReviewAndSubmit));

        // A previous cycle's instrument is not security for this one — the repo query is cycle-scoped,
        // so this must read as "nothing declared" rather than silently passing the gate.
        Assert.False(result.IsSuccess);
        Assert.Contains("security instrument", result.Error);
    }

    private class FakeProfileRepo(RhshfCreditProfile profile) : IRhshfCreditProfileRepository
    {
        public Task<RhshfCreditProfile?> GetByReferenceAsync(string reference, CancellationToken ct = default)
            => Task.FromResult<RhshfCreditProfile?>(profile.Reference == reference ? profile : null);
        public Task<RhshfCreditProfile?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<RhshfCreditProfile?>(null);
        public Task<RhshfCreditProfile?> GetBySubmissionIdAsync(Guid submissionId, CancellationToken ct = default) => Task.FromResult<RhshfCreditProfile?>(null);
        public Task AddAsync(RhshfCreditProfile p, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<RhshfCreditProfile>> GetAllForListAsync(Guid? branchId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<RhshfCreditProfile>>([]);
        public Task<IReadOnlyList<RhshfCreditProfile>> GetQueueAsync(RhshfInternalStage stage, Guid? branchId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RhshfCreditProfile>>([]);
        public Task<RhshfSupportingDocument?> GetSupportingDocumentByIdAsync(Guid documentId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private class FakeRequirementRepo : IRhshfDocumentRequirementRepository
    {
        public Task<IReadOnlyList<RhshfDocumentRequirement>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<RhshfDocumentRequirement>>([]);
        public Task<IReadOnlyList<RhshfDocumentRequirement>> GetActiveAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<RhshfDocumentRequirement>>([]);
        public Task<RhshfDocumentRequirement?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<RhshfDocumentRequirement?>(null);
        public Task AddAsync(RhshfDocumentRequirement requirement, CancellationToken ct = default) => Task.CompletedTask;
        public void Update(RhshfDocumentRequirement requirement) { }
    }

    private class FakeGuarantorRepo(RhshfGuarantor[] guarantors) : IRhshfGuarantorRepository
    {
        public Task<IReadOnlyList<RhshfGuarantor>> GetByProfileIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RhshfGuarantor>>(guarantors);
        public Task<RhshfGuarantor?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<RhshfGuarantor?>(null);
        public Task AddAsync(RhshfGuarantor g, CancellationToken ct = default) => Task.CompletedTask;
        public void Remove(RhshfGuarantor g) { }
    }

    /// <summary>Cycle-scoped, like the real repository — that scoping is what the stale-cycle test
    /// above relies on.</summary>
    private class FakeCollateralRepo(RhshfCollateral[] collateral) : IRhshfCollateralRepository
    {
        public Task<IReadOnlyList<RhshfCollateral>> GetByProfileAndCycleAsync(Guid profileId, int cycleNumber, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RhshfCollateral>>(
                [.. collateral.Where(c => c.RhshfCreditProfileId == profileId && c.CycleNumber == cycleNumber)]);
        public Task<RhshfCollateral?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<RhshfCollateral?>(null);
        public Task AddAsync(RhshfCollateral c, CancellationToken ct = default) => Task.CompletedTask;
        public void Remove(RhshfCollateral c) { }
        public Task<RhshfCollateralDocument?> GetDocumentByIdAsync(Guid documentId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private class FakeUow : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
    }
}

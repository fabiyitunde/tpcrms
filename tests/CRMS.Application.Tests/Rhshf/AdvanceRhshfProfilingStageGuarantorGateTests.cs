using CRMS.Application.Rhshf.Commands;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Tests.Rhshf;

/// <summary>
/// The submit-time gate that every individual guarantor must carry a BVN (companies are checked by
/// RC). It lives in the handler rather than the domain because guarantors are a separate aggregate
/// the profile doesn't own — the counterpart to the director-BVN gate that is in the domain.
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

    private static AdvanceRhshfProfilingStageHandler Handler(RhshfCreditProfile profile, params RhshfGuarantor[] guarantors)
        => new(new FakeProfileRepo(profile), new FakeRequirementRepo(), new FakeGuarantorRepo(guarantors), new FakeUow());

    private static RhshfGuarantor Individual(string? bvn) => RhshfGuarantor.Create(
        Guid.NewGuid(), RhshfGuarantorType.Individual, "Ada Guarantor", bvn, null, "Friend", null, null, null, 1_000_000m, null).Value;

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

    private class FakeProfileRepo(RhshfCreditProfile profile) : IRhshfCreditProfileRepository
    {
        public Task<RhshfCreditProfile?> GetByReferenceAsync(string reference, CancellationToken ct = default)
            => Task.FromResult<RhshfCreditProfile?>(profile.Reference == reference ? profile : null);
        public Task<RhshfCreditProfile?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<RhshfCreditProfile?>(null);
        public Task<RhshfCreditProfile?> GetBySubmissionIdAsync(Guid submissionId, CancellationToken ct = default) => Task.FromResult<RhshfCreditProfile?>(null);
        public Task AddAsync(RhshfCreditProfile p, CancellationToken ct = default) => Task.CompletedTask;
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

    private class FakeUow : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
    }
}

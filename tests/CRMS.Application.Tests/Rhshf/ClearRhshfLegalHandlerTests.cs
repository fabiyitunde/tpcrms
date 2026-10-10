using CRMS.Application.Rhshf.Commands;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Tests.Rhshf;

public class ClearRhshfLegalHandlerTests
{
    private const decimal TotalEopValue = 51_500_000.00m;

    /// <summary>Legal clearance now sits between Appraisal and Risk Review, so a case reaches it
    /// straight off a Proceed appraisal — no ratification or offer in the picture.</summary>
    private static RhshfCreditProfile MakeProfileAtLegalClearance()
    {
        var result = RhshfCreditProfile.Create(
            submissionId: Guid.NewGuid(), programmeCode: "RH-SHF-DRY-2026", programmeName: "Renewed Hope",
            sessionCode: "2026-DRY", sessionName: "Dry Season 2026", facId: Guid.NewGuid(),
            companyName: "Alliedsoft Limited", rcNumber: "RC123456", tin: "01234567-0001",
            boaAccountNumber: "0123456789", contactEmail: "fac@company.com", contactPhone: "+2348012345678",
            state: "Kano", lga: "Nassarawa", totalEopValue: TotalEopValue, currency: "NGN", farmerCount: 1200,
            callbackUrl: "https://portal.example.gov.ng/api/integrations/crms/webhook",
            certifiedByAdmin: "admin@boa.gov.ng", certifiedAt: DateTime.UtcNow, rawSubmissionPayload: "{}",
            eopLines: null, resolvedBranchId: null, resolvedOfficeId: null);
        var profile = result.Value;

        foreach (var stage in new[]
        {
            RhshfProfilingStage.CompanyVerification, RhshfProfilingStage.CreditBureauCheck,
            RhshfProfilingStage.EopReview, RhshfProfilingStage.SupportingDocuments, RhshfProfilingStage.ReviewAndSubmit,
        })
        {
            profile.AdvanceStageForTest(stage);
        }

        profile.AppraiseWithFinancials(Guid.NewGuid(), RhshfAppraisalOutcome.Proceed, null);

        return profile; // now InternalStage == LegalClearance
    }

    [Fact]
    public async Task Clear_Granted_AdvancesToRiskReview_RecordsClearance()
    {
        var profile = MakeProfileAtLegalClearance();
        var legalRepo = new FakeLegalClearanceRepository();
        var handler = new ClearRhshfLegalHandler(new FakeProfileRepository(profile), legalRepo, new FakeUnitOfWork());

        var result = await handler.Handle(new ClearRhshfLegalCommand(
            profile.Reference, Guid.NewGuid(), RhshfLegalClearanceOutcome.Granted, "all clear"));

        Assert.True(result.IsSuccess);
        // Risk is the last internal look before circulation, so legal hands off to it — not to
        // pre-deployment, which is now reached from offer acceptance instead.
        Assert.Equal(RhshfInternalStage.RiskReview, profile.InternalStage);
        Assert.NotNull(legalRepo.Added);
        Assert.Equal(RhshfLegalClearanceOutcome.Granted, legalRepo.Added!.Outcome);
    }

    [Fact]
    public async Task Clear_Granted_SeedsNoPreDeploymentChecklist()
    {
        var profile = MakeProfileAtLegalClearance();
        var handler = new ClearRhshfLegalHandler(
            new FakeProfileRepository(profile), new FakeLegalClearanceRepository(), new FakeUnitOfWork());

        var result = await handler.Handle(new ClearRhshfLegalCommand(
            profile.Reference, Guid.NewGuid(), RhshfLegalClearanceOutcome.Granted, "all clear"));

        Assert.True(result.IsSuccess);
        // The gate checklist belongs to the post-offer stage now, so nothing is seeded this early.
        Assert.Empty(profile.PreDeploymentChecklist);
    }

    [Fact]
    public async Task Clear_RecordsTheClearingLegalOfficer()
    {
        var profile = MakeProfileAtLegalClearance();
        var legalOfficerId = Guid.NewGuid();
        var legalRepo = new FakeLegalClearanceRepository();
        var handler = new ClearRhshfLegalHandler(new FakeProfileRepository(profile), legalRepo, new FakeUnitOfWork());

        await handler.Handle(new ClearRhshfLegalCommand(
            profile.Reference, legalOfficerId, RhshfLegalClearanceOutcome.Granted, null));

        // The id has to be stored: risk review, the committee vote and ratification all exclude it.
        Assert.Equal(legalOfficerId, legalRepo.Added!.LegalOfficerId);
    }

    [Fact]
    public async Task Clear_Returned_GoesBackToTheFac_AtTheRequestedStage()
    {
        var profile = MakeProfileAtLegalClearance();
        var handler = new ClearRhshfLegalHandler(new FakeProfileRepository(profile), new FakeLegalClearanceRepository(), new FakeUnitOfWork());

        var result = await handler.Handle(new ClearRhshfLegalCommand(
            profile.Reference, Guid.NewGuid(), RhshfLegalClearanceOutcome.Returned, "fix the deed",
            RhshfProfilingStage.CreditBureauCheck));

        Assert.True(result.IsSuccess);
        // The defect is in what the FAC declared, and the FAC is who can correct it.
        Assert.Equal(RhshfCaseStatus.ProfilingInProgress, profile.Status);
        Assert.Equal(RhshfProfilingStage.CreditBureauCheck, profile.CurrentStage);
        Assert.Null(profile.InternalStage);
    }

    [Fact]
    public async Task Clear_Declined_IsTerminal()
    {
        var profile = MakeProfileAtLegalClearance();
        var handler = new ClearRhshfLegalHandler(new FakeProfileRepository(profile), new FakeLegalClearanceRepository(), new FakeUnitOfWork());

        var result = await handler.Handle(new ClearRhshfLegalCommand(
            profile.Reference, Guid.NewGuid(), RhshfLegalClearanceOutcome.Declined, "title dispute"));

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfCaseStatus.Declined, profile.Status);
        Assert.Equal("CRMS Legal Clearance", profile.DecidedBy);
    }

    private class FakeProfileRepository : IRhshfCreditProfileRepository
    {
        private readonly RhshfCreditProfile? _profile;
        public FakeProfileRepository(RhshfCreditProfile? profile) => _profile = profile;
        public Task<RhshfCreditProfile?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_profile?.Id == id ? _profile : null);
        public Task<RhshfCreditProfile?> GetByReferenceAsync(string reference, CancellationToken ct = default)
            => Task.FromResult(_profile?.Reference == reference ? _profile : null);
        public Task<RhshfCreditProfile?> GetBySubmissionIdAsync(Guid submissionId, CancellationToken ct = default)
            => Task.FromResult(_profile?.SubmissionId == submissionId ? _profile : null);
        public Task AddAsync(RhshfCreditProfile profile, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<RhshfCreditProfile>> GetAllForListAsync(Guid? branchId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<RhshfCreditProfile>>([]);
        public Task<IReadOnlyList<RhshfCreditProfile>> GetQueueAsync(RhshfInternalStage stage, Guid? branchId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RhshfCreditProfile>>(
                _profile is not null && _profile.InternalStage == stage ? [_profile] : []);
        public Task<RhshfSupportingDocument?> GetSupportingDocumentByIdAsync(Guid documentId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private class FakeLegalClearanceRepository : IRhshfLegalClearanceRepository
    {
        public RhshfLegalClearance? Added { get; private set; }
        public Task<IReadOnlyList<RhshfLegalClearance>> GetByProfileAndCycleAsync(Guid rhshfCreditProfileId, int cycleNumber, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RhshfLegalClearance>>(Added is not null ? [Added] : []);
        public Task AddAsync(RhshfLegalClearance clearance, CancellationToken ct = default)
        {
            Added = clearance;
            return Task.CompletedTask;
        }
    }

    private class FakeTemplateRepository : IRhshfPreDeploymentChecklistTemplateRepository
    {
        private readonly IReadOnlyList<RhshfPreDeploymentChecklistTemplate> _active;
        public FakeTemplateRepository(IReadOnlyList<RhshfPreDeploymentChecklistTemplate>? active = null) => _active = active ?? [];

        public Task<IReadOnlyList<RhshfPreDeploymentChecklistTemplate>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(_active);
        public Task<IReadOnlyList<RhshfPreDeploymentChecklistTemplate>> GetActiveAsync(CancellationToken ct = default) => Task.FromResult(_active);
        public Task<RhshfPreDeploymentChecklistTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(RhshfPreDeploymentChecklistTemplate template, CancellationToken ct = default) => Task.CompletedTask;
        public void Update(RhshfPreDeploymentChecklistTemplate template) { }
    }

    private class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
    }
}

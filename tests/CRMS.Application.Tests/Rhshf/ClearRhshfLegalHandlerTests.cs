using CRMS.Application.Rhshf.Commands;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Tests.Rhshf;

public class ClearRhshfLegalHandlerTests
{
    private const decimal TotalEopValue = 51_500_000.00m;

    private static (RhshfCreditProfile Profile, Guid FinalApproverId) MakeProfileAtLegalClearance()
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
        profile.ReviewRisk(Guid.NewGuid(), RhshfRiskReviewOutcome.Cleared, null);
        profile.AdvanceToRatification();
        var finalApproverId = Guid.NewGuid();
        profile.Ratify(finalApproverId, RhshfRatificationOutcome.Ratified, TotalEopValue, null, null, []);

        var offer = RhshfOffer.Create(profile.Id, profile.CurrentCycleNumber, "path/offer-cycle1.pdf").Value;
        offer.AddDocument("signed.pdf", "application/pdf", "path/signed.pdf", 1024);
        offer.Accept(null);
        profile.AdvanceToLegalClearance();

        return (profile, finalApproverId);
    }

    [Fact]
    public async Task Clear_Granted_AdvancesToPreDeploymentVerification_RecordsClearance()
    {
        var (profile, finalApproverId) = MakeProfileAtLegalClearance();
        var legalRepo = new FakeLegalClearanceRepository();
        var handler = new ClearRhshfLegalHandler(new FakeProfileRepository(profile), legalRepo, new FakeTemplateRepository(), new FakeUnitOfWork());

        var result = await handler.Handle(new ClearRhshfLegalCommand(
            profile.Reference, Guid.NewGuid(), RhshfLegalClearanceOutcome.Granted, "all clear"));

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfInternalStage.PreDeploymentVerification, profile.InternalStage);
        Assert.NotNull(legalRepo.Added);
        Assert.Equal(RhshfLegalClearanceOutcome.Granted, legalRepo.Added!.Outcome);
    }

    [Fact]
    public async Task Clear_Granted_SeedsPreDeploymentChecklistFromActiveTemplates()
    {
        var (profile, _) = MakeProfileAtLegalClearance();
        var template = RhshfPreDeploymentChecklistTemplate.Create("Gate Item", null, isMandatory: true, sortOrder: 10).Value;
        var handler = new ClearRhshfLegalHandler(
            new FakeProfileRepository(profile), new FakeLegalClearanceRepository(),
            new FakeTemplateRepository([template]), new FakeUnitOfWork());

        var result = await handler.Handle(new ClearRhshfLegalCommand(
            profile.Reference, Guid.NewGuid(), RhshfLegalClearanceOutcome.Granted, "all clear"));

        Assert.True(result.IsSuccess);
        Assert.Single(profile.PreDeploymentChecklist);
        Assert.Equal("Gate Item", profile.PreDeploymentChecklist.Single().Title);
    }

    [Fact]
    public async Task Clear_SamePersonAsFinalApprover_Fails_DoesNotTransitionProfile()
    {
        var (profile, finalApproverId) = MakeProfileAtLegalClearance();
        var legalRepo = new FakeLegalClearanceRepository();
        var handler = new ClearRhshfLegalHandler(new FakeProfileRepository(profile), legalRepo, new FakeTemplateRepository(), new FakeUnitOfWork());

        var result = await handler.Handle(new ClearRhshfLegalCommand(
            profile.Reference, finalApproverId, RhshfLegalClearanceOutcome.Granted, null));

        Assert.False(result.IsSuccess);
        Assert.Null(legalRepo.Added);
        Assert.Equal(RhshfInternalStage.LegalClearance, profile.InternalStage);
    }

    [Fact]
    public async Task Clear_Returned_RoutesBackToRatification_NotAppraisal()
    {
        var (profile, _) = MakeProfileAtLegalClearance();
        var handler = new ClearRhshfLegalHandler(new FakeProfileRepository(profile), new FakeLegalClearanceRepository(), new FakeTemplateRepository(), new FakeUnitOfWork());

        var result = await handler.Handle(new ClearRhshfLegalCommand(
            profile.Reference, Guid.NewGuid(), RhshfLegalClearanceOutcome.Returned, "fix the deed"));

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfInternalStage.Ratification, profile.InternalStage);
        Assert.Equal(RhshfCaseStatus.UnderReview, profile.Status);
    }

    [Fact]
    public async Task Clear_Declined_IsTerminal()
    {
        var (profile, _) = MakeProfileAtLegalClearance();
        var handler = new ClearRhshfLegalHandler(new FakeProfileRepository(profile), new FakeLegalClearanceRepository(), new FakeTemplateRepository(), new FakeUnitOfWork());

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

using CRMS.Application.Rhshf.Commands;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Tests.Rhshf;

public class RecordRhshfEligibilityChecklistHandlerTests
{
    private const decimal TotalEopValue = 51_500_000.00m;

    private static RhshfCreditProfile MakeProfileUnderReview()
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

        return profile; // now UnderReview, InternalStage == Appraisal
    }

    [Fact]
    public async Task Record_AllCriteria_Succeeds()
    {
        var profile = MakeProfileUnderReview();
        var eligibilityRepo = new FakeEligibilityRepository();
        var handler = new RecordRhshfEligibilityChecklistHandler(new FakeProfileRepository(profile), eligibilityRepo, new FakeUnitOfWork());

        var criteria = Enum.GetValues<RhshfEligibilityCriterion>()
            .Select(c => new RhshfEligibilityCriterionInput(c, true, null)).ToList();
        var result = await handler.Handle(new RecordRhshfEligibilityChecklistCommand(profile.Reference, Guid.NewGuid(), criteria));

        Assert.True(result.IsSuccess);
        Assert.Equal(11, eligibilityRepo.Added.Count);
    }

    [Fact]
    public async Task Record_Twice_SecondCallFails()
    {
        var profile = MakeProfileUnderReview();
        var eligibilityRepo = new FakeEligibilityRepository();
        var handler = new RecordRhshfEligibilityChecklistHandler(new FakeProfileRepository(profile), eligibilityRepo, new FakeUnitOfWork());
        var criteria = Enum.GetValues<RhshfEligibilityCriterion>()
            .Select(c => new RhshfEligibilityCriterionInput(c, true, null)).ToList();
        await handler.Handle(new RecordRhshfEligibilityChecklistCommand(profile.Reference, Guid.NewGuid(), criteria));

        var second = await handler.Handle(new RecordRhshfEligibilityChecklistCommand(profile.Reference, Guid.NewGuid(), criteria));

        Assert.False(second.IsSuccess);
    }

    [Fact]
    public async Task Record_EmptyCriteria_Fails()
    {
        var profile = MakeProfileUnderReview();
        var handler = new RecordRhshfEligibilityChecklistHandler(new FakeProfileRepository(profile), new FakeEligibilityRepository(), new FakeUnitOfWork());

        var result = await handler.Handle(new RecordRhshfEligibilityChecklistCommand(profile.Reference, Guid.NewGuid(), []));

        Assert.False(result.IsSuccess);
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
        public Task<IReadOnlyList<RhshfCreditProfile>> GetQueueAsync(RhshfInternalStage stage, Guid? branchId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RhshfCreditProfile>>(
                _profile is not null && _profile.InternalStage == stage ? [_profile] : []);
        public Task<RhshfSupportingDocument?> GetSupportingDocumentByIdAsync(Guid documentId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private class FakeEligibilityRepository : IRhshfEligibilityCheckRepository
    {
        public List<RhshfEligibilityCheck> Added { get; } = [];
        public Task<IReadOnlyList<RhshfEligibilityCheck>> GetByProfileAndCycleAsync(Guid rhshfCreditProfileId, int cycleNumber, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RhshfEligibilityCheck>>(
                Added.Where(c => c.RhshfCreditProfileId == rhshfCreditProfileId && c.CycleNumber == cycleNumber).ToList());
        public Task AddAsync(RhshfEligibilityCheck check, CancellationToken ct = default)
        {
            Added.Add(check);
            return Task.CompletedTask;
        }
    }

    private class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
    }
}

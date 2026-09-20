using CRMS.Application.Rhshf.Commands;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Tests.Rhshf;

public class RecordRhshfCollateralHandlerTests
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
            profile.AdvanceStage(stage);
        }

        return profile;
    }

    [Fact]
    public async Task Record_BankGuarantee_Succeeds()
    {
        var profile = MakeProfileUnderReview();
        var collateralRepo = new FakeCollateralRepository();
        var handler = new RecordRhshfCollateralHandler(new FakeProfileRepository(profile), collateralRepo, new FakeUnitOfWork());

        var result = await handler.Handle(new RecordRhshfCollateralCommand(
            profile.Reference, Guid.NewGuid(), RhshfCollateralType.BankGuarantee, null,
            "BG-2026-001", DateTime.UtcNow, DateTime.UtcNow.AddYears(1),
            "First Merchant Bank", 22_400_000m, true,
            null, null, null, null, null, null));

        Assert.True(result.IsSuccess);
        Assert.Single(collateralRepo.Added);
    }

    [Fact]
    public async Task Record_TwoInstruments_BothPersist()
    {
        var profile = MakeProfileUnderReview();
        var collateralRepo = new FakeCollateralRepository();
        var handler = new RecordRhshfCollateralHandler(new FakeProfileRepository(profile), collateralRepo, new FakeUnitOfWork());

        await handler.Handle(new RecordRhshfCollateralCommand(
            profile.Reference, Guid.NewGuid(), RhshfCollateralType.BankGuarantee, null,
            "BG-2026-001", null, null, "First Merchant Bank", 22_400_000m, true, null, null, null, null, null, null));
        await handler.Handle(new RecordRhshfCollateralCommand(
            profile.Reference, Guid.NewGuid(), RhshfCollateralType.LegalMortgage, null,
            null, null, null, null, null, null, null,
            "3-hectare warehouse plot", 30_000_000m, "KN/2026/00123", "Kano State Land Registry", RhshfCollateralPerfectionStatus.Pending));

        Assert.Equal(2, collateralRepo.Added.Count);
    }

    [Fact]
    public async Task Record_InvalidBankGuarantee_Fails_NotPersisted()
    {
        var profile = MakeProfileUnderReview();
        var collateralRepo = new FakeCollateralRepository();
        var handler = new RecordRhshfCollateralHandler(new FakeProfileRepository(profile), collateralRepo, new FakeUnitOfWork());

        var result = await handler.Handle(new RecordRhshfCollateralCommand(
            profile.Reference, Guid.NewGuid(), RhshfCollateralType.BankGuarantee, null,
            null, null, null, null, null, null, null, null, null, null, null, null));

        Assert.False(result.IsSuccess);
        Assert.Empty(collateralRepo.Added);
    }

    [Fact]
    public async Task Record_CaseNotFound_Fails()
    {
        var handler = new RecordRhshfCollateralHandler(new FakeProfileRepository(null), new FakeCollateralRepository(), new FakeUnitOfWork());

        var result = await handler.Handle(new RecordRhshfCollateralCommand(
            "RHSHF-2026-000000", Guid.NewGuid(), RhshfCollateralType.BankGuarantee, null,
            "BG-1", null, null, "Bank", 1_000_000m, true, null, null, null, null, null, null));

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

    private class FakeCollateralRepository : IRhshfCollateralRepository
    {
        public List<RhshfCollateral> Added { get; } = [];
        public Task<IReadOnlyList<RhshfCollateral>> GetByProfileAndCycleAsync(Guid rhshfCreditProfileId, int cycleNumber, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RhshfCollateral>>(
                Added.Where(c => c.RhshfCreditProfileId == rhshfCreditProfileId && c.CycleNumber == cycleNumber).ToList());
        public Task<RhshfCollateral?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Added.FirstOrDefault(c => c.Id == id));
        public Task AddAsync(RhshfCollateral collateral, CancellationToken ct = default)
        {
            Added.Add(collateral);
            return Task.CompletedTask;
        }
        public Task<RhshfCollateralDocument?> GetDocumentByIdAsync(Guid documentId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
    }
}

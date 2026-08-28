using CRMS.Application.Rhshf.Commands;
using CRMS.Application.Rhshf.Interfaces;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Tests.Rhshf;

public class RatifyRhshfCaseHandlerTests
{
    private const decimal TotalEopValue = 51_500_000.00m;

    private static RhshfCreditProfile MakeProfileAtRatification()
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

        profile.Appraise(Guid.NewGuid(), RhshfAppraisalOutcome.Proceed, null);
        profile.ReviewRisk(Guid.NewGuid(), RhshfRiskReviewOutcome.Cleared, null);
        profile.AdvanceToRatification();

        return profile;
    }

    [Fact]
    public async Task Ratify_Ratified_GeneratesOfferAndUploadsDocument()
    {
        var profile = MakeProfileAtRatification();
        var offerRepo = new FakeOfferRepository();
        var fileStorage = new FakeFileStorage();
        var handler = new RatifyRhshfCaseHandler(
            new FakeProfileRepository(profile), new FakeCommitteeRepository(), offerRepo,
            new FakePdfGenerator(), fileStorage, new FakeUnitOfWork());

        var result = await handler.Handle(new RatifyRhshfCaseCommand(
            profile.Reference, Guid.NewGuid(), RhshfRatificationOutcome.Ratified, TotalEopValue, null, null));

        Assert.True(result.IsSuccess);
        Assert.NotNull(offerRepo.Added);
        Assert.Equal(RhshfOfferStatus.Generated, offerRepo.Added!.Status);
        Assert.Single(fileStorage.UploadedPaths);
        Assert.Equal(RhshfInternalStage.AwaitingOfferAcceptance, profile.InternalStage);
    }

    [Fact]
    public async Task Ratify_ExcludesCommitteeApprovers()
    {
        var profile = MakeProfileAtRatification();
        var committeeApproverId = Guid.NewGuid();
        var review = RhshfCommitteeReview.Create(profile.Id, profile.CurrentCycleNumber, 1, 1).Value;
        review.CastVote(committeeApproverId, RhshfCommitteeVoteChoice.Approve, null, []);
        var handler = new RatifyRhshfCaseHandler(
            new FakeProfileRepository(profile), new FakeCommitteeRepository(review), new FakeOfferRepository(),
            new FakePdfGenerator(), new FakeFileStorage(), new FakeUnitOfWork());

        var result = await handler.Handle(new RatifyRhshfCaseCommand(
            profile.Reference, committeeApproverId, RhshfRatificationOutcome.Ratified, TotalEopValue, null, null));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Ratify_MismatchedAmount_DoesNotGenerateOffer()
    {
        var profile = MakeProfileAtRatification();
        var offerRepo = new FakeOfferRepository();
        var handler = new RatifyRhshfCaseHandler(
            new FakeProfileRepository(profile), new FakeCommitteeRepository(), offerRepo,
            new FakePdfGenerator(), new FakeFileStorage(), new FakeUnitOfWork());

        var result = await handler.Handle(new RatifyRhshfCaseCommand(
            profile.Reference, Guid.NewGuid(), RhshfRatificationOutcome.Ratified, TotalEopValue - 1, null, null));

        Assert.False(result.IsSuccess);
        Assert.Null(offerRepo.Added);
    }

    [Fact]
    public async Task Ratify_ReturnToFac_DoesNotGenerateOffer()
    {
        var profile = MakeProfileAtRatification();
        var offerRepo = new FakeOfferRepository();
        var handler = new RatifyRhshfCaseHandler(
            new FakeProfileRepository(profile), new FakeCommitteeRepository(), offerRepo,
            new FakePdfGenerator(), new FakeFileStorage(), new FakeUnitOfWork());

        var result = await handler.Handle(new RatifyRhshfCaseCommand(
            profile.Reference, Guid.NewGuid(), RhshfRatificationOutcome.ReturnToFac, null, "need more info", RhshfProfilingStage.SupportingDocuments));

        Assert.True(result.IsSuccess);
        Assert.Null(offerRepo.Added);
        Assert.Equal(RhshfCaseStatus.ProfilingInProgress, profile.Status);
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
    }

    private class FakeCommitteeRepository : IRhshfCommitteeReviewRepository
    {
        private readonly RhshfCommitteeReview? _review;
        public FakeCommitteeRepository(RhshfCommitteeReview? review = null) => _review = review;
        public Task<RhshfCommitteeReview?> GetByProfileAndCycleAsync(Guid rhshfCreditProfileId, int cycleNumber, CancellationToken ct = default)
            => Task.FromResult(_review is not null && _review.RhshfCreditProfileId == rhshfCreditProfileId && _review.CycleNumber == cycleNumber ? _review : null);
        public Task AddAsync(RhshfCommitteeReview review, CancellationToken ct = default) => Task.CompletedTask;
    }

    private class FakeOfferRepository : IRhshfOfferRepository
    {
        public RhshfOffer? Added { get; private set; }
        public Task<RhshfOffer?> GetByProfileAndCycleAsync(Guid rhshfCreditProfileId, int cycleNumber, CancellationToken ct = default)
            => Task.FromResult(Added);
        public Task AddAsync(RhshfOffer offer, CancellationToken ct = default)
        {
            Added = offer;
            return Task.CompletedTask;
        }
    }

    private class FakePdfGenerator : IRhshfOfferLetterPdfGenerator
    {
        public Task<byte[]> GenerateAsync(RhshfOfferLetterData data, CancellationToken ct = default)
            => Task.FromResult(new byte[] { 1, 2, 3 });
    }

    private class FakeFileStorage : IFileStorageService
    {
        public List<string> UploadedPaths { get; } = [];

        public Task<string> UploadAsync(string containerName, string fileName, byte[] content, string contentType, CancellationToken ct = default)
        {
            var path = $"{containerName}/{fileName}";
            UploadedPaths.Add(path);
            return Task.FromResult(path);
        }

        public Task<string> UploadAsync(string containerName, string fileName, Stream content, string contentType, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<byte[]> DownloadAsync(string storagePath, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Stream> GetStreamAsync(string storagePath, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> DeleteAsync(string storagePath, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(string storagePath, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string?> GetPresignedUrlAsync(string storagePath, TimeSpan expiry, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IEnumerable<string>> ListFilesAsync(string containerName, string? prefix = null, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
    }
}

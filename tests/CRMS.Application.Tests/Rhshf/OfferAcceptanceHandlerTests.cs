using CRMS.Application.Rhshf.Commands;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Tests.Rhshf;

public class OfferAcceptanceHandlerTests
{
    private const decimal TotalEopValue = 51_500_000.00m;

    private static RhshfCreditProfile MakeProfileAwaitingOfferAcceptance()
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
        profile.Ratify(Guid.NewGuid(), RhshfRatificationOutcome.Ratified, TotalEopValue, null, null, []);

        return profile;
    }

    [Fact]
    public async Task Upload_ThenAccept_Succeeds_AdvancesToLegalClearance()
    {
        var profile = MakeProfileAwaitingOfferAcceptance();
        var offer = RhshfOffer.Create(profile.Id, profile.CurrentCycleNumber, "path/offer.pdf").Value;
        var offerRepo = new FakeOfferRepository(offer);
        var fileStorage = new FakeFileStorage();

        var uploadHandler = new UploadSignedOfferHandler(new FakeProfileRepository(profile), offerRepo, fileStorage, new FakeUnitOfWork());
        var uploadResult = await uploadHandler.Handle(new UploadSignedOfferCommand(profile.Reference, "signed.pdf", "application/pdf", [1, 2, 3]));
        Assert.True(uploadResult.IsSuccess);

        var acceptHandler = new AcceptRhshfOfferHandler(new FakeProfileRepository(profile), offerRepo, new FakeUnitOfWork());
        var acceptResult = await acceptHandler.Handle(new AcceptRhshfOfferCommand(profile.Reference, "all good"));

        Assert.True(acceptResult.IsSuccess);
        Assert.Equal(RhshfOfferStatus.Accepted, offer.Status);
        Assert.Equal(RhshfInternalStage.LegalClearance, profile.InternalStage);
    }

    [Fact]
    public async Task Accept_WithoutUpload_Fails_ProfileUnchanged()
    {
        var profile = MakeProfileAwaitingOfferAcceptance();
        var offer = RhshfOffer.Create(profile.Id, profile.CurrentCycleNumber, "path/offer.pdf").Value;
        var handler = new AcceptRhshfOfferHandler(new FakeProfileRepository(profile), new FakeOfferRepository(offer), new FakeUnitOfWork());

        var result = await handler.Handle(new AcceptRhshfOfferCommand(profile.Reference, null));

        Assert.False(result.IsSuccess);
        Assert.Equal(RhshfInternalStage.AwaitingOfferAcceptance, profile.InternalStage);
    }

    [Fact]
    public async Task Reject_Succeeds_CancelsProfile_NotDeclined()
    {
        var profile = MakeProfileAwaitingOfferAcceptance();
        var offer = RhshfOffer.Create(profile.Id, profile.CurrentCycleNumber, "path/offer.pdf").Value;
        var handler = new RejectRhshfOfferHandler(new FakeProfileRepository(profile), new FakeOfferRepository(offer), new FakeUnitOfWork());

        var result = await handler.Handle(new RejectRhshfOfferCommand(profile.Reference, "not interested"));

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfOfferStatus.Rejected, offer.Status);
        Assert.Equal(RhshfCaseStatus.Cancelled, profile.Status);
        Assert.Null(profile.DecisionOutcome);
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

    private class FakeOfferRepository : IRhshfOfferRepository
    {
        private readonly RhshfOffer? _offer;
        public FakeOfferRepository(RhshfOffer? offer) => _offer = offer;
        public Task<RhshfOffer?> GetByProfileAndCycleAsync(Guid rhshfCreditProfileId, int cycleNumber, CancellationToken ct = default)
            => Task.FromResult(_offer is not null && _offer.RhshfCreditProfileId == rhshfCreditProfileId && _offer.CycleNumber == cycleNumber ? _offer : null);
        public Task AddAsync(RhshfOffer offer, CancellationToken ct = default) => Task.CompletedTask;
        public Task<RhshfOfferDocument?> GetDocumentByIdAsync(Guid documentId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private class FakeFileStorage : IFileStorageService
    {
        public Task<string> UploadAsync(string containerName, string fileName, byte[] content, string contentType, CancellationToken ct = default)
            => Task.FromResult($"{containerName}/{fileName}");
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

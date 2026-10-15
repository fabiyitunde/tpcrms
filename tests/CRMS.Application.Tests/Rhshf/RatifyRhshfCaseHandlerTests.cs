using CRMS.Domain.Common;
using CRMS.Domain.Aggregates.ProductCatalog;
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
            profile.AdvanceStageForTest(stage);
        }

        profile.AppraiseWithFinancials(Guid.NewGuid(), RhshfAppraisalOutcome.Proceed, null);
        profile.ClearLegalForTest();
        profile.ReviewRisk(Guid.NewGuid(), RhshfRiskReviewOutcome.Cleared, null);
        profile.AdvanceToRatification();

        return profile;
    }

    [Fact]
    public async Task Ratify_Ratified_GeneratesOneMergedDocument()
    {
        // The whole point of the merge: one document, so the FAC signs and returns one file. No KFS
        // path is attached, which is what makes HasRequiredSignedDocuments ask for one signature
        // instead of two.
        var profile = MakeProfileAtRatification();
        var offerRepo = new FakeOfferRepository();
        var fileStorage = new FakeFileStorage();
        var handler = new RatifyRhshfCaseHandler(
            new FakeProfileRepository(profile), new FakeCommitteeRepository(), new SharedFakeLegalClearanceRepository(), offerRepo,
            new FakeCollateralRepo(), new FakeOfferDocumentGenerator(), new FakeKfsGenerator(),
            new FakeLoanProductRepo(), new FakeFineract(), fileStorage, new FakeUnitOfWork());

        var result = await handler.Handle(new RatifyRhshfCaseCommand(
            profile.Reference, Guid.NewGuid(), RhshfRatificationOutcome.Ratified, TotalEopValue, null, null));

        Assert.True(result.IsSuccess);
        Assert.NotNull(offerRepo.Added);
        Assert.Equal(RhshfOfferStatus.Generated, offerRepo.Added!.Status);
        Assert.Single(fileStorage.UploadedPaths);
        Assert.Null(offerRepo.Added.KfsDocumentPath);
        Assert.True(offerRepo.Added.HasRequiredSignedDocuments == false); // nothing signed yet
        Assert.Equal(RhshfInternalStage.AwaitingOfferAcceptance, profile.InternalStage);
    }

    [Fact]
    public async Task Ratify_PassesTheApprovedTermsIntoTheDocument()
    {
        var profile = MakeProfileAtRatification();
        var generator = new FakeOfferDocumentGenerator();
        var handler = new RatifyRhshfCaseHandler(
            new FakeProfileRepository(profile), new FakeCommitteeRepository(), new SharedFakeLegalClearanceRepository(),
            new FakeOfferRepository(), new FakeCollateralRepo(), generator, new FakeKfsGenerator(),
            new FakeLoanProductRepo(), new FakeFineract(), new FakeFileStorage(), new FakeUnitOfWork());

        await handler.Handle(new RatifyRhshfCaseCommand(
            profile.Reference, Guid.NewGuid(), RhshfRatificationOutcome.Ratified, TotalEopValue, null, null));

        var data = generator.Received;
        Assert.NotNull(data);
        Assert.Equal(TotalEopValue, data!.ApprovedAmount);
        Assert.Equal(profile.Reference, data.Reference);
        Assert.Equal(7, data.ValidityDays);              // the approved template's seven days
        Assert.Null(data.GraceMonths);                   // unsettled, so left unstated
        Assert.Empty(data.ProductFees);                  // no product configured in this fake
        Assert.NotNull(data.FeesAccount);
        // No itemised financing on this case, so the plan falls back to a balancing row that still
        // foots to the approved amount.
        Assert.False(data.PlanIsItemised);
        Assert.Equal(TotalEopValue, data.PlanRows.Sum(r => r.Amount));
    }

    [Fact]
    public async Task Ratify_ExcludesCommitteeApprovers()
    {
        var profile = MakeProfileAtRatification();
        var committeeApproverId = Guid.NewGuid();
        var review = RhshfCommitteeReview.Create(profile.Id, profile.CurrentCycleNumber, 1, 1, CommitteeType.BranchCredit, null).Value;
        review.CastVote(committeeApproverId, RhshfCommitteeVoteChoice.Approve, null, []);
        var handler = new RatifyRhshfCaseHandler(
            new FakeProfileRepository(profile), new FakeCommitteeRepository(review), new SharedFakeLegalClearanceRepository(), new FakeOfferRepository(),
            new FakeCollateralRepo(), new FakeOfferDocumentGenerator(), new FakeKfsGenerator(),
            new FakeLoanProductRepo(), new FakeFineract(), new FakeFileStorage(), new FakeUnitOfWork());

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
            new FakeProfileRepository(profile), new FakeCommitteeRepository(), new SharedFakeLegalClearanceRepository(), offerRepo,
            new FakeCollateralRepo(), new FakeOfferDocumentGenerator(), new FakeKfsGenerator(),
            new FakeLoanProductRepo(), new FakeFineract(), new FakeFileStorage(), new FakeUnitOfWork());

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
            new FakeProfileRepository(profile), new FakeCommitteeRepository(), new SharedFakeLegalClearanceRepository(), offerRepo,
            new FakeCollateralRepo(), new FakeOfferDocumentGenerator(), new FakeKfsGenerator(),
            new FakeLoanProductRepo(), new FakeFineract(), new FakeFileStorage(), new FakeUnitOfWork());

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
        public Task<IReadOnlyList<RhshfCreditProfile>> GetAllForListAsync(Guid? branchId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<RhshfCreditProfile>>([]);
        public Task<IReadOnlyList<RhshfCreditProfile>> GetQueueAsync(RhshfInternalStage stage, Guid? branchId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RhshfCreditProfile>>(
                _profile is not null && _profile.InternalStage == stage ? [_profile] : []);
        public Task<IReadOnlyList<RhshfReusableDocument>> GetReusableDocumentsForFacAsync(Guid facId, Guid excludeProfileId, RhshfDocumentCategory category, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<RhshfSupportingDocument?> GetSupportingDocumentByIdAsync(Guid documentId, CancellationToken ct = default) => throw new NotSupportedException();
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
        public Task<RhshfOfferDocument?> GetDocumentByIdAsync(Guid documentId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private class FakeOfferDocumentGenerator : IRhshfOfferDocumentGenerator
    {
        public RhshfOfferDocumentData? Received { get; private set; }

        public Task<byte[]> GenerateAsync(RhshfOfferDocumentData data, CancellationToken ct = default)
        {
            Received = data;
            return Task.FromResult(new byte[] { 1, 2, 3 });
        }
    }

    private class FakeCollateralRepo : IRhshfCollateralRepository
    {
        public Task<IReadOnlyList<RhshfCollateral>> GetByProfileAndCycleAsync(Guid rhshfCreditProfileId, int cycleNumber, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RhshfCollateral>>([]);
        public Task<RhshfCollateral?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<RhshfCollateral?>(null);
        public Task AddAsync(RhshfCollateral collateral, CancellationToken ct = default) => Task.CompletedTask;
        public void Remove(RhshfCollateral collateral) { }
        public Task<RhshfCollateralDocument?> GetDocumentByIdAsync(Guid documentId, CancellationToken ct = default)
            => Task.FromResult<RhshfCollateralDocument?>(null);
    }

    /// <summary>No RH-SHF product configured — the letter must still generate, stating the fees are
    /// per the approved credit terms rather than failing on a catalogue lookup.</summary>
    private class FakeLoanProductRepo : ILoanProductRepository
    {
        public Task<LoanProduct?> GetActiveRhshfProductAsync(CancellationToken ct = default)
            => Task.FromResult<LoanProduct?>(null);
        public Task<LoanProduct?> GetActiveNampProductAsync(CancellationToken ct = default)
            => Task.FromResult<LoanProduct?>(null);
        public Task<LoanProduct?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<LoanProduct?>(null);
        public Task<LoanProduct?> GetByCodeAsync(string code, CancellationToken ct = default)
            => Task.FromResult<LoanProduct?>(null);
        public Task<IReadOnlyList<LoanProduct>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<LoanProduct>>([]);
        public Task<IReadOnlyList<LoanProduct>> GetByStatusAsync(ProductStatus status, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<LoanProduct>>([]);
        public Task<IReadOnlyList<LoanProduct>> GetByTypeAsync(LoanProductType type, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<LoanProduct>>([]);
        public Task<IReadOnlyList<LoanProduct>> GetActiveByTypeAsync(LoanProductType type, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<LoanProduct>>([]);
        public Task<bool> ExistsAsync(string code, CancellationToken ct = default) => Task.FromResult(false);
        public Task AddAsync(LoanProduct product, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(LoanProduct product, CancellationToken ct = default) => Task.CompletedTask;
        public Task AddChecklistTemplateItemAsync(DisbursementChecklistTemplate item, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    /// <summary>Only the product catalogue is reached, and only to read a product's fees. It answers
    /// with an empty catalogue so the document falls back to "per the approved credit terms".</summary>
    private class FakeFineract : IFineractDirectService
    {
        public Task<Result<IReadOnlyList<FineractLoanProduct>>> GetLoanProductsAsync(bool activeOnly = true, CancellationToken ct = default)
            => Task.FromResult(Result.Success<IReadOnlyList<FineractLoanProduct>>([]));
        public Task<Result<FineractBookingResult>> BookApprovedLoanAsync(FineractLoanBookingRequest request, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<Result<NampBoaAccountInfo>> GetNampBoaAccountAsync(string boaAccountNumber, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<Result<FineractClientInfo>> GetClientByIdAsync(long clientId, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<Result<ProposedRepaymentSchedule>> CalculateRepaymentScheduleAsync(ScheduleCalculationRequest request, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<Result<ClientAccountSummary>> GetClientAccountsAsync(long clientId, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<Result<FineractLoanDetail>> GetLoanDetailAsync(long loanId, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<Result<CustomerExposure>> GetCustomerExposureAsync(long clientId, string accountNumber, string customerName, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private class FakeKfsGenerator : IRhshfKfsPdfGenerator
    {
        public Task<byte[]> GenerateAsync(RhshfKfsData data, CancellationToken ct = default)
            => Task.FromResult(new byte[] { 4, 5, 6 });
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

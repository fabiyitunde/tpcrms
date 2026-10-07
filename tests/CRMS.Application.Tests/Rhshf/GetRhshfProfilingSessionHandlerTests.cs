using CRMS.Application.Rhshf.Queries;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Tests.Rhshf;

/// <summary>
/// Covers the offer-routing signal (portal integration note §4.1). Once profiling is submitted the
/// wizard shows a completed panel — but if an offer is waiting, "you can close this tab" is the
/// wrong thing to say and the case stalls there. IsAwaitingOfferAcceptance is what lets that panel
/// route the FAC onward.
/// </summary>
public class GetRhshfProfilingSessionHandlerTests
{
    private const decimal TotalEopValue = 51_500_000.00m;

    private static RhshfCreditProfile MakeProfile() => RhshfCreditProfile.Create(
        submissionId: Guid.NewGuid(), programmeCode: "RH-SHF-DRY-2026", programmeName: "Renewed Hope",
        sessionCode: "2026-DRY", sessionName: "Dry Season 2026", facId: Guid.NewGuid(),
        companyName: "Alliedsoft Limited", rcNumber: "RC123456", tin: "01234567-0001",
        boaAccountNumber: "0123456789", contactEmail: "fac@company.com", contactPhone: "+2348012345678",
        state: "Kano", lga: "Nassarawa", totalEopValue: TotalEopValue, currency: "NGN", farmerCount: 1200,
        callbackUrl: "https://portal.example.gov.ng/api/integrations/crms/webhook",
        certifiedByAdmin: "admin@boa.gov.ng", certifiedAt: DateTime.UtcNow, rawSubmissionPayload: "{}",
        eopLines: null, resolvedBranchId: null, resolvedOfficeId: null).Value;

    private static RhshfCreditProfile Ratified()
    {
        var profile = MakeProfile();
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

    private static GetRhshfProfilingSessionHandler Handler(RhshfCreditProfile profile, RhshfOffer? offer = null)
        => new(new FakeProfileRepo(profile), new FakeRequirementRepo(), new FakeOfferRepo(offer), new FakeGuarantorRepo());

    private class FakeGuarantorRepo : CRMS.Domain.Interfaces.IRhshfGuarantorRepository
    {
        public Task<IReadOnlyList<CRMS.Domain.Aggregates.Rhshf.RhshfGuarantor>> GetByProfileIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CRMS.Domain.Aggregates.Rhshf.RhshfGuarantor>>([]);
        public Task<CRMS.Domain.Aggregates.Rhshf.RhshfGuarantor?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<CRMS.Domain.Aggregates.Rhshf.RhshfGuarantor?>(null);
        public Task AddAsync(CRMS.Domain.Aggregates.Rhshf.RhshfGuarantor g, CancellationToken ct = default) => Task.CompletedTask;
        public void Remove(CRMS.Domain.Aggregates.Rhshf.RhshfGuarantor g) { }
    }

    [Fact]
    public async Task IsAwaitingOfferAcceptance_IsFalse_WhileTheFacIsStillProfiling()
    {
        var profile = MakeProfile();

        var result = await Handler(profile).Handle(new GetRhshfProfilingSessionQuery(profile.Reference));

        Assert.True(result.IsSuccess);
        Assert.False(result.Data!.IsAwaitingOfferAcceptance);
    }

    [Fact]
    public async Task IsAwaitingOfferAcceptance_IsTrue_WhenAGeneratedOfferIsWaiting()
    {
        var profile = Ratified();
        var offer = RhshfOffer.Create(profile.Id, profile.CurrentCycleNumber, "path/offer.pdf").Value;

        var result = await Handler(profile, offer).Handle(new GetRhshfProfilingSessionQuery(profile.Reference));

        Assert.True(result.Data!.IsAwaitingOfferAcceptance);
    }

    [Fact]
    public async Task IsAwaitingOfferAcceptance_IsFalse_OnceTheOfferHasBeenAccepted()
    {
        // Otherwise the completed panel keeps inviting the FAC back into an offer they already
        // signed — the mirror of the original stall.
        var profile = Ratified();
        var offer = RhshfOffer.Create(profile.Id, profile.CurrentCycleNumber, "path/offer.pdf").Value;
        offer.AddDocument("signed.pdf", "application/pdf", "path/signed.pdf", 1024);
        offer.Accept(null);
        profile.AdvanceToLegalClearance();

        var result = await Handler(profile, offer).Handle(new GetRhshfProfilingSessionQuery(profile.Reference));

        Assert.False(result.Data!.IsAwaitingOfferAcceptance);
    }

    [Fact]
    public async Task IsAwaitingOfferAcceptance_IsFalse_WhenTheStageSaysSoButNoOfferExistsYet()
    {
        // Ratification sets the stage before the Application layer has generated the PDF. Linking
        // to an offer page with nothing behind it would be worse than saying nothing.
        var profile = Ratified();

        var result = await Handler(profile, offer: null).Handle(new GetRhshfProfilingSessionQuery(profile.Reference));

        Assert.False(result.Data!.IsAwaitingOfferAcceptance);
    }

    [Fact]
    public async Task TinIsSurfaced_EvenWhenAbsent()
    {
        var profile = RhshfCreditProfile.Create(
            submissionId: Guid.NewGuid(), programmeCode: "P", programmeName: "P", sessionCode: "S", sessionName: "S",
            facId: Guid.NewGuid(), companyName: "C", rcNumber: "RC1", tin: null, boaAccountNumber: "0123456789",
            contactEmail: "a@b.com", contactPhone: "+2348012345678", state: "Kano", lga: "N",
            totalEopValue: TotalEopValue, currency: "NGN", farmerCount: 10,
            callbackUrl: "https://x.example/cb", certifiedByAdmin: "a@b.com", certifiedAt: DateTime.UtcNow,
            rawSubmissionPayload: "{}", eopLines: null, resolvedBranchId: null, resolvedOfficeId: null).Value;

        var result = await Handler(profile).Handle(new GetRhshfProfilingSessionQuery(profile.Reference));

        Assert.True(result.IsSuccess);
        Assert.Null(result.Data!.Tin);
    }

    private class FakeProfileRepo : IRhshfCreditProfileRepository
    {
        private readonly RhshfCreditProfile _profile;
        public FakeProfileRepo(RhshfCreditProfile profile) => _profile = profile;

        public Task<RhshfCreditProfile?> GetByReferenceAsync(string reference, CancellationToken ct = default)
            => Task.FromResult<RhshfCreditProfile?>(_profile.Reference == reference ? _profile : null);
        public Task<RhshfCreditProfile?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<RhshfCreditProfile?>(null);
        public Task<RhshfCreditProfile?> GetBySubmissionIdAsync(Guid submissionId, CancellationToken ct = default)
            => Task.FromResult<RhshfCreditProfile?>(null);
        public Task AddAsync(RhshfCreditProfile profile, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<RhshfCreditProfile>> GetQueueAsync(RhshfInternalStage stage, Guid? branchId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RhshfCreditProfile>>([]);
        public Task<RhshfSupportingDocument?> GetSupportingDocumentByIdAsync(Guid documentId, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private class FakeRequirementRepo : IRhshfDocumentRequirementRepository
    {
        public Task<IReadOnlyList<RhshfDocumentRequirement>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RhshfDocumentRequirement>>([]);
        public Task<IReadOnlyList<RhshfDocumentRequirement>> GetActiveAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RhshfDocumentRequirement>>([]);
        public Task<RhshfDocumentRequirement?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<RhshfDocumentRequirement?>(null);
        public Task AddAsync(RhshfDocumentRequirement requirement, CancellationToken ct = default) => Task.CompletedTask;
        public void Update(RhshfDocumentRequirement requirement) { }
    }

    private class FakeOfferRepo : IRhshfOfferRepository
    {
        private readonly RhshfOffer? _offer;
        public FakeOfferRepo(RhshfOffer? offer) => _offer = offer;

        public Task<RhshfOffer?> GetByProfileAndCycleAsync(Guid profileId, int cycleNumber, CancellationToken ct = default)
            => Task.FromResult(_offer);
        public Task AddAsync(RhshfOffer offer, CancellationToken ct = default) => Task.CompletedTask;
        public Task<RhshfOfferDocument?> GetDocumentByIdAsync(Guid documentId, CancellationToken ct = default)
            => throw new NotSupportedException();
    }
}

using CRMS.Application.Rhshf.Queries;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Tests.Rhshf;

public class GetRhshfCaseStatusHandlerTests
{
    private const decimal TotalEopValue = 51_500_000.00m;

    private static RhshfCreditProfile MakeProfile()
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
        return result.Value;
    }

    [Fact]
    public async Task Status_RightAfterCreate_IsProfilingPending_WithFirstStage()
    {
        var profile = MakeProfile();
        var handler = new GetRhshfCaseStatusHandler(new FakeProfileRepository(profile), new FakeOfferRepository(), new FakeRhshfPublicUrlProvider());

        var result = await handler.Handle(new GetRhshfCaseStatusQuery(profile.Reference));

        Assert.True(result.IsSuccess);
        Assert.Equal("PROFILING_PENDING", result.Data!.Status);
        Assert.NotNull(result.Data.Stage);
        Assert.Equal("companyVerification", result.Data.Stage!.Current);
        Assert.Equal(1, result.Data.Stage.Index);
        Assert.Equal(5, result.Data.Stage.Total);
        Assert.Null(result.Data.Decision);
        Assert.Null(result.Data.ActionRequired);
    }

    [Fact]
    public async Task Status_DuringProfiling_ReflectsCurrentStage()
    {
        var profile = MakeProfile();
        profile.AdvanceStage(RhshfProfilingStage.CompanyVerification);
        profile.AdvanceStage(RhshfProfilingStage.CreditBureauCheck);
        profile.AdvanceStage(RhshfProfilingStage.EopReview);
        var handler = new GetRhshfCaseStatusHandler(new FakeProfileRepository(profile), new FakeOfferRepository(), new FakeRhshfPublicUrlProvider());

        var result = await handler.Handle(new GetRhshfCaseStatusQuery(profile.Reference));

        Assert.Equal("PROFILING_IN_PROGRESS", result.Data!.Status);
        Assert.Equal("supportingDocuments", result.Data.Stage!.Current);
        Assert.Equal(4, result.Data.Stage.Index);
    }

    [Fact]
    public async Task Status_UnderReview_ThroughLegalClearance_HasNullStageAndNullDecision()
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
        var offer = RhshfOffer.Create(profile.Id, profile.CurrentCycleNumber, "path/offer.pdf").Value;
        offer.AddDocument("signed.pdf", "application/pdf", "path/signed.pdf", 1024);
        offer.Accept(null);
        profile.AdvanceToLegalClearance();
        var handler = new GetRhshfCaseStatusHandler(new FakeProfileRepository(profile), new FakeOfferRepository(offer), new FakeRhshfPublicUrlProvider());

        var result = await handler.Handle(new GetRhshfCaseStatusQuery(profile.Reference));

        Assert.Equal("UNDER_REVIEW", result.Data!.Status);
        Assert.Null(result.Data.Stage);
        Assert.Null(result.Data.Decision);
        Assert.Null(result.Data.ActionRequired);
    }

    [Fact]
    public async Task Status_AwaitingOfferAcceptance_Unresolved_HasReviewOfferActionRequired()
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
        var offer = RhshfOffer.Create(profile.Id, profile.CurrentCycleNumber, "path/offer.pdf").Value;
        var handler = new GetRhshfCaseStatusHandler(new FakeProfileRepository(profile), new FakeOfferRepository(offer), new FakeRhshfPublicUrlProvider());

        var result = await handler.Handle(new GetRhshfCaseStatusQuery(profile.Reference));

        Assert.Equal("UNDER_REVIEW", result.Data!.Status);
        Assert.Equal("REVIEW_OFFER", result.Data.ActionRequired);
        Assert.Equal($"https://crms.test/rhshf/offer/{profile.Reference}", result.Data.ActionUrl);
        Assert.Null(result.Data.Decision);
    }

    [Fact]
    public async Task Status_AfterOfferAccepted_NoLongerHasActionRequired()
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
        var offer = RhshfOffer.Create(profile.Id, profile.CurrentCycleNumber, "path/offer.pdf").Value;
        offer.AddDocument("signed.pdf", "application/pdf", "path/signed.pdf", 1024);
        offer.Accept(null);
        profile.AdvanceToLegalClearance();
        var handler = new GetRhshfCaseStatusHandler(new FakeProfileRepository(profile), new FakeOfferRepository(offer), new FakeRhshfPublicUrlProvider());

        var result = await handler.Handle(new GetRhshfCaseStatusQuery(profile.Reference));

        Assert.Null(result.Data!.ActionRequired);
    }

    [Fact]
    public async Task Status_ApprovedAfterDisbursement_PopulatesDecision()
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
        var offer = RhshfOffer.Create(profile.Id, profile.CurrentCycleNumber, "path/offer.pdf").Value;
        offer.AddDocument("signed.pdf", "application/pdf", "path/signed.pdf", 1024);
        offer.Accept(null);
        profile.AdvanceToLegalClearance();
        profile.AdvanceToDisbursement(); // lands at PreDeploymentVerification
        var template = RhshfPreDeploymentChecklistTemplate.Create("Gate Item", null, isMandatory: true, sortOrder: 10).Value;
        profile.SeedPreDeploymentChecklist([template]);
        var item = profile.PreDeploymentChecklist.Single(i => i.CycleNumber == profile.CurrentCycleNumber);
        profile.ConfirmPreDeploymentChecklistItem(item.Id, Guid.NewGuid(), true, null);
        profile.CompletePreDeploymentVerification(Guid.NewGuid(), null);
        profile.RecordDisbursementAttempt(Guid.NewGuid(), TotalEopValue, "0987654321", "Agro Inputs Ltd", RhshfDisbursementStatus.Booked, 9001L, "LN-009001", null);
        var handler = new GetRhshfCaseStatusHandler(new FakeProfileRepository(profile), new FakeOfferRepository(offer), new FakeRhshfPublicUrlProvider());

        var result = await handler.Handle(new GetRhshfCaseStatusQuery(profile.Reference));

        Assert.Equal("APPROVED", result.Data!.Status);
        Assert.NotNull(result.Data.Decision);
        Assert.Equal("APPROVED", result.Data.Decision!.Outcome);
        Assert.Equal(TotalEopValue, result.Data.Decision.ApprovedAmount);
        Assert.Empty(result.Data.Decision.Reasons);
    }

    [Fact]
    public async Task Status_Cancelled_DecisionStaysNull()
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
        var offer = RhshfOffer.Create(profile.Id, profile.CurrentCycleNumber, "path/offer.pdf").Value;
        offer.Reject("not interested");
        profile.CancelDueToOfferRejection("not interested");
        var handler = new GetRhshfCaseStatusHandler(new FakeProfileRepository(profile), new FakeOfferRepository(offer), new FakeRhshfPublicUrlProvider());

        var result = await handler.Handle(new GetRhshfCaseStatusQuery(profile.Reference));

        Assert.Equal("CANCELLED", result.Data!.Status);
        Assert.Null(result.Data.Decision);
        Assert.Null(result.Data.ActionRequired);
    }

    [Fact]
    public async Task Status_CaseNotFound_Fails()
    {
        var handler = new GetRhshfCaseStatusHandler(new FakeProfileRepository(null), new FakeOfferRepository(), new FakeRhshfPublicUrlProvider());

        var result = await handler.Handle(new GetRhshfCaseStatusQuery("RHSHF-2026-999999"));

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

    private class FakeOfferRepository : IRhshfOfferRepository
    {
        private readonly RhshfOffer? _offer;
        public FakeOfferRepository(RhshfOffer? offer = null) => _offer = offer;
        public Task<RhshfOffer?> GetByProfileAndCycleAsync(Guid rhshfCreditProfileId, int cycleNumber, CancellationToken ct = default)
            => Task.FromResult(_offer is not null && _offer.RhshfCreditProfileId == rhshfCreditProfileId && _offer.CycleNumber == cycleNumber ? _offer : null);
        public Task AddAsync(RhshfOffer offer, CancellationToken ct = default) => Task.CompletedTask;
        public Task<RhshfOfferDocument?> GetDocumentByIdAsync(Guid documentId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    // ── actionUrl (portal integration note §4.1) ─────────────────────────────
    //
    // The portal holds only the profilingUrl from submit, which points at a completed wizard with
    // no route onward. Without a URL beside actionRequired, a FAC with an offer waiting has nowhere
    // to go and the case stalls — so these assert the pairing, not just the flag.

    [Fact]
    public async Task ActionUrl_IsNull_WhenNoActionIsPending()
    {
        var profile = MakeProfile();
        var handler = new GetRhshfCaseStatusHandler(
            new FakeProfileRepository(profile), new FakeOfferRepository(), new FakeRhshfPublicUrlProvider());

        var result = await handler.Handle(new GetRhshfCaseStatusQuery(profile.Reference));

        Assert.Null(result.Data!.ActionRequired);
        Assert.Null(result.Data.ActionUrl);
    }

    [Fact]
    public async Task ActionUrl_IsNull_WhenNoPublicHostIsConfigured()
    {
        // Better the portal sees a missing URL it can detect than a plausible one built on a
        // placeholder host — that is exactly what shipped "crms.example.com" to them.
        var profile = AwaitingOfferAcceptance(out var offer);
        var handler = new GetRhshfCaseStatusHandler(
            new FakeProfileRepository(profile), new FakeOfferRepository(offer),
            new FakeRhshfPublicUrlProvider(host: null));

        var result = await handler.Handle(new GetRhshfCaseStatusQuery(profile.Reference));

        Assert.Equal("REVIEW_OFFER", result.Data!.ActionRequired);
        Assert.Null(result.Data.ActionUrl);
    }

    [Fact]
    public async Task ActionUrl_PointsAtTheOfferPage_NotTheProfilingPage()
    {
        var profile = AwaitingOfferAcceptance(out var offer);
        var handler = new GetRhshfCaseStatusHandler(
            new FakeProfileRepository(profile), new FakeOfferRepository(offer), new FakeRhshfPublicUrlProvider());

        var result = await handler.Handle(new GetRhshfCaseStatusQuery(profile.Reference));

        Assert.Contains("/rhshf/offer/", result.Data!.ActionUrl);
        Assert.DoesNotContain("/rhshf/profiling/", result.Data.ActionUrl);
        Assert.EndsWith(profile.Reference, result.Data.ActionUrl);
        // No token: the portal mints its own via /token and appends it.
        Assert.DoesNotContain("token=", result.Data.ActionUrl);
    }

    private static RhshfCreditProfile AwaitingOfferAcceptance(out RhshfOffer offer)
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
        offer = RhshfOffer.Create(profile.Id, profile.CurrentCycleNumber, "path/offer.pdf").Value;
        return profile;
    }
}

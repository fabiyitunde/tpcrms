using System.Text.Json;
using CRMS.Application.Rhshf.Webhooks;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Infrastructure.ExternalServices.Rhshf;
using Xunit;

namespace CRMS.Infrastructure.Tests.Rhshf;

public class RhshfWebhookPayloadBuilderTests
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
    public void Build_Decided_Approved_MatchesBriefShape()
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
        profile.ClearLegalForTest();
        profile.ReviewRisk(Guid.NewGuid(), RhshfRiskReviewOutcome.Cleared, null);
        profile.AdvanceToRatification();
        profile.Ratify(Guid.NewGuid(), RhshfRatificationOutcome.Ratified, TotalEopValue, null, null, []);
        var offer = RhshfOffer.Create(profile.Id, profile.CurrentCycleNumber, "path/offer.pdf").Value;
        offer.AddDocument("signed.pdf", "application/pdf", "path/signed.pdf", 1024);
        offer.Accept(null);
        profile.AdvanceToPreDeploymentFromOffer(); // offer accepted -> PreDeploymentVerification
        var template = RhshfPreDeploymentChecklistTemplate.Create("Gate Item", null, isMandatory: true, sortOrder: 10).Value;
        profile.SeedPreDeploymentChecklist([template]);
        var item = profile.PreDeploymentChecklist.Single(i => i.CycleNumber == profile.CurrentCycleNumber);
        profile.ConfirmPreDeploymentChecklistItem(item.Id, Guid.NewGuid(), true, null);
        profile.CompletePreDeploymentVerification(Guid.NewGuid(), null);
        profile.RecordDisbursementAttempt(Guid.NewGuid(), TotalEopValue, "0987654321", "Agro Inputs Ltd", RhshfDisbursementStatus.Booked, 9001L, "LN-009001", null);

        var eventId = "evt_test123";
        var occurredAt = DateTime.UtcNow;
        var payload = RhshfWebhookPayloadBuilder.Build(profile, RhshfCallbackEventType.Decided, eventId, occurredAt);
        var json = JsonSerializer.Serialize(payload, RhshfCallbackService.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(profile.Reference, root.GetProperty("reference").GetString());
        Assert.Equal(profile.SubmissionId, root.GetProperty("submissionId").GetGuid());
        Assert.Equal("APPROVED", root.GetProperty("status").GetString());
        Assert.Equal(eventId, root.GetProperty("eventId").GetString());
        Assert.False(root.TryGetProperty("actionRequired", out _), "Decided payload must not carry actionRequired.");

        var decision = root.GetProperty("decision");
        Assert.Equal("APPROVED", decision.GetProperty("outcome").GetString());
        Assert.Equal(TotalEopValue, decision.GetProperty("approvedAmount").GetDecimal());
        Assert.Equal("NGN", decision.GetProperty("currency").GetString());
        Assert.Equal(0, decision.GetProperty("reasons").GetArrayLength());
        Assert.Equal("CRMS Disbursement", decision.GetProperty("decidedBy").GetString());
    }

    [Fact]
    public void Build_Decided_Cancelled_DecisionIsNull()
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
        profile.ClearLegalForTest();
        profile.ReviewRisk(Guid.NewGuid(), RhshfRiskReviewOutcome.Cleared, null);
        profile.AdvanceToRatification();
        profile.Ratify(Guid.NewGuid(), RhshfRatificationOutcome.Ratified, TotalEopValue, null, null, []);
        var offer = RhshfOffer.Create(profile.Id, profile.CurrentCycleNumber, "path/offer.pdf").Value;
        offer.Reject("not interested");
        profile.CancelDueToOfferRejection("not interested");

        var payload = RhshfWebhookPayloadBuilder.Build(profile, RhshfCallbackEventType.Decided, "evt_cancel", DateTime.UtcNow);
        var json = JsonSerializer.Serialize(payload, RhshfCallbackService.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("CANCELLED", root.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("decision").ValueKind);
        Assert.Equal("OFFER_REJECTED_BY_FAC", root.GetProperty("statusReasonCode").GetString());
    }

    [Fact]
    public void Build_OfferReady_SetsActionRequired_StatusUnderReview_DecisionNull()
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
        profile.ClearLegalForTest();
        profile.ReviewRisk(Guid.NewGuid(), RhshfRiskReviewOutcome.Cleared, null);
        profile.AdvanceToRatification();
        profile.Ratify(Guid.NewGuid(), RhshfRatificationOutcome.Ratified, TotalEopValue, null, null, []);
        var offer = RhshfOffer.Create(profile.Id, profile.CurrentCycleNumber, "path/offer.pdf").Value;

        var payload = RhshfWebhookPayloadBuilder.Build(profile, RhshfCallbackEventType.OfferReady, "evt_offer", DateTime.UtcNow, offer);
        var json = JsonSerializer.Serialize(payload, RhshfCallbackService.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("UNDER_REVIEW", root.GetProperty("status").GetString());
        Assert.Equal("REVIEW_OFFER", root.GetProperty("actionRequired").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("decision").ValueKind);
    }

    [Fact]
    public void Build_OfferReady_CarriesActionUrl_WhenTheCallerResolvesOne()
    {
        var profile = AtAwaitingOfferAcceptance(out var offer);
        var offerUrl = $"https://crms.test/rhshf/offer/{profile.Reference}";

        var payload = RhshfWebhookPayloadBuilder.Build(
            profile, RhshfCallbackEventType.OfferReady, "evt_offer", DateTime.UtcNow, offer, offerUrl);
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(payload, RhshfCallbackService.JsonOptions));

        Assert.Equal("REVIEW_OFFER", doc.RootElement.GetProperty("actionRequired").GetString());
        Assert.Equal(offerUrl, doc.RootElement.GetProperty("actionUrl").GetString());
    }

    [Fact]
    public void Build_OfferReady_OmitsActionUrl_WhenNoPublicHostIsConfigured()
    {
        // Omitted rather than serialised as null — same treatment actionRequired already gets on
        // the terminal payload, so the portal sees an absent field, not an empty one.
        var profile = AtAwaitingOfferAcceptance(out var offer);

        var payload = RhshfWebhookPayloadBuilder.Build(
            profile, RhshfCallbackEventType.OfferReady, "evt_offer", DateTime.UtcNow, offer, offerUrl: null);
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(payload, RhshfCallbackService.JsonOptions));

        Assert.False(doc.RootElement.TryGetProperty("actionUrl", out _));
    }

    [Fact]
    public void Build_Decided_NeverCarriesActionUrl()
    {
        // A terminal outcome has nothing for the FAC to action; an actionUrl there would invite the
        // portal to route them into an offer page for a case that is already closed.
        var profile = AtAwaitingOfferAcceptance(out var offer);
        offer.Reject("not interested");
        profile.CancelDueToOfferRejection("not interested");

        var payload = RhshfWebhookPayloadBuilder.Build(
            profile, RhshfCallbackEventType.Decided, "evt_decided", DateTime.UtcNow, offer,
            offerUrl: $"https://crms.test/rhshf/offer/{profile.Reference}");
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(payload, RhshfCallbackService.JsonOptions));

        Assert.False(doc.RootElement.TryGetProperty("actionUrl", out _));
        Assert.False(doc.RootElement.TryGetProperty("actionRequired", out _));
    }

    // ── Credit status for the portal (CRMSQA#1) ─────────────────────────────
    //
    // status stayed UNDER_REVIEW from appraisal to disbursement, and the portal showed exactly that
    // while an offer sat waiting. creditStatus says where the case actually is.

    [Fact]
    public void Build_OfferReady_SaysOfferReady_WithTheOfferDetails()
    {
        var profile = AtAwaitingOfferAcceptance(out var offer);

        var root = Serialize(RhshfWebhookPayloadBuilder.Build(
            profile, RhshfCallbackEventType.OfferReady, "evt_offer", DateTime.UtcNow, offer));

        Assert.Equal("UNDER_REVIEW", root.GetProperty("status").GetString()); // unchanged for existing consumers
        Assert.Equal("OFFER_READY", root.GetProperty("creditStatus").GetString());
        Assert.Equal("OFFER_READY", root.GetProperty("eventType").GetString());
        Assert.True(root.GetProperty("stateVersion").GetInt32() > 0);

        // CRMSQA#1: a deadline beside the action, and the contract version on every update.
        Assert.Equal(offer.ExpiresAt, root.GetProperty("actionDeadline").GetDateTime());
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());

        var offerJson = root.GetProperty("offer");
        Assert.Equal("AVAILABLE", offerJson.GetProperty("status").GetString());
        Assert.Equal(1, offerJson.GetProperty("version").GetInt32());
        Assert.Equal(TotalEopValue, offerJson.GetProperty("approvedAmount").GetDecimal());
        Assert.Equal("NGN", offerJson.GetProperty("currency").GetString());
        Assert.False(root.TryGetProperty("correction", out _));
    }

    /// <summary>
    /// CRMSQA#3 contract points 4 and 5: the offer and the decision tell the portal which EOP version
    /// they were made on, using the v2 hash when the portal sent one (it also covers the provider
    /// assignment and rate), so the portal can refuse to carry them over to an amended EOP.
    /// </summary>
    [Fact]
    public void Build_OfferReady_NamesTheEopVersionTheDecisionWasMadeOn()
    {
        var eopId = Guid.NewGuid();
        var profile = Submitted();
        Assert.True(profile.AttachLateFinancing(new RhshfFinancingInput(
            [new RhshfFinancingLineInput(eopId, "Maize", "NPK Fertilizer", "Fertilizer", null, "Agro Ltd",
                1m, "Kg", TotalEopValue / 1200m, "perFarmer", 1200, TotalEopValue, 0m, true)],
            [], TotalEopValue, 0m, 0m, TotalEopValue, TotalEopValue, 0m,
            [new RhshfFinancingEopInput(eopId, "Maize", "Kano", 1200, TotalEopValue, 0m, 0m, 0m, 0m,
                TotalEopValue, TotalEopValue, "EOP-1A2B3C4D", DateTime.UtcNow, 1,
                "v1hash", "rhshf-eop-snapshot-v1", "v2hash", "rhshf-eop-snapshot-v2")])).IsSuccess);

        var beforeOffer = Serialize(RhshfWebhookPayloadBuilder.Build(profile, RhshfCallbackEventType.OfferReady, "evt_0", DateTime.UtcNow));
        Assert.False(beforeOffer.TryGetProperty("decisionBasis", out _)); // nothing decided yet

        profile.AppraiseWithFinancials(Guid.NewGuid(), RhshfAppraisalOutcome.Proceed, null);
        profile.ClearLegalForTest();
        profile.ReviewRisk(Guid.NewGuid(), RhshfRiskReviewOutcome.Cleared, null);
        profile.AdvanceToRatification();
        profile.Ratify(Guid.NewGuid(), RhshfRatificationOutcome.Ratified, TotalEopValue, null, null, []);
        var offer = RhshfOffer.Create(profile.Id, profile.CurrentCycleNumber, "path/offer.pdf").Value;

        var root = Serialize(RhshfWebhookPayloadBuilder.Build(profile, RhshfCallbackEventType.OfferReady, "evt_1", DateTime.UtcNow, offer));

        var basis = Assert.Single(root.GetProperty("decisionBasis").EnumerateArray());
        Assert.Equal(eopId, basis.GetProperty("eopId").GetGuid());
        Assert.Equal("EOP-1A2B3C4D", basis.GetProperty("eopReference").GetString());
        Assert.Equal(1, basis.GetProperty("revision").GetInt32());
        Assert.Equal("v2hash", basis.GetProperty("snapshotHash").GetString());
        Assert.Equal("rhshf-eop-snapshot-v2", basis.GetProperty("snapshotHashScheme").GetString());
    }

    [Fact]
    public void Build_LateOfferReadyRetry_ReportsWhereTheCaseIsNow_NotTheStaleInstruction()
    {
        // A retry of the offer-ready event can land after the FAC accepted. Telling the portal to
        // "review the offer" again would put the case back a step on their screen.
        var profile = AtAwaitingOfferAcceptance(out var offer);
        var versionAtOffer = Serialize(RhshfWebhookPayloadBuilder.Build(
            profile, RhshfCallbackEventType.OfferReady, "evt_offer", DateTime.UtcNow, offer))
            .GetProperty("stateVersion").GetInt32();
        offer.AddDocument("signed.pdf", "application/pdf", "path/signed.pdf", 1024);
        offer.Accept(null);
        profile.AdvanceToPreDeploymentFromOffer();

        var root = Serialize(RhshfWebhookPayloadBuilder.Build(
            profile, RhshfCallbackEventType.OfferReady, "evt_offer", DateTime.UtcNow, offer,
            offerUrl: "https://crms.test/rhshf/offer/x"));

        Assert.Equal("OFFER_ACCEPTED", root.GetProperty("creditStatus").GetString());
        Assert.False(root.TryGetProperty("actionRequired", out _));
        Assert.Equal("ACCEPTED", root.GetProperty("offer").GetProperty("status").GetString());
        Assert.True(root.GetProperty("stateVersion").GetInt32() > versionAtOffer);
    }

    [Fact]
    public void Build_ReturnedToFac_SaysCorrectionRequired_WithTheOfficersReason()
    {
        var profile = Submitted();
        profile.Appraise(Guid.NewGuid(), RhshfAppraisalOutcome.ReturnToFac, "The bank statement is missing June.",
            RhshfProfilingStage.SupportingDocuments);
        var profilingUrl = $"https://crms.test/rhshf/profiling/{profile.Reference}";

        var root = Serialize(RhshfWebhookPayloadBuilder.Build(
            profile, RhshfCallbackEventType.CorrectionRequired, "evt_return", DateTime.UtcNow,
            currentOffer: null, offerUrl: null, profilingUrl: profilingUrl));

        Assert.Equal("PROFILING_IN_PROGRESS", root.GetProperty("status").GetString());
        Assert.Equal("CORRECTION_REQUIRED", root.GetProperty("creditStatus").GetString());
        Assert.Equal("CORRECTION_REQUIRED", root.GetProperty("eventType").GetString());
        Assert.Equal("CORRECT_PROFILE", root.GetProperty("actionRequired").GetString());
        Assert.Equal(profilingUrl, root.GetProperty("actionUrl").GetString());
        var correction = root.GetProperty("correction");
        Assert.Equal("supportingDocuments", correction.GetProperty("stage").GetString());
        Assert.Equal("The bank statement is missing June.", correction.GetProperty("message").GetString());
    }

    /// <summary>CRMSQA#1: the reason picked from the administrator's list travels as a code beside
    /// the note, on the correction and as the case's status reason.</summary>
    [Fact]
    public void Build_ReturnedToFac_CarriesTheReasonCode()
    {
        var profile = Submitted();
        profile.Appraise(Guid.NewGuid(), RhshfAppraisalOutcome.ReturnToFac, "The bank statement is missing June.",
            RhshfProfilingStage.SupportingDocuments, "DOCUMENT_MISSING");

        var root = Serialize(RhshfWebhookPayloadBuilder.Build(profile, RhshfCallbackEventType.CorrectionRequired, "evt_r", DateTime.UtcNow));

        Assert.Equal("DOCUMENT_MISSING", root.GetProperty("correction").GetProperty("reasonCode").GetString());
        Assert.Equal("DOCUMENT_MISSING", root.GetProperty("statusReasonCode").GetString());
    }

    [Fact]
    public void Build_Declined_CarriesTheDeclineReasonCode_AndAnOpenCaseHasNone()
    {
        var open = Submitted();
        Assert.False(Serialize(RhshfWebhookPayloadBuilder.Build(open, RhshfCallbackEventType.SubmittedForReview, "evt_o", DateTime.UtcNow))
            .TryGetProperty("statusReasonCode", out _));

        var profile = Submitted();
        profile.Appraise(Guid.NewGuid(), RhshfAppraisalOutcome.Decline, "Adverse bureau report.", null, "ADVERSE_CREDIT_HISTORY");

        var root = Serialize(RhshfWebhookPayloadBuilder.Build(profile, RhshfCallbackEventType.Decided, "evt_d", DateTime.UtcNow));

        Assert.Equal("CREDIT_DECLINED", root.GetProperty("creditStatus").GetString());
        Assert.Equal("ADVERSE_CREDIT_HISTORY", root.GetProperty("statusReasonCode").GetString());
        Assert.Equal("Adverse bureau report.", root.GetProperty("decision").GetProperty("reasons")[0].GetString());
    }

    [Fact]
    public void Build_AfterResubmission_CorrectionIsGone_AndCaseIsUnderReview()
    {
        var profile = Submitted();
        profile.Appraise(Guid.NewGuid(), RhshfAppraisalOutcome.ReturnToFac, "Fix the statement.", null);
        profile.AdvanceStageForTest(RhshfProfilingStage.ReviewAndSubmit);

        var root = Serialize(RhshfWebhookPayloadBuilder.Build(
            profile, RhshfCallbackEventType.CorrectionSubmitted, "evt_resubmit", DateTime.UtcNow));

        Assert.Equal("UNDER_CREDIT_REVIEW", root.GetProperty("creditStatus").GetString());
        Assert.False(root.TryGetProperty("correction", out _));
        Assert.False(root.TryGetProperty("actionRequired", out _));
    }

    [Fact]
    public void Build_FirstTimeProfiling_IsNotMistakenForACorrection()
    {
        var profile = MakeProfile();
        profile.AdvanceStageForTest(RhshfProfilingStage.CompanyVerification);

        var root = Serialize(RhshfWebhookPayloadBuilder.Build(
            profile, RhshfCallbackEventType.SubmittedForReview, "evt_x", DateTime.UtcNow));

        Assert.Equal("PROFILING_IN_PROGRESS", root.GetProperty("creditStatus").GetString());
        Assert.False(root.TryGetProperty("correction", out _));
    }

    [Fact]
    public void PortalEvents_AreRaised_AtEachPointTheFacsNextStepChanges()
    {
        var profile = MakeProfile();
        foreach (var stage in new[]
        {
            RhshfProfilingStage.CompanyVerification, RhshfProfilingStage.CreditBureauCheck,
            RhshfProfilingStage.EopReview, RhshfProfilingStage.SupportingDocuments,
        })
        {
            profile.AdvanceStageForTest(stage);
        }
        Assert.Empty(PortalEvents(profile)); // moving through the form is not news to the portal

        profile.AdvanceStageForTest(RhshfProfilingStage.ReviewAndSubmit);
        Assert.Equal([RhshfCallbackEventType.SubmittedForReview], PortalEvents(profile));

        profile.ClearDomainEvents();
        profile.Appraise(Guid.NewGuid(), RhshfAppraisalOutcome.ReturnToFac, "Fix it.", null);
        Assert.Equal([RhshfCallbackEventType.CorrectionRequired], PortalEvents(profile));

        profile.ClearDomainEvents();
        profile.AdvanceStageForTest(RhshfProfilingStage.ReviewAndSubmit);
        Assert.Equal([RhshfCallbackEventType.CorrectionSubmitted], PortalEvents(profile));

        profile.AppraiseWithFinancials(Guid.NewGuid(), RhshfAppraisalOutcome.Proceed, null);
        profile.ClearLegalForTest();
        profile.ReviewRisk(Guid.NewGuid(), RhshfRiskReviewOutcome.Cleared, null);
        profile.AdvanceToRatification();
        profile.Ratify(Guid.NewGuid(), RhshfRatificationOutcome.Ratified, TotalEopValue, null, null, []);
        profile.ClearDomainEvents();
        profile.AdvanceToPreDeploymentFromOffer();
        Assert.Equal([RhshfCallbackEventType.OfferAccepted], PortalEvents(profile));
    }

    private static List<RhshfCallbackEventType> PortalEvents(RhshfCreditProfile profile) =>
        profile.DomainEvents.OfType<RhshfPortalUpdateEvent>().Select(e => e.EventType).ToList();

    private static JsonElement Serialize(RhshfWebhookPayload payload) =>
        JsonDocument.Parse(JsonSerializer.Serialize(payload, RhshfCallbackService.JsonOptions)).RootElement;

    private static RhshfCreditProfile Submitted()
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
        return profile;
    }

    private static RhshfCreditProfile AtAwaitingOfferAcceptance(out RhshfOffer offer)
    {
        var profile = Submitted();
        profile.AppraiseWithFinancials(Guid.NewGuid(), RhshfAppraisalOutcome.Proceed, null);
        profile.ClearLegalForTest();
        profile.ReviewRisk(Guid.NewGuid(), RhshfRiskReviewOutcome.Cleared, null);
        profile.AdvanceToRatification();
        profile.Ratify(Guid.NewGuid(), RhshfRatificationOutcome.Ratified, TotalEopValue, null, null, []);
        offer = RhshfOffer.Create(profile.Id, profile.CurrentCycleNumber, "path/offer.pdf").Value;
        return profile;
    }
}

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
        profile.ReviewRisk(Guid.NewGuid(), RhshfRiskReviewOutcome.Cleared, null);
        profile.AdvanceToRatification();
        profile.Ratify(Guid.NewGuid(), RhshfRatificationOutcome.Ratified, TotalEopValue, null, null, []);

        var payload = RhshfWebhookPayloadBuilder.Build(profile, RhshfCallbackEventType.OfferReady, "evt_offer", DateTime.UtcNow);
        var json = JsonSerializer.Serialize(payload, RhshfCallbackService.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("UNDER_REVIEW", root.GetProperty("status").GetString());
        Assert.Equal("REVIEW_OFFER", root.GetProperty("actionRequired").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("decision").ValueKind);
    }
}

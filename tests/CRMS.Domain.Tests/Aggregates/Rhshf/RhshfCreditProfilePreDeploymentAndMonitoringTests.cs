using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Tests.Aggregates.Rhshf;

public class RhshfCreditProfilePreDeploymentAndMonitoringTests
{
    private const decimal TotalEopValue = 51_500_000.00m;

    private static RhshfCreditProfile CreateProfileAtPreDeploymentVerification()
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

        var offer = RhshfOffer.Create(profile.Id, profile.CurrentCycleNumber, "path/offer-cycle1.pdf").Value;
        offer.AddDocument("signed.pdf", "application/pdf", "path/signed.pdf", 1024);
        offer.Accept(null);
        profile.AdvanceToLegalClearance();
        profile.AdvanceToDisbursement();

        return profile; // now InternalStage == PreDeploymentVerification, no checklist seeded yet
    }

    private static RhshfPreDeploymentChecklistTemplate Mandatory(string title, int sort = 10) =>
        RhshfPreDeploymentChecklistTemplate.Create(title, null, isMandatory: true, sortOrder: sort).Value;

    private static RhshfPreDeploymentChecklistTemplate Optional(string title, int sort = 20) =>
        RhshfPreDeploymentChecklistTemplate.Create(title, null, isMandatory: false, sortOrder: sort).Value;

    private static RhshfPreDeploymentChecklistTemplate OfferDocsAuto(string title, int sort = 30) =>
        RhshfPreDeploymentChecklistTemplate.Create(title, null, isMandatory: true, sortOrder: sort,
            RhshfPreDeploymentVerificationKind.OfferDocuments).Value;

    [Fact]
    public void CompletePreDeployment_AutoOfferDocumentsItem_PassesWhenSyncedSatisfied()
    {
        var profile = CreateProfileAtPreDeploymentVerification();
        profile.SeedPreDeploymentChecklist([OfferDocsAuto("Signed offer & KFS")]);

        // The app layer derives this from the offer; here we assert the gate honours a satisfied sync.
        profile.SyncAutoOfferDocumentItems(offerDocumentsOnFile: true);
        var result = profile.CompletePreDeploymentVerification(Guid.NewGuid(), null);

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfInternalStage.Disbursement, profile.InternalStage);
    }

    [Fact]
    public void CompletePreDeployment_AutoOfferDocumentsItem_BlockedWhenNotOnFile()
    {
        var profile = CreateProfileAtPreDeploymentVerification();
        profile.SeedPreDeploymentChecklist([OfferDocsAuto("Signed offer & KFS")]);

        profile.SyncAutoOfferDocumentItems(offerDocumentsOnFile: false);
        var result = profile.CompletePreDeploymentVerification(Guid.NewGuid(), null);

        Assert.True(result.IsFailure);
        Assert.Equal(RhshfInternalStage.PreDeploymentVerification, profile.InternalStage);
    }

    [Fact]
    public void ConfirmPreDeployment_AutoOfferDocumentsItem_CannotBeConfirmedByHand()
    {
        var profile = CreateProfileAtPreDeploymentVerification();
        profile.SeedPreDeploymentChecklist([OfferDocsAuto("Signed offer & KFS")]);
        var item = profile.PreDeploymentChecklist.Single();

        var result = profile.ConfirmPreDeploymentChecklistItem(item.Id, Guid.NewGuid(), true, "manual tick");

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void SeedPreDeploymentChecklist_InstantiatesActiveTemplatesForCurrentCycle()
    {
        var profile = CreateProfileAtPreDeploymentVerification();

        profile.SeedPreDeploymentChecklist([Mandatory("A"), Optional("B")]);

        Assert.Equal(2, profile.PreDeploymentChecklist.Count);
        Assert.All(profile.PreDeploymentChecklist, i => Assert.Equal(profile.CurrentCycleNumber, i.CycleNumber));
    }

    [Fact]
    public void SeedPreDeploymentChecklist_CalledTwiceForSameCycle_IsIdempotent()
    {
        var profile = CreateProfileAtPreDeploymentVerification();
        profile.SeedPreDeploymentChecklist([Mandatory("A")]);

        profile.SeedPreDeploymentChecklist([Mandatory("A"), Optional("B")]);

        Assert.Single(profile.PreDeploymentChecklist);
    }

    [Fact]
    public void CompletePreDeploymentVerification_MandatoryItemUnconfirmed_Fails()
    {
        var profile = CreateProfileAtPreDeploymentVerification();
        profile.SeedPreDeploymentChecklist([Mandatory("A")]);

        var result = profile.CompletePreDeploymentVerification(Guid.NewGuid(), null);

        Assert.True(result.IsFailure);
        Assert.Equal(RhshfInternalStage.PreDeploymentVerification, profile.InternalStage);
    }

    [Fact]
    public void CompletePreDeploymentVerification_OptionalItemUnconfirmed_StillSucceeds()
    {
        var profile = CreateProfileAtPreDeploymentVerification();
        profile.SeedPreDeploymentChecklist([Mandatory("A"), Optional("B")]);
        var mandatoryItem = profile.PreDeploymentChecklist.Single(i => i.Title == "A");
        profile.ConfirmPreDeploymentChecklistItem(mandatoryItem.Id, Guid.NewGuid(), true, null);

        var result = profile.CompletePreDeploymentVerification(Guid.NewGuid(), null);

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfInternalStage.Disbursement, profile.InternalStage);
    }

    [Fact]
    public void CompletePreDeploymentVerification_NoItemsRecorded_Fails()
    {
        var profile = CreateProfileAtPreDeploymentVerification();

        var result = profile.CompletePreDeploymentVerification(Guid.NewGuid(), null);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void ConfirmPreDeploymentChecklistItem_WhenNotAtGateStage_Fails()
    {
        var profile = CreateProfileAtPreDeploymentVerification();
        profile.SeedPreDeploymentChecklist([Mandatory("A")]);
        var item = profile.PreDeploymentChecklist.Single();
        profile.ConfirmPreDeploymentChecklistItem(item.Id, Guid.NewGuid(), true, null);
        profile.CompletePreDeploymentVerification(Guid.NewGuid(), null); // now at Disbursement

        var result = profile.ConfirmPreDeploymentChecklistItem(item.Id, Guid.NewGuid(), false, "too late");

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void MarkClosed_FromActive_Succeeds()
    {
        var profile = CreateProfileAtPreDeploymentVerification();
        profile.SeedPreDeploymentChecklist([Mandatory("A")]);
        var item = profile.PreDeploymentChecklist.Single();
        profile.ConfirmPreDeploymentChecklistItem(item.Id, Guid.NewGuid(), true, null);
        profile.CompletePreDeploymentVerification(Guid.NewGuid(), null);
        profile.RecordDisbursementAttempt(Guid.NewGuid(), TotalEopValue, "0987654321", "Agro Inputs Ltd", RhshfDisbursementStatus.Booked, 12345L, "LN-000123", null);

        var result = profile.MarkClosed(Guid.NewGuid());

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfInternalStage.Closed, profile.InternalStage);
        Assert.Equal(RhshfCaseStatus.Approved, profile.Status); // decision stays Approved regardless
    }

    [Fact]
    public void MarkClosed_WhenNotActive_Fails()
    {
        var profile = CreateProfileAtPreDeploymentVerification();

        var result = profile.MarkClosed(Guid.NewGuid());

        Assert.True(result.IsFailure);
    }
}

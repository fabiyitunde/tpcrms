using CRMS.Domain.Common;
using CRMS.Domain.Aggregates.ProductCatalog;
using CRMS.Application.Rhshf.Commands;
using CRMS.Application.Rhshf.Interfaces;
using CRMS.Application.Rhshf.Services;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Tests.Rhshf;

public class RatifyRhshfCaseHandlerTests
{
    private const decimal TotalEopValue = 51_500_000.00m;

    /// <summary>The document payload builder, with the fakes the handler itself no longer holds.</summary>
    private static RhshfOfferDocumentDataBuilder Builder() =>
        new(new FakeCollateralRepo(), new FakeLoanProductRepo(), new FakeFineract(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<RhshfOfferDocumentDataBuilder>.Instance);

    private static RhshfCreditProfile MakeProfileAtRatification(
        IEnumerable<RhshfEopLineInput>? eopLines = null, RhshfFinancingInput? financing = null,
        decimal totalEopValue = TotalEopValue, int farmerCount = 1200)
    {
        var result = RhshfCreditProfile.Create(
            submissionId: Guid.NewGuid(), programmeCode: "RH-SHF-DRY-2026", programmeName: "Renewed Hope",
            sessionCode: "2026-DRY", sessionName: "Dry Season 2026", facId: Guid.NewGuid(),
            companyName: "Alliedsoft Limited", rcNumber: "RC123456", tin: "01234567-0001",
            boaAccountNumber: "0123456789", contactEmail: "fac@company.com", contactPhone: "+2348012345678",
            state: "Kano", lga: "Nassarawa", totalEopValue: totalEopValue, currency: "NGN", farmerCount: farmerCount,
            callbackUrl: "https://portal.example.gov.ng/api/integrations/crms/webhook",
            certifiedByAdmin: "admin@boa.gov.ng", certifiedAt: DateTime.UtcNow, rawSubmissionPayload: "{}",
            eopLines: eopLines, resolvedBranchId: null, resolvedOfficeId: null, financing: financing);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : null);
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
            new FakeOfferDocumentGenerator(), Builder(), fileStorage, new FakeUnitOfWork());

        var result = await handler.Handle(new RatifyRhshfCaseCommand(
            profile.Reference, Guid.NewGuid(), RhshfRatificationOutcome.Ratified, TotalEopValue, null, null, OverrideMissingBreakdown: true, OverrideReason: "Legacy case"));

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
            new FakeOfferRepository(), generator, Builder(), new FakeFileStorage(), new FakeUnitOfWork());

        await handler.Handle(new RatifyRhshfCaseCommand(
            profile.Reference, Guid.NewGuid(), RhshfRatificationOutcome.Ratified, TotalEopValue, null, null, OverrideMissingBreakdown: true, OverrideReason: "Legacy case"));

        var data = generator.Received;
        Assert.NotNull(data);
        Assert.Equal(TotalEopValue, data!.ApprovedAmount);
        Assert.Equal(profile.Reference, data.Reference);
        Assert.Equal(7, data.ValidityDays);              // the approved template's seven days
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
            new FakeOfferDocumentGenerator(), Builder(), new FakeFileStorage(), new FakeUnitOfWork());

        var result = await handler.Handle(new RatifyRhshfCaseCommand(
            profile.Reference, committeeApproverId, RhshfRatificationOutcome.Ratified, TotalEopValue, null, null, OverrideMissingBreakdown: true, OverrideReason: "Legacy case"));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Ratify_MismatchedAmount_DoesNotGenerateOffer()
    {
        var profile = MakeProfileAtRatification();
        var offerRepo = new FakeOfferRepository();
        var handler = new RatifyRhshfCaseHandler(
            new FakeProfileRepository(profile), new FakeCommitteeRepository(), new SharedFakeLegalClearanceRepository(), offerRepo,
            new FakeOfferDocumentGenerator(), Builder(), new FakeFileStorage(), new FakeUnitOfWork());

        var result = await handler.Handle(new RatifyRhshfCaseCommand(
            profile.Reference, Guid.NewGuid(), RhshfRatificationOutcome.Ratified, TotalEopValue - 1, null, null, OverrideMissingBreakdown: true, OverrideReason: "Legacy case"));

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
            new FakeOfferDocumentGenerator(), Builder(), new FakeFileStorage(), new FakeUnitOfWork());

        var result = await handler.Handle(new RatifyRhshfCaseCommand(
            profile.Reference, Guid.NewGuid(), RhshfRatificationOutcome.ReturnToFac, null, "need more info", RhshfProfilingStage.SupportingDocuments));

        Assert.True(result.IsSuccess);
        Assert.Null(offerRepo.Added);
        Assert.Equal(RhshfCaseStatus.ProfilingInProgress, profile.Status);
    }

    [Fact]
    public async Task Ratify_UnitemisedPlan_NeverPrintsTheZeroQuantityPlaceholder()
    {
        // The portal's crop-level summary line always carries 0 kg. Printed, it read as a real figure.
        var profile = MakeProfileAtRatification([new RhshfEopLineInput("Maize", 0m, 0m, TotalEopValue)]);
        var generator = new FakeOfferDocumentGenerator();
        var handler = new RatifyRhshfCaseHandler(
            new FakeProfileRepository(profile), new FakeCommitteeRepository(), new SharedFakeLegalClearanceRepository(),
            new FakeOfferRepository(), generator, Builder(), new FakeFileStorage(), new FakeUnitOfWork());

        await handler.Handle(new RatifyRhshfCaseCommand(
            profile.Reference, Guid.NewGuid(), RhshfRatificationOutcome.Ratified, TotalEopValue, null, null, OverrideMissingBreakdown: true, OverrideReason: "Legacy case"));

        var row = Assert.Single(generator.Received!.PlanRows);
        Assert.Equal("Maize", row.Description);
        Assert.Null(row.QuantityDisplay);
    }

    // ── Corrected offer (CRMSQA#2) ───────────────────────────────────────────────────────────────

    private static (RhshfCreditProfile Profile, RhshfOffer Accepted) AcceptedOffer()
    {
        var profile = MakeProfileAtRatification();
        profile.Ratify(Guid.NewGuid(), RhshfRatificationOutcome.Ratified, TotalEopValue, null, null, []);
        var offer = RhshfOffer.Create(profile.Id, profile.CurrentCycleNumber, "rhshf-offers/offer-cycle1.pdf").Value;
        offer.AddDocument("signed.pdf", "application/pdf", "path/signed.pdf", 1024);
        offer.Accept(null);
        profile.AdvanceToPreDeploymentFromOffer();
        return (profile, offer);
    }

    private static IssueCorrectedRhshfOfferHandler CorrectionHandler(
        RhshfCreditProfile profile, FakeOfferRepository offers, FakeFileStorage storage) =>
        new(new FakeProfileRepository(profile), offers, new FakeOfferDocumentGenerator(), Builder(), storage, new FakeUnitOfWork());

    [Fact]
    public async Task CorrectedOffer_KeepsTheAcceptedOne_IssuesANewOne_AndWaitsForTheFac()
    {
        var (profile, accepted) = AcceptedOffer();
        var offers = new FakeOfferRepository(accepted);
        var storage = new FakeFileStorage();

        var result = await CorrectionHandler(profile, offers, storage).Handle(
            new IssueCorrectedRhshfOfferCommand(profile.Reference, Guid.NewGuid(), "Plan did not list the EOP inputs."));

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfOfferStatus.Superseded, accepted.Status);
        Assert.Equal("rhshf-offers/offer-cycle1.pdf", accepted.OfferDocumentPath); // the signed record is untouched
        Assert.Single(accepted.Documents);

        var corrected = offers.Added!;
        Assert.Equal(RhshfOfferStatus.Generated, corrected.Status);
        Assert.NotEqual(accepted.OfferDocumentPath, corrected.OfferDocumentPath);
        Assert.Contains("-v2", Assert.Single(storage.UploadedPaths));

        Assert.Equal(RhshfInternalStage.AwaitingOfferAcceptance, profile.InternalStage);
        Assert.Equal(2, profile.OfferVersion);
        Assert.Equal("Plan did not list the EOP inputs.", profile.StatusHistory.Last().Note);
        Assert.Contains(profile.DomainEvents, e => e is RhshfOfferReadyEvent); // the portal hears OFFER_READY
    }

    [Fact]
    public async Task CorrectedOffer_ReplacesAnUnansweredOffer_SoTheWrongOneCanNoLongerBeAccepted()
    {
        // Grandbai's case: a corrected offer went out with the wrong farmer count and the FAC had not
        // answered it. Replacing it in place would leave two documents in circulation as one version.
        var profile = MakeProfileAtRatification();
        profile.Ratify(Guid.NewGuid(), RhshfRatificationOutcome.Ratified, TotalEopValue, null, null, []);
        var pending = IssuedMinutesAgo(profile, 15);
        var offers = new FakeOfferRepository(pending);

        var result = await CorrectionHandler(profile, offers, new FakeFileStorage()).Handle(
            new IssueCorrectedRhshfOfferCommand(profile.Reference, Guid.NewGuid(), "Wrong farmer count.", pending.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfOfferStatus.Superseded, pending.Status);
        Assert.False(pending.Accept(null).IsSuccess);
        Assert.Equal(RhshfOfferStatus.Generated, offers.Added!.Status);
        Assert.Equal(2, profile.OfferVersion);
        Assert.Equal(RhshfInternalStage.AwaitingOfferAcceptance, profile.InternalStage);
    }

    private static RhshfOffer IssuedMinutesAgo(RhshfCreditProfile profile, int minutes)
    {
        var offer = RhshfOffer.Create(profile.Id, profile.CurrentCycleNumber, "rhshf-offers/offer-cycle1.pdf").Value;
        typeof(RhshfOffer).GetProperty(nameof(RhshfOffer.GeneratedAt))!.SetValue(offer, DateTime.UtcNow.AddMinutes(-minutes));
        return offer;
    }

    [Fact]
    public async Task CorrectedOffer_IsRefused_WhenTheOfferWasIssuedMinutesAgo()
    {
        // Grandbai's double issue: a second correction 25 seconds after the first sent the FAC two
        // versions. An unanswered offer this fresh is not replaced again.
        var profile = MakeProfileAtRatification();
        profile.Ratify(Guid.NewGuid(), RhshfRatificationOutcome.Ratified, TotalEopValue, null, null, []);
        var fresh = IssuedMinutesAgo(profile, 1);
        var offers = new FakeOfferRepository(fresh);

        var result = await CorrectionHandler(profile, offers, new FakeFileStorage()).Handle(
            new IssueCorrectedRhshfOfferCommand(profile.Reference, Guid.NewGuid(), "Again.", fresh.Id));

        Assert.False(result.IsSuccess);
        Assert.Contains("Wait 10 minutes", result.Error);
        Assert.Null(offers.Added);
        Assert.Equal(RhshfOfferStatus.Generated, fresh.Status);
    }

    [Fact]
    public async Task CorrectedOffer_FromAStalePage_IsRefused()
    {
        // The officer's page still shows the offer that has since been replaced.
        var (profile, accepted) = AcceptedOffer();
        var offers = new FakeOfferRepository(accepted);

        var result = await CorrectionHandler(profile, offers, new FakeFileStorage()).Handle(
            new IssueCorrectedRhshfOfferCommand(profile.Reference, Guid.NewGuid(), "Wrong plan.", ExpectedOfferId: Guid.NewGuid()));

        Assert.False(result.IsSuccess);
        Assert.Contains("already been replaced", result.Error);
        Assert.Null(offers.Added);
        Assert.Equal(RhshfOfferStatus.Accepted, accepted.Status);
    }

    [Fact]
    public async Task CorrectedOffer_OfAFreshlyAcceptedOffer_IsNotHeldBack()
    {
        // The wait protects an offer the FAC has not seen through yet. Once they have accepted, a
        // correction is a deliberate act on a settled document and goes ahead at once.
        var (profile, accepted) = AcceptedOffer();
        var offers = new FakeOfferRepository(accepted);

        var result = await CorrectionHandler(profile, offers, new FakeFileStorage()).Handle(
            new IssueCorrectedRhshfOfferCommand(profile.Reference, Guid.NewGuid(), "Wrong plan.", accepted.Id));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CorrectedOffer_IsRefused_OnceTheFacHasRejected()
    {
        // A rejection cancels the case; there is nothing left to correct.
        var profile = MakeProfileAtRatification();
        profile.Ratify(Guid.NewGuid(), RhshfRatificationOutcome.Ratified, TotalEopValue, null, null, []);
        var rejected = RhshfOffer.Create(profile.Id, profile.CurrentCycleNumber, "rhshf-offers/offer-cycle1.pdf").Value;
        rejected.Reject("No");
        profile.CancelDueToOfferRejection("No");
        var offers = new FakeOfferRepository(rejected);

        var result = await CorrectionHandler(profile, offers, new FakeFileStorage()).Handle(
            new IssueCorrectedRhshfOfferCommand(profile.Reference, Guid.NewGuid(), "Wrong plan."));

        Assert.False(result.IsSuccess);
        Assert.Null(offers.Added);
    }

    // ── Ratification without a breakdown (CRMSQA#2) ──────────────────────────────────────────────

    [Fact]
    public async Task Ratify_WithoutABreakdown_IsRefused_UnlessOverridden()
    {
        var profile = MakeProfileAtRatification();
        var offers = new FakeOfferRepository();
        var handler = new RatifyRhshfCaseHandler(
            new FakeProfileRepository(profile), new FakeCommitteeRepository(), new SharedFakeLegalClearanceRepository(),
            offers, new FakeOfferDocumentGenerator(), Builder(), new FakeFileStorage(), new FakeUnitOfWork());

        var refused = await handler.Handle(new RatifyRhshfCaseCommand(
            profile.Reference, Guid.NewGuid(), RhshfRatificationOutcome.Ratified, TotalEopValue, null, null));
        Assert.False(refused.IsSuccess);
        Assert.Null(offers.Added);
        Assert.Equal(RhshfInternalStage.Ratification, profile.InternalStage);

        var noReason = await handler.Handle(new RatifyRhshfCaseCommand(
            profile.Reference, Guid.NewGuid(), RhshfRatificationOutcome.Ratified, TotalEopValue, null, null,
            OverrideMissingBreakdown: true, OverrideReason: " "));
        Assert.False(noReason.IsSuccess);

        var overridden = await handler.Handle(new RatifyRhshfCaseCommand(
            profile.Reference, Guid.NewGuid(), RhshfRatificationOutcome.Ratified, TotalEopValue, null, null,
            OverrideMissingBreakdown: true, OverrideReason: "Pre-October case; portal cannot resend."));
        Assert.True(overridden.IsSuccess);
        Assert.Contains(profile.StatusHistory, h => h.Note == "Pre-October case; portal cannot resend.");
    }

    [Fact]
    public async Task Ratify_ReturnToFac_NeedsNoBreakdown()
    {
        // The block is on issuing an offer, not on every ratification decision.
        var profile = MakeProfileAtRatification();
        var handler = new RatifyRhshfCaseHandler(
            new FakeProfileRepository(profile), new FakeCommitteeRepository(), new SharedFakeLegalClearanceRepository(),
            new FakeOfferRepository(), new FakeOfferDocumentGenerator(), Builder(), new FakeFileStorage(), new FakeUnitOfWork());

        var result = await handler.Handle(new RatifyRhshfCaseCommand(
            profile.Reference, Guid.NewGuid(), RhshfRatificationOutcome.ReturnToFac, null, "fix it", RhshfProfilingStage.EopReview));

        Assert.True(result.IsSuccess);
    }

    // ── The itemised offer (CRMSQA#2) ────────────────────────────────────────────────────────────

    private const decimal GrandbaiTotal = 12_884_464.00m;

    /// <summary>Grandbai's breakdown exactly as the portal sent it on 23 Oct.</summary>
    private static RhshfFinancingInput GrandbaiFinancing(Guid eopId) => new(
        [
            new(eopId, "Maize", "NPK Fertilizer", "Fertilizer", "NPK 20:10:10", "AgroEquip Solutions Ltd", 2m, "Kg", 1_120m, "perFarmer", 30, 67_200m, 44_800m, true),
            new(eopId, "Maize", "Urea Fertilizer", "Fertilizer", null, "Leorichardson Input Nigeria Limited", 1m, "Kg", 1_080m, "perFarmer", 30, 32_400m, 21_600m, true),
            new(eopId, "Maize", "Herbicide", "Agrochemical", "Hallakat", "AgriKnowledge Services Ltd", 5m, "Litre", 58_000m, "perFarmer", 30, 8_700_000m, 0m, true),
            new(eopId, "Maize", "Pesticide", "Agrochemical", "Insecticide", "Leorichardson Input Nigeria Limited", 1m, "Litre", 15_000m, "perFarmer", 30, 450_000m, 0m, true),
            new(eopId, "Maize", "Improved Seeds", "Seeds", "improved Maize Seeds", "AgriLime Nigeria Ltd", 1m, "Kg", 50_000m, "perFarmer", 30, 1_500_000m, 0m, true),
            new(eopId, "Maize", "Extension Services", "Services", null, null, 1m, "Per Farmer", 30_000m, "perFarmer", 30, 900_000m, 0m, true),
        ],
        [
            new("insurance", "Insurance", 4m, "percent of the per-farmer input subtotal", 465_984m, true),
            new("workingCapital", "Working capital", 5m, "percent of the per-farmer input subtotal", 582_480m, true),
            new("techServicesFee", "Technology services fee", 4_000m, "naira per farmer, x farmers", 120_000m, true),
        ],
        InputsTotal: 11_649_600m, PackagingExcessTotal: 66_400m, ChargesTotal: 1_168_464m,
        ApprovedEopTotal: GrandbaiTotal, LoanAmount: GrandbaiTotal, ExcludedFromLoan: 0m,
        Eops: [new(eopId, "Maize", "Kano", 30, 11_649_600m, 66_400m, 465_984m, 582_480m, 120_000m, GrandbaiTotal, GrandbaiTotal)]);

    private static async Task<RhshfOfferDocumentData> RatifiedDocument(RhshfCreditProfile profile, decimal total)
    {
        var generator = new FakeOfferDocumentGenerator();
        var handler = new RatifyRhshfCaseHandler(
            new FakeProfileRepository(profile), new FakeCommitteeRepository(), new SharedFakeLegalClearanceRepository(),
            new FakeOfferRepository(), generator, Builder(), new FakeFileStorage(), new FakeUnitOfWork());
        var result = await handler.Handle(new RatifyRhshfCaseCommand(
            profile.Reference, Guid.NewGuid(), RhshfRatificationOutcome.Ratified, total, null, null));
        Assert.True(result.IsSuccess, result.Error);
        return generator.Received!;
    }

    [Fact]
    public async Task Grandbai_OfferCarriesEveryInput_TheFinancedFarmers_AndFootsToTheApprovedAmount()
    {
        var eopId = Guid.NewGuid();
        var profile = MakeProfileAtRatification(
            [new RhshfEopLineInput("Maize", 0m, 0m, GrandbaiTotal)], GrandbaiFinancing(eopId), GrandbaiTotal, farmerCount: 220);

        var data = await RatifiedDocument(profile, GrandbaiTotal);

        Assert.Equal(30, data.FarmerCount);              // financed, from the EOP
        Assert.Equal(220, data.RegisteredFarmerCount);   // registered under the FAC, shown beside it
        Assert.Equal(1, data.OfferVersion);

        var schedule = Assert.Single(data.Schedules!);
        Assert.Equal(6, schedule.Rows.Count);
        var npk = schedule.Rows.Single(r => r.Input == "NPK Fertilizer");
        Assert.Equal("AgroEquip Solutions Ltd", npk.Supplier);
        Assert.Equal("2 Kg", npk.PerFarmer);
        Assert.Equal("60 Kg", npk.TotalQuantity);
        Assert.Equal(1_120m, npk.UnitPrice);
        Assert.Equal(44_800m, npk.PackagingExcess);
        Assert.Equal("30 farmers", schedule.Rows.Single(r => r.Input == "Extension Services").TotalQuantity);
        Assert.Null(schedule.Rows.Single(r => r.Input == "Extension Services").Supplier); // the portal sends none
        Assert.Equal(GrandbaiTotal, schedule.Total);
        Assert.Equal(GrandbaiTotal, schedule.Subtotals.Sum(s => s.Amount));
        Assert.Equal(GrandbaiTotal, schedule.Rows.Sum(r => r.Amount + r.PackagingExcess)
            + schedule.Subtotals.Where(s => s.Label is "Insurance" or "Working capital" or "Technology services fee").Sum(s => s.Amount));

        Assert.Equal(GrandbaiTotal, data.Summary!.Sum(s => s.Amount));
        Assert.Equal(10_749_600m, data.Summary!.Single(s => s.Label == "Physical inputs").Amount);
        Assert.Equal(900_000m, data.Summary!.Single(s => s.Label == "Services").Amount);
        Assert.All(data.Summary!, s => Assert.Equal(RhshfOfferSummaryRow.FinancedInLoan, s.Treatment));
    }

    [Fact]
    public async Task TwoEops_GetAScheduleEach_AndTheSummaryCoversBoth()
    {
        var maize = Guid.NewGuid();
        var rice = Guid.NewGuid();
        var financing = new RhshfFinancingInput(
            [
                new(maize, "Maize", "NPK Fertilizer", "Fertilizer", null, "Supplier A", 2m, "Kg", 1_000m, "perFarmer", 10, 20_000m, 0m, true),
                new(rice, "Rice", "Urea Fertilizer", "Fertilizer", null, "Supplier B", 3m, "Kg", 1_000m, "perFarmer", 20, 60_000m, 0m, true),
            ],
            [new("insurance", "Insurance", 4m, null, 3_200m, true)],
            InputsTotal: 80_000m, PackagingExcessTotal: 0m, ChargesTotal: 3_200m,
            ApprovedEopTotal: 83_200m, LoanAmount: 83_200m, ExcludedFromLoan: 0m,
            Eops:
            [
                new(maize, "Maize", "Kano", 10, 20_000m, 0m, 800m, 0m, 0m, 20_800m, 20_800m),
                new(rice, "Rice", "Kebbi", 20, 60_000m, 0m, 2_400m, 0m, 0m, 62_400m, 62_400m),
            ]);
        var profile = MakeProfileAtRatification(null, financing, 83_200m, farmerCount: 500);

        var data = await RatifiedDocument(profile, 83_200m);

        Assert.Equal(2, data.Schedules!.Count);
        Assert.Contains(data.Schedules!, s => s.Title.StartsWith("Maize, Kano, 10 farmers") && s.Total == 20_800m);
        Assert.Contains(data.Schedules!, s => s.Title.StartsWith("Rice, Kebbi, 20 farmers") && s.Total == 62_400m);
        Assert.Equal(30, data.FarmerCount);
        Assert.Equal(83_200m, data.Summary!.Sum(s => s.Amount));
    }

    [Fact]
    public async Task ABankGuaranteeSmallerThanTheFacility_IsNotDescribedAsCoveringIt()
    {
        var profile = MakeProfileAtRatification(null, GrandbaiFinancing(Guid.NewGuid()), GrandbaiTotal, farmerCount: 220);
        var guarantee = RhshfCollateral.Create(
            profile.Id, profile.CurrentCycleNumber, RhshfCollateralType.BankGuarantee, Guid.NewGuid(), null,
            "4839895", null, null, "GT Bank", 60_000m, true, null, null, null, null, null).Value;
        var generator = new FakeOfferDocumentGenerator();
        var builder = new RhshfOfferDocumentDataBuilder(new FakeCollateralRepo(guarantee), new FakeLoanProductRepo(), new FakeFineract(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<RhshfOfferDocumentDataBuilder>.Instance);
        var handler = new RatifyRhshfCaseHandler(
            new FakeProfileRepository(profile), new FakeCommitteeRepository(), new SharedFakeLegalClearanceRepository(),
            new FakeOfferRepository(), generator, builder, new FakeFileStorage(), new FakeUnitOfWork());

        await handler.Handle(new RatifyRhshfCaseCommand(
            profile.Reference, Guid.NewGuid(), RhshfRatificationOutcome.Ratified, GrandbaiTotal, null, null));

        var security = Assert.Single(generator.Received!.SecurityDescriptions);
        Assert.Contains("0.47% of the facility amount", security);
        Assert.DoesNotContain("principal and accrued interest", security);
    }

    [Fact]
    public async Task CorrectedOffer_NeedsAReason()
    {
        var (profile, accepted) = AcceptedOffer();
        var offers = new FakeOfferRepository(accepted);

        var result = await CorrectionHandler(profile, offers, new FakeFileStorage()).Handle(
            new IssueCorrectedRhshfOfferCommand(profile.Reference, Guid.NewGuid(), "  "));

        Assert.False(result.IsSuccess);
        Assert.Null(offers.Added);
        Assert.Equal(RhshfInternalStage.PreDeploymentVerification, profile.InternalStage);
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
        private readonly RhshfOffer? _existing;
        public FakeOfferRepository(RhshfOffer? existing = null) => _existing = existing;
        public RhshfOffer? Added { get; private set; }
        // The most recent offer wins, as in the real repository.
        public Task<RhshfOffer?> GetByProfileAndCycleAsync(Guid rhshfCreditProfileId, int cycleNumber, CancellationToken ct = default)
            => Task.FromResult(Added ?? _existing);
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
        private readonly RhshfCollateral[] _collateral;
        public FakeCollateralRepo(params RhshfCollateral[] collateral) => _collateral = collateral;
        public Task<IReadOnlyList<RhshfCollateral>> GetByProfileAndCycleAsync(Guid rhshfCreditProfileId, int cycleNumber, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RhshfCollateral>>(_collateral);
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

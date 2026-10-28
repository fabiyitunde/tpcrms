using CRMS.Application.Common;
using CRMS.Application.Rhshf.Interfaces;
using CRMS.Application.Rhshf.Services;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>
/// Final Approver's ratification (design doc §3.6, Phase 6). On Ratified, generates the offer
/// document and creates the RhshfOffer for this cycle in the same transaction — external status
/// stays UnderReview throughout (design doc §6 #9). The Ratified outcome raises the offer-ready
/// event, which sends the portal its callback after commit.
/// </summary>
public record RatifyRhshfCaseCommand(
    string Reference, Guid FinalApproverId, RhshfRatificationOutcome Outcome, decimal? ApprovedAmount,
    string? Notes, RhshfProfilingStage? ReturnToStage,
    bool OverrideMissingBreakdown = false, string? OverrideReason = null) : IRequest<ApplicationResult>;

public class RatifyRhshfCaseHandler : IRequestHandler<RatifyRhshfCaseCommand, ApplicationResult>
{
    private const string BankName = "Bank of Agriculture";
    private const string OfferContainerName = "rhshf-offers";

    /// <summary>Seven days, per the approved template. Shorter than the thirty the old letter
    /// stated — a deliberate change, taken from the document the bank approved.</summary>
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfCommitteeReviewRepository _committeeRepo;
    private readonly IRhshfLegalClearanceRepository _legalRepo;
    private readonly IRhshfOfferRepository _offerRepo;
    private readonly IRhshfOfferDocumentGenerator _offerDocumentGenerator;
    private readonly RhshfOfferDocumentDataBuilder _documentDataBuilder;
    private readonly IFileStorageService _fileStorage;
    private readonly IUnitOfWork _uow;

    public RatifyRhshfCaseHandler(
        IRhshfCreditProfileRepository repo,
        IRhshfCommitteeReviewRepository committeeRepo,
        IRhshfLegalClearanceRepository legalRepo,
        IRhshfOfferRepository offerRepo,
        IRhshfOfferDocumentGenerator offerDocumentGenerator,
        RhshfOfferDocumentDataBuilder documentDataBuilder,
        IFileStorageService fileStorage,
        IUnitOfWork uow)
    {
        _repo = repo;
        _committeeRepo = committeeRepo;
        _legalRepo = legalRepo;
        _offerRepo = offerRepo;
        _offerDocumentGenerator = offerDocumentGenerator;
        _documentDataBuilder = documentDataBuilder;
        _fileStorage = fileStorage;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(RatifyRhshfCaseCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        var cycleNumber = profile.CurrentCycleNumber;
        var excludedActorIds = profile.GetCurrentCycleAppraisalAndRiskActorIds().ToList();
        var committeeReview = await _committeeRepo.GetByProfileAndCycleAsync(profile.Id, cycleNumber, ct);
        if (committeeReview is not null)
        {
            excludedActorIds.AddRange(committeeReview.Votes
                .Where(v => v.Vote == RhshfCommitteeVoteChoice.Approve)
                .Select(v => v.UserId));
        }

        // The Legal Officer who cleared the security before circulation is part of the same chain.
        var clearances = await _legalRepo.GetByProfileAndCycleAsync(profile.Id, cycleNumber, ct);
        excludedActorIds.AddRange(clearances.Select(c => c.LegalOfficerId));

        // No offer without the portal's itemised breakdown: the letter would print a crop-level plan
        // the borrower cannot reconcile to their EOP (CRMSQA#2). The portal can still send it — a
        // repeat submission now attaches one — and for a case it never will, a Final Approver can
        // override with a reason, which goes into the trail.
        var ratifyingWithoutBreakdown = request.Outcome == RhshfRatificationOutcome.Ratified && !profile.HasItemisedFinancing;
        if (ratifyingWithoutBreakdown && !request.OverrideMissingBreakdown)
            return ApplicationResult.Failure(
                "This case has no itemised breakdown from the portal, so its offer letter could only show a " +
                "crop-level plan. Ask the portal team to resend the submission with the breakdown, or ratify " +
                "with the override and give a reason.");
        if (ratifyingWithoutBreakdown && string.IsNullOrWhiteSpace(request.OverrideReason))
            return ApplicationResult.Failure("Give a reason for ratifying without the itemised breakdown.");

        var result = profile.Ratify(request.FinalApproverId, request.Outcome, request.ApprovedAmount, request.Notes, request.ReturnToStage, excludedActorIds);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        if (ratifyingWithoutBreakdown)
            profile.RecordRatificationWithoutBreakdown(request.FinalApproverId, request.OverrideReason!);

        if (request.Outcome == RhshfRatificationOutcome.Ratified)
        {
            // One document now, not two. The Key Facts Statement is folded in as a panel, so the
            // FAC signs and returns a single file — and RhshfOffer.AttachKfs is deliberately NOT
            // called, which is what makes HasRequiredSignedDocuments ask for one signature instead
            // of two. Offers issued before this change keep their KFS and still require both.
            var build = await _documentDataBuilder.BuildAsync(profile, request.ApprovedAmount!.Value, ct);
            var pdfBytes = await _offerDocumentGenerator.GenerateAsync(build.Data, ct);

            var storagePath = await _fileStorage.UploadAsync(
                OfferContainerName, $"{profile.Reference}/offer-cycle{cycleNumber}.pdf", pdfBytes, "application/pdf", ct);

            var offerResult = RhshfOffer.Create(profile.Id, cycleNumber, storagePath);
            if (offerResult.IsFailure)
                return ApplicationResult.Failure(offerResult.Error);

            // Anything core banking would have deducted twice. Recorded on the offer so the case
            // shows it to whoever can get the product fixed.
            offerResult.Value.RecordSuppressedProductFees(build.SuppressedProductFees);

            await _offerRepo.AddAsync(offerResult.Value, ct);
        }

        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}

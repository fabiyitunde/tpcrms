using CRMS.Application.Common;
using CRMS.Application.Rhshf.Interfaces;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>
/// Final Approver's ratification (design doc §3.6, Phase 6). On Ratified, generates the offer
/// document and creates the RhshfOffer for this cycle in the same transaction — external status
/// stays UnderReview throughout (design doc §6 #9); no webhook fires here.
/// </summary>
public record RatifyRhshfCaseCommand(
    string Reference, Guid FinalApproverId, RhshfRatificationOutcome Outcome, decimal? ApprovedAmount,
    string? Notes, RhshfProfilingStage? ReturnToStage) : IRequest<ApplicationResult>;

public class RatifyRhshfCaseHandler : IRequestHandler<RatifyRhshfCaseCommand, ApplicationResult>
{
    private const string BankName = "Bank of Agriculture";
    private const string OfferContainerName = "rhshf-offers";

    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfCommitteeReviewRepository _committeeRepo;
    private readonly IRhshfOfferRepository _offerRepo;
    private readonly IRhshfOfferLetterPdfGenerator _pdfGenerator;
    private readonly IFileStorageService _fileStorage;
    private readonly IUnitOfWork _uow;

    public RatifyRhshfCaseHandler(
        IRhshfCreditProfileRepository repo,
        IRhshfCommitteeReviewRepository committeeRepo,
        IRhshfOfferRepository offerRepo,
        IRhshfOfferLetterPdfGenerator pdfGenerator,
        IFileStorageService fileStorage,
        IUnitOfWork uow)
    {
        _repo = repo;
        _committeeRepo = committeeRepo;
        _offerRepo = offerRepo;
        _pdfGenerator = pdfGenerator;
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

        var result = profile.Ratify(request.FinalApproverId, request.Outcome, request.ApprovedAmount, request.Notes, request.ReturnToStage, excludedActorIds);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        if (request.Outcome == RhshfRatificationOutcome.Ratified)
        {
            var pdfBytes = await _pdfGenerator.GenerateAsync(new RhshfOfferLetterData(
                Reference: profile.Reference,
                CompanyName: profile.CompanyName,
                RcNumber: profile.RcNumber,
                ProgrammeName: profile.ProgrammeName,
                SessionName: profile.SessionName,
                ApprovedAmount: request.ApprovedAmount!.Value,
                Currency: profile.Currency,
                GeneratedDate: DateTime.UtcNow,
                BankName: BankName), ct);

            var storagePath = await _fileStorage.UploadAsync(
                OfferContainerName, $"{profile.Reference}/offer-cycle{cycleNumber}.pdf", pdfBytes, "application/pdf", ct);

            var offerResult = RhshfOffer.Create(profile.Id, cycleNumber, storagePath);
            if (offerResult.IsFailure)
                return ApplicationResult.Failure(offerResult.Error);

            await _offerRepo.AddAsync(offerResult.Value, ct);
        }

        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}

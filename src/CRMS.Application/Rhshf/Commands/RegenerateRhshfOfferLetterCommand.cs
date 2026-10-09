using CRMS.Application.Common;
using CRMS.Application.Rhshf.Interfaces;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>
/// Re-generates the offer letter PDF for the current cycle and re-points the offer at it — recovery
/// for a case whose generated letter is missing or unreadable (e.g. an offer created outside the
/// normal ratify path, or a lost storage file). The content is rebuilt from the ratified amount and
/// case data, so a regenerated letter matches any copy the FAC already downloaded. Only permitted
/// while the FAC has not yet accepted/rejected — an accepted offer is contractual and is never
/// silently replaced (changing terms is a fresh offer via Return-to-FAC).
/// </summary>
public record RegenerateRhshfOfferLetterCommand(string Reference, Guid ActorUserId) : IRequest<ApplicationResult>;

public class RegenerateRhshfOfferLetterHandler : IRequestHandler<RegenerateRhshfOfferLetterCommand, ApplicationResult>
{
    private const string BankName = "Bank of Agriculture";
    private const string OfferContainerName = "rhshf-offers";

    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfOfferRepository _offerRepo;
    private readonly IRhshfOfferLetterPdfGenerator _pdfGenerator;
    private readonly IRhshfKfsPdfGenerator _kfsGenerator;
    private readonly IFileStorageService _fileStorage;
    private readonly IUnitOfWork _uow;

    public RegenerateRhshfOfferLetterHandler(
        IRhshfCreditProfileRepository repo, IRhshfOfferRepository offerRepo,
        IRhshfOfferLetterPdfGenerator pdfGenerator, IRhshfKfsPdfGenerator kfsGenerator,
        IFileStorageService fileStorage, IUnitOfWork uow)
    {
        _repo = repo;
        _offerRepo = offerRepo;
        _pdfGenerator = pdfGenerator;
        _kfsGenerator = kfsGenerator;
        _fileStorage = fileStorage;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(RegenerateRhshfOfferLetterCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        var offer = await _offerRepo.GetByProfileAndCycleAsync(profile.Id, profile.CurrentCycleNumber, ct);
        if (offer is null)
            return ApplicationResult.Failure("No offer exists for this case — it must be ratified before a letter can be generated.");

        if (profile.ApprovedAmount is null)
            return ApplicationResult.Failure("No ratified amount is on file for this case.");

        var appraisal = profile.GetCurrentCycleFinancialAppraisal();
        var pdfBytes = await _pdfGenerator.GenerateAsync(new RhshfOfferLetterData(
            Reference: profile.Reference,
            CompanyName: profile.CompanyName,
            RcNumber: profile.RcNumber,
            ProgrammeName: profile.ProgrammeName,
            SessionName: profile.SessionName,
            ApprovedAmount: profile.ApprovedAmount.Value,
            Currency: profile.Currency,
            GeneratedDate: DateTime.UtcNow,
            BankName: BankName,
            InterestRatePercent: appraisal?.InterestRatePercent,
            CycleMonths: appraisal?.CycleMonths,
            AmountDueAtHarvest: appraisal?.AmountDueAtHarvest), ct);

        var storagePath = await _fileStorage.UploadAsync(
            OfferContainerName, $"{profile.Reference}/offer-cycle{offer.CycleNumber}.pdf", pdfBytes, "application/pdf", ct);

        var result = offer.RegenerateDocument(storagePath);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        // Regenerate the accompanying Key Facts Statement too, so the package stays complete. Shares the
        // ratify path's builder so a regenerated KFS is identical to the one issued at ratification.
        var kfsBytes = await _kfsGenerator.GenerateAsync(
            RatifyRhshfCaseHandler.BuildKfsData(profile, profile.ApprovedAmount.Value, appraisal), ct);
        var kfsPath = await _fileStorage.UploadAsync(
            OfferContainerName, $"{profile.Reference}/kfs-cycle{offer.CycleNumber}.pdf", kfsBytes, "application/pdf", ct);
        offer.AttachKfs(kfsPath);

        // The offer was loaded tracked, so the OfferDocumentPath change persists on save.
        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}

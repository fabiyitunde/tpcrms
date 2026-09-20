using CRMS.Application.Common;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Queries;

/// <summary>Shared result shape for every RH-SHF staff document download — none of these existed
/// before (Phase: Detail page rebuild); staff previously had no way to view what a FAC uploaded.</summary>
public record RhshfDownloadedFileDto(byte[] Content, string FileName, string ContentType);

public record DownloadRhshfSupportingDocumentQuery(Guid DocumentId) : IRequest<ApplicationResult<RhshfDownloadedFileDto>>;

public class DownloadRhshfSupportingDocumentHandler : IRequestHandler<DownloadRhshfSupportingDocumentQuery, ApplicationResult<RhshfDownloadedFileDto>>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IFileStorageService _fileStorage;

    public DownloadRhshfSupportingDocumentHandler(IRhshfCreditProfileRepository repo, IFileStorageService fileStorage)
    {
        _repo = repo;
        _fileStorage = fileStorage;
    }

    public async Task<ApplicationResult<RhshfDownloadedFileDto>> Handle(DownloadRhshfSupportingDocumentQuery request, CancellationToken ct = default)
    {
        var document = await _repo.GetSupportingDocumentByIdAsync(request.DocumentId, ct);
        if (document is null)
            return ApplicationResult<RhshfDownloadedFileDto>.Failure("Document not found.");

        var content = await _fileStorage.DownloadAsync(document.StoragePath, ct);
        return ApplicationResult<RhshfDownloadedFileDto>.Success(new RhshfDownloadedFileDto(content, document.FileName, document.ContentType));
    }
}

public record DownloadRhshfOfferDocumentQuery(Guid DocumentId) : IRequest<ApplicationResult<RhshfDownloadedFileDto>>;

public class DownloadRhshfOfferDocumentHandler : IRequestHandler<DownloadRhshfOfferDocumentQuery, ApplicationResult<RhshfDownloadedFileDto>>
{
    private readonly IRhshfOfferRepository _offerRepo;
    private readonly IFileStorageService _fileStorage;

    public DownloadRhshfOfferDocumentHandler(IRhshfOfferRepository offerRepo, IFileStorageService fileStorage)
    {
        _offerRepo = offerRepo;
        _fileStorage = fileStorage;
    }

    public async Task<ApplicationResult<RhshfDownloadedFileDto>> Handle(DownloadRhshfOfferDocumentQuery request, CancellationToken ct = default)
    {
        var document = await _offerRepo.GetDocumentByIdAsync(request.DocumentId, ct);
        if (document is null)
            return ApplicationResult<RhshfDownloadedFileDto>.Failure("Document not found.");

        var content = await _fileStorage.DownloadAsync(document.StoragePath, ct);
        return ApplicationResult<RhshfDownloadedFileDto>.Success(new RhshfDownloadedFileDto(content, document.FileName, document.ContentType));
    }
}

public record DownloadRhshfCollateralDocumentQuery(Guid DocumentId) : IRequest<ApplicationResult<RhshfDownloadedFileDto>>;

public class DownloadRhshfCollateralDocumentHandler : IRequestHandler<DownloadRhshfCollateralDocumentQuery, ApplicationResult<RhshfDownloadedFileDto>>
{
    private readonly IRhshfCollateralRepository _collateralRepo;
    private readonly IFileStorageService _fileStorage;

    public DownloadRhshfCollateralDocumentHandler(IRhshfCollateralRepository collateralRepo, IFileStorageService fileStorage)
    {
        _collateralRepo = collateralRepo;
        _fileStorage = fileStorage;
    }

    public async Task<ApplicationResult<RhshfDownloadedFileDto>> Handle(DownloadRhshfCollateralDocumentQuery request, CancellationToken ct = default)
    {
        var document = await _collateralRepo.GetDocumentByIdAsync(request.DocumentId, ct);
        if (document is null)
            return ApplicationResult<RhshfDownloadedFileDto>.Failure("Document not found.");

        var content = await _fileStorage.DownloadAsync(document.StoragePath, ct);
        return ApplicationResult<RhshfDownloadedFileDto>.Success(new RhshfDownloadedFileDto(content, document.FileName, document.ContentType));
    }
}

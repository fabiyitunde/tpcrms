using CRMS.Application.Common;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>Attaches a scan of the collateral instrument (guarantee letter, CRG certificate,
/// mortgage title deed) to an existing RhshfCollateral record.</summary>
public record AddRhshfCollateralDocumentCommand(Guid CollateralId, string FileName, string ContentType, byte[] Content)
    : IRequest<ApplicationResult>;

public class AddRhshfCollateralDocumentHandler : IRequestHandler<AddRhshfCollateralDocumentCommand, ApplicationResult>
{
    private const string ContainerName = "rhshf-collateral";
    private const long MaxSizeBytes = 10 * 1024 * 1024;

    private readonly IRhshfCollateralRepository _collateralRepo;
    private readonly IFileStorageService _fileStorage;
    private readonly IUnitOfWork _uow;

    public AddRhshfCollateralDocumentHandler(IRhshfCollateralRepository collateralRepo, IFileStorageService fileStorage, IUnitOfWork uow)
    {
        _collateralRepo = collateralRepo;
        _fileStorage = fileStorage;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(AddRhshfCollateralDocumentCommand request, CancellationToken ct = default)
    {
        if (request.Content.Length == 0)
            return ApplicationResult.Failure("File is empty.");
        if (request.Content.Length > MaxSizeBytes)
            return ApplicationResult.Failure("File exceeds the 10 MB size limit.");

        var collateral = await _collateralRepo.GetByIdAsync(request.CollateralId, ct);
        if (collateral is null)
            return ApplicationResult.Failure("Collateral record not found.");

        var storagePath = await _fileStorage.UploadAsync(
            ContainerName, $"{request.CollateralId}/{Guid.NewGuid()}-{request.FileName}", request.Content, request.ContentType, ct);

        collateral.AddDocument(request.FileName, request.ContentType, storagePath, request.Content.Length);

        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}

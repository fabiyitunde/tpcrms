using CRMS.Application.Common;
using CRMS.Application.Access;
using CRMS.Domain.Aggregates.Access;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using CRMS.Domain.Services;

namespace CRMS.Application.Rhshf.Queries;

/// <summary>Shared result shape for every RH-SHF staff document download — none of these existed
/// before (Phase: Detail page rebuild); staff previously had no way to view what a FAC uploaded.</summary>
public record RhshfDownloadedFileDto(byte[] Content, string FileName, string ContentType);

/// <summary>A staff member opening (<paramref name="IsView"/>) or downloading a FAC document. A
/// director's or manager's CV or ID needs <see cref="FeatureKeys.RhshfPersonalDocumentsView"/>; every
/// access is written to the audit trail.</summary>
public record DownloadRhshfSupportingDocumentQuery(Guid DocumentId, Guid UserId = default, bool IsView = false)
    : IRequest<ApplicationResult<RhshfDownloadedFileDto>>;

public class DownloadRhshfSupportingDocumentHandler : IRequestHandler<DownloadRhshfSupportingDocumentQuery, ApplicationResult<RhshfDownloadedFileDto>>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IFileStorageService _fileStorage;
    private readonly IFeatureAccessService? _access;
    private readonly AuditService? _audit;
    private readonly IUserNameResolver? _names;

    public DownloadRhshfSupportingDocumentHandler(IRhshfCreditProfileRepository repo, IFileStorageService fileStorage,
        IFeatureAccessService? access = null, AuditService? audit = null, IUserNameResolver? names = null)
    {
        _repo = repo;
        _fileStorage = fileStorage;
        _access = access;
        _audit = audit;
        _names = names;
    }

    public async Task<ApplicationResult<RhshfDownloadedFileDto>> Handle(DownloadRhshfSupportingDocumentQuery request, CancellationToken ct = default)
    {
        var document = await _repo.GetSupportingDocumentByIdAsync(request.DocumentId, ct);
        if (document is null)
            return ApplicationResult<RhshfDownloadedFileDto>.Failure("Document not found.");

        if (document.IsPersonal
            && (_access is null || !await _access.IsAllowedAsync(request.UserId, FeatureKeys.RhshfPersonalDocumentsView, ct)))
            return ApplicationResult<RhshfDownloadedFileDto>.Failure(
                "You do not have access to directors' and managers' CVs and IDs.");

        var content = await _fileStorage.DownloadAsync(document.StoragePath, ct);
        await RhshfDocumentAudit.LogAccessAsync(_audit, _names, request.UserId, document,
            request.IsView ? DataAccessType.View : DataAccessType.Download, ct);
        return ApplicationResult<RhshfDownloadedFileDto>.Success(new RhshfDownloadedFileDto(content, document.FileName, document.ContentType));
    }
}

/// <summary>Audit lines for RH-SHF document and identity access. A failed log line never blocks
/// the action: the officer has already been allowed it.</summary>
public static class RhshfDocumentAudit
{
    public static async Task LogAccessAsync(AuditService? audit, IUserNameResolver? names, Guid userId,
        RhshfSupportingDocument document, DataAccessType accessType, CancellationToken ct)
    {
        if (audit is null || userId == Guid.Empty) return;
        try
        {
            var userName = names is null ? userId.ToString() : await names.ResolveAsync(userId, ct);
            var dataType = document.Category == RhshfDocumentCategory.DirectorMeansOfId
                ? SensitiveDataType.PersonalInformation
                : document.IsPersonal ? SensitiveDataType.EmploymentInformation : SensitiveDataType.FinancialStatement;
            await audit.LogDataAccessAsync(userId, userName, string.Empty, dataType,
                nameof(RhshfSupportingDocument), document.Id, accessType,
                entityReference: document.FileName,
                accessReason: $"RH-SHF {document.Category} document {accessType.ToString().ToLowerInvariant()}", ct: ct);
        }
        catch
        {
            // Swallowed by design — see the class summary.
        }
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

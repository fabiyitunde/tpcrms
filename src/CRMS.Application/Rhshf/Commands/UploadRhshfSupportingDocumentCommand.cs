using CRMS.Application.Common;
using CRMS.Application.Rhshf.Interfaces;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>
/// §4 SupportingDocuments stage. Uploads are categorised as of Phase D, so the required-document
/// checklist can tell what has actually been supplied. Uses IFileStorageService directly, the same
/// generic storage abstraction every other document upload in CRMS already uses.
/// </summary>
public record UploadRhshfSupportingDocumentCommand(
    string Reference, RhshfDocumentCategory Category, string FileName, string ContentType, byte[] Content,
    /// <summary>The director or manager a per-person document (CV, ID) belongs to.</summary>
    Guid? DirectorId = null, Guid? ManagementStaffId = null)
    : IRequest<ApplicationResult>;

public class UploadRhshfSupportingDocumentHandler : IRequestHandler<UploadRhshfSupportingDocumentCommand, ApplicationResult>
{
    private const string ContainerName = "rhshf-documents";
    private const long DefaultMaxSizeBytes = 10 * 1024 * 1024;
    private const long FarmerRegisterMaxSizeBytes = 20 * 1024 * 1024;

    /// <summary>The largest file a slot takes. The farmers register lists every smallholder with
    /// biometric evidence, so BOA allows it 20 MB; every other document stays at 10 MB.</summary>
    public static long MaxSizeBytesFor(RhshfDocumentCategory category) =>
        category == RhshfDocumentCategory.FarmerRegister ? FarmerRegisterMaxSizeBytes : DefaultMaxSizeBytes;

    /// <summary>The limit as the FAC reads it, e.g. "20 MB".</summary>
    public static string MaxSizeText(RhshfDocumentCategory category) =>
        $"{MaxSizeBytesFor(category) / (1024 * 1024)} MB";

    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfDocumentRequirementRepository _requirementRepo;
    private readonly IRhshfDocumentUploadValidator _validator;
    private readonly IFileStorageService _fileStorage;
    private readonly IUnitOfWork _uow;

    public UploadRhshfSupportingDocumentHandler(
        IRhshfCreditProfileRepository repo,
        IRhshfDocumentRequirementRepository requirementRepo,
        IRhshfDocumentUploadValidator validator,
        IFileStorageService fileStorage,
        IUnitOfWork uow)
    {
        _repo = repo;
        _requirementRepo = requirementRepo;
        _validator = validator;
        _fileStorage = fileStorage;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(UploadRhshfSupportingDocumentCommand request, CancellationToken ct = default)
    {
        if (request.Content.Length == 0)
            return ApplicationResult.Failure("File is empty.");
        if (request.Content.Length > MaxSizeBytesFor(request.Category))
            return ApplicationResult.Failure($"File exceeds the {MaxSizeText(request.Category)} size limit.");

        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        // Is this the kind of document the slot asked for? Checked before the bytes go anywhere:
        // a rejected file should leave nothing behind in storage.
        var requirement = (await _requirementRepo.GetActiveAsync(ct))
            .FirstOrDefault(r => r.Category == request.Category);

        var valid = _validator.Validate(requirement, request.FileName, request.Content);
        if (valid.IsFailure)
            return ApplicationResult.Failure(valid.Error);

        // The person must be on this case — the ids come from a form post, and a document filed
        // against someone else's director would be readable from the wrong case.
        if (request.DirectorId.HasValue && profile.Directors.All(d => d.Id != request.DirectorId))
            return ApplicationResult.Failure("That director is not on this case.");
        if (request.ManagementStaffId.HasValue && profile.ManagementStaff.All(m => m.Id != request.ManagementStaffId))
            return ApplicationResult.Failure("That member of management is not on this case.");
        if (profile.CurrentStage != RhshfProfilingStage.SupportingDocuments)
            return ApplicationResult.Failure("Case is not on the supporting documents stage.");

        // Upload next. The checks above cover everything the domain call below can refuse, so a
        // stored blob is not orphaned by a domain failure under normal operation.
        var storagePath = await _fileStorage.UploadAsync(
            ContainerName, $"{profile.Reference}/{Guid.NewGuid()}-{request.FileName}", request.Content, request.ContentType, ct);

        var result = profile.AddSupportingDocument(
            request.Category, request.FileName, request.ContentType, storagePath, request.Content.Length,
            request.DirectorId, request.ManagementStaffId);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}

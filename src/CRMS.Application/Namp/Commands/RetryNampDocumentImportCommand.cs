using System.Text.Json;
using CRMS.Application.Common;
using CRMS.Application.Namp.Interfaces;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace CRMS.Application.Namp.Commands;

public record RetryNampDocumentImportCommand(Guid NampApplicationId) : IRequest<ApplicationResult<string>>;

public class RetryNampDocumentImportHandler
    : IRequestHandler<RetryNampDocumentImportCommand, ApplicationResult<string>>
{
    private readonly INampApplicationRepository _appRepo;
    private readonly INampStagingRepository _stagingRepo;
    private readonly INampPortalS3Downloader _nampPortalS3;
    private readonly IFileStorageService _fileStorage;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<RetryNampDocumentImportHandler> _logger;

    public RetryNampDocumentImportHandler(
        INampApplicationRepository appRepo,
        INampStagingRepository stagingRepo,
        INampPortalS3Downloader nampPortalS3,
        IFileStorageService fileStorage,
        IUnitOfWork uow,
        ILogger<RetryNampDocumentImportHandler> logger)
    {
        _appRepo       = appRepo;
        _stagingRepo   = stagingRepo;
        _nampPortalS3  = nampPortalS3;
        _fileStorage   = fileStorage;
        _uow           = uow;
        _logger        = logger;
    }

    public async Task<ApplicationResult<string>> Handle(
        RetryNampDocumentImportCommand request, CancellationToken ct = default)
    {
        var app = await _appRepo.GetByIdWithDetailsAsync(request.NampApplicationId, ct);
        if (app is null)
            return ApplicationResult<string>.Failure("NAMP application not found.");

        var staging = await _stagingRepo.GetByApplicationReferenceAsync(app.ApplicationReference, ct);
        if (staging is null)
            return ApplicationResult<string>.Failure("Original staging record not found — cannot re-import documents.");

        if (string.IsNullOrWhiteSpace(staging.RawPayload))
            return ApplicationResult<string>.Failure("Staging record has no payload to import documents from.");

        RecallPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<RecallPayload>(staging.RawPayload,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            return ApplicationResult<string>.Failure($"Could not parse staging payload: {ex.Message}");
        }

        var portalDocs = payload?.Documents ?? [];
        if (portalDocs.Count == 0)
            return ApplicationResult<string>.Success("No portal documents found in the staging payload.");

        var existingFileNames = app.Documents
            .Where(d => d.Stage == NampDocumentStage.Origination)
            .Select(d => d.FileName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        int imported = 0, skipped = 0, failed = 0;

        foreach (var doc in portalDocs)
        {
            if (string.IsNullOrWhiteSpace(doc.S3BucketName) || string.IsNullOrWhiteSpace(doc.S3Key) || string.IsNullOrWhiteSpace(doc.FileName))
                continue;

            if (existingFileNames.Contains(doc.FileName))
            {
                skipped++;
                continue;
            }

            var docCategory = doc.DocumentType switch
            {
                "AuditedAccounts"              => NampDocumentCategory.FinancialModel,
                "BankStatement"                => NampDocumentCategory.CreditReport,
                "CertificateOfIncorporation"
                    or "CompanyProfile"
                    or "TaxClearanceCertificate"
                    or "ValidIdentification"   => NampDocumentCategory.SupportingDocument,
                _                              => NampDocumentCategory.General,
            };

            string storagePath;
            try
            {
                var fileBytes = await _nampPortalS3.DownloadAsync(doc.S3BucketName, doc.S3Key, ct);
                if (fileBytes is null)
                {
                    _logger.LogWarning("RetryDocImport: download returned null for '{FileName}' (S3Key: {S3Key}) — skipped.", doc.FileName, doc.S3Key);
                    failed++;
                    continue;
                }

                storagePath = await _fileStorage.UploadAsync(
                    containerName: $"namp/{app.Id}/documents",
                    fileName: doc.FileName,
                    content: fileBytes,
                    contentType: doc.ContentType ?? "application/octet-stream",
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RetryDocImport: failed to transfer '{FileName}' — skipped.", doc.FileName);
                failed++;
                continue;
            }

            var newDoc = app.UploadDocument(
                stage: NampDocumentStage.Origination,
                fileName: doc.FileName,
                contentType: doc.ContentType ?? "application/octet-stream",
                fileSize: doc.FileSizeBytes ?? 0,
                storagePath: storagePath,
                uploadedByUserId: Guid.Empty,
                category: docCategory,
                description: doc.DocumentTypeLabel);

            await _appRepo.AddNampDocumentAsync(newDoc, ct);
            imported++;
        }

        if (imported > 0)
            await _uow.SaveChangesAsync(ct);

        var summary = $"{imported} document(s) imported";
        if (skipped > 0) summary += $", {skipped} already present (skipped)";
        if (failed > 0) summary += $", {failed} failed (check S3 permissions)";

        return ApplicationResult<string>.Success(summary);
    }
}

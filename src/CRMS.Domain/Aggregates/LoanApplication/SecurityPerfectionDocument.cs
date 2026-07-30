using CRMS.Domain.Common;

namespace CRMS.Domain.Aggregates.LoanApplication;

public class SecurityPerfectionDocument : Entity
{
    public Guid ApplicationId { get; private set; }

    /// <summary>"LoanLevel" or "CollateralSpecific"</summary>
    public string Category { get; private set; } = string.Empty;

    public Guid? CollateralId { get; private set; }

    /// <summary>Snapshot of collateral description at upload time for display.</summary>
    public string CollateralDescription { get; private set; } = string.Empty;

    /// <summary>Free text — e.g. "Facility Agreement", "CAC Charge Certificate", "Lien Letter".</summary>
    public string DocumentType { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public string FileName { get; private set; } = string.Empty;
    public string StoragePath { get; private set; } = string.Empty;
    public long FileSizeBytes { get; private set; }
    public string ContentType { get; private set; } = string.Empty;

    public Guid UploadedByUserId { get; private set; }
    public DateTime UploadedAt { get; private set; }

    private SecurityPerfectionDocument() { }

    public static Result<SecurityPerfectionDocument> CreateLoanLevel(
        Guid applicationId,
        string documentType,
        string description,
        string fileName,
        string storagePath,
        long fileSizeBytes,
        string contentType,
        Guid uploadedByUserId)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return Result.Failure<SecurityPerfectionDocument>("File name is required");
        if (string.IsNullOrWhiteSpace(storagePath))
            return Result.Failure<SecurityPerfectionDocument>("Storage path is required");
        if (string.IsNullOrWhiteSpace(documentType))
            return Result.Failure<SecurityPerfectionDocument>("Document type is required");

        return Result.Success(new SecurityPerfectionDocument
        {
            ApplicationId = applicationId,
            Category = "LoanLevel",
            DocumentType = documentType,
            Description = description,
            FileName = fileName,
            StoragePath = storagePath,
            FileSizeBytes = fileSizeBytes,
            ContentType = contentType,
            UploadedByUserId = uploadedByUserId,
            UploadedAt = DateTime.UtcNow
        });
    }

    public static Result<SecurityPerfectionDocument> CreateCollateralSpecific(
        Guid applicationId,
        Guid collateralId,
        string collateralDescription,
        string documentType,
        string description,
        string fileName,
        string storagePath,
        long fileSizeBytes,
        string contentType,
        Guid uploadedByUserId)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return Result.Failure<SecurityPerfectionDocument>("File name is required");
        if (string.IsNullOrWhiteSpace(storagePath))
            return Result.Failure<SecurityPerfectionDocument>("Storage path is required");
        if (string.IsNullOrWhiteSpace(documentType))
            return Result.Failure<SecurityPerfectionDocument>("Document type is required");

        return Result.Success(new SecurityPerfectionDocument
        {
            ApplicationId = applicationId,
            Category = "CollateralSpecific",
            CollateralId = collateralId,
            CollateralDescription = collateralDescription,
            DocumentType = documentType,
            Description = description,
            FileName = fileName,
            StoragePath = storagePath,
            FileSizeBytes = fileSizeBytes,
            ContentType = contentType,
            UploadedByUserId = uploadedByUserId,
            UploadedAt = DateTime.UtcNow
        });
    }
}

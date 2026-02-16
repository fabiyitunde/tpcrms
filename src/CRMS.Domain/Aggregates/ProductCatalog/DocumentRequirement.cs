using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.ProductCatalog;

public class DocumentRequirement : Entity
{
    public Guid LoanProductId { get; private set; }
    public DocumentType DocumentType { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsMandatory { get; private set; }
    public int? MaxFileSizeMB { get; private set; }
    public string? AllowedExtensions { get; private set; }

    private DocumentRequirement() { }

    internal static Result<DocumentRequirement> Create(
        Guid loanProductId,
        DocumentType documentType,
        string name,
        bool isMandatory,
        int? maxFileSizeMB,
        string? allowedExtensions)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<DocumentRequirement>("Document name is required");

        if (maxFileSizeMB.HasValue && maxFileSizeMB <= 0)
            return Result.Failure<DocumentRequirement>("Max file size must be greater than 0");

        return Result.Success(new DocumentRequirement
        {
            LoanProductId = loanProductId,
            DocumentType = documentType,
            Name = name,
            IsMandatory = isMandatory,
            MaxFileSizeMB = maxFileSizeMB ?? 10,
            AllowedExtensions = allowedExtensions ?? ".pdf,.jpg,.jpeg,.png"
        });
    }

    public Result Update(
        string name,
        bool isMandatory,
        int? maxFileSizeMB,
        string? allowedExtensions)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure("Document name is required");

        if (maxFileSizeMB.HasValue && maxFileSizeMB <= 0)
            return Result.Failure("Max file size must be greater than 0");

        Name = name;
        IsMandatory = isMandatory;
        MaxFileSizeMB = maxFileSizeMB ?? 10;
        AllowedExtensions = allowedExtensions ?? ".pdf,.jpg,.jpeg,.png";

        return Result.Success();
    }

    public bool IsValidExtension(string fileName)
    {
        if (string.IsNullOrWhiteSpace(AllowedExtensions))
            return true;

        var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
        if (string.IsNullOrEmpty(extension))
            return false;

        var allowed = AllowedExtensions.Split(',').Select(e => e.Trim().ToLowerInvariant());
        return allowed.Contains(extension);
    }

    public bool IsValidFileSize(long fileSizeBytes)
    {
        if (!MaxFileSizeMB.HasValue)
            return true;

        var maxBytes = MaxFileSizeMB.Value * 1024 * 1024;
        return fileSizeBytes <= maxBytes;
    }
}

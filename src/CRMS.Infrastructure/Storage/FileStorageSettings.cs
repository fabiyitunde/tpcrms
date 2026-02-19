namespace CRMS.Infrastructure.Storage;

/// <summary>
/// Configuration settings for file storage.
/// </summary>
public class FileStorageSettings
{
    public const string SectionName = "FileStorage";

    /// <summary>
    /// Storage provider: "Local", "S3", "AzureBlob"
    /// </summary>
    public string Provider { get; set; } = "Local";

    /// <summary>
    /// Local file storage path (for Provider = "Local")
    /// </summary>
    public string? LocalPath { get; set; }

    /// <summary>
    /// S3 configuration (for Provider = "S3")
    /// </summary>
    public S3Settings? S3 { get; set; }
}

public class S3Settings
{
    /// <summary>
    /// S3 bucket name (required)
    /// </summary>
    public string BucketName { get; set; } = string.Empty;

    /// <summary>
    /// AWS region (e.g., "us-east-1", "eu-west-1")
    /// </summary>
    public string Region { get; set; } = "us-east-1";

    /// <summary>
    /// Optional service URL for S3-compatible services (MinIO, DigitalOcean Spaces, etc.)
    /// Leave empty for AWS S3.
    /// </summary>
    public string? ServiceUrl { get; set; }

    /// <summary>
    /// AWS Access Key ID (optional - can use IAM roles instead)
    /// </summary>
    public string? AccessKey { get; set; }

    /// <summary>
    /// AWS Secret Access Key (optional - can use IAM roles instead)
    /// </summary>
    public string? SecretKey { get; set; }

    /// <summary>
    /// Optional key prefix for all stored files
    /// </summary>
    public string? KeyPrefix { get; set; }
}

namespace CRMS.Domain.Interfaces;

/// <summary>
/// Abstraction for file storage operations.
/// Implementations can use local file system, S3, Azure Blob, MinIO, etc.
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Upload a file and return the storage path/key.
    /// </summary>
    Task<string> UploadAsync(string containerName, string fileName, byte[] content, string contentType, CancellationToken ct = default);
    
    /// <summary>
    /// Upload a file stream and return the storage path/key.
    /// </summary>
    Task<string> UploadAsync(string containerName, string fileName, Stream content, string contentType, CancellationToken ct = default);
    
    /// <summary>
    /// Download a file by its storage path/key.
    /// </summary>
    Task<byte[]> DownloadAsync(string storagePath, CancellationToken ct = default);
    
    /// <summary>
    /// Get a stream for reading a file.
    /// </summary>
    Task<Stream> GetStreamAsync(string storagePath, CancellationToken ct = default);
    
    /// <summary>
    /// Delete a file by its storage path/key.
    /// </summary>
    Task<bool> DeleteAsync(string storagePath, CancellationToken ct = default);
    
    /// <summary>
    /// Check if a file exists.
    /// </summary>
    Task<bool> ExistsAsync(string storagePath, CancellationToken ct = default);
    
    /// <summary>
    /// Generate a pre-signed URL for temporary access (for S3/Azure Blob).
    /// Returns null if not supported by the implementation.
    /// </summary>
    Task<string?> GetPresignedUrlAsync(string storagePath, TimeSpan expiry, CancellationToken ct = default);
}

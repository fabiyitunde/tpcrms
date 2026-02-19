using CRMS.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CRMS.Infrastructure.Storage;

/// <summary>
/// Local file system implementation of IFileStorageService.
/// For development and single-server deployments.
/// </summary>
public class LocalFileStorageService : IFileStorageService
{
    private readonly string _basePath;
    private readonly ILogger<LocalFileStorageService> _logger;

    public LocalFileStorageService(IConfiguration configuration, ILogger<LocalFileStorageService> logger)
    {
        _basePath = configuration["FileStorage:LocalPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "storage");
        _logger = logger;
        
        if (!Directory.Exists(_basePath))
        {
            Directory.CreateDirectory(_basePath);
        }
    }

    public async Task<string> UploadAsync(string containerName, string fileName, byte[] content, string contentType, CancellationToken ct = default)
    {
        var containerPath = Path.Combine(_basePath, containerName);
        if (!Directory.Exists(containerPath))
        {
            Directory.CreateDirectory(containerPath);
        }

        var uniqueFileName = $"{Guid.NewGuid():N}_{fileName}";
        var filePath = Path.Combine(containerPath, uniqueFileName);
        
        await File.WriteAllBytesAsync(filePath, content, ct);
        
        var storagePath = $"{containerName}/{uniqueFileName}";
        _logger.LogInformation("File uploaded to local storage: {StoragePath}", storagePath);
        
        return storagePath;
    }

    public async Task<string> UploadAsync(string containerName, string fileName, Stream content, string contentType, CancellationToken ct = default)
    {
        using var memoryStream = new MemoryStream();
        await content.CopyToAsync(memoryStream, ct);
        return await UploadAsync(containerName, fileName, memoryStream.ToArray(), contentType, ct);
    }

    public async Task<byte[]> DownloadAsync(string storagePath, CancellationToken ct = default)
    {
        var filePath = Path.Combine(_basePath, storagePath.Replace('/', Path.DirectorySeparatorChar));
        
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {storagePath}");
        }

        return await File.ReadAllBytesAsync(filePath, ct);
    }

    public Task<Stream> GetStreamAsync(string storagePath, CancellationToken ct = default)
    {
        var filePath = Path.Combine(_basePath, storagePath.Replace('/', Path.DirectorySeparatorChar));
        
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {storagePath}");
        }

        Stream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    public Task<bool> DeleteAsync(string storagePath, CancellationToken ct = default)
    {
        var filePath = Path.Combine(_basePath, storagePath.Replace('/', Path.DirectorySeparatorChar));
        
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            _logger.LogInformation("File deleted from local storage: {StoragePath}", storagePath);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task<bool> ExistsAsync(string storagePath, CancellationToken ct = default)
    {
        var filePath = Path.Combine(_basePath, storagePath.Replace('/', Path.DirectorySeparatorChar));
        return Task.FromResult(File.Exists(filePath));
    }

    public Task<string?> GetPresignedUrlAsync(string storagePath, TimeSpan expiry, CancellationToken ct = default)
    {
        // Local file storage doesn't support pre-signed URLs
        // Return null - caller should use DownloadAsync instead
        return Task.FromResult<string?>(null);
    }
}

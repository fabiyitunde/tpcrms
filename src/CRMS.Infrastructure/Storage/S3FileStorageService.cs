using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using CRMS.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CRMS.Infrastructure.Storage;

/// <summary>
/// AWS S3 implementation of IFileStorageService.
/// Also works with S3-compatible services like MinIO, DigitalOcean Spaces, etc.
/// </summary>
public class S3FileStorageService : IFileStorageService, IDisposable
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly string? _keyPrefix;
    private readonly ILogger<S3FileStorageService> _logger;
    private bool _disposed;

    public S3FileStorageService(IConfiguration configuration, ILogger<S3FileStorageService> logger)
    {
        _logger = logger;

        var s3Settings = configuration.GetSection("FileStorage:S3");
        
        _bucketName = s3Settings["BucketName"] 
            ?? throw new InvalidOperationException("S3 BucketName is required");
        _keyPrefix = s3Settings["KeyPrefix"]; // Optional prefix for all keys

        var serviceUrl = s3Settings["ServiceUrl"]; // For MinIO or other S3-compatible services
        var accessKey = s3Settings["AccessKey"];
        var secretKey = s3Settings["SecretKey"];
        var region = s3Settings["Region"] ?? "us-east-1";

        var config = new AmazonS3Config
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(region)
        };

        // For S3-compatible services (MinIO, etc.)
        if (!string.IsNullOrEmpty(serviceUrl))
        {
            config.ServiceURL = serviceUrl;
            config.ForcePathStyle = true; // Required for MinIO
        }

        if (!string.IsNullOrEmpty(accessKey) && !string.IsNullOrEmpty(secretKey))
        {
            _s3Client = new AmazonS3Client(accessKey, secretKey, config);
        }
        else
        {
            // Use default credentials (IAM role, environment variables, etc.)
            _s3Client = new AmazonS3Client(config);
        }

        _logger.LogInformation("S3 file storage initialized. Bucket: {Bucket}, Region: {Region}, ServiceUrl: {ServiceUrl}",
            _bucketName, region, serviceUrl ?? "AWS");
    }

    public async Task<string> UploadAsync(string containerName, string fileName, byte[] content, string contentType, CancellationToken ct = default)
    {
        using var stream = new MemoryStream(content);
        return await UploadAsync(containerName, fileName, stream, contentType, ct);
    }

    public async Task<string> UploadAsync(string containerName, string fileName, Stream content, string contentType, CancellationToken ct = default)
    {
        var key = BuildKey(containerName, fileName);

        try
        {
            using var transferUtility = new TransferUtility(_s3Client);
            
            var uploadRequest = new TransferUtilityUploadRequest
            {
                BucketName = _bucketName,
                Key = key,
                InputStream = content,
                ContentType = contentType,
                AutoCloseStream = false
            };

            await transferUtility.UploadAsync(uploadRequest, ct);

            _logger.LogInformation("File uploaded to S3: {Key}", key);
            
            return key;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Failed to upload file to S3: {Key}", key);
            throw new IOException($"Failed to upload file to S3: {ex.Message}", ex);
        }
    }

    public async Task<byte[]> DownloadAsync(string storagePath, CancellationToken ct = default)
    {
        try
        {
            var request = new GetObjectRequest
            {
                BucketName = _bucketName,
                Key = storagePath
            };

            using var response = await _s3Client.GetObjectAsync(request, ct);
            using var memoryStream = new MemoryStream();
            await response.ResponseStream.CopyToAsync(memoryStream, ct);
            
            return memoryStream.ToArray();
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new FileNotFoundException($"File not found in S3: {storagePath}");
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Failed to download file from S3: {Key}", storagePath);
            throw new IOException($"Failed to download file from S3: {ex.Message}", ex);
        }
    }

    public async Task<Stream> GetStreamAsync(string storagePath, CancellationToken ct = default)
    {
        try
        {
            var request = new GetObjectRequest
            {
                BucketName = _bucketName,
                Key = storagePath
            };

            var response = await _s3Client.GetObjectAsync(request, ct);
            return response.ResponseStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new FileNotFoundException($"File not found in S3: {storagePath}");
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Failed to get file stream from S3: {Key}", storagePath);
            throw new IOException($"Failed to get file stream from S3: {ex.Message}", ex);
        }
    }

    public async Task<bool> DeleteAsync(string storagePath, CancellationToken ct = default)
    {
        try
        {
            var request = new DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = storagePath
            };

            await _s3Client.DeleteObjectAsync(request, ct);
            _logger.LogInformation("File deleted from S3: {Key}", storagePath);
            
            return true;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Failed to delete file from S3: {Key}", storagePath);
            return false;
        }
    }

    public async Task<bool> ExistsAsync(string storagePath, CancellationToken ct = default)
    {
        try
        {
            var request = new GetObjectMetadataRequest
            {
                BucketName = _bucketName,
                Key = storagePath
            };

            await _s3Client.GetObjectMetadataAsync(request, ct);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Failed to check file existence in S3: {Key}", storagePath);
            throw;
        }
    }

    public Task<string?> GetPresignedUrlAsync(string storagePath, TimeSpan expiry, CancellationToken ct = default)
    {
        try
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = storagePath,
                Expires = DateTime.UtcNow.Add(expiry),
                Verb = HttpVerb.GET
            };

            var url = _s3Client.GetPreSignedURL(request);
            return Task.FromResult<string?>(url);
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Failed to generate pre-signed URL for S3: {Key}", storagePath);
            return Task.FromResult<string?>(null);
        }
    }

    private string BuildKey(string containerName, string fileName)
    {
        var uniqueFileName = $"{Guid.NewGuid():N}_{fileName}";
        
        if (!string.IsNullOrEmpty(_keyPrefix))
        {
            return $"{_keyPrefix}/{containerName}/{uniqueFileName}";
        }
        
        return $"{containerName}/{uniqueFileName}";
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _s3Client?.Dispose();
            _disposed = true;
        }
    }
}

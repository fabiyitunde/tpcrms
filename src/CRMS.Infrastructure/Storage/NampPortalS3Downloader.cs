using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using CRMS.Application.Namp.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CRMS.Infrastructure.Storage;

/// <summary>
/// Downloads documents from the NAMP portal's S3 bucket using separate credentials.
/// Config section: NampPortalS3 (BucketName, AccessKey, SecretKey, Region, ServiceUrl).
/// </summary>
public class NampPortalS3Downloader : INampPortalS3Downloader, IDisposable
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly ILogger<NampPortalS3Downloader> _logger;
    private bool _disposed;

    public NampPortalS3Downloader(IConfiguration configuration, ILogger<NampPortalS3Downloader> logger)
    {
        _logger = logger;

        var section = configuration.GetSection("NampPortalS3");
        _bucketName = section["BucketName"]
            ?? throw new InvalidOperationException("NampPortalS3:BucketName is required");

        var accessKey  = section["AccessKey"];
        var secretKey  = section["SecretKey"];
        var region     = section["Region"] ?? "us-east-1";
        var serviceUrl = section["ServiceUrl"];

        var config = new AmazonS3Config
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(region)
        };

        if (!string.IsNullOrEmpty(serviceUrl))
        {
            config.ServiceURL  = serviceUrl;
            config.ForcePathStyle = true;
        }

        _s3Client = !string.IsNullOrEmpty(accessKey) && !string.IsNullOrEmpty(secretKey)
            ? new AmazonS3Client(accessKey, secretKey, config)
            : new AmazonS3Client(config);

        _logger.LogInformation(
            "NAMP portal S3 downloader initialised. Bucket: {Bucket}, Region: {Region}",
            _bucketName, region);
    }

    public async Task<byte[]?> DownloadAsync(string s3Key, CancellationToken ct = default)
    {
        try
        {
            var request = new GetObjectRequest
            {
                BucketName = _bucketName,
                Key        = s3Key
            };

            using var response = await _s3Client.GetObjectAsync(request, ct);
            using var ms       = new MemoryStream();
            await response.ResponseStream.CopyToAsync(ms, ct);
            return ms.ToArray();
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("NAMP portal document not found in S3. Key: {S3Key}", s3Key);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download NAMP portal document. Key: {S3Key}", s3Key);
            return null;
        }
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

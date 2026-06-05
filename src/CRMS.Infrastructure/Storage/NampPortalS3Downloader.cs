using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using CRMS.Application.Namp.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CRMS.Infrastructure.Storage;

/// <summary>
/// Downloads documents from S3 using the host IAM role (Elastic Beanstalk instance profile).
/// No explicit credentials or bucket name are needed in config — the bucket name comes from
/// the per-document payload field at recall time.
/// Config section: NampPortalS3 (Region, ServiceUrl — both optional).
/// </summary>
public class NampPortalS3Downloader : INampPortalS3Downloader, IDisposable
{
    private readonly IAmazonS3 _s3Client;
    private readonly ILogger<NampPortalS3Downloader> _logger;
    private bool _disposed;

    public NampPortalS3Downloader(IConfiguration configuration, ILogger<NampPortalS3Downloader> logger)
    {
        _logger = logger;

        var section    = configuration.GetSection("NampPortalS3");
        var region     = section["Region"] ?? "eu-west-1";
        var serviceUrl = section["ServiceUrl"];

        var config = new AmazonS3Config
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(region)
        };

        if (!string.IsNullOrEmpty(serviceUrl))
        {
            config.ServiceURL     = serviceUrl;
            config.ForcePathStyle = true;
        }

        // Credentials come from the IAM instance profile — no keys required in config.
        _s3Client = new AmazonS3Client(config);

        _logger.LogInformation(
            "NAMP portal S3 downloader initialised (IAM role credentials). Region: {Region}", region);
    }

    public async Task<byte[]?> DownloadAsync(string bucketName, string s3Key, CancellationToken ct = default)
    {
        try
        {
            var request = new GetObjectRequest
            {
                BucketName = bucketName,
                Key        = s3Key
            };

            using var response = await _s3Client.GetObjectAsync(request, ct);
            using var ms       = new MemoryStream();
            await response.ResponseStream.CopyToAsync(ms, ct);
            return ms.ToArray();
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("NAMP portal document not found in S3. Bucket: {Bucket}, Key: {S3Key}", bucketName, s3Key);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download NAMP portal document. Bucket: {Bucket}, Key: {S3Key}", bucketName, s3Key);
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

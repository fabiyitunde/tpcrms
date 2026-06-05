namespace CRMS.Application.Namp.Interfaces;

/// <summary>
/// Downloads origination documents from an S3 bucket into CRMS storage at recall time.
/// The bucket name is taken from the per-document payload field so it is never hardcoded in config.
/// Credentials are sourced from the host IAM role (Elastic Beanstalk instance profile).
/// Returns null on any failure — callers should log and skip the document rather than aborting recall.
/// </summary>
public interface INampPortalS3Downloader
{
    Task<byte[]?> DownloadAsync(string bucketName, string s3Key, CancellationToken ct = default);
}

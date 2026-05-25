namespace CRMS.Application.Namp.Interfaces;

/// <summary>
/// Downloads origination documents from the NAMP portal's S3 bucket into CRMS storage at recall time.
/// The NAMP portal and CRMS use separate S3 buckets/accounts, so documents must be copied
/// at the moment of recall so that CRMS staff can open and download them during the loan process.
/// Returns null on any failure — callers should log and skip the document rather than aborting recall.
/// </summary>
public interface INampPortalS3Downloader
{
    Task<byte[]?> DownloadAsync(string s3Key, CancellationToken ct = default);
}

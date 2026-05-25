using CRMS.Application.Namp.Interfaces;
using Microsoft.Extensions.Logging;

namespace CRMS.Infrastructure.Storage;

/// <summary>
/// Mock implementation used in development. Returns null for every download so that recall
/// still completes — documents simply won't be attached. Set NampPortalS3:UseMock=false
/// and supply real credentials to enable actual downloads.
/// </summary>
public class MockNampPortalS3Downloader : INampPortalS3Downloader
{
    private readonly ILogger<MockNampPortalS3Downloader> _logger;

    public MockNampPortalS3Downloader(ILogger<MockNampPortalS3Downloader> logger)
    {
        _logger = logger;
    }

    public Task<byte[]?> DownloadAsync(string s3Key, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "MockNampPortalS3Downloader: skipping document download (mock mode). Key: {S3Key}", s3Key);
        return Task.FromResult<byte[]?>(null);
    }
}

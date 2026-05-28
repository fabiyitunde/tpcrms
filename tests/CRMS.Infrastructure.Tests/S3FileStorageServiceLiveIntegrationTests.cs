using CRMS.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit.Abstractions;

namespace CRMS.Infrastructure.Tests;

/// <summary>
/// Live integration tests for S3FileStorageService against the real crms-documents bucket.
///
/// Configuration (in order of priority):
///   1. Environment variables
///   2. appsettings.test.json
///
/// Required:
///   FileStorage:S3:BucketName  — e.g. "crms-documents"
///   FileStorage:S3:Region      — e.g. "eu-west-1"
///
/// Optional (omit to use IAM role / AWS CLI default credentials):
///   FileStorage:S3:AccessKey
///   FileStorage:S3:SecretKey
///
/// Tests are skipped if BucketName is not configured.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "LiveAPI")]
public class S3FileStorageServiceLiveIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly S3FileStorageService? _service;
    private readonly string _bucketName;
    private readonly List<string> _uploadedKeys = [];

    public S3FileStorageServiceLiveIntegrationTests(ITestOutputHelper output)
    {
        _output = output;

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.test.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        _bucketName = configuration["FileStorage:S3:BucketName"] ?? "";

        if (string.IsNullOrEmpty(_bucketName))
        {
            _output.WriteLine("FileStorage:S3:BucketName not configured — live S3 tests will be skipped");
            return;
        }

        var logger = new Mock<ILogger<S3FileStorageService>>().Object;
        _service = new S3FileStorageService(configuration, logger);
        _output.WriteLine($"Initialized S3FileStorageService. Bucket: {_bucketName}");
    }

    private void SkipIfNotConfigured() =>
        Skip.If(_service is null, "FileStorage:S3:BucketName not configured");

    // ── Upload / Download ────────────────────────────────────────────────────

    [SkippableFact]
    public async Task UploadAsync_ThenDownloadAsync_RoundTripsContent()
    {
        SkipIfNotConfigured();

        var content = "Hello from CRMS S3 integration test"u8.ToArray();
        var key = await _service!.UploadAsync("test-container", "roundtrip.txt", content, "text/plain");
        _uploadedKeys.Add(key);

        _output.WriteLine($"Uploaded key: {key}");

        var downloaded = await _service.DownloadAsync(key);

        Assert.Equal(content, downloaded);
        _output.WriteLine($"Downloaded {downloaded.Length} bytes — content matches ✓");
    }

    [SkippableFact]
    public async Task UploadAsync_Stream_ThenDownloadAsync_RoundTripsContent()
    {
        SkipIfNotConfigured();

        var content = "Stream upload test content"u8.ToArray();
        using var stream = new MemoryStream(content);

        var key = await _service!.UploadAsync("test-container", "stream-upload.txt", stream, "text/plain");
        _uploadedKeys.Add(key);

        _output.WriteLine($"Uploaded key: {key}");

        var downloaded = await _service.DownloadAsync(key);
        Assert.Equal(content, downloaded);
        _output.WriteLine($"Stream upload/download round-trip ✓");
    }

    // ── Exists ───────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task ExistsAsync_AfterUpload_ReturnsTrue()
    {
        SkipIfNotConfigured();

        var content = "exists check"u8.ToArray();
        var key = await _service!.UploadAsync("test-container", "exists-check.txt", content, "text/plain");
        _uploadedKeys.Add(key);

        var exists = await _service.ExistsAsync(key);
        Assert.True(exists, $"Expected key to exist: {key}");
        _output.WriteLine($"ExistsAsync returned true for {key} ✓");
    }

    [SkippableFact]
    public async Task ExistsAsync_ForNonExistentKey_ReturnsFalse()
    {
        SkipIfNotConfigured();

        var fakeKey = $"test-container/nonexistent-{Guid.NewGuid():N}.txt";
        var exists = await _service!.ExistsAsync(fakeKey);
        Assert.False(exists);
        _output.WriteLine($"ExistsAsync correctly returned false for non-existent key ✓");
    }

    // ── Delete ───────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task DeleteAsync_ExistingKey_RemovesFromBucket()
    {
        SkipIfNotConfigured();

        var content = "to be deleted"u8.ToArray();
        var key = await _service!.UploadAsync("test-container", "delete-me.txt", content, "text/plain");

        _output.WriteLine($"Uploaded: {key}");

        var deleted = await _service.DeleteAsync(key);
        Assert.True(deleted, "DeleteAsync should return true");

        var stillExists = await _service.ExistsAsync(key);
        Assert.False(stillExists, "File should no longer exist after delete");

        _output.WriteLine($"Deleted and confirmed gone ✓");
        // No need to add to _uploadedKeys — already deleted
    }

    // ── GetStream ────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task GetStreamAsync_ReturnsReadableStream()
    {
        SkipIfNotConfigured();

        var content = "stream download test"u8.ToArray();
        var key = await _service!.UploadAsync("test-container", "stream-download.txt", content, "text/plain");
        _uploadedKeys.Add(key);

        await using var stream = await _service.GetStreamAsync(key);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);

        Assert.Equal(content, ms.ToArray());
        _output.WriteLine($"GetStreamAsync returned correct content ✓");
    }

    // ── List ─────────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task ListFilesAsync_AfterUpload_ContainsUploadedKey()
    {
        SkipIfNotConfigured();

        var content = "list files test"u8.ToArray();
        var key = await _service!.UploadAsync("test-list-container", "list-test.txt", content, "text/plain");
        _uploadedKeys.Add(key);

        var files = (await _service.ListFilesAsync("test-list-container")).ToList();

        _output.WriteLine($"Listed {files.Count} file(s) in test-list-container");
        foreach (var f in files)
            _output.WriteLine($"  {f}");

        Assert.Contains(key, files);
        _output.WriteLine($"Uploaded key found in listing ✓");
    }

    // ── Error cases ──────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task DownloadAsync_NonExistentKey_ThrowsFileNotFoundException()
    {
        SkipIfNotConfigured();

        var fakeKey = $"test-container/nonexistent-{Guid.NewGuid():N}.txt";

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _service!.DownloadAsync(fakeKey));

        _output.WriteLine($"DownloadAsync correctly threw FileNotFoundException ✓");
    }

    [SkippableFact]
    public async Task GetStreamAsync_NonExistentKey_ThrowsFileNotFoundException()
    {
        SkipIfNotConfigured();

        var fakeKey = $"test-container/nonexistent-{Guid.NewGuid():N}.txt";

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _service!.GetStreamAsync(fakeKey));

        _output.WriteLine($"GetStreamAsync correctly threw FileNotFoundException ✓");
    }

    // ── Full round-trip ──────────────────────────────────────────────────────

    [SkippableFact]
    public async Task FullRoundTrip_UploadExistsDownloadDelete()
    {
        SkipIfNotConfigured();

        _output.WriteLine("=== Full S3 Round-Trip Test ===");

        var content = "Full round-trip test content — CRMS S3"u8.ToArray();

        _output.WriteLine("\n1. Uploading...");
        var key = await _service!.UploadAsync("test-container", "full-roundtrip.txt", content, "text/plain");
        _output.WriteLine($"   Key: {key}");

        _output.WriteLine("\n2. Checking existence...");
        Assert.True(await _service.ExistsAsync(key));
        _output.WriteLine("   Exists: true ✓");

        _output.WriteLine("\n3. Downloading...");
        var downloaded = await _service.DownloadAsync(key);
        Assert.Equal(content, downloaded);
        _output.WriteLine($"   Downloaded {downloaded.Length} bytes, content matches ✓");

        _output.WriteLine("\n4. Deleting...");
        Assert.True(await _service.DeleteAsync(key));
        _output.WriteLine("   Deleted ✓");

        _output.WriteLine("\n5. Confirming gone...");
        Assert.False(await _service.ExistsAsync(key));
        _output.WriteLine("   Exists: false ✓");

        _output.WriteLine("\n=== Round-Trip Complete ===");
    }

    // ── Cleanup ──────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_service is null || _uploadedKeys.Count == 0) return;

        foreach (var key in _uploadedKeys)
        {
            try { _service.DeleteAsync(key).GetAwaiter().GetResult(); }
            catch { /* best-effort cleanup */ }
        }

        _service.Dispose();
    }
}

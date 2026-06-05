using System.Diagnostics;
using CRMS.Domain.Interfaces;
using CRMS.Infrastructure.ExternalServices.Namp;
using CRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CRMS.API.Controllers;

/// <summary>
/// Non-destructive connectivity probe for every external dependency.
/// Auth: X-Api-Key header (same key as the NAMP webhook).
/// GET /api/health/connectivity
/// </summary>
[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    private readonly IFileStorageService _storage;
    private readonly ICoreBankingService _coreBanking;
    private readonly IFineractDirectService _fineract;
    private readonly CRMSDbContext _db;
    private readonly NampSettings _nampSettings;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        IFileStorageService storage,
        ICoreBankingService coreBanking,
        IFineractDirectService fineract,
        CRMSDbContext db,
        IOptions<NampSettings> nampSettings,
        ILogger<HealthController> logger)
    {
        _storage = storage;
        _coreBanking = coreBanking;
        _fineract = fineract;
        _db = db;
        _nampSettings = nampSettings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Probes S3, CoreBanking, and Fineract without writing any permanent data.
    /// S3: upload a 1-byte sentinel file → verify exists → delete.
    /// CoreBanking: attempt an account lookup (any response = reachable).
    /// Fineract: fetch loan product list (read-only).
    /// Returns HTTP 200 with per-service pass/fail regardless of individual probe outcomes.
    /// </summary>
    [HttpGet("connectivity")]
    public async Task<IActionResult> CheckConnectivity(CancellationToken ct)
    {
        // Auth: same constant-time API key check as the webhook
        if (!string.IsNullOrEmpty(_nampSettings.ApiKey))
        {
            Request.Headers.TryGetValue("X-Api-Key", out var provided);
            if (!CryptographicEquals(_nampSettings.ApiKey, provided.ToString()))
            {
                _logger.LogWarning("Health/connectivity: rejected request from {Ip} — invalid API key",
                    HttpContext.Connection.RemoteIpAddress);
                return Unauthorized("Invalid or missing API key.");
            }
        }

        var probes = new List<ProbeResult>();

        probes.Add(await ProbeDatabaseAsync(ct));
        probes.Add(await ProbeS3Async(ct));
        probes.Add(await ProbeCoreBankingAsync(ct));
        probes.Add(await ProbeFineractAsync(ct));

        var allOk = probes.All(p => p.Status == "ok");

        _logger.LogInformation("Health/connectivity: {Passed}/{Total} probes passed",
            probes.Count(p => p.Status == "ok"), probes.Count);

        return Ok(new
        {
            timestamp = DateTime.UtcNow,
            overall = allOk ? "ok" : "degraded",
            probes
        });
    }

    // ── Probes ────────────────────────────────────────────────────────────────

    private async Task<ProbeResult> ProbeDatabaseAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var canConnect = await _db.Database.CanConnectAsync(ct);
            sw.Stop();
            return canConnect
                ? new ProbeResult("Database", "ok", $"Connected to MySQL/RDS in {sw.ElapsedMilliseconds}ms")
                : new ProbeResult("Database", "error", $"CanConnectAsync returned false after {sw.ElapsedMilliseconds}ms");
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "Health/connectivity: Database probe failed");
            return new ProbeResult("Database", "error", Summarise(ex));
        }
    }

    private async Task<ProbeResult> ProbeS3Async(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var key = $"diagnostics/ping-{DateTime.UtcNow:yyyyMMddHHmmss}.txt";

        try
        {
            // Upload
            var storedKey = await _storage.UploadAsync(
                containerName: "diagnostics",
                fileName: $"ping-{DateTime.UtcNow:yyyyMMddHHmmss}.txt",
                content: new byte[] { 0x50 }, // 'P'
                contentType: "text/plain",
                ct: ct);

            // Verify
            var exists = await _storage.ExistsAsync(storedKey, ct);

            // Cleanup — non-fatal if this fails
            try { await _storage.DeleteAsync(storedKey, ct); } catch { /* ignore */ }

            sw.Stop();

            return exists
                ? new ProbeResult("S3", "ok", $"Upload → exists → delete succeeded in {sw.ElapsedMilliseconds}ms")
                : new ProbeResult("S3", "error", $"File uploaded but ExistsAsync returned false (key: {storedKey})");
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "Health/connectivity: S3 probe failed");
            return new ProbeResult("S3", "error", Summarise(ex));
        }
    }

    private async Task<ProbeResult> ProbeCoreBankingAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            // GetAccountInfoAsync hits tpgateway (the real CoreBanking service).
            // Any structured response (success or 404) proves OAuth2 + connectivity are working.
            var result = await _coreBanking.GetAccountInfoAsync("0000000029", ct);
            sw.Stop();

            if (result.IsSuccess)
                return new ProbeResult("CoreBanking", "ok",
                    $"Account lookup responded in {sw.ElapsedMilliseconds}ms (account: {result.Value.AccountNumber})");

            // A structured error from the remote API = reachable
            if (IsNetworkError(result.Error))
                return new ProbeResult("CoreBanking", "error",
                    $"Network/connectivity failure in {sw.ElapsedMilliseconds}ms: {result.Error}");

            return new ProbeResult("CoreBanking", "ok",
                $"Reachable in {sw.ElapsedMilliseconds}ms (API responded: {result.Error})");
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "Health/connectivity: CoreBanking probe failed");
            return new ProbeResult("CoreBanking", "error", Summarise(ex));
        }
    }

    private async Task<ProbeResult> ProbeFineractAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            // GET /loanproducts — pure read, no side-effects
            var result = await _fineract.GetLoanProductsAsync(activeOnly: false, ct);
            sw.Stop();

            if (result.IsSuccess)
                return new ProbeResult("Fineract", "ok",
                    $"{result.Value.Count} loan product(s) returned in {sw.ElapsedMilliseconds}ms");

            if (IsNetworkError(result.Error))
                return new ProbeResult("Fineract", "error",
                    $"Network/connectivity failure in {sw.ElapsedMilliseconds}ms: {result.Error}");

            return new ProbeResult("Fineract", "ok",
                $"Reachable in {sw.ElapsedMilliseconds}ms (API responded: {result.Error})");
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "Health/connectivity: Fineract probe failed");
            return new ProbeResult("Fineract", "error", Summarise(ex));
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Heuristic: does the error message look like a network-level failure
    /// rather than a business-level API response?
    /// </summary>
    private static bool IsNetworkError(string? error)
    {
        if (string.IsNullOrWhiteSpace(error)) return false;
        var lower = error.ToLowerInvariant();
        return lower.Contains("timeout")
            || lower.Contains("timed out")
            || lower.Contains("connection refused")
            || lower.Contains("connection reset")
            || lower.Contains("no such host")
            || lower.Contains("name resolution")
            || lower.Contains("unreachable")
            || lower.Contains("ssl")
            || lower.Contains("handshake");
    }

    private static string Summarise(Exception ex) =>
        ex.InnerException is not null
            ? $"{ex.GetType().Name}: {ex.InnerException.Message}"
            : $"{ex.GetType().Name}: {ex.Message}";

    /// <summary>Constant-time string comparison to prevent timing attacks.</summary>
    private static bool CryptographicEquals(string expected, string provided)
    {
        if (expected.Length != provided.Length) return false;
        int result = 0;
        for (int i = 0; i < expected.Length; i++)
            result |= expected[i] ^ provided[i];
        return result == 0;
    }

    private record ProbeResult(string Service, string Status, string Detail);
}

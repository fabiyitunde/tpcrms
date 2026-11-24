using System.Diagnostics;
using System.Text.Json;
using CRMS.Application.CreditCheck;
using CRMS.Application.CustomerCheck;
using CRMS.Domain.Aggregates.Billing;
using CRMS.Domain.Aggregates.CreditCheck;
using CRMS.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CRMS.Infrastructure.BackgroundServices;

/// <summary>Wakes the staff-check worker when a check is queued; extra nudges while one is pending
/// are dropped, since the worker drains the queue when it wakes.</summary>
public class StaffCreditCheckSignal : IStaffCreditCheckSignal
{
    private readonly SemaphoreSlim _gate = new(0, 1);

    public void Nudge()
    {
        try { _gate.Release(); }
        catch (SemaphoreFullException) { }
    }

    public async Task WaitAsync(TimeSpan timeout, CancellationToken ct)
    {
        try { await _gate.WaitAsync(timeout, ct); }
        catch (OperationCanceledException) { }
    }
}

/// <summary>
/// Runs one claimed staff check: decrypts the request, runs the individual or company check for
/// the member of staff who asked, and stores the encrypted result. Third-party calls are billed
/// under "Other lookups" with the staff member as the trigger, never under the Partner API, and
/// nothing is written to the partner tables or the bureau store partners reuse from.
/// </summary>
public class StaffCreditCheckRunner
{
    private readonly IStaffCreditCheckRepository _checks;
    private readonly ICustomerCheckService _customerCheck;
    private readonly ICompanyCheckService _companyCheck;
    private readonly ICustomerCheckCipher _cipher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CustomerCheckSettings _settings;
    private readonly ILogger<StaffCreditCheckRunner> _logger;

    public StaffCreditCheckRunner(
        IStaffCreditCheckRepository checks, ICustomerCheckService customerCheck, ICompanyCheckService companyCheck,
        ICustomerCheckCipher cipher, IUnitOfWork unitOfWork, IOptions<CustomerCheckSettings> settings,
        ILogger<StaffCreditCheckRunner> logger)
    {
        _checks = checks;
        _customerCheck = customerCheck;
        _companyCheck = companyCheck;
        _cipher = cipher;
        _unitOfWork = unitOfWork;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>Who a staff check runs as: every section, no reuse of stored reports (staff expect
    /// a fresh pull), nothing stored for partners, billed to "Other lookups".</summary>
    public static CustomerCheckRequester RequesterFor(StaffCreditCheck check, IEnumerable<string> sections) => new(
        $"CRMS staff · {check.RequestedByName}",
        sections.ToHashSet(),
        ReuseBureauReports: false,
        ReuseMaxAgeDays: 0,
        StoreBureauReports: false,
        check.RequestedByUserId,
        BillingFlow.Other,
        $"Staff credit check · {check.RequestedByName}",
        check.RequestedByUserId);

    public async Task RunAsync(Guid checkId, CancellationToken stopping)
    {
        var check = await _checks.GetByIdAsync(checkId, stopping);
        if (check is null || check.Status != StaffCreditCheck.Processing)
            return;

        if (check.RunAttempts > _settings.MaxRunAttempts)
        {
            check.Fail("The check was interrupted by a restart more than once. Run it again.", 0, DateTime.UtcNow);
            await SaveAsync(check, stopping);
            return;
        }

        var watch = Stopwatch.StartNew();
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(stopping);
            cts.CancelAfter(_settings.JobTimeoutMs);
            var json = _cipher.Unprotect(check.RequestCiphertext);

            if (check.SubjectType == StaffCreditCheck.Company)
            {
                var query = JsonSerializer.Deserialize<CompanyCheckQuery>(json, StaffCreditChecks.Json)
                    ?? throw new InvalidOperationException("The stored request could not be read.");
                var outcome = await _companyCheck.CheckAsync(RequesterFor(check, CompanyCheckSections.All), query, cts.Token);
                var r = outcome.Response;
                var advisory = r.AiAdvisory;
                var scored = advisory.Status == SectionStatuses.Ok;
                check.Complete(
                    _cipher.Protect(JsonSerializer.Serialize(r, StaffCreditChecks.Json)),
                    [
                        new(CompanyCheckSections.CompanyRegistration, r.CompanyRegistration.Status),
                        new(CompanyCheckSections.CreditBureau, r.CreditBureau.Status),
                        new(CompanyCheckSections.FraudCheck, r.FraudCheck.Status),
                        new(CompanyCheckSections.BankAccounts, r.BankAccounts.Status),
                        new(CompanyCheckSections.BankLoans, r.BankLoans.Status),
                        new(CompanyCheckSections.CrmsFacilities, r.CrmsFacilities.Status),
                        new(CompanyCheckSections.AiAdvisory, r.AiAdvisory.Status),
                    ],
                    r.CompanyRegistration.CompanyName ?? query.CompanyName, outcome.BureauUsed, outcome.BilledCalls,
                    Elapsed(watch), DateTime.UtcNow,
                    scored ? advisory.OverallScore : null, scored ? advisory.OverallRating : null, scored ? advisory.Recommendation : null,
                    r.Warnings.Count);
            }
            else
            {
                var query = JsonSerializer.Deserialize<CustomerCheckQuery>(json, StaffCreditChecks.Json)
                    ?? throw new InvalidOperationException("The stored request could not be read.");
                var outcome = await _customerCheck.CheckAsync(RequesterFor(check, CustomerCheckSections.All), query, cts.Token);
                var r = outcome.Response;
                var advisory = r.AiAdvisory;
                var scored = advisory.Status == SectionStatuses.Ok;
                check.Complete(
                    _cipher.Protect(JsonSerializer.Serialize(r, StaffCreditChecks.Json)),
                    CustomerCheckJobRunner.Outcomes(r),
                    r.Identity.FullName, outcome.BureauUsed, outcome.BilledCalls,
                    Elapsed(watch), DateTime.UtcNow,
                    scored ? advisory.OverallScore : null, scored ? advisory.OverallRating : null, scored ? advisory.Recommendation : null,
                    r.Warnings.Count);
            }
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            // Shutting down mid-check: leave it PROCESSING so it is claimed again after the restart.
            throw;
        }
        catch (OperationCanceledException)
        {
            check.Fail("The check did not finish within the time allowed. Run it again, or with fewer sections.", Elapsed(watch), DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Staff credit check {CheckId} failed.", check.Id);
            check.Fail("The check could not be completed.", Elapsed(watch), DateTime.UtcNow);
        }

        await SaveAsync(check, stopping);
        _logger.LogInformation("Staff credit check {CheckId} ({Type}) {Status} in {Ms} ms.", check.Id, check.SubjectType, check.Status, check.DurationMs);
    }

    private async Task SaveAsync(StaffCreditCheck check, CancellationToken ct)
    {
        _checks.Update(check);
        try { await _unitOfWork.SaveChangesAsync(ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Staff credit check {CheckId}: the result could not be saved; it will be run again.", check.Id);
        }
    }

    private static int Elapsed(Stopwatch watch) => (int)Math.Min(int.MaxValue, watch.ElapsedMilliseconds);
}

/// <summary>
/// The staff checks' own poll loop, hosted only by the intranet: the API host's partner worker
/// never sees these rows, and these never take one of its slots.
/// </summary>
public class StaffCreditCheckWorker : BackgroundService
{
    private const int Concurrency = 2;

    private readonly IServiceScopeFactory _scopes;
    private readonly StaffCreditCheckSignal _signal;
    private readonly CustomerCheckSettings _settings;
    private readonly ILogger<StaffCreditCheckWorker> _logger;

    public StaffCreditCheckWorker(
        IServiceScopeFactory scopes, StaffCreditCheckSignal signal,
        IOptions<CustomerCheckSettings> settings, ILogger<StaffCreditCheckWorker> logger)
    {
        _scopes = scopes;
        _signal = signal;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Staff credit check worker started.");
        var running = new List<Task>();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                running.RemoveAll(t => t.IsCompleted);
                while (running.Count < Concurrency)
                {
                    var id = await ClaimAsync(stoppingToken);
                    if (id is null) break;
                    running.Add(RunOneAsync(id.Value, stoppingToken));
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Staff credit check worker: error in a polling pass.");
            }

            await _signal.WaitAsync(TimeSpan.FromSeconds(Math.Max(1, _settings.WorkerPollSeconds)), stoppingToken);
        }

        try { await Task.WhenAll(running); }
        catch (Exception) { }
        _logger.LogInformation("Staff credit check worker stopped.");
    }

    private async Task<Guid?> ClaimAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var checks = scope.ServiceProvider.GetRequiredService<IStaffCreditCheckRepository>();
        var now = DateTime.UtcNow;
        return await checks.ClaimNextAsync(now, now.AddMinutes(-Math.Max(1, _settings.StaleProcessingMinutes)), ct);
    }

    private async Task RunOneAsync(Guid checkId, CancellationToken ct)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<StaffCreditCheckRunner>().RunAsync(checkId, ct);
            _signal.Nudge();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Staff credit check worker: check {CheckId} threw.", checkId);
        }
    }
}

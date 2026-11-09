using CRMS.Application.Billing;
using CRMS.Domain.Aggregates.Billing;
using CRMS.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CRMS.Infrastructure.Services.Billing;

/// <summary>
/// Writes each usage row in a scope of its own, so the row is committed the moment the call is
/// known to have happened, whatever the surrounding unit of work goes on to do. Never throws: a
/// billing failure is logged loudly and the business operation continues.
/// </summary>
public sealed class BillingLedger : IBillingLedger
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<BillingLedger> _logger;

    public BillingLedger(IServiceScopeFactory scopes, ILogger<BillingLedger> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    public async Task RecordAsync(BillingUsageDraft draft, CancellationToken ct = default)
    {
        try
        {
            var scope = draft.Scope ?? BillingContext.Current ?? new BillingScope(BillingFlow.Unattributed, null, null);
            var created = BillingUsage.Create(
                draft.Item, scope.Flow, scope.CaseId, scope.CaseReference, scope.SubjectType, draft.SubjectHint,
                draft.Succeeded, draft.Detail, Environment.MachineName, scope.TriggeredBy,
                draft.OccurredAtUtc ?? DateTime.UtcNow, draft.Source, draft.SourceKey, draft.DurationMs);
            if (created.IsFailure)
            {
                _logger.LogError("Billing row for {Item} not recorded: {Error}", draft.Item, created.Error);
                return;
            }

            using var sp = _scopes.CreateScope();
            var db = sp.ServiceProvider.GetRequiredService<CRMSDbContext>();
            db.BillingUsages.Add(created.Value);
            // Not the caller's token: a cancelled request has still made (and paid for) the call.
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Billing row for {Item} could not be written", draft.Item);
        }
    }
}

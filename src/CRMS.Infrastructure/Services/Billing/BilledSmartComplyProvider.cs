using System.Diagnostics;
using CRMS.Application.Billing;
using CRMS.Domain.Aggregates.Billing;
using CRMS.Domain.Common;
using CRMS.Domain.Interfaces;

namespace CRMS.Infrastructure.Services.Billing;

/// <summary>
/// Sits in front of the SmartComply client, real or mock, and writes one usage row per operation,
/// whatever the answer was. Every caller in both hosts goes through this, including the background
/// bureau workers, so no call is missed and no call site has to remember to bill. The access check
/// passes straight through: it sends no subject and SmartComply does not charge for it.
/// </summary>
public sealed class BilledSmartComplyProvider : ISmartComplyProvider
{
    private readonly ISmartComplyProvider _inner;
    private readonly IBillingLedger _ledger;

    public BilledSmartComplyProvider(ISmartComplyProvider inner, IBillingLedger ledger)
    {
        _inner = inner;
        _ledger = ledger;
    }

    public ISmartComplyProvider Inner => _inner;

    private async Task<Result<T>> Bill<T>(string item, string? hint, Func<Task<Result<T>>> call)
    {
        var sw = Stopwatch.StartNew();
        Result<T> result;
        try
        {
            result = await call();
        }
        catch (Exception ex)
        {
            sw.Stop();
            await _ledger.RecordAsync(new BillingUsageDraft(item, false, ex.Message, hint, (int)sw.ElapsedMilliseconds));
            throw;
        }
        sw.Stop();
        await _ledger.RecordAsync(new BillingUsageDraft(item, result.IsSuccess, result.IsSuccess ? null : result.Error, hint, (int)sw.ElapsedMilliseconds));
        return result;
    }

    // Individual credit reports
    public Task<Result<SmartComplyIndividualCreditReport>> GetFirstCentralSummaryAsync(string bvn, CancellationToken ct = default) =>
        Bill(BillingItems.FirstCentralSummary, BillingHints.Last4(bvn), () => _inner.GetFirstCentralSummaryAsync(bvn, ct));
    public Task<Result<SmartComplyIndividualCreditReport>> GetFirstCentralFullAsync(string bvn, CancellationToken ct = default) =>
        Bill(BillingItems.FirstCentralFull, BillingHints.Last4(bvn), () => _inner.GetFirstCentralFullAsync(bvn, ct));
    public Task<Result<SmartComplyCreditScore>> GetFirstCentralScoreAsync(string bvn, CancellationToken ct = default) =>
        Bill(BillingItems.FirstCentralScore, BillingHints.Last4(bvn), () => _inner.GetFirstCentralScoreAsync(bvn, ct));
    public Task<Result<SmartComplyIndividualCreditReport>> GetCreditRegistrySummaryAsync(string bvn, CancellationToken ct = default) =>
        Bill(BillingItems.CreditRegistrySummary, BillingHints.Last4(bvn), () => _inner.GetCreditRegistrySummaryAsync(bvn, ct));
    public Task<Result<SmartComplyIndividualCreditReport>> GetCreditRegistryFullAsync(string bvn, CancellationToken ct = default) =>
        Bill(BillingItems.CreditRegistryFull, BillingHints.Last4(bvn), () => _inner.GetCreditRegistryFullAsync(bvn, ct));
    public Task<Result<SmartComplyIndividualCreditReport>> GetCreditRegistryAdvancedAsync(string bvn, CancellationToken ct = default) =>
        Bill(BillingItems.CreditRegistryAdvanced, BillingHints.Last4(bvn), () => _inner.GetCreditRegistryAdvancedAsync(bvn, ct));
    public Task<Result<SmartComplyCreditScore>> GetCRCScoreAsync(string bvn, CancellationToken ct = default) =>
        Bill(BillingItems.CrcScore, BillingHints.Last4(bvn), () => _inner.GetCRCScoreAsync(bvn, ct));
    public Task<Result<SmartComplyIndividualCreditReport>> GetCRCHistoryAsync(string bvn, CancellationToken ct = default) =>
        Bill(BillingItems.CrcHistory, BillingHints.Last4(bvn), () => _inner.GetCRCHistoryAsync(bvn, ct));
    public Task<Result<SmartComplyIndividualCreditReport>> GetCRCFullAsync(string bvn, CancellationToken ct = default) =>
        Bill(BillingItems.CrcFull, BillingHints.Last4(bvn), () => _inner.GetCRCFullAsync(bvn, ct));
    public Task<Result<SmartComplyIndividualCreditReport>> GetCreditPremiumAsync(string bvn, CancellationToken ct = default) =>
        Bill(BillingItems.Premium, BillingHints.Last4(bvn), () => _inner.GetCreditPremiumAsync(bvn, ct));

    // Business credit reports
    public Task<Result<SmartComplyBusinessCreditReport>> GetCRCBusinessHistoryAsync(string rcNumber, CancellationToken ct = default) =>
        Bill(BillingItems.BusinessCrc, BillingHints.Rc(rcNumber), () => _inner.GetCRCBusinessHistoryAsync(rcNumber, ct));
    public Task<Result<SmartComplyBusinessCreditReport>> GetFirstCentralBusinessAsync(string rcNumber, CancellationToken ct = default) =>
        Bill(BillingItems.BusinessFirstCentral, BillingHints.Rc(rcNumber), () => _inner.GetFirstCentralBusinessAsync(rcNumber, ct));
    public Task<Result<SmartComplyBusinessCreditReport>> GetPremiumBusinessAsync(string rcNumber, CancellationToken ct = default) =>
        Bill(BillingItems.BusinessPremium, BillingHints.Rc(rcNumber), () => _inner.GetPremiumBusinessAsync(rcNumber, ct));
    public Task<Result<string>> GetBusinessCreditRawJsonAsync(string bureau, string rcNumber, CancellationToken ct = default) =>
        Bill(BusinessItem(bureau), BillingHints.Rc(rcNumber), () => _inner.GetBusinessCreditRawJsonAsync(bureau, rcNumber, ct));

    public static string BusinessItem(string bureau) => bureau?.Trim().ToLowerInvariant() switch
    {
        "first_central" or "firstcentral" => BillingItems.BusinessFirstCentral,
        "premium" => BillingItems.BusinessPremium,
        _ => BillingItems.BusinessCrc,
    };

    // Loan fraud
    public Task<Result<SmartComplyLoanFraudResult>> CheckIndividualLoanFraudAsync(SmartComplyIndividualLoanRequest request, CancellationToken ct = default) =>
        Bill(BillingItems.FraudIndividual, BillingHints.Last4(request.Bvn), () => _inner.CheckIndividualLoanFraudAsync(request, ct));
    public Task<Result<SmartComplyLoanFraudResult>> CheckBusinessLoanFraudAsync(SmartComplyBusinessLoanRequest request, CancellationToken ct = default) =>
        Bill(BillingItems.FraudBusiness, BillingHints.Rc(request.RcNumber), () => _inner.CheckBusinessLoanFraudAsync(request, ct));

    // Identity
    public Task<Result<SmartComplyBvnResult>> VerifyBvnAsync(string bvn, CancellationToken ct = default) =>
        Bill(BillingItems.Bvn, BillingHints.Last4(bvn), () => _inner.VerifyBvnAsync(bvn, ct));
    public Task<Result<SmartComplyBvnAdvancedResult>> VerifyBvnAdvancedAsync(string bvn, CancellationToken ct = default) =>
        Bill(BillingItems.BvnAdvanced, BillingHints.Last4(bvn), () => _inner.VerifyBvnAdvancedAsync(bvn, ct));
    public Task<Result<SmartComplyNinResult>> VerifyNinAsync(string nin, CancellationToken ct = default) =>
        Bill(BillingItems.Nin, BillingHints.Last4(nin), () => _inner.VerifyNinAsync(nin, ct));
    public Task<Result<SmartComplyTinResult>> VerifyTinAsync(string tin, CancellationToken ct = default) =>
        Bill(BillingItems.Tin, BillingHints.Last4(tin), () => _inner.VerifyTinAsync(tin, ct));
    public Task<Result<SmartComplyCacResult>> VerifyCacAsync(string rcNumber, CancellationToken ct = default) =>
        Bill(BillingItems.Cac, BillingHints.Rc(rcNumber), () => _inner.VerifyCacAsync(rcNumber, ct));
    public Task<Result<SmartComplyCacResult>> VerifyCacAdvancedAsync(string rcNumber, string companyName, string companyType = "RC", CancellationToken ct = default) =>
        Bill(BillingItems.CacAdvanced, BillingHints.Rc(rcNumber), () => _inner.VerifyCacAdvancedAsync(rcNumber, companyName, companyType, ct));
    public Task<Result<string>> GetCacAdvancedRawJsonAsync(string rcNumber, string companyName, string companyType = "RC", CancellationToken ct = default) =>
        Bill(BillingItems.CacAdvanced, BillingHints.Rc(rcNumber), () => _inner.GetCacAdvancedRawJsonAsync(rcNumber, companyName, companyType, ct));

    // Free: no subject, nothing looked up.
    public Task<SmartComplyAccessCheck> CheckAccessAsync(CancellationToken ct = default) => _inner.CheckAccessAsync(ct);
}

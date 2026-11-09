using CRMS.Application.Billing;
using CRMS.Application.Billing.Commands;
using CRMS.Domain.Aggregates.Billing;
using CRMS.Domain.Enums;
using CRMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Services.Billing;

/// <summary>
/// Rebuilds third-party usage from before billing existed, out of what each call left behind: a
/// bureau report row per bureau check, a CAC timestamp or snapshot per CAC lookup, and the partner
/// API's per-check call counts. BVN, NIN, TIN and fraud checks outside those left nothing to count.
/// Every draft carries a source key, so running this twice adds nothing the second time.
/// </summary>
public sealed class BillingBackfillSource : IBillingBackfillSource
{
    private readonly CRMSDbContext _db;

    public BillingBackfillSource(CRMSDbContext db) => _db = db;

    public async Task<IReadOnlyList<BillingUsageDraft>> ReconstructAsync(DateTime before, CancellationToken ct = default)
    {
        var drafts = new List<BillingUsageDraft>();

        var corporateRefs = await _db.LoanApplications.AsNoTracking().Select(a => new { a.Id, a.ApplicationNumber }).ToDictionaryAsync(a => a.Id, a => a.ApplicationNumber, ct);
        var nampRefs = await _db.NampApplications.AsNoTracking().Select(a => new { a.Id, a.ApplicationNumber }).ToDictionaryAsync(a => a.Id, a => a.ApplicationNumber, ct);
        var rhshfRefs = await _db.RhshfCreditProfiles.AsNoTracking().Select(p => new { p.Id, p.Reference }).ToDictionaryAsync(p => p.Id, p => p.Reference, ct);

        // 1. Bureau checks for all three tracks, guarantors and directors included.
        var reports = await _db.BureauReports.AsNoTracking()
            .Where(r => r.Provider != CreditBureauProvider.CreditRegistry && r.RequestedAt < before)
            .Select(r => new
            {
                r.Id, r.LoanApplicationId, r.NampApplicationId, r.RhshfCreditProfileId, r.SubjectType, r.Status, r.PartyType,
                r.CreditScore, HasFraud = r.FraudCheckRawJson != null, r.RequestedAt, r.CompletedAt, r.ErrorMessage, r.RequestedByUserId, r.BVN, r.RegistryId,
            })
            .ToListAsync(ct);
        foreach (var r in reports)
        {
            var (flow, caseId, reference) =
                r.LoanApplicationId is { } la ? (BillingFlow.Corporate, la, corporateRefs.GetValueOrDefault(la)) :
                r.NampApplicationId is { } na ? (BillingFlow.Namp, na, nampRefs.GetValueOrDefault(na)) :
                r.RhshfCreditProfileId is { } rp ? (BillingFlow.Rhshf, (Guid?)rp, rhshfRefs.GetValueOrDefault(rp)) :
                (BillingFlow.Unattributed, null, null);
            var subject = r.PartyType ?? (r.SubjectType == SubjectType.Business ? "Company" : "Applicant");
            var scope = new BillingScope(flow, caseId, reference, subject, r.RequestedByUserId == Guid.Empty ? null : r.RequestedByUserId);
            var at = r.CompletedAt ?? r.RequestedAt;
            var ok = r.Status != BureauReportStatus.Failed;
            var detail = ok ? null : r.ErrorMessage;

            if (r.SubjectType == SubjectType.Business)
            {
                drafts.Add(new BillingUsageDraft(BillingItems.BusinessCrc, ok, detail, BillingHints.Rc(r.RegistryId), null, scope, at, BillingUsageSource.Reconstructed, $"br:{r.Id:N}:{BillingItems.BusinessCrc}"));
                if (r.HasFraud)
                    drafts.Add(new BillingUsageDraft(BillingItems.FraudBusiness, true, null, BillingHints.Rc(r.RegistryId), null, scope, at, BillingUsageSource.Reconstructed, $"br:{r.Id:N}:{BillingItems.FraudBusiness}"));
            }
            else
            {
                var hint = BillingHints.Last4(r.BVN);
                drafts.Add(new BillingUsageDraft(BillingItems.CrcFull, ok, detail, hint, null, scope, at, BillingUsageSource.Reconstructed, $"br:{r.Id:N}:{BillingItems.CrcFull}"));
                if (r.CreditScore is not null)
                    drafts.Add(new BillingUsageDraft(BillingItems.CrcScore, true, null, hint, null, scope, at, BillingUsageSource.Reconstructed, $"br:{r.Id:N}:{BillingItems.CrcScore}"));
                if (r.HasFraud)
                    drafts.Add(new BillingUsageDraft(BillingItems.FraudIndividual, true, null, hint, null, scope, at, BillingUsageSource.Reconstructed, $"br:{r.Id:N}:{BillingItems.FraudIndividual}"));
            }
        }

        // 2. CAC lookups: one per NAMP application fetched, one per RH-SHF profile fetched, one per RH-SHF directors cross-check snapshot.
        var nampCac = await _db.NampApplications.AsNoTracking()
            .Where(a => a.CacFetchedAt != null && a.CacFetchedAt < before)
            .Select(a => new { a.Id, a.ApplicationNumber, a.CacFetchedAt, a.RcNumber })
            .ToListAsync(ct);
        foreach (var a in nampCac)
            drafts.Add(new BillingUsageDraft(BillingItems.CacAdvanced, true, null, BillingHints.Rc(a.RcNumber), null,
                new BillingScope(BillingFlow.Namp, a.Id, a.ApplicationNumber, "Company"), a.CacFetchedAt, BillingUsageSource.Reconstructed, $"namp-cac:{a.Id:N}"));

        var rhshfCac = await _db.RhshfCreditProfiles.AsNoTracking()
            .Where(p => p.CacFetchedAt != null && p.CacFetchedAt < before)
            .Select(p => new { p.Id, p.Reference, p.CacFetchedAt, p.RcNumber })
            .ToListAsync(ct);
        foreach (var p in rhshfCac)
            drafts.Add(new BillingUsageDraft(BillingItems.CacAdvanced, true, null, BillingHints.Rc(p.RcNumber), null,
                new BillingScope(BillingFlow.Rhshf, p.Id, p.Reference, "Company"), p.CacFetchedAt, BillingUsageSource.Reconstructed, $"rhshf-cac:{p.Id:N}"));

        var crossChecks = await _db.RhshfDirectorCrossChecks.AsNoTracking()
            .Where(x => x.FetchedAt < before)
            .Select(x => new { x.Id, x.RhshfCreditProfileId, x.FetchedAt, x.FetchedByUserId })
            .ToListAsync(ct);
        foreach (var x in crossChecks)
            drafts.Add(new BillingUsageDraft(BillingItems.CacAdvanced, true, null, null, null,
                new BillingScope(BillingFlow.Rhshf, x.RhshfCreditProfileId, rhshfRefs.GetValueOrDefault(x.RhshfCreditProfileId), "Directors", x.FetchedByUserId),
                x.FetchedAt, BillingUsageSource.Reconstructed, $"rhshf-xcheck:{x.Id:N}"));

        // 3. Partner checks: the check itself plus the calls its log counted.
        var logs = await _db.CustomerCheckLogs.AsNoTracking()
            .Where(l => l.CreatedAt < before)
            .Select(l => new { l.Id, l.RequestId, l.ConsumerName, l.BureauUsed, l.IdentityCalls, l.BureauCalls, l.ScoreCalls, l.FraudCalls, l.AdvisoryCalls, l.Error, l.CreatedAt })
            .ToListAsync(ct);
        foreach (var l in logs)
        {
            var scope = new BillingScope(BillingFlow.PartnerApi, l.Id, $"{l.ConsumerName} · {l.RequestId}", "Customer");
            drafts.Add(new BillingUsageDraft(BillingItems.PartnerCheck, l.Error is null, l.Error, null, null, scope, l.CreatedAt, BillingUsageSource.Reconstructed, $"cclog:{l.Id:N}:check"));
            Repeat(BillingItems.BvnAdvanced, l.IdentityCalls);
            Repeat(BureauFullItem(l.BureauUsed), l.BureauCalls);
            Repeat(BureauScoreItem(l.BureauUsed), l.ScoreCalls);
            Repeat(BillingItems.FraudIndividual, l.FraudCalls);
            Repeat(BillingItems.AdvisoryNarrative, l.AdvisoryCalls);

            void Repeat(string item, int n)
            {
                for (var i = 1; i <= n; i++)
                    drafts.Add(new BillingUsageDraft(item, true, null, null, null, scope, l.CreatedAt, BillingUsageSource.Reconstructed, $"cclog:{l.Id:N}:{item}:{i}"));
            }
        }

        return drafts;
    }

    public static string BureauFullItem(string? bureau) => bureau switch
    {
        "FirstCentral" => BillingItems.FirstCentralFull,
        "CreditRegistry" => BillingItems.CreditRegistryFull,
        _ => BillingItems.CrcFull,
    };

    public static string BureauScoreItem(string? bureau) => bureau switch
    {
        "FirstCentral" => BillingItems.FirstCentralScore,
        _ => BillingItems.CrcScore,
    };
}

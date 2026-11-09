using CRMS.Application.Billing;
using CRMS.Domain.Aggregates.Billing;
using CRMS.Domain.Enums;
using CRMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CRMS.Infrastructure.Services.Billing;

/// <summary>
/// Reads every application of every track as a flat billing snapshot: when it arrived, what became
/// of it and when. Projections only, no aggregates loaded, so the whole book is a few thousand
/// small rows even in production.
/// </summary>
public sealed class BillingLoanSource : IBillingLoanSource
{
    private readonly CRMSDbContext _db;

    public BillingLoanSource(CRMSDbContext db) => _db = db;

    public async Task<IReadOnlyList<BillingLoanSnapshot>> AllLoansAsync(CancellationToken ct = default)
    {
        var result = new List<BillingLoanSnapshot>();

        var corporate = await _db.LoanApplications.AsNoTracking()
            .Select(a => new
            {
                a.Id, a.ApplicationNumber, a.CustomerName, a.Status, a.CreatedAt, a.SubmittedAt, a.DisbursedAt, a.FineractLoanId, a.ModifiedAt,
                Requested = a.RequestedAmount != null ? (decimal?)a.RequestedAmount.Amount : null,
                Approved = a.ApprovedAmount != null ? (decimal?)a.ApprovedAmount.Amount : null,
            })
            .ToListAsync(ct);
        foreach (var a in corporate)
        {
            var outcome = BillingLoanClassifier.Corporate(a.Status, a.FineractLoanId is not null);
            DateTime? at = outcome switch
            {
                BillingLoanOutcome.Booked => a.DisbursedAt ?? a.ModifiedAt ?? a.CreatedAt,
                BillingLoanOutcome.Processing => null,
                _ => a.ModifiedAt ?? a.CreatedAt,
            };
            result.Add(new BillingLoanSnapshot(BillingFlow.Corporate, a.Id, a.ApplicationNumber, a.CustomerName, a.SubmittedAt ?? a.CreatedAt, outcome, at, a.Approved ?? a.Requested));
        }

        var namp = await _db.NampApplications.AsNoTracking()
            .Select(a => new
            {
                a.Id, a.ApplicationNumber, a.ApplicantName, a.CompanyName, a.Status, a.CreatedAt, a.RecalledAt,
                a.FineractDisbursedOn, a.DeployedAt, a.FineractLoanId, a.FineractLoanAccountNumber, a.ModifiedAt, a.LoanAmount,
            })
            .ToListAsync(ct);
        foreach (var a in namp)
        {
            var outcome = BillingLoanClassifier.Namp(a.Status, a.FineractLoanAccountNumber is not null || a.FineractDisbursedOn is not null);
            DateTime? at = outcome switch
            {
                BillingLoanOutcome.Booked => a.FineractDisbursedOn ?? a.DeployedAt ?? a.ModifiedAt ?? a.CreatedAt,
                BillingLoanOutcome.Processing => null,
                _ => a.ModifiedAt ?? a.CreatedAt,
            };
            var name = string.IsNullOrWhiteSpace(a.CompanyName) ? a.ApplicantName : a.CompanyName;
            result.Add(new BillingLoanSnapshot(BillingFlow.Namp, a.Id, a.ApplicationNumber, name, a.CreatedAt, outcome, at, a.LoanAmount));
        }

        var rhshf = await _db.RhshfCreditProfiles.AsNoTracking()
            .Select(p => new
            {
                p.Id, p.Reference, p.CompanyName, p.Status, p.ReceivedAt, p.DecidedAt, p.UpdatedAt, p.ApprovedAmount, p.InternalStage,
                Booked = p.Disbursements.Any(d => d.FineractLoanId != null),
            })
            .ToListAsync(ct);
        foreach (var p in rhshf)
        {
            var isBooked = p.Booked || p.InternalStage is RhshfInternalStage.Completed or RhshfInternalStage.Active or RhshfInternalStage.Closed;
            var outcome = BillingLoanClassifier.Rhshf(p.Status, isBooked);
            DateTime? at = outcome == BillingLoanOutcome.Processing ? null : p.DecidedAt ?? p.UpdatedAt;
            result.Add(new BillingLoanSnapshot(BillingFlow.Rhshf, p.Id, p.Reference, p.CompanyName, p.ReceivedAt, outcome, at, p.ApprovedAmount));
        }

        return result;
    }
}

using CRMS.Application.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>
/// Runs the automated business credit-bureau pull at the CreditBureauCheck stage (§4 stage 2).
/// Idempotent: a no-op if the check already ran for this case. Uses ISmartComplyProvider directly —
/// it's a generic, stateless external client (no coupling to any loan aggregate), unlike
/// ProcessLoanCreditChecksCommand which is hard-wired to Corporate's LoanApplication/Guarantor/
/// Collateral repositories and workflow service. RH-SHF does not reuse BureauReport/
/// IBureauReportRepository either — that generic-looking aggregate is tuned for staff review
/// screens shared with Corporate/NAMP; RH-SHF keeps its own bureau result self-contained instead.
/// </summary>
public record EnsureRhshfBureauCheckCommand(string Reference) : IRequest<ApplicationResult>;

public class EnsureRhshfBureauCheckHandler : IRequestHandler<EnsureRhshfBureauCheckCommand, ApplicationResult>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly ISmartComplyProvider _smartComply;
    private readonly IUnitOfWork _uow;

    public EnsureRhshfBureauCheckHandler(IRhshfCreditProfileRepository repo, ISmartComplyProvider smartComply, IUnitOfWork uow)
    {
        _repo = repo;
        _smartComply = smartComply;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(EnsureRhshfBureauCheckCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        if (profile.BureauCheckOutcome != RhshfBureauOutcome.NotRun)
            return ApplicationResult.Success(); // already run — idempotent no-op

        var reportResult = await _smartComply.GetCRCBusinessHistoryAsync(profile.RcNumber, ct);
        if (reportResult.IsFailure)
        {
            profile.RecordBureauCheckFailure();
            await _uow.SaveChangesAsync(ct);
            return ApplicationResult.Success(); // failure is recorded, not surfaced as a blocking error
        }

        var summary = reportResult.Value.Summary;
        // Same "has credit issues" rule already used elsewhere in this codebase (ProcessLoanCreditChecksCommand).
        var outcome = summary.TotalNoOfDelinquentFacilities > 0 ? RhshfBureauOutcome.Flagged : RhshfBureauOutcome.Cleared;

        profile.RecordBureauCheck(
            outcome: outcome,
            totalLoans: summary.TotalNoOfLoans,
            activeLoans: summary.TotalNoOfActiveLoans,
            delinquentFacilities: summary.TotalNoOfDelinquentFacilities,
            totalOutstanding: summary.TotalOutstanding,
            totalOverdue: summary.TotalOverdue,
            rawJson: null);

        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}

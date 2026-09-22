using CRMS.Application.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Queries;

public record GetRhshfDisbursementHistoryQuery(string Reference) : IRequest<ApplicationResult<List<RhshfDisbursementDto>>>;

public record RhshfDisbursementDto(
    int CycleNumber, Guid DisbursementOfficerId, string DisbursementOfficerName, DateTime BookedAt, decimal DisbursedAmount,
    string SupplierAccountNumber, string? SupplierName, RhshfDisbursementStatus Status,
    string? FineractLoanAccountNumber, string? FailureReason);

public class GetRhshfDisbursementHistoryHandler : IRequestHandler<GetRhshfDisbursementHistoryQuery, ApplicationResult<List<RhshfDisbursementDto>>>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IUserNameResolver _names;

    public GetRhshfDisbursementHistoryHandler(IRhshfCreditProfileRepository repo, IUserNameResolver names)
    {
        _repo = repo;
        _names = names;
    }

    public async Task<ApplicationResult<List<RhshfDisbursementDto>>> Handle(GetRhshfDisbursementHistoryQuery request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult<List<RhshfDisbursementDto>>.Failure("Case not found.");

        var attempts = profile.Disbursements
            .Where(d => d.CycleNumber == profile.CurrentCycleNumber)
            .OrderBy(d => d.BookedAt)
            .ToList();

        var names = await _names.ResolveManyAsync(attempts.Select(d => d.DisbursementOfficerId), ct);
        var dto = attempts
            .Select(d => new RhshfDisbursementDto(
                d.CycleNumber, d.DisbursementOfficerId,
                names.TryGetValue(d.DisbursementOfficerId, out var n) ? n : "—",
                d.BookedAt, d.DisbursedAmount,
                d.SupplierAccountNumber, d.SupplierName, d.Status, d.FineractLoanAccountNumber, d.FailureReason))
            .ToList();

        return ApplicationResult<List<RhshfDisbursementDto>>.Success(dto);
    }
}

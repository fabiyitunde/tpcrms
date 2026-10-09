using CRMS.Application.Common;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Queries;

/// <summary>
/// Pulls the live BOA account details for a case from Core Banking so the Disbursement Officer can
/// eyeball them against the offer and confirm the "account verified" gate item — the pull-and-confirm
/// model, rather than asking for a document upload. Read-only; never bills (CBS, not CAC).
/// </summary>
public record GetRhshfBoaAccountSnapshotQuery(string Reference) : IRequest<ApplicationResult<RhshfBoaAccountSnapshotDto>>;

public record RhshfBoaAccountSnapshotDto(
    string AccountNumber,
    string? AccountName,
    string? AccountType,
    string? Status,
    bool Found,
    string? Error);

public class GetRhshfBoaAccountSnapshotHandler
    : IRequestHandler<GetRhshfBoaAccountSnapshotQuery, ApplicationResult<RhshfBoaAccountSnapshotDto>>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly ICoreBankingService _cbs;

    public GetRhshfBoaAccountSnapshotHandler(IRhshfCreditProfileRepository repo, ICoreBankingService cbs)
    {
        _repo = repo;
        _cbs = cbs;
    }

    public async Task<ApplicationResult<RhshfBoaAccountSnapshotDto>> Handle(
        GetRhshfBoaAccountSnapshotQuery request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult<RhshfBoaAccountSnapshotDto>.Failure("Case not found.");

        var accountNumber = profile.BoaAccountNumber;

        // Prefer the account record (carries name + status); fall back to the customer lookup for the name.
        var accountResult = await _cbs.GetAccountInfoAsync(accountNumber, ct);
        if (accountResult.IsSuccess)
        {
            var a = accountResult.Value;
            return ApplicationResult<RhshfBoaAccountSnapshotDto>.Success(
                new RhshfBoaAccountSnapshotDto(accountNumber, a.AccountName, a.AccountType, a.Status, true, null));
        }

        var customerResult = await _cbs.GetCustomerByAccountNumberAsync(accountNumber, ct);
        if (customerResult.IsSuccess)
        {
            var c = customerResult.Value;
            return ApplicationResult<RhshfBoaAccountSnapshotDto>.Success(
                new RhshfBoaAccountSnapshotDto(accountNumber, c.FullName, c.CustomerType.ToString(), null, true, null));
        }

        return ApplicationResult<RhshfBoaAccountSnapshotDto>.Success(
            new RhshfBoaAccountSnapshotDto(accountNumber, null, null, null, false,
                customerResult.Error ?? accountResult.Error ?? "Could not retrieve account details from Core Banking."));
    }
}

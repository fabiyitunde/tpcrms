using CRMS.Application.Common;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Queries;

/// <summary>
/// Name enquiry against Core Banking for an arbitrary 10-digit NUBAN — used on the disbursement modal so
/// the officer can confirm the input-supplier account resolves to the expected name before booking.
/// Read-only; never bills (CBS, not CAC).
/// </summary>
public record LookupCoreBankingAccountQuery(string AccountNumber) : IRequest<ApplicationResult<CoreBankingAccountLookupDto>>;

public record CoreBankingAccountLookupDto(
    string AccountNumber,
    string? AccountName,
    string? AccountType,
    string? Status,
    bool Found,
    string? Error);

public class LookupCoreBankingAccountHandler
    : IRequestHandler<LookupCoreBankingAccountQuery, ApplicationResult<CoreBankingAccountLookupDto>>
{
    private readonly ICoreBankingService _cbs;

    public LookupCoreBankingAccountHandler(ICoreBankingService cbs) => _cbs = cbs;

    public async Task<ApplicationResult<CoreBankingAccountLookupDto>> Handle(
        LookupCoreBankingAccountQuery request, CancellationToken ct = default)
    {
        var accountNumber = (request.AccountNumber ?? string.Empty).Trim();
        if (accountNumber.Length != 10 || !accountNumber.All(char.IsDigit))
            return ApplicationResult<CoreBankingAccountLookupDto>.Success(
                new CoreBankingAccountLookupDto(accountNumber, null, null, null, false,
                    "Enter a valid 10-digit NUBAN account number."));

        // Prefer the account record (name + status); fall back to the customer lookup for the name.
        var accountResult = await _cbs.GetAccountInfoAsync(accountNumber, ct);
        if (accountResult.IsSuccess)
        {
            var a = accountResult.Value;
            return ApplicationResult<CoreBankingAccountLookupDto>.Success(
                new CoreBankingAccountLookupDto(accountNumber, a.AccountName, a.AccountType, a.Status, true, null));
        }

        var customerResult = await _cbs.GetCustomerByAccountNumberAsync(accountNumber, ct);
        if (customerResult.IsSuccess)
        {
            var c = customerResult.Value;
            return ApplicationResult<CoreBankingAccountLookupDto>.Success(
                new CoreBankingAccountLookupDto(accountNumber, c.FullName, c.CustomerType.ToString(), null, true, null));
        }

        return ApplicationResult<CoreBankingAccountLookupDto>.Success(
            new CoreBankingAccountLookupDto(accountNumber, null, null, null, false,
                customerResult.Error ?? accountResult.Error ?? "Account not found in Core Banking."));
    }
}

using CRMS.Application.Common;
using CRMS.Application.CoreBanking.DTOs;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.CoreBanking.Queries;

public record GetCorporateAccountDataQuery(
    string AccountNumber,
    bool IncludeStatement = true,
    int StatementMonths = 6
) : IRequest<ApplicationResult<CorporateAccountDataDto>>;

public class GetCorporateAccountDataHandler : IRequestHandler<GetCorporateAccountDataQuery, ApplicationResult<CorporateAccountDataDto>>
{
    private readonly ICoreBankingService _coreBankingService;

    public GetCorporateAccountDataHandler(ICoreBankingService coreBankingService)
    {
        _coreBankingService = coreBankingService;
    }

    public async Task<ApplicationResult<CorporateAccountDataDto>> Handle(GetCorporateAccountDataQuery request, CancellationToken ct = default)
    {
        // Get customer info
        var customerResult = await _coreBankingService.GetCustomerByAccountNumberAsync(request.AccountNumber, ct);
        if (customerResult.IsFailure)
            return ApplicationResult<CorporateAccountDataDto>.Failure(customerResult.Error);

        var customer = customerResult.Value;
        if (customer.CustomerType != CustomerType.Corporate)
            return ApplicationResult<CorporateAccountDataDto>.Failure("Account is not a corporate account");

        // Get corporate info
        var corporateResult = await _coreBankingService.GetCorporateInfoAsync(request.AccountNumber, ct);
        if (corporateResult.IsFailure)
            return ApplicationResult<CorporateAccountDataDto>.Failure(corporateResult.Error);

        // Get account info
        var accountResult = await _coreBankingService.GetAccountInfoAsync(request.AccountNumber, ct);
        if (accountResult.IsFailure)
            return ApplicationResult<CorporateAccountDataDto>.Failure(accountResult.Error);

        // Get directors
        var directorsResult = await _coreBankingService.GetDirectorsAsync(customer.CustomerId, ct);
        var directors = directorsResult.IsSuccess ? directorsResult.Value : [];

        // Get signatories
        var signatoriesResult = await _coreBankingService.GetSignatoriesAsync(request.AccountNumber, ct);
        var signatories = signatoriesResult.IsSuccess ? signatoriesResult.Value : [];

        // Get statement if requested
        AccountStatementDto? statementDto = null;
        if (request.IncludeStatement)
        {
            var toDate = DateTime.UtcNow;
            var fromDate = toDate.AddMonths(-request.StatementMonths);
            var statementResult = await _coreBankingService.GetStatementAsync(request.AccountNumber, fromDate, toDate, ct);
            
            if (statementResult.IsSuccess)
            {
                var statement = statementResult.Value;
                statementDto = new AccountStatementDto(
                    statement.AccountNumber,
                    statement.FromDate,
                    statement.ToDate,
                    statement.OpeningBalance,
                    statement.ClosingBalance,
                    statement.TotalCredits,
                    statement.TotalDebits,
                    statement.Transactions.Select(t => new StatementTransactionDto(
                        t.TransactionId,
                        t.Date,
                        t.Description,
                        t.Amount,
                        t.Type.ToString(),
                        t.RunningBalance,
                        t.Reference
                    )).ToList()
                );
            }
        }

        var corporate = corporateResult.Value;
        var account = accountResult.Value;

        var result = new CorporateAccountDataDto(
            Customer: new CustomerInfoDto(
                customer.CustomerId,
                customer.FullName,
                customer.CustomerType.ToString(),
                customer.Email,
                customer.PhoneNumber,
                customer.BVN,
                customer.DateOfBirth,
                customer.Address
            ),
            Corporate: new CorporateInfoDto(
                corporate.CorporateId,
                corporate.CompanyName,
                corporate.RegistrationNumber,
                corporate.Industry,
                corporate.IncorporationDate,
                corporate.RegisteredAddress,
                corporate.TaxIdentificationNumber
            ),
            Account: new AccountInfoDto(
                account.AccountNumber,
                account.AccountName,
                account.AccountType,
                account.Currency,
                account.CurrentBalance,
                account.AvailableBalance,
                account.Status,
                account.OpenedDate
            ),
            Directors: directors.Select(d => new DirectorInfoDto(
                d.DirectorId,
                d.FullName,
                d.BVN,
                d.Email,
                d.PhoneNumber,
                d.Address,
                d.DateOfBirth,
                d.Nationality,
                d.ShareholdingPercent
            )).ToList(),
            Signatories: signatories.Select(s => new SignatoryInfoDto(
                s.SignatoryId,
                s.FullName,
                s.BVN,
                s.Email,
                s.PhoneNumber,
                s.MandateType,
                s.Designation
            )).ToList(),
            Statement: statementDto
        );

        return ApplicationResult<CorporateAccountDataDto>.Success(result);
    }
}

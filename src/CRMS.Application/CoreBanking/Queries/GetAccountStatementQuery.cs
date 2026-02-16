using CRMS.Application.Common;
using CRMS.Application.CoreBanking.DTOs;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.CoreBanking.Queries;

public record GetAccountStatementQuery(
    string AccountNumber,
    DateTime FromDate,
    DateTime ToDate
) : IRequest<ApplicationResult<AccountStatementDto>>;

public class GetAccountStatementHandler : IRequestHandler<GetAccountStatementQuery, ApplicationResult<AccountStatementDto>>
{
    private readonly ICoreBankingService _coreBankingService;

    public GetAccountStatementHandler(ICoreBankingService coreBankingService)
    {
        _coreBankingService = coreBankingService;
    }

    public async Task<ApplicationResult<AccountStatementDto>> Handle(GetAccountStatementQuery request, CancellationToken ct = default)
    {
        var result = await _coreBankingService.GetStatementAsync(request.AccountNumber, request.FromDate, request.ToDate, ct);
        
        if (result.IsFailure)
            return ApplicationResult<AccountStatementDto>.Failure(result.Error);

        var statement = result.Value;
        var dto = new AccountStatementDto(
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

        return ApplicationResult<AccountStatementDto>.Success(dto);
    }
}

public record GetAccountInfoQuery(string AccountNumber) : IRequest<ApplicationResult<AccountInfoDto>>;

public class GetAccountInfoHandler : IRequestHandler<GetAccountInfoQuery, ApplicationResult<AccountInfoDto>>
{
    private readonly ICoreBankingService _coreBankingService;

    public GetAccountInfoHandler(ICoreBankingService coreBankingService)
    {
        _coreBankingService = coreBankingService;
    }

    public async Task<ApplicationResult<AccountInfoDto>> Handle(GetAccountInfoQuery request, CancellationToken ct = default)
    {
        var result = await _coreBankingService.GetAccountInfoAsync(request.AccountNumber, ct);
        
        if (result.IsFailure)
            return ApplicationResult<AccountInfoDto>.Failure(result.Error);

        var account = result.Value;
        return ApplicationResult<AccountInfoDto>.Success(new AccountInfoDto(
            account.AccountNumber,
            account.AccountName,
            account.AccountType,
            account.Currency,
            account.CurrentBalance,
            account.AvailableBalance,
            account.Status,
            account.OpenedDate
        ));
    }
}

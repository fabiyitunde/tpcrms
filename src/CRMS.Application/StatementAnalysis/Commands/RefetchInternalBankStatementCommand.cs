using CRMS.Application.Common;
using CRMS.Domain.Aggregates.StatementAnalysis;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.StatementAnalysis.Commands;

public record RefetchInternalBankStatementCommand(Guid LoanApplicationId, Guid RequestedByUserId)
    : IRequest<ApplicationResult>;

public class RefetchInternalBankStatementHandler : IRequestHandler<RefetchInternalBankStatementCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _loanAppRepo;
    private readonly IBankStatementRepository _bankStatementRepo;
    private readonly ICoreBankingService _coreBankingService;
    private readonly IUnitOfWork _unitOfWork;

    public RefetchInternalBankStatementHandler(
        ILoanApplicationRepository loanAppRepo,
        IBankStatementRepository bankStatementRepo,
        ICoreBankingService coreBankingService,
        IUnitOfWork unitOfWork)
    {
        _loanAppRepo = loanAppRepo;
        _bankStatementRepo = bankStatementRepo;
        _coreBankingService = coreBankingService;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(RefetchInternalBankStatementCommand request, CancellationToken ct = default)
    {
        var loanApp = await _loanAppRepo.GetByIdAsync(request.LoanApplicationId, ct);
        if (loanApp == null)
            return ApplicationResult.Failure("Loan application not found");

        if (string.IsNullOrEmpty(loanApp.AccountNumber))
            return ApplicationResult.Failure("No account number on this application");

        // Delete existing internal statement(s)
        var existing = await _bankStatementRepo.GetByLoanApplicationIdAsync(request.LoanApplicationId, ct);
        foreach (var stmt in existing.Where(s => s.IsInternal))
            _bankStatementRepo.Delete(stmt);

        await _unitOfWork.SaveChangesAsync(ct);

        // Re-fetch: 6 months back from today
        var toDate = DateTime.UtcNow;
        var fromDate = toDate.AddMonths(-6);

        var statementResult = await _coreBankingService.GetStatementAsync(loanApp.AccountNumber, fromDate, toDate, ct);
        if (!statementResult.IsSuccess)
            return ApplicationResult.Failure("Core banking returned an error: " + statementResult.Error);

        var stmt2 = statementResult.Value;
        var bankStatementResult = BankStatement.Create(
            stmt2.AccountNumber,
            loanApp.CustomerName,
            "Own Bank",
            stmt2.FromDate,
            stmt2.ToDate,
            stmt2.OpeningBalance,
            stmt2.ClosingBalance,
            StatementFormat.JSON,
            StatementSource.CoreBanking,
            request.RequestedByUserId,
            null,
            null,
            request.LoanApplicationId
        );

        if (!bankStatementResult.IsSuccess)
            return ApplicationResult.Failure("Failed to create bank statement: " + bankStatementResult.Error);

        var bankStatement = bankStatementResult.Value;
        foreach (var tx in stmt2.Transactions)
        {
            bankStatement.AddTransaction(
                tx.Date, tx.Description, tx.Amount,
                tx.Type == TransactionType.Credit
                    ? StatementTransactionType.Credit
                    : StatementTransactionType.Debit,
                tx.RunningBalance, tx.Reference);
        }

        await _bankStatementRepo.AddAsync(bankStatement, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}

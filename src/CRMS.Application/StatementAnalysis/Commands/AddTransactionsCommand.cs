using CRMS.Application.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.StatementAnalysis.Commands;

public record TransactionInput(
    DateTime Date,
    string Description,
    decimal Amount,
    StatementTransactionType Type,
    decimal RunningBalance,
    string? Reference
);

public record AddTransactionsCommand(
    Guid StatementId,
    List<TransactionInput> Transactions
) : IRequest<ApplicationResult<int>>;

public class AddTransactionsHandler : IRequestHandler<AddTransactionsCommand, ApplicationResult<int>>
{
    private readonly IBankStatementRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public AddTransactionsHandler(IBankStatementRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<int>> Handle(AddTransactionsCommand request, CancellationToken ct = default)
    {
        var statement = await _repository.GetByIdAsync(request.StatementId, ct);
        if (statement == null)
            return ApplicationResult<int>.Failure("Statement not found");

        var addedCount = 0;
        foreach (var txn in request.Transactions)
        {
            var result = statement.AddTransaction(
                txn.Date,
                txn.Description,
                txn.Amount,
                txn.Type,
                txn.RunningBalance,
                txn.Reference
            );

            if (result.IsSuccess)
                addedCount++;
        }

        _repository.Update(statement);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<int>.Success(addedCount);
    }
}

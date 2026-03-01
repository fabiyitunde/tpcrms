using CRMS.Application.Common;
using CRMS.Application.StatementAnalysis.DTOs;
using CRMS.Domain.Aggregates.StatementAnalysis;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.StatementAnalysis.Commands;

public record UploadStatementCommand(
    string AccountNumber,
    string AccountName,
    string BankName,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    decimal OpeningBalance,
    decimal ClosingBalance,
    StatementFormat Format,
    StatementSource Source,
    Guid UploadedByUserId,
    string? OriginalFileName,
    string? FilePath,
    Guid? LoanApplicationId,
    string Currency = "NGN"
) : IRequest<ApplicationResult<BankStatementDto>>;

public class UploadStatementHandler : IRequestHandler<UploadStatementCommand, ApplicationResult<BankStatementDto>>
{
    private readonly IBankStatementRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UploadStatementHandler(IBankStatementRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<BankStatementDto>> Handle(UploadStatementCommand request, CancellationToken ct = default)
    {
        var result = BankStatement.Create(
            request.AccountNumber,
            request.AccountName,
            request.BankName,
            request.PeriodStart,
            request.PeriodEnd,
            request.OpeningBalance,
            request.ClosingBalance,
            request.Format,
            request.Source,
            request.UploadedByUserId,
            request.OriginalFileName,
            request.FilePath,
            request.LoanApplicationId,
            request.Currency
        );

        if (result.IsFailure)
            return ApplicationResult<BankStatementDto>.Failure(result.Error);

        await _repository.AddAsync(result.Value, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<BankStatementDto>.Success(MapToDto(result.Value));
    }

    private static BankStatementDto MapToDto(BankStatement s) => new(
        s.Id,
        s.LoanApplicationId,
        s.AccountNumber,
        s.AccountName,
        s.BankName,
        s.Currency,
        s.PeriodStart,
        s.PeriodEnd,
        s.OpeningBalance,
        s.ClosingBalance,
        s.Format.ToString(),
        s.Source.ToString(),
        s.AnalysisStatus.ToString(),
        s.OriginalFileName,
        s.Transactions.Count,
        s.CreatedAt,
        null
    );
}

public record VerifyStatementCommand(Guid StatementId, Guid VerifiedByUserId, string? Notes) : IRequest<ApplicationResult>;

public class VerifyStatementHandler : IRequestHandler<VerifyStatementCommand, ApplicationResult>
{
    private readonly IBankStatementRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public VerifyStatementHandler(IBankStatementRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(VerifyStatementCommand request, CancellationToken ct = default)
    {
        var statement = await _repository.GetByIdWithTransactionsAsync(request.StatementId, ct);
        if (statement == null)
            return ApplicationResult.Failure("Statement not found");

        var result = statement.Verify(request.VerifiedByUserId, request.Notes);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        _repository.Update(statement);
        await _unitOfWork.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}

public record RejectStatementCommand(Guid StatementId, Guid RejectedByUserId, string Reason) : IRequest<ApplicationResult>;

public class RejectStatementHandler : IRequestHandler<RejectStatementCommand, ApplicationResult>
{
    private readonly IBankStatementRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public RejectStatementHandler(IBankStatementRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(RejectStatementCommand request, CancellationToken ct = default)
    {
        var statement = await _repository.GetByIdWithTransactionsAsync(request.StatementId, ct);
        if (statement == null)
            return ApplicationResult.Failure("Statement not found");

        var result = statement.Reject(request.RejectedByUserId, request.Reason);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        _repository.Update(statement);
        await _unitOfWork.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}

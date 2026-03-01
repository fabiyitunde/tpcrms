using CRMS.Application.Common;
using CRMS.Application.LoanApplication.DTOs;
using CRMS.Domain.Aggregates.StatementAnalysis;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using CRMS.Domain.ValueObjects;
using LA = CRMS.Domain.Aggregates.LoanApplication;

namespace CRMS.Application.LoanApplication.Commands;

public record InitiateCorporateLoanCommand(
    Guid LoanProductId,
    string ProductCode,
    string AccountNumber,
    decimal RequestedAmount,
    string Currency,
    int RequestedTenorMonths,
    decimal InterestRatePerAnnum,
    InterestRateType InterestRateType,
    Guid InitiatedByUserId,
    Guid? BranchId,
    string? Purpose,
    string? RegistrationNumberOverride = null,
    DateTime? IncorporationDateOverride = null
) : IRequest<ApplicationResult<LoanApplicationDto>>;

public class InitiateCorporateLoanHandler : IRequestHandler<InitiateCorporateLoanCommand, ApplicationResult<LoanApplicationDto>>
{
    private readonly ILoanApplicationRepository _repository;
    private readonly ICoreBankingService _coreBankingService;
    private readonly IBankStatementRepository _bankStatementRepository;
    private readonly IUnitOfWork _unitOfWork;

    public InitiateCorporateLoanHandler(
        ILoanApplicationRepository repository,
        ICoreBankingService coreBankingService,
        IBankStatementRepository bankStatementRepository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _coreBankingService = coreBankingService;
        _bankStatementRepository = bankStatementRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<LoanApplicationDto>> Handle(InitiateCorporateLoanCommand request, CancellationToken ct = default)
    {
        // Fetch customer info from core banking
        var customerResult = await _coreBankingService.GetCustomerByAccountNumberAsync(request.AccountNumber, ct);
        if (customerResult.IsFailure)
            return ApplicationResult<LoanApplicationDto>.Failure($"Failed to fetch customer: {customerResult.Error}");

        var customer = customerResult.Value;
        if (customer.CustomerType != CustomerType.Corporate)
            return ApplicationResult<LoanApplicationDto>.Failure("Account is not a corporate account");

        // Fetch corporate info to get RC number and incorporation date
        var corporateResult = await _coreBankingService.GetCorporateInfoAsync(request.AccountNumber, ct);
        var registrationNumber = corporateResult.IsSuccess
            ? (corporateResult.Value.RegistrationNumber?.ToUpperInvariant() ?? request.RegistrationNumberOverride?.ToUpperInvariant())
            : request.RegistrationNumberOverride?.ToUpperInvariant();
        var incorporationDate = corporateResult.IsSuccess
            ? (corporateResult.Value.IncorporationDate ?? request.IncorporationDateOverride)
            : request.IncorporationDateOverride;

        var amount = Money.Create(request.RequestedAmount, request.Currency);

        var applicationResult = LA.LoanApplication.CreateCorporate(
            request.LoanProductId,
            request.ProductCode,
            request.AccountNumber,
            customer.CustomerId,
            customer.FullName,
            amount,
            request.RequestedTenorMonths,
            request.InterestRatePerAnnum,
            request.InterestRateType,
            request.InitiatedByUserId,
            request.BranchId,
            request.Purpose,
            registrationNumber,
            incorporationDate
        );

        if (applicationResult.IsFailure)
            return ApplicationResult<LoanApplicationDto>.Failure(applicationResult.Error);

        var application = applicationResult.Value;

        // Auto-pull directors and signatories
        var directorsResult = await _coreBankingService.GetDirectorsAsync(customer.CustomerId, ct);
        if (directorsResult.IsSuccess)
        {
            foreach (var director in directorsResult.Value)
            {
                application.AddParty(
                    Domain.Enums.PartyType.Director,
                    director.FullName,
                    director.BVN,
                    director.Email,
                    director.PhoneNumber,
                    null,
                    director.ShareholdingPercent
                );
            }
        }

        var signatoriesResult = await _coreBankingService.GetSignatoriesAsync(request.AccountNumber, ct);
        if (signatoriesResult.IsSuccess)
        {
            foreach (var signatory in signatoriesResult.Value)
            {
                application.AddParty(
                    Domain.Enums.PartyType.Signatory,
                    signatory.FullName,
                    signatory.BVN,
                    signatory.Email,
                    signatory.PhoneNumber,
                    signatory.Designation,
                    null
                );
            }
        }

        await _repository.AddAsync(application, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        // Auto-fetch and persist 6-month bank statement from core banking
        var toDate = DateTime.UtcNow;
        var fromDate = toDate.AddMonths(-6);
        var statementResult = await _coreBankingService.GetStatementAsync(request.AccountNumber, fromDate, toDate, ct);
        if (statementResult.IsSuccess)
        {
            var stmt = statementResult.Value;
            var bankStatementResult = BankStatement.Create(
                stmt.AccountNumber,
                customer.FullName,
                "Own Bank",
                stmt.FromDate,
                stmt.ToDate,
                stmt.OpeningBalance,
                stmt.ClosingBalance,
                StatementFormat.JSON,
                StatementSource.CoreBanking,
                request.InitiatedByUserId,
                null,
                null,
                application.Id
            );
            if (bankStatementResult.IsSuccess)
            {
                var bankStatement = bankStatementResult.Value;
                foreach (var tx in stmt.Transactions)
                {
                    bankStatement.AddTransaction(
                        tx.Date, tx.Description, tx.Amount,
                        tx.Type == TransactionType.Credit
                            ? StatementTransactionType.Credit
                            : StatementTransactionType.Debit,
                        tx.RunningBalance, tx.Reference);
                }
                await _bankStatementRepository.AddAsync(bankStatement, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }
        }

        return ApplicationResult<LoanApplicationDto>.Success(MapToDto(application));
    }

    private static LoanApplicationDto MapToDto(LA.LoanApplication app)
    {
        return new LoanApplicationDto(
            app.Id,
            app.ApplicationNumber,
            app.Type.ToString(),
            app.Status.ToString(),
            app.LoanProductId,
            app.ProductCode,
            app.AccountNumber,
            app.CustomerId,
            app.CustomerName,
            app.RegistrationNumber,
            app.RequestedAmount.Amount,
            app.RequestedAmount.Currency,
            app.RequestedTenorMonths,
            app.InterestRatePerAnnum,
            app.InterestRateType.ToString(),
            app.Purpose,
            app.ApprovedAmount?.Amount,
            app.ApprovedTenorMonths,
            app.ApprovedInterestRate,
            app.InitiatedByUserId,
            app.BranchId,
            app.SubmittedAt,
            app.BranchApprovedAt,
            app.FinalApprovedAt,
            app.DisbursedAt,
            app.CoreBankingLoanId,
            app.CreatedAt,
            app.ModifiedAt,
            app.Documents.Select(d => new LoanApplicationDocumentDto(
                d.Id,
                d.Category.ToString(),
                d.Status.ToString(),
                d.FileName,
                d.FileSize,
                d.ContentType,
                d.Description,
                d.UploadedAt,
                d.VerifiedAt,
                d.RejectionReason
            )).ToList(),
            app.Parties.Select(p => new LoanApplicationPartyDto(
                p.Id,
                p.PartyType.ToString(),
                p.FullName,
                p.BVN,
                p.Email,
                p.PhoneNumber,
                p.Designation,
                p.ShareholdingPercent,
                p.BVNVerified
            )).ToList(),
            app.IncorporationDate
        );
    }
}

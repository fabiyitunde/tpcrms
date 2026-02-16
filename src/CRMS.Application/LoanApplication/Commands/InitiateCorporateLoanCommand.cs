using CRMS.Application.Common;
using CRMS.Application.LoanApplication.DTOs;
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
    string? Purpose
) : IRequest<ApplicationResult<LoanApplicationDto>>;

public class InitiateCorporateLoanHandler : IRequestHandler<InitiateCorporateLoanCommand, ApplicationResult<LoanApplicationDto>>
{
    private readonly ILoanApplicationRepository _repository;
    private readonly ICoreBankingService _coreBankingService;
    private readonly IUnitOfWork _unitOfWork;

    public InitiateCorporateLoanHandler(
        ILoanApplicationRepository repository,
        ICoreBankingService coreBankingService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _coreBankingService = coreBankingService;
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
            request.Purpose
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
            )).ToList()
        );
    }
}

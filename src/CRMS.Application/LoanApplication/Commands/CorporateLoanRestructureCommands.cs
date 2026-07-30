using CRMS.Application.Common;
using CRMS.Application.Namp.DTOs;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using CRMS.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace CRMS.Application.LoanApplication.Commands;

// ─── Legal Officer: mark legal review complete (no status change) ─────────────

public record CompleteLegalReviewCommand(Guid ApplicationId, Guid UserId, string Note) : IRequest<ApplicationResult>;

public class CompleteLegalReviewHandler : IRequestHandler<CompleteLegalReviewCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CompleteLegalReviewHandler(ILoanApplicationRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(CompleteLegalReviewCommand request, CancellationToken ct = default)
    {
        var application = await _repository.GetByIdAsync(request.ApplicationId, ct);
        if (application == null)
            return ApplicationResult.Failure("Loan application not found");

        var result = application.CompleteLegalReview(request.UserId, request.Note);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        _repository.Update(application);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}

// ─── Credit Officer: save credit appraisal ───────────────────────────────────

public record SaveCreditAppraisalCommand(
    Guid ApplicationId,
    Guid UserId,
    decimal? Dscr,
    decimal? Leverage,
    decimal? CurrentRatio,
    decimal? Ltv,
    string? CapacityRating,
    string? Recommendation,
    string? Notes,
    string? MemoPath,
    string? MemoFileName,
    bool ClearMemo = false
) : IRequest<ApplicationResult>;

public class SaveCreditAppraisalHandler : IRequestHandler<SaveCreditAppraisalCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public SaveCreditAppraisalHandler(ILoanApplicationRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(SaveCreditAppraisalCommand request, CancellationToken ct = default)
    {
        var application = await _repository.GetByIdAsync(request.ApplicationId, ct);
        if (application == null)
            return ApplicationResult.Failure("Loan application not found");

        var result = application.SaveCreditAppraisal(
            request.UserId,
            request.Dscr,
            request.Leverage,
            request.CurrentRatio,
            request.Ltv,
            request.CapacityRating,
            request.Recommendation,
            request.Notes,
            request.MemoPath,
            request.MemoFileName,
            request.ClearMemo);

        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        _repository.Update(application);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}

// ─── Credit Officer or Legal Officer: return application to LO ───────────────

public record ReturnFromCreditReviewCommand(Guid ApplicationId, Guid UserId, string Reason) : IRequest<ApplicationResult>;

public class ReturnFromCreditReviewHandler : IRequestHandler<ReturnFromCreditReviewCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ReturnFromCreditReviewHandler(ILoanApplicationRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(ReturnFromCreditReviewCommand request, CancellationToken ct = default)
    {
        var application = await _repository.GetByIdAsync(request.ApplicationId, ct);
        if (application == null)
            return ApplicationResult.Failure("Loan application not found");

        var result = application.ReturnFromCreditReview(request.UserId, request.Reason);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        _repository.Update(application);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}

// ─── Credit Officer: approve → CommitteeCirculation (gated on legal flag) ────

public record ApproveCreditReviewCommand(Guid ApplicationId, Guid UserId, string? Note) : IRequest<ApplicationResult>;

public class ApproveCreditReviewHandler : IRequestHandler<ApproveCreditReviewCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ApproveCreditReviewHandler(ILoanApplicationRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(ApproveCreditReviewCommand request, CancellationToken ct = default)
    {
        var application = await _repository.GetByIdAsync(request.ApplicationId, ct);
        if (application == null)
            return ApplicationResult.Failure("Loan application not found");

        var result = application.ApproveCreditReview(request.UserId, request.Note);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        _repository.Update(application);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}

// ─── Final Approver: ratify committee decision → Ratification ────────────────

public record RatifyCorporateLoanCommand(
    Guid ApplicationId,
    Guid UserId,
    decimal? ApprovedAmount,
    int? ApprovedTenorMonths,
    decimal? ApprovedInterestRate,
    string? Note
) : IRequest<ApplicationResult>;

public class RatifyCorporateLoanHandler : IRequestHandler<RatifyCorporateLoanCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public RatifyCorporateLoanHandler(ILoanApplicationRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(RatifyCorporateLoanCommand request, CancellationToken ct = default)
    {
        var application = await _repository.GetByIdAsync(request.ApplicationId, ct);
        if (application == null)
            return ApplicationResult.Failure("Loan application not found");

        var amount = request.ApprovedAmount.HasValue
            ? Money.Create(request.ApprovedAmount.Value, application.RequestedAmount.Currency)
            : application.RequestedAmount;
        var tenor = request.ApprovedTenorMonths ?? application.ApprovedTenorMonths ?? application.RequestedTenorMonths;
        var rate = request.ApprovedInterestRate ?? application.ApprovedInterestRate ?? application.InterestRatePerAnnum;

        var result = application.Ratify(request.UserId, amount, tenor, rate, request.Note);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        _repository.Update(application);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}

// ─── Issue offer letter from Ratification → OfferGenerated ───────────────────

public record IssueRatifiedOfferLetterCommand(Guid ApplicationId, Guid UserId) : IRequest<ApplicationResult>;

public class IssueRatifiedOfferLetterHandler : IRequestHandler<IssueRatifiedOfferLetterCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _repository;
    private readonly ILoanProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;

    public IssueRatifiedOfferLetterHandler(
        ILoanApplicationRepository repository,
        ILoanProductRepository productRepository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(IssueRatifiedOfferLetterCommand request, CancellationToken ct = default)
    {
        var loanApp = await _repository.GetByIdWithChecklistAsync(request.ApplicationId, ct);
        if (loanApp == null)
            return ApplicationResult.Failure("Loan application not found");

        var issueResult = loanApp.IssueRatifiedOfferLetter(request.UserId);
        if (issueResult.IsFailure)
            return ApplicationResult.Failure(issueResult.Error);

        var product = await _productRepository.GetByIdAsync(loanApp.LoanProductId, ct);
        if (product != null && product.DisbursementChecklist.Any())
            loanApp.SeedChecklistItems(product.DisbursementChecklist);

        _repository.Update(loanApp);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}

// ─── Final Approver: decline ratification → RatificationDeclined ─────────────

public record DeclineRatificationCommand(Guid ApplicationId, Guid UserId, string Reason) : IRequest<ApplicationResult>;

public class DeclineRatificationHandler : IRequestHandler<DeclineRatificationCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public DeclineRatificationHandler(ILoanApplicationRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(DeclineRatificationCommand request, CancellationToken ct = default)
    {
        var application = await _repository.GetByIdAsync(request.ApplicationId, ct);
        if (application == null)
            return ApplicationResult.Failure("Loan application not found");

        var result = application.DeclineRatification(request.UserId, request.Reason);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        _repository.Update(application);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}

// ─── Loan Officer: record customer acceptance OfferGenerated → OfferAccepted ──

public record RecordCorporateOfferAcceptanceCommand(
    Guid ApplicationId,
    Guid UserId,
    DateTime CustomerSignedAt,
    OfferAcceptanceMethod AcceptanceMethod,
    bool KfsAcknowledged
) : IRequest<ApplicationResult>;

public class RecordCorporateOfferAcceptanceHandler : IRequestHandler<RecordCorporateOfferAcceptanceCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public RecordCorporateOfferAcceptanceHandler(ILoanApplicationRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(RecordCorporateOfferAcceptanceCommand request, CancellationToken ct = default)
    {
        var loanApp = await _repository.GetByIdWithChecklistAsync(request.ApplicationId, ct);
        if (loanApp == null)
            return ApplicationResult.Failure("Loan application not found");

        var result = loanApp.RecordCorporateOfferAcceptance(
            request.UserId,
            request.CustomerSignedAt,
            request.AcceptanceMethod,
            request.KfsAcknowledged);

        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        _repository.Update(loanApp);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}

// ─── Legal Officer: complete security perfection → Disbursement ───────────────

public record CompleteSecurityPerfectionNewCommand(Guid ApplicationId, Guid UserId, string? Note) : IRequest<ApplicationResult>;

public class CompleteSecurityPerfectionNewHandler : IRequestHandler<CompleteSecurityPerfectionNewCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CompleteSecurityPerfectionNewHandler(ILoanApplicationRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(CompleteSecurityPerfectionNewCommand request, CancellationToken ct = default)
    {
        var application = await _repository.GetByIdAsync(request.ApplicationId, ct);
        if (application == null)
            return ApplicationResult.Failure("Loan application not found");

        var result = application.CompleteSecurityPerfection(request.UserId, request.Note);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        _repository.Update(application);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}

// ─── DeploymentOfficer: disburse → Disbursed (Fineract auto-booking) ────────

public record CompleteCorporateDisbursementCommand(
    Guid ApplicationId,
    Guid UserId
) : IRequest<ApplicationResult>;

public class CompleteCorporateDisbursementHandler : IRequestHandler<CompleteCorporateDisbursementCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _repository;
    private readonly ILoanProductRepository _productRepo;
    private readonly IFineractDirectService _fineractService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CompleteCorporateDisbursementHandler> _logger;

    public CompleteCorporateDisbursementHandler(
        ILoanApplicationRepository repository,
        ILoanProductRepository productRepo,
        IFineractDirectService fineractService,
        IUnitOfWork unitOfWork,
        ILogger<CompleteCorporateDisbursementHandler> logger)
    {
        _repository = repository;
        _productRepo = productRepo;
        _fineractService = fineractService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApplicationResult> Handle(CompleteCorporateDisbursementCommand request, CancellationToken ct = default)
    {
        var application = await _repository.GetByIdAsync(request.ApplicationId, ct);
        if (application == null)
            return ApplicationResult.Failure("Loan application not found");

        if (application.ApprovedAmount == null || application.ApprovedAmount.Amount <= 0)
            return ApplicationResult.Failure("Approved loan amount is not set — ratification must complete before disbursement.");
        if (application.ApprovedTenorMonths is null or <= 0)
            return ApplicationResult.Failure("Approved tenor is not set — ratification must complete before disbursement.");
        if (application.ApprovedInterestRate is null)
            return ApplicationResult.Failure("Approved interest rate is not set — ratification must complete before disbursement.");

        // Resolve Fineract clientId from the customer's BOA account number.
        var accountResult = await _fineractService.GetNampBoaAccountAsync(application.AccountNumber, ct);
        if (accountResult.IsFailure)
            return ApplicationResult.Failure($"Could not resolve customer Fineract account: {accountResult.Error}");

        // Resolve FineractProductId from the loan product.
        var product = await _productRepo.GetByIdAsync(application.LoanProductId, ct);
        if (product?.FineractProductId is null or <= 0)
            return ApplicationResult.Failure("Loan product is not mapped to a Fineract product — contact system admin.");

        var bookingRequest = new FineractLoanBookingRequest(
            ClientId: accountResult.Value.ClientId,
            ProductId: product.FineractProductId.Value,
            Principal: application.ApprovedAmount.Amount,
            TenorMonths: application.ApprovedTenorMonths.Value,
            InterestRatePerAnnum: application.ApprovedInterestRate.Value,
            ValueDate: DateTime.UtcNow,
            RepaymentAccountNumber: application.AccountNumber,
            DisburseToSavings: true,
            CreateRepaymentStandingInstruction: true);

        var bookingResult = await _fineractService.BookApprovedLoanAsync(bookingRequest, ct);
        if (bookingResult.IsFailure)
            return ApplicationResult.Failure($"Fineract loan booking failed: {bookingResult.Error}");

        _logger.LogInformation("Corporate disbursement: Fineract loan booked — LoanId={LoanId}, Account={Account}",
            bookingResult.Value.LoanId, bookingResult.Value.LoanAccountNumber);

        var result = application.CompleteDisbursement(bookingResult.Value.LoanAccountNumber, request.UserId, bookingResult.Value.LoanId);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        _repository.Update(application);
        await _unitOfWork.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}

// ─── Loan Account: get live Fineract loan details ─────────────────────────────

public record GetCorporateLoanAccountQuery(Guid ApplicationId)
    : IRequest<ApplicationResult<NampLoanAccountDto>>;

public class GetCorporateLoanAccountHandler
    : IRequestHandler<GetCorporateLoanAccountQuery, ApplicationResult<NampLoanAccountDto>>
{
    private readonly ILoanApplicationRepository _repository;
    private readonly IFineractDirectService _fineract;

    public GetCorporateLoanAccountHandler(ILoanApplicationRepository repository, IFineractDirectService fineract)
    {
        _repository = repository;
        _fineract = fineract;
    }

    public async Task<ApplicationResult<NampLoanAccountDto>> Handle(
        GetCorporateLoanAccountQuery request, CancellationToken ct = default)
    {
        var app = await _repository.GetByIdAsync(request.ApplicationId, ct);
        if (app is null)
            return ApplicationResult<NampLoanAccountDto>.Failure("Loan application not found.");

        long fineractLoanId;

        if (app.FineractLoanId.HasValue)
        {
            fineractLoanId = app.FineractLoanId.Value;
        }
        else if (!string.IsNullOrWhiteSpace(app.CoreBankingLoanId))
        {
            // Legacy path: FineractLoanId was not stored — resolve it via client account lookup.
            var accountResult = await _fineract.GetNampBoaAccountAsync(app.AccountNumber, ct);
            if (accountResult.IsFailure)
                return ApplicationResult<NampLoanAccountDto>.Failure($"Could not resolve customer Fineract account: {accountResult.Error}");

            var clientAccounts = await _fineract.GetClientAccountsAsync(accountResult.Value.ClientId, ct);
            if (clientAccounts.IsFailure)
                return ApplicationResult<NampLoanAccountDto>.Failure($"Could not fetch client loan accounts: {clientAccounts.Error}");

            var match = clientAccounts.Value.LoanAccounts
                .FirstOrDefault(l => l.AccountNo == app.CoreBankingLoanId);
            if (match is null)
                return ApplicationResult<NampLoanAccountDto>.Failure($"Loan account {app.CoreBankingLoanId} not found in the Core Banking System for this client.");

            fineractLoanId = match.Id;
        }
        else
        {
            return ApplicationResult<NampLoanAccountDto>.Failure("No Core Banking loan account is linked to this application.");
        }

        var result = await _fineract.GetLoanDetailAsync(fineractLoanId, ct);
        if (!result.IsSuccess)
            return ApplicationResult<NampLoanAccountDto>.Failure($"Could not load loan account from Fineract: {result.Error}");

        var loan = result.Value;
        var now = DateTime.UtcNow;

        var dto = new NampLoanAccountDto(
            LoanId: loan.Id,
            AccountNo: loan.AccountNo,
            ProductName: loan.ProductName,
            Status: loan.Status,
            DisbursementDate: loan.DisbursementDate,
            MaturityDate: loan.MaturityDate,
            TotalExpectedRepayment: loan.Summary.TotalExpectedRepayment,
            TotalRepayment: loan.Summary.TotalRepayment,
            TotalOutstanding: loan.Summary.TotalOutstanding,
            PrincipalDisbursed: loan.Summary.PrincipalDisbursed,
            PrincipalPaid: loan.Summary.PrincipalPaid,
            PrincipalOutstanding: loan.Summary.PrincipalOutstanding,
            InterestCharged: loan.Summary.InterestCharged,
            InterestPaid: loan.Summary.InterestPaid,
            InterestOutstanding: loan.Summary.InterestOutstanding,
            PenaltyChargesOutstanding: loan.Summary.PenaltyChargesOutstanding,
            Schedule: loan.RepaymentSchedule
                .Where(p => p.Period > 0)
                .Select(p => new NampLoanSchedulePeriodDto(
                    Period: p.Period,
                    DueDate: p.DueDate,
                    PrincipalDue: p.PrincipalDue,
                    PrincipalPaid: p.PrincipalPaid,
                    InterestDue: p.InterestDue,
                    InterestPaid: p.InterestPaid,
                    TotalDue: p.TotalDue,
                    TotalPaid: p.TotalPaid,
                    TotalOutstanding: p.TotalOutstanding,
                    Complete: p.Complete,
                    IsOverdue: !p.Complete && p.DueDate < now
                ))
                .ToList()
        );

        return ApplicationResult<NampLoanAccountDto>.Success(dto);
    }
}

// ─── Loan Account: mark loan as closed ───────────────────────────────────────

public record MarkCorporateClosedCommand(Guid ApplicationId, Guid UserId) : IRequest<ApplicationResult>;

public class MarkCorporateClosedHandler : IRequestHandler<MarkCorporateClosedCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public MarkCorporateClosedHandler(ILoanApplicationRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(MarkCorporateClosedCommand request, CancellationToken ct = default)
    {
        var application = await _repository.GetByIdAsync(request.ApplicationId, ct);
        if (application is null)
            return ApplicationResult.Failure("Loan application not found");

        var result = application.MarkAsClosed(request.UserId);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        _repository.Update(application);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}

// ─── Operations: return from Disbursement → SecurityPerfection ───────────────

public record ReturnFromDisbursementNewCommand(Guid ApplicationId, Guid UserId, string Reason) : IRequest<ApplicationResult>;

public class ReturnFromDisbursementNewHandler : IRequestHandler<ReturnFromDisbursementNewCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ReturnFromDisbursementNewHandler(ILoanApplicationRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(ReturnFromDisbursementNewCommand request, CancellationToken ct = default)
    {
        var application = await _repository.GetByIdAsync(request.ApplicationId, ct);
        if (application == null)
            return ApplicationResult.Failure("Loan application not found");

        var result = application.ReturnFromDisbursement(request.UserId, request.Reason);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        _repository.Update(application);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}

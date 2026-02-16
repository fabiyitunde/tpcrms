using CRMS.Application.Common;
using CRMS.Application.LoanApplication.DTOs;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using LA = CRMS.Domain.Aggregates.LoanApplication;

namespace CRMS.Application.LoanApplication.Queries;

public record GetLoanApplicationByIdQuery(Guid Id) : IRequest<ApplicationResult<LoanApplicationDto>>;

public class GetLoanApplicationByIdHandler : IRequestHandler<GetLoanApplicationByIdQuery, ApplicationResult<LoanApplicationDto>>
{
    private readonly ILoanApplicationRepository _repository;

    public GetLoanApplicationByIdHandler(ILoanApplicationRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<LoanApplicationDto>> Handle(GetLoanApplicationByIdQuery request, CancellationToken ct = default)
    {
        var application = await _repository.GetByIdAsync(request.Id, ct);
        if (application == null)
            return ApplicationResult<LoanApplicationDto>.Failure("Loan application not found");

        return ApplicationResult<LoanApplicationDto>.Success(MapToDto(application));
    }

    private static LoanApplicationDto MapToDto(LA.LoanApplication app) => new(
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
            d.Id, d.Category.ToString(), d.Status.ToString(), d.FileName,
            d.FileSize, d.ContentType, d.Description, d.UploadedAt,
            d.VerifiedAt, d.RejectionReason)).ToList(),
        app.Parties.Select(p => new LoanApplicationPartyDto(
            p.Id, p.PartyType.ToString(), p.FullName, p.BVN, p.Email,
            p.PhoneNumber, p.Designation, p.ShareholdingPercent, p.BVNVerified)).ToList()
    );
}

public record GetLoanApplicationByNumberQuery(string ApplicationNumber) : IRequest<ApplicationResult<LoanApplicationDto>>;

public class GetLoanApplicationByNumberHandler : IRequestHandler<GetLoanApplicationByNumberQuery, ApplicationResult<LoanApplicationDto>>
{
    private readonly ILoanApplicationRepository _repository;

    public GetLoanApplicationByNumberHandler(ILoanApplicationRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<LoanApplicationDto>> Handle(GetLoanApplicationByNumberQuery request, CancellationToken ct = default)
    {
        var application = await _repository.GetByApplicationNumberAsync(request.ApplicationNumber, ct);
        if (application == null)
            return ApplicationResult<LoanApplicationDto>.Failure("Loan application not found");

        return ApplicationResult<LoanApplicationDto>.Success(MapToDto(application));
    }

    private static LoanApplicationDto MapToDto(LA.LoanApplication app) => new(
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
            d.Id, d.Category.ToString(), d.Status.ToString(), d.FileName,
            d.FileSize, d.ContentType, d.Description, d.UploadedAt,
            d.VerifiedAt, d.RejectionReason)).ToList(),
        app.Parties.Select(p => new LoanApplicationPartyDto(
            p.Id, p.PartyType.ToString(), p.FullName, p.BVN, p.Email,
            p.PhoneNumber, p.Designation, p.ShareholdingPercent, p.BVNVerified)).ToList()
    );
}

public record GetLoanApplicationsByStatusQuery(LoanApplicationStatus Status) : IRequest<ApplicationResult<List<LoanApplicationSummaryDto>>>;

public class GetLoanApplicationsByStatusHandler : IRequestHandler<GetLoanApplicationsByStatusQuery, ApplicationResult<List<LoanApplicationSummaryDto>>>
{
    private readonly ILoanApplicationRepository _repository;

    public GetLoanApplicationsByStatusHandler(ILoanApplicationRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<List<LoanApplicationSummaryDto>>> Handle(GetLoanApplicationsByStatusQuery request, CancellationToken ct = default)
    {
        var applications = await _repository.GetByStatusAsync(request.Status, ct);
        var dtos = applications.Select(app => new LoanApplicationSummaryDto(
            app.Id,
            app.ApplicationNumber,
            app.Type.ToString(),
            app.Status.ToString(),
            app.ProductCode,
            app.CustomerName,
            app.RequestedAmount.Amount,
            app.RequestedAmount.Currency,
            app.SubmittedAt,
            app.CreatedAt
        )).ToList();

        return ApplicationResult<List<LoanApplicationSummaryDto>>.Success(dtos);
    }
}

public record GetMyLoanApplicationsQuery(Guid UserId) : IRequest<ApplicationResult<List<LoanApplicationSummaryDto>>>;

public class GetMyLoanApplicationsHandler : IRequestHandler<GetMyLoanApplicationsQuery, ApplicationResult<List<LoanApplicationSummaryDto>>>
{
    private readonly ILoanApplicationRepository _repository;

    public GetMyLoanApplicationsHandler(ILoanApplicationRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<List<LoanApplicationSummaryDto>>> Handle(GetMyLoanApplicationsQuery request, CancellationToken ct = default)
    {
        var applications = await _repository.GetByInitiatorAsync(request.UserId, ct);
        var dtos = applications.Select(app => new LoanApplicationSummaryDto(
            app.Id,
            app.ApplicationNumber,
            app.Type.ToString(),
            app.Status.ToString(),
            app.ProductCode,
            app.CustomerName,
            app.RequestedAmount.Amount,
            app.RequestedAmount.Currency,
            app.SubmittedAt,
            app.CreatedAt
        )).ToList();

        return ApplicationResult<List<LoanApplicationSummaryDto>>.Success(dtos);
    }
}

public record GetPendingBranchReviewQuery(Guid? BranchId) : IRequest<ApplicationResult<List<LoanApplicationSummaryDto>>>;

public class GetPendingBranchReviewHandler : IRequestHandler<GetPendingBranchReviewQuery, ApplicationResult<List<LoanApplicationSummaryDto>>>
{
    private readonly ILoanApplicationRepository _repository;

    public GetPendingBranchReviewHandler(ILoanApplicationRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<List<LoanApplicationSummaryDto>>> Handle(GetPendingBranchReviewQuery request, CancellationToken ct = default)
    {
        var applications = await _repository.GetPendingBranchReviewAsync(request.BranchId, ct);
        var dtos = applications.Select(app => new LoanApplicationSummaryDto(
            app.Id,
            app.ApplicationNumber,
            app.Type.ToString(),
            app.Status.ToString(),
            app.ProductCode,
            app.CustomerName,
            app.RequestedAmount.Amount,
            app.RequestedAmount.Currency,
            app.SubmittedAt,
            app.CreatedAt
        )).ToList();

        return ApplicationResult<List<LoanApplicationSummaryDto>>.Success(dtos);
    }
}

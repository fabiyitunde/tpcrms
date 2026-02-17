using CRMS.Application.Common;
using CRMS.Application.CreditBureau.DTOs;
using CRMS.Domain.Aggregates.CreditBureau;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.CreditBureau.Queries;

public record GetBureauReportByIdQuery(Guid Id) : IRequest<ApplicationResult<BureauReportDto>>;

public class GetBureauReportByIdHandler : IRequestHandler<GetBureauReportByIdQuery, ApplicationResult<BureauReportDto>>
{
    private readonly IBureauReportRepository _repository;

    public GetBureauReportByIdHandler(IBureauReportRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<BureauReportDto>> Handle(GetBureauReportByIdQuery request, CancellationToken ct = default)
    {
        var report = await _repository.GetByIdWithDetailsAsync(request.Id, ct);
        if (report == null)
            return ApplicationResult<BureauReportDto>.Failure("Bureau report not found");

        return ApplicationResult<BureauReportDto>.Success(MapToDto(report));
    }

    private static BureauReportDto MapToDto(BureauReport r) => new(
        r.Id,
        r.LoanApplicationId,
        r.Provider.ToString(),
        r.SubjectType.ToString(),
        r.Status.ToString(),
        r.BVN,
        r.RegistryId,
        r.SubjectName,
        r.CreditScore,
        r.ScoreGrade,
        r.ReportDate,
        r.TotalAccounts,
        r.PerformingAccounts,
        r.NonPerformingAccounts,
        r.ClosedAccounts,
        r.TotalOutstandingBalance,
        r.TotalCreditLimit,
        r.MaxDelinquencyDays,
        r.HasLegalActions,
        r.RequestReference,
        r.RequestedAt,
        r.CompletedAt,
        r.ErrorMessage,
        r.Accounts.Select(a => new BureauAccountDto(
            a.Id, a.AccountNumber, a.CreditorName, a.AccountType, a.Status.ToString(),
            a.DelinquencyLevel.ToString(), a.CreditLimit, a.Balance, a.DateOpened,
            a.LastPaymentDate, a.PaymentProfile, a.LegalStatus.ToString()
        )).ToList(),
        r.ScoreFactors.Select(f => new BureauScoreFactorDto(
            f.Id, f.FactorCode, f.FactorDescription, f.Impact, f.Rank
        )).ToList()
    );
}

public record GetBureauReportsByLoanApplicationQuery(Guid LoanApplicationId) : IRequest<ApplicationResult<List<BureauReportSummaryDto>>>;

public class GetBureauReportsByLoanApplicationHandler : IRequestHandler<GetBureauReportsByLoanApplicationQuery, ApplicationResult<List<BureauReportSummaryDto>>>
{
    private readonly IBureauReportRepository _repository;

    public GetBureauReportsByLoanApplicationHandler(IBureauReportRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<List<BureauReportSummaryDto>>> Handle(GetBureauReportsByLoanApplicationQuery request, CancellationToken ct = default)
    {
        var reports = await _repository.GetByLoanApplicationIdAsync(request.LoanApplicationId, ct);
        
        var dtos = reports.Select(r => new BureauReportSummaryDto(
            r.Id,
            r.Provider.ToString(),
            r.SubjectName,
            r.Status.ToString(),
            r.CreditScore,
            r.ScoreGrade,
            r.RequestedAt,
            r.CompletedAt
        )).ToList();

        return ApplicationResult<List<BureauReportSummaryDto>>.Success(dtos);
    }
}

public record SearchBureauByBVNQuery(string BVN) : IRequest<ApplicationResult<BureauSearchResultDto>>;

public class SearchBureauByBVNHandler : IRequestHandler<SearchBureauByBVNQuery, ApplicationResult<BureauSearchResultDto>>
{
    private readonly ICreditBureauProvider _bureauProvider;

    public SearchBureauByBVNHandler(ICreditBureauProvider bureauProvider)
    {
        _bureauProvider = bureauProvider;
    }

    public async Task<ApplicationResult<BureauSearchResultDto>> Handle(SearchBureauByBVNQuery request, CancellationToken ct = default)
    {
        var result = await _bureauProvider.SearchByBVNAsync(request.BVN, ct);
        if (result.IsFailure)
            return ApplicationResult<BureauSearchResultDto>.Failure(result.Error);

        var search = result.Value;
        return ApplicationResult<BureauSearchResultDto>.Success(new BureauSearchResultDto(
            search.Found,
            search.RegistryId,
            search.FullName,
            search.BVN,
            search.DateOfBirth,
            search.Gender,
            search.Phone,
            search.Email,
            search.Address
        ));
    }
}

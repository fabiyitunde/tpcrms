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
        r.ActiveLoans,
        r.PerformingAccounts,
        r.DelinquentFacilities,
        r.ClosedAccounts,
        r.TotalOutstandingBalance,
        r.TotalOverdue,
        r.TotalCreditLimit,
        r.MaxDelinquencyDays,
        r.HasLegalActions,
        r.RequestReference,
        r.RequestedAt,
        r.CompletedAt,
        r.ErrorMessage,
        r.FraudRiskScore,
        r.FraudRecommendation,
        r.PartyId,
        r.PartyType,
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

        // When a ConsentRequired check is retried, a new report is created alongside the old one.
        // Show only the latest report per BVN (or per business subject) so duplicates don't appear.
        var deduped = reports
            .GroupBy(r => r.BVN ?? $"business:{r.SubjectType}")
            .Select(g => g.OrderByDescending(r => r.RequestedAt).First())
            .ToList();

        var dtos = deduped.Select(r => new BureauReportSummaryDto(
            r.Id,
            r.Provider.ToString(),
            r.SubjectType.ToString(),
            r.SubjectName,
            r.Status.ToString(),
            r.CreditScore,
            r.ScoreGrade,
            r.ActiveLoans,
            r.TotalOutstandingBalance,
            r.TotalOverdue,
            r.MaxDelinquencyDays,
            r.HasLegalActions,
            r.FraudRiskScore,
            r.FraudRecommendation,
            r.PartyId,
            r.PartyType,
            r.RequestedAt,
            r.CompletedAt,
            r.ErrorMessage
        )).ToList();

        return ApplicationResult<List<BureauReportSummaryDto>>.Success(dtos);
    }
}

public record SearchBureauByBVNQuery(string BVN) : IRequest<ApplicationResult<BureauSearchResultDto>>;

public class SearchBureauByBVNHandler : IRequestHandler<SearchBureauByBVNQuery, ApplicationResult<BureauSearchResultDto>>
{
    private readonly ISmartComplyProvider _smartComplyProvider;

    public SearchBureauByBVNHandler(ISmartComplyProvider smartComplyProvider)
    {
        _smartComplyProvider = smartComplyProvider;
    }

    public async Task<ApplicationResult<BureauSearchResultDto>> Handle(SearchBureauByBVNQuery request, CancellationToken ct = default)
    {
        var result = await _smartComplyProvider.VerifyBvnAsync(request.BVN, ct);
        if (result.IsFailure)
            return ApplicationResult<BureauSearchResultDto>.Failure(result.Error);

        var bvnData = result.Value;
        var fullName = string.Join(" ", new[] { bvnData.FirstName, bvnData.MiddleName, bvnData.LastName }
            .Where(n => !string.IsNullOrWhiteSpace(n)));

        return ApplicationResult<BureauSearchResultDto>.Success(new BureauSearchResultDto(
            Found: !string.IsNullOrWhiteSpace(fullName),
            RegistryId: request.BVN,
            FullName: fullName,
            BVN: request.BVN,
            DateOfBirth: bvnData.DateOfBirth,
            Gender: null,
            Phone: bvnData.PhoneNumber,
            Email: null,
            Address: null
        ));
    }
}

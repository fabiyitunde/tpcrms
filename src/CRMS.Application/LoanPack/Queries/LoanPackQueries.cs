using CRMS.Application.Common;
using CRMS.Domain.Interfaces;
using LP = CRMS.Domain.Aggregates.LoanPack;

namespace CRMS.Application.LoanPack.Queries;

public record LoanPackDto(
    Guid Id,
    Guid LoanApplicationId,
    string ApplicationNumber,
    int Version,
    string Status,
    DateTime GeneratedAt,
    string GeneratedByUserName,
    string FileName,
    string StoragePath,
    long FileSizeBytes,
    string CustomerName,
    string ProductName,
    decimal RequestedAmount,
    decimal? RecommendedAmount,
    int? OverallRiskScore,
    string? RiskRating,
    int DirectorCount,
    int BureauReportCount,
    int CollateralCount,
    int GuarantorCount,
    bool IncludesAIAdvisory
);

public record LoanPackSummaryDto(
    Guid Id,
    int Version,
    string Status,
    DateTime GeneratedAt,
    string GeneratedByUserName,
    string FileName,
    long FileSizeBytes
);

// Get loan pack by ID
public record GetLoanPackByIdQuery(Guid Id) : IRequest<ApplicationResult<LoanPackDto>>;

public class GetLoanPackByIdHandler : IRequestHandler<GetLoanPackByIdQuery, ApplicationResult<LoanPackDto>>
{
    private readonly ILoanPackRepository _repository;

    public GetLoanPackByIdHandler(ILoanPackRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<LoanPackDto>> Handle(GetLoanPackByIdQuery request, CancellationToken ct = default)
    {
        var pack = await _repository.GetByIdAsync(request.Id, ct);
        if (pack == null)
            return ApplicationResult<LoanPackDto>.Failure("Loan pack not found");

        return ApplicationResult<LoanPackDto>.Success(MapToDto(pack));
    }

    private static LoanPackDto MapToDto(LP.LoanPack pack) => new(
        pack.Id,
        pack.LoanApplicationId,
        pack.ApplicationNumber,
        pack.Version,
        pack.Status.ToString(),
        pack.GeneratedAt,
        pack.GeneratedByUserName,
        pack.FileName,
        pack.StoragePath,
        pack.FileSizeBytes,
        pack.CustomerName,
        pack.ProductName,
        pack.RequestedAmount,
        pack.RecommendedAmount,
        pack.OverallRiskScore,
        pack.RiskRating,
        pack.DirectorCount,
        pack.BureauReportCount,
        pack.CollateralCount,
        pack.GuarantorCount,
        pack.IncludesAIAdvisory);
}

// Get latest loan pack for application
public record GetLatestLoanPackQuery(Guid LoanApplicationId) : IRequest<ApplicationResult<LoanPackDto>>;

public class GetLatestLoanPackHandler : IRequestHandler<GetLatestLoanPackQuery, ApplicationResult<LoanPackDto>>
{
    private readonly ILoanPackRepository _repository;

    public GetLatestLoanPackHandler(ILoanPackRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<LoanPackDto>> Handle(GetLatestLoanPackQuery request, CancellationToken ct = default)
    {
        var pack = await _repository.GetLatestByLoanApplicationIdAsync(request.LoanApplicationId, ct);
        if (pack == null)
            return ApplicationResult<LoanPackDto>.Failure("No loan pack found for this application");

        return ApplicationResult<LoanPackDto>.Success(new LoanPackDto(
            pack.Id,
            pack.LoanApplicationId,
            pack.ApplicationNumber,
            pack.Version,
            pack.Status.ToString(),
            pack.GeneratedAt,
            pack.GeneratedByUserName,
            pack.FileName,
            pack.StoragePath,
            pack.FileSizeBytes,
            pack.CustomerName,
            pack.ProductName,
            pack.RequestedAmount,
            pack.RecommendedAmount,
            pack.OverallRiskScore,
            pack.RiskRating,
            pack.DirectorCount,
            pack.BureauReportCount,
            pack.CollateralCount,
            pack.GuarantorCount,
            pack.IncludesAIAdvisory));
    }
}

// Get all versions for application
public record GetLoanPackVersionsQuery(Guid LoanApplicationId) : IRequest<ApplicationResult<List<LoanPackSummaryDto>>>;

public class GetLoanPackVersionsHandler : IRequestHandler<GetLoanPackVersionsQuery, ApplicationResult<List<LoanPackSummaryDto>>>
{
    private readonly ILoanPackRepository _repository;

    public GetLoanPackVersionsHandler(ILoanPackRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<List<LoanPackSummaryDto>>> Handle(GetLoanPackVersionsQuery request, CancellationToken ct = default)
    {
        var packs = await _repository.GetAllByLoanApplicationIdAsync(request.LoanApplicationId, ct);

        var dtos = packs.Select(p => new LoanPackSummaryDto(
            p.Id,
            p.Version,
            p.Status.ToString(),
            p.GeneratedAt,
            p.GeneratedByUserName,
            p.FileName,
            p.FileSizeBytes)).ToList();

        return ApplicationResult<List<LoanPackSummaryDto>>.Success(dtos);
    }
}

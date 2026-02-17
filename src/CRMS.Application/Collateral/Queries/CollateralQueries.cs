using CRMS.Application.Collateral.DTOs;
using CRMS.Application.Common;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Collateral.Queries;

public record GetCollateralByIdQuery(Guid Id) : IRequest<ApplicationResult<CollateralDto>>;

public class GetCollateralByIdHandler : IRequestHandler<GetCollateralByIdQuery, ApplicationResult<CollateralDto>>
{
    private readonly ICollateralRepository _repository;

    public GetCollateralByIdHandler(ICollateralRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<CollateralDto>> Handle(GetCollateralByIdQuery request, CancellationToken ct = default)
    {
        var collateral = await _repository.GetByIdWithDetailsAsync(request.Id, ct);
        if (collateral == null)
            return ApplicationResult<CollateralDto>.Failure("Collateral not found");

        return ApplicationResult<CollateralDto>.Success(MapToDto(collateral));
    }

    private static CollateralDto MapToDto(Domain.Aggregates.Collateral.Collateral c) => new(
        c.Id, c.LoanApplicationId, c.CollateralReference, c.Type.ToString(), c.Status.ToString(),
        c.PerfectionStatus.ToString(), c.Description, c.AssetIdentifier, c.Location, c.OwnerName,
        c.OwnershipType, c.MarketValue?.Amount, c.ForcedSaleValue?.Amount, c.HaircutPercentage,
        c.AcceptableValue?.Amount, c.MarketValue?.Currency, c.LastValuationDate, c.NextRevaluationDue,
        c.LienType?.ToString(), c.LienReference, c.LienRegistrationDate, c.IsInsured,
        c.InsurancePolicyNumber, c.InsuredValue?.Amount, c.InsuranceExpiryDate, c.CreatedAt, c.ApprovedAt,
        c.RejectionReason,
        c.Valuations.Select(v => new CollateralValuationDto(v.Id, v.Type.ToString(), v.Status.ToString(),
            v.ValuationDate, v.MarketValue.Amount, v.ForcedSaleValue?.Amount, v.MarketValue.Currency,
            v.ValuerName, v.ValuerCompany, v.ExpiryDate)).ToList(),
        c.Documents.Select(d => new CollateralDocumentDto(d.Id, d.DocumentType, d.FileName,
            d.FileSizeBytes, d.IsVerified, d.UploadedAt)).ToList()
    );
}

public record GetCollateralByLoanApplicationQuery(Guid LoanApplicationId) : IRequest<ApplicationResult<List<CollateralSummaryDto>>>;

public class GetCollateralByLoanApplicationHandler : IRequestHandler<GetCollateralByLoanApplicationQuery, ApplicationResult<List<CollateralSummaryDto>>>
{
    private readonly ICollateralRepository _repository;

    public GetCollateralByLoanApplicationHandler(ICollateralRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<List<CollateralSummaryDto>>> Handle(GetCollateralByLoanApplicationQuery request, CancellationToken ct = default)
    {
        var collaterals = await _repository.GetByLoanApplicationIdAsync(request.LoanApplicationId, ct);

        var dtos = collaterals.Select(c => new CollateralSummaryDto(
            c.Id, c.CollateralReference, c.Type.ToString(), c.Status.ToString(),
            c.Description, c.AcceptableValue?.Amount, c.AcceptableValue?.Currency,
            c.PerfectionStatus.ToString(), c.CreatedAt
        )).ToList();

        return ApplicationResult<List<CollateralSummaryDto>>.Success(dtos);
    }
}

public record CalculateLTVQuery(Guid LoanApplicationId, decimal LoanAmount, string Currency) : IRequest<ApplicationResult<LTVCalculationDto>>;

public record LTVCalculationDto(
    decimal TotalAcceptableCollateralValue,
    decimal LoanAmount,
    decimal LTV,
    bool IsAdequate,
    string Assessment,
    List<CollateralSummaryDto> Collaterals
);

public class CalculateLTVHandler : IRequestHandler<CalculateLTVQuery, ApplicationResult<LTVCalculationDto>>
{
    private readonly ICollateralRepository _repository;

    public CalculateLTVHandler(ICollateralRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<LTVCalculationDto>> Handle(CalculateLTVQuery request, CancellationToken ct = default)
    {
        var collaterals = await _repository.GetByLoanApplicationIdAsync(request.LoanApplicationId, ct);

        var approvedCollaterals = collaterals
            .Where(c => c.Status == Domain.Enums.CollateralStatus.Approved || 
                        c.Status == Domain.Enums.CollateralStatus.Perfected)
            .ToList();

        var totalAcceptableValue = approvedCollaterals
            .Where(c => c.AcceptableValue != null && c.AcceptableValue.Currency == request.Currency)
            .Sum(c => c.AcceptableValue!.Amount);

        var ltv = totalAcceptableValue > 0 
            ? (request.LoanAmount / totalAcceptableValue) * 100 
            : 100;

        var isAdequate = ltv <= 80; // Standard LTV threshold
        var assessment = ltv switch
        {
            <= 50 => "Excellent - Well secured",
            <= 70 => "Good - Adequately secured",
            <= 80 => "Acceptable - Marginally secured",
            <= 100 => "Poor - Under-collateralized",
            _ => "Critical - Significantly under-collateralized"
        };

        var collateralDtos = approvedCollaterals.Select(c => new CollateralSummaryDto(
            c.Id, c.CollateralReference, c.Type.ToString(), c.Status.ToString(),
            c.Description, c.AcceptableValue?.Amount, c.AcceptableValue?.Currency,
            c.PerfectionStatus.ToString(), c.CreatedAt
        )).ToList();

        return ApplicationResult<LTVCalculationDto>.Success(new LTVCalculationDto(
            totalAcceptableValue, request.LoanAmount, Math.Round(ltv, 2),
            isAdequate, assessment, collateralDtos
        ));
    }
}

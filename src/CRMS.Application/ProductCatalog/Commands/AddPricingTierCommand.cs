using CRMS.Application.Common;
using CRMS.Application.ProductCatalog.DTOs;
using CRMS.Application.ProductCatalog.Mappings;
using CRMS.Domain.Interfaces;
using CRMS.Domain.ValueObjects;

namespace CRMS.Application.ProductCatalog.Commands;

public record AddPricingTierCommand(
    Guid ProductId,
    string Name,
    decimal InterestRatePerAnnum,
    InterestRateType RateType,
    decimal? ProcessingFeePercent,
    decimal? ProcessingFeeFixed,
    string? ProcessingFeeCurrency,
    int? MinCreditScore,
    int? MaxCreditScore
) : IRequest<ApplicationResult<PricingTierDto>>;

public class AddPricingTierHandler : IRequestHandler<AddPricingTierCommand, ApplicationResult<PricingTierDto>>
{
    private readonly ILoanProductRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public AddPricingTierHandler(ILoanProductRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<PricingTierDto>> Handle(AddPricingTierCommand request, CancellationToken ct = default)
    {
        var product = await _repository.GetByIdAsync(request.ProductId, ct);
        if (product == null)
            return ApplicationResult<PricingTierDto>.Failure("Product not found");

        Money? processingFeeFixed = null;
        if (request.ProcessingFeeFixed.HasValue)
            processingFeeFixed = Money.Create(request.ProcessingFeeFixed.Value, request.ProcessingFeeCurrency ?? "NGN");

        var result = product.AddPricingTier(
            request.Name,
            request.InterestRatePerAnnum,
            request.RateType,
            request.ProcessingFeePercent,
            processingFeeFixed,
            request.MinCreditScore,
            request.MaxCreditScore
        );

        if (result.IsFailure)
            return ApplicationResult<PricingTierDto>.Failure(result.Error);

        await _repository.UpdateAsync(product, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<PricingTierDto>.Success(result.Value.ToDto());
    }
}

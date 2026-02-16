using CRMS.Application.Common;
using CRMS.Application.ProductCatalog.DTOs;
using CRMS.Application.ProductCatalog.Mappings;
using CRMS.Domain.Interfaces;
using CRMS.Domain.ValueObjects;

namespace CRMS.Application.ProductCatalog.Commands;

public class UpdateLoanProductHandler : IRequestHandler<UpdateLoanProductCommand, ApplicationResult<LoanProductDto>>
{
    private readonly ILoanProductRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateLoanProductHandler(ILoanProductRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<LoanProductDto>> Handle(UpdateLoanProductCommand request, CancellationToken ct = default)
    {
        var product = await _repository.GetByIdAsync(request.Id, ct);
        if (product == null)
            return ApplicationResult<LoanProductDto>.Failure("Product not found");

        var minAmount = Money.Create(request.MinAmount, request.Currency);
        var maxAmount = Money.Create(request.MaxAmount, request.Currency);

        var result = product.Update(
            request.Name,
            request.Description,
            minAmount,
            maxAmount,
            request.MinTenorMonths,
            request.MaxTenorMonths
        );

        if (result.IsFailure)
            return ApplicationResult<LoanProductDto>.Failure(result.Error);

        await _repository.UpdateAsync(product, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<LoanProductDto>.Success(product.ToDto());
    }
}

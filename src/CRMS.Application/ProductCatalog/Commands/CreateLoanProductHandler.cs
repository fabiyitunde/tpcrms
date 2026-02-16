using CRMS.Application.Common;
using CRMS.Application.ProductCatalog.DTOs;
using CRMS.Application.ProductCatalog.Mappings;
using CRMS.Domain.Aggregates.ProductCatalog;
using CRMS.Domain.Interfaces;
using CRMS.Domain.ValueObjects;

namespace CRMS.Application.ProductCatalog.Commands;

public class CreateLoanProductHandler : IRequestHandler<CreateLoanProductCommand, ApplicationResult<LoanProductDto>>
{
    private readonly ILoanProductRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateLoanProductHandler(ILoanProductRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<LoanProductDto>> Handle(CreateLoanProductCommand request, CancellationToken ct = default)
    {
        if (await _repository.ExistsAsync(request.Code, ct))
            return ApplicationResult<LoanProductDto>.Failure($"Product with code '{request.Code}' already exists");

        var minAmount = Money.Create(request.MinAmount, request.Currency);
        var maxAmount = Money.Create(request.MaxAmount, request.Currency);

        var productResult = LoanProduct.Create(
            request.Code,
            request.Name,
            request.Description,
            request.Type,
            minAmount,
            maxAmount,
            request.MinTenorMonths,
            request.MaxTenorMonths
        );

        if (productResult.IsFailure)
            return ApplicationResult<LoanProductDto>.Failure(productResult.Error);

        await _repository.AddAsync(productResult.Value, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<LoanProductDto>.Success(productResult.Value.ToDto());
    }
}

using CRMS.Application.Common;
using CRMS.Application.ProductCatalog.DTOs;
using CRMS.Application.ProductCatalog.Mappings;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.ProductCatalog.Queries;

public record GetLoanProductByIdQuery(Guid Id) : IRequest<ApplicationResult<LoanProductDto>>;

public class GetLoanProductByIdHandler : IRequestHandler<GetLoanProductByIdQuery, ApplicationResult<LoanProductDto>>
{
    private readonly ILoanProductRepository _repository;

    public GetLoanProductByIdHandler(ILoanProductRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<LoanProductDto>> Handle(GetLoanProductByIdQuery request, CancellationToken ct = default)
    {
        var product = await _repository.GetByIdAsync(request.Id, ct);
        if (product == null)
            return ApplicationResult<LoanProductDto>.Failure("Product not found");

        return ApplicationResult<LoanProductDto>.Success(product.ToDto());
    }
}

public record GetLoanProductByCodeQuery(string Code) : IRequest<ApplicationResult<LoanProductDto>>;

public class GetLoanProductByCodeHandler : IRequestHandler<GetLoanProductByCodeQuery, ApplicationResult<LoanProductDto>>
{
    private readonly ILoanProductRepository _repository;

    public GetLoanProductByCodeHandler(ILoanProductRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<LoanProductDto>> Handle(GetLoanProductByCodeQuery request, CancellationToken ct = default)
    {
        var product = await _repository.GetByCodeAsync(request.Code, ct);
        if (product == null)
            return ApplicationResult<LoanProductDto>.Failure("Product not found");

        return ApplicationResult<LoanProductDto>.Success(product.ToDto());
    }
}

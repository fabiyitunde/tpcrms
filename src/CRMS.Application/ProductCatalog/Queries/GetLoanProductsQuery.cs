using CRMS.Application.Common;
using CRMS.Application.ProductCatalog.DTOs;
using CRMS.Application.ProductCatalog.Mappings;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.ProductCatalog.Queries;

public record GetAllLoanProductsQuery : IRequest<ApplicationResult<List<LoanProductSummaryDto>>>;

public class GetAllLoanProductsHandler : IRequestHandler<GetAllLoanProductsQuery, ApplicationResult<List<LoanProductSummaryDto>>>
{
    private readonly ILoanProductRepository _repository;

    public GetAllLoanProductsHandler(ILoanProductRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<List<LoanProductSummaryDto>>> Handle(GetAllLoanProductsQuery request, CancellationToken ct = default)
    {
        var products = await _repository.GetAllAsync(ct);
        var dtos = products.Select(p => p.ToSummaryDto()).ToList();
        return ApplicationResult<List<LoanProductSummaryDto>>.Success(dtos);
    }
}

public record GetLoanProductsByTypeQuery(LoanProductType Type) : IRequest<ApplicationResult<List<LoanProductSummaryDto>>>;

public class GetLoanProductsByTypeHandler : IRequestHandler<GetLoanProductsByTypeQuery, ApplicationResult<List<LoanProductSummaryDto>>>
{
    private readonly ILoanProductRepository _repository;

    public GetLoanProductsByTypeHandler(ILoanProductRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<List<LoanProductSummaryDto>>> Handle(GetLoanProductsByTypeQuery request, CancellationToken ct = default)
    {
        var products = await _repository.GetByTypeAsync(request.Type, ct);
        var dtos = products.Select(p => p.ToSummaryDto()).ToList();
        return ApplicationResult<List<LoanProductSummaryDto>>.Success(dtos);
    }
}

public record GetActiveLoanProductsByTypeQuery(LoanProductType Type) : IRequest<ApplicationResult<List<LoanProductSummaryDto>>>;

public class GetActiveLoanProductsByTypeHandler : IRequestHandler<GetActiveLoanProductsByTypeQuery, ApplicationResult<List<LoanProductSummaryDto>>>
{
    private readonly ILoanProductRepository _repository;

    public GetActiveLoanProductsByTypeHandler(ILoanProductRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<List<LoanProductSummaryDto>>> Handle(GetActiveLoanProductsByTypeQuery request, CancellationToken ct = default)
    {
        var products = await _repository.GetActiveByTypeAsync(request.Type, ct);
        var dtos = products.Select(p => p.ToSummaryDto()).ToList();
        return ApplicationResult<List<LoanProductSummaryDto>>.Success(dtos);
    }
}

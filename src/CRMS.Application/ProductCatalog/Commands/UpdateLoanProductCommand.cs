using CRMS.Application.Common;
using CRMS.Application.ProductCatalog.DTOs;

namespace CRMS.Application.ProductCatalog.Commands;

public record UpdateLoanProductCommand(
    Guid Id,
    string Name,
    string Description,
    decimal MinAmount,
    decimal MaxAmount,
    string Currency,
    int MinTenorMonths,
    int MaxTenorMonths
) : IRequest<ApplicationResult<LoanProductDto>>;

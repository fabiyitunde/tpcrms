using CRMS.Application.Common;
using CRMS.Application.ProductCatalog.DTOs;
using CRMS.Domain.Enums;

namespace CRMS.Application.ProductCatalog.Commands;

public record UpdateLoanProductCommand(
    Guid Id,
    string Name,
    string Description,
    LoanProductType Type,
    decimal MinAmount,
    decimal MaxAmount,
    string Currency,
    int MinTenorMonths,
    int MaxTenorMonths,
    decimal BaseInterestRate = 0m,
    int? FineractProductId = null
) : IRequest<ApplicationResult<LoanProductDto>>;

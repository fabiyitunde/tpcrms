using CRMS.Application.Common;
using CRMS.Application.ProductCatalog.DTOs;
using CRMS.Domain.Enums;

namespace CRMS.Application.ProductCatalog.Commands;

public record CreateLoanProductCommand(
    string Code,
    string Name,
    string Description,
    LoanProductType Type,
    decimal MinAmount,
    decimal MaxAmount,
    string Currency,
    int MinTenorMonths,
    int MaxTenorMonths
) : IRequest<ApplicationResult<LoanProductDto>>;

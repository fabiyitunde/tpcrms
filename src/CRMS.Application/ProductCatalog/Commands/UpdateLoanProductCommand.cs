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
    int? FineractProductId = null,
    // RH-SHF only: days an offer stays open for acceptance; null means the default.
    int? OfferValidityDays = null,
    // RH-SHF only: where an amended case restarts; null means appraisal.
    RhshfInternalStage? AmendmentRestartStage = null
) : IRequest<ApplicationResult<LoanProductDto>>;

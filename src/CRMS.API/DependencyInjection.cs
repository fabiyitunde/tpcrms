using CRMS.Application.Common;
using CRMS.Application.Identity.Commands;
using CRMS.Application.Identity.DTOs;
using CRMS.Application.Identity.Queries;
using CRMS.Application.ProductCatalog.Commands;
using CRMS.Application.ProductCatalog.DTOs;
using CRMS.Application.ProductCatalog.Queries;

namespace CRMS.API;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // ProductCatalog handlers
        services.AddScoped<IRequestHandler<CreateLoanProductCommand, ApplicationResult<LoanProductDto>>, CreateLoanProductHandler>();
        services.AddScoped<IRequestHandler<UpdateLoanProductCommand, ApplicationResult<LoanProductDto>>, UpdateLoanProductHandler>();
        services.AddScoped<IRequestHandler<ActivateLoanProductCommand, ApplicationResult>, ActivateLoanProductHandler>();
        services.AddScoped<IRequestHandler<AddPricingTierCommand, ApplicationResult<PricingTierDto>>, AddPricingTierHandler>();
        services.AddScoped<IRequestHandler<GetLoanProductByIdQuery, ApplicationResult<LoanProductDto>>, GetLoanProductByIdHandler>();
        services.AddScoped<IRequestHandler<GetLoanProductByCodeQuery, ApplicationResult<LoanProductDto>>, GetLoanProductByCodeHandler>();
        services.AddScoped<IRequestHandler<GetAllLoanProductsQuery, ApplicationResult<List<LoanProductSummaryDto>>>, GetAllLoanProductsHandler>();
        services.AddScoped<IRequestHandler<GetLoanProductsByTypeQuery, ApplicationResult<List<LoanProductSummaryDto>>>, GetLoanProductsByTypeHandler>();
        services.AddScoped<IRequestHandler<GetActiveLoanProductsByTypeQuery, ApplicationResult<List<LoanProductSummaryDto>>>, GetActiveLoanProductsByTypeHandler>();

        // Identity handlers
        services.AddScoped<IRequestHandler<RegisterUserCommand, ApplicationResult<UserDto>>, RegisterUserHandler>();
        services.AddScoped<IRequestHandler<GetUserByIdQuery, ApplicationResult<UserDto>>, GetUserByIdHandler>();
        services.AddScoped<IRequestHandler<GetAllUsersQuery, ApplicationResult<List<UserSummaryDto>>>, GetAllUsersHandler>();

        return services;
    }
}

using CRMS.Application.Common;
using CRMS.Application.CoreBanking.DTOs;
using CRMS.Application.CoreBanking.Queries;
using CRMS.Application.Identity.Commands;
using CRMS.Application.Identity.DTOs;
using CRMS.Application.Identity.Queries;
using CRMS.Application.LoanApplication.Commands;
using CRMS.Application.LoanApplication.DTOs;
using CRMS.Application.LoanApplication.Queries;
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

        // CoreBanking handlers
        services.AddScoped<IRequestHandler<GetCorporateAccountDataQuery, ApplicationResult<CorporateAccountDataDto>>, GetCorporateAccountDataHandler>();
        services.AddScoped<IRequestHandler<GetAccountInfoQuery, ApplicationResult<AccountInfoDto>>, GetAccountInfoHandler>();
        services.AddScoped<IRequestHandler<GetAccountStatementQuery, ApplicationResult<AccountStatementDto>>, GetAccountStatementHandler>();

        // LoanApplication handlers
        services.AddScoped<IRequestHandler<InitiateCorporateLoanCommand, ApplicationResult<LoanApplicationDto>>, InitiateCorporateLoanHandler>();
        services.AddScoped<IRequestHandler<SubmitLoanApplicationCommand, ApplicationResult>, SubmitLoanApplicationHandler>();
        services.AddScoped<IRequestHandler<ApproveBranchCommand, ApplicationResult>, ApproveBranchHandler>();
        services.AddScoped<IRequestHandler<ReturnFromBranchCommand, ApplicationResult>, ReturnFromBranchHandler>();
        services.AddScoped<IRequestHandler<UploadDocumentCommand, ApplicationResult<LoanApplicationDocumentDto>>, UploadDocumentHandler>();
        services.AddScoped<IRequestHandler<VerifyDocumentCommand, ApplicationResult>, VerifyDocumentHandler>();
        services.AddScoped<IRequestHandler<GetLoanApplicationByIdQuery, ApplicationResult<LoanApplicationDto>>, GetLoanApplicationByIdHandler>();
        services.AddScoped<IRequestHandler<GetLoanApplicationByNumberQuery, ApplicationResult<LoanApplicationDto>>, GetLoanApplicationByNumberHandler>();
        services.AddScoped<IRequestHandler<GetLoanApplicationsByStatusQuery, ApplicationResult<List<LoanApplicationSummaryDto>>>, GetLoanApplicationsByStatusHandler>();
        services.AddScoped<IRequestHandler<GetMyLoanApplicationsQuery, ApplicationResult<List<LoanApplicationSummaryDto>>>, GetMyLoanApplicationsHandler>();
        services.AddScoped<IRequestHandler<GetPendingBranchReviewQuery, ApplicationResult<List<LoanApplicationSummaryDto>>>, GetPendingBranchReviewHandler>();

        return services;
    }
}

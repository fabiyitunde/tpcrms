using CRMS.Application.Common;
using CRMS.Application.CoreBanking.Queries;
using CRMS.Application.Identity.Commands;
using CRMS.Application.Identity.Queries;
using CRMS.Application.LoanApplication.Commands;
using CRMS.Application.LoanApplication.Queries;
using CRMS.Application.ProductCatalog.Commands;
using CRMS.Application.ProductCatalog.Queries;
using CRMS.Application.StatementAnalysis.Commands;
using CRMS.Application.StatementAnalysis.Queries;
using CRMS.Domain.Services;
using CoreBankingDtos = CRMS.Application.CoreBanking.DTOs;
using IdentityDtos = CRMS.Application.Identity.DTOs;
using LoanAppDtos = CRMS.Application.LoanApplication.DTOs;
using ProductDtos = CRMS.Application.ProductCatalog.DTOs;
using StatementDtos = CRMS.Application.StatementAnalysis.DTOs;

namespace CRMS.API;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // ProductCatalog handlers
        services.AddScoped<IRequestHandler<CreateLoanProductCommand, ApplicationResult<ProductDtos.LoanProductDto>>, CreateLoanProductHandler>();
        services.AddScoped<IRequestHandler<UpdateLoanProductCommand, ApplicationResult<ProductDtos.LoanProductDto>>, UpdateLoanProductHandler>();
        services.AddScoped<IRequestHandler<ActivateLoanProductCommand, ApplicationResult>, ActivateLoanProductHandler>();
        services.AddScoped<IRequestHandler<AddPricingTierCommand, ApplicationResult<ProductDtos.PricingTierDto>>, AddPricingTierHandler>();
        services.AddScoped<IRequestHandler<GetLoanProductByIdQuery, ApplicationResult<ProductDtos.LoanProductDto>>, GetLoanProductByIdHandler>();
        services.AddScoped<IRequestHandler<GetLoanProductByCodeQuery, ApplicationResult<ProductDtos.LoanProductDto>>, GetLoanProductByCodeHandler>();
        services.AddScoped<IRequestHandler<GetAllLoanProductsQuery, ApplicationResult<List<ProductDtos.LoanProductSummaryDto>>>, GetAllLoanProductsHandler>();
        services.AddScoped<IRequestHandler<GetLoanProductsByTypeQuery, ApplicationResult<List<ProductDtos.LoanProductSummaryDto>>>, GetLoanProductsByTypeHandler>();
        services.AddScoped<IRequestHandler<GetActiveLoanProductsByTypeQuery, ApplicationResult<List<ProductDtos.LoanProductSummaryDto>>>, GetActiveLoanProductsByTypeHandler>();

        // Identity handlers
        services.AddScoped<IRequestHandler<RegisterUserCommand, ApplicationResult<IdentityDtos.UserDto>>, RegisterUserHandler>();
        services.AddScoped<IRequestHandler<GetUserByIdQuery, ApplicationResult<IdentityDtos.UserDto>>, GetUserByIdHandler>();
        services.AddScoped<IRequestHandler<GetAllUsersQuery, ApplicationResult<List<IdentityDtos.UserSummaryDto>>>, GetAllUsersHandler>();

        // CoreBanking handlers
        services.AddScoped<IRequestHandler<GetCorporateAccountDataQuery, ApplicationResult<CoreBankingDtos.CorporateAccountDataDto>>, GetCorporateAccountDataHandler>();
        services.AddScoped<IRequestHandler<GetAccountInfoQuery, ApplicationResult<CoreBankingDtos.AccountInfoDto>>, GetAccountInfoHandler>();
        services.AddScoped<IRequestHandler<GetAccountStatementQuery, ApplicationResult<CoreBankingDtos.AccountStatementDto>>, GetAccountStatementHandler>();

        // LoanApplication handlers
        services.AddScoped<IRequestHandler<InitiateCorporateLoanCommand, ApplicationResult<LoanAppDtos.LoanApplicationDto>>, InitiateCorporateLoanHandler>();
        services.AddScoped<IRequestHandler<SubmitLoanApplicationCommand, ApplicationResult>, SubmitLoanApplicationHandler>();
        services.AddScoped<IRequestHandler<ApproveBranchCommand, ApplicationResult>, ApproveBranchHandler>();
        services.AddScoped<IRequestHandler<ReturnFromBranchCommand, ApplicationResult>, ReturnFromBranchHandler>();
        services.AddScoped<IRequestHandler<UploadDocumentCommand, ApplicationResult<LoanAppDtos.LoanApplicationDocumentDto>>, UploadDocumentHandler>();
        services.AddScoped<IRequestHandler<VerifyDocumentCommand, ApplicationResult>, VerifyDocumentHandler>();
        services.AddScoped<IRequestHandler<GetLoanApplicationByIdQuery, ApplicationResult<LoanAppDtos.LoanApplicationDto>>, GetLoanApplicationByIdHandler>();
        services.AddScoped<IRequestHandler<GetLoanApplicationByNumberQuery, ApplicationResult<LoanAppDtos.LoanApplicationDto>>, GetLoanApplicationByNumberHandler>();
        services.AddScoped<IRequestHandler<GetLoanApplicationsByStatusQuery, ApplicationResult<List<LoanAppDtos.LoanApplicationSummaryDto>>>, GetLoanApplicationsByStatusHandler>();
        services.AddScoped<IRequestHandler<GetMyLoanApplicationsQuery, ApplicationResult<List<LoanAppDtos.LoanApplicationSummaryDto>>>, GetMyLoanApplicationsHandler>();
        services.AddScoped<IRequestHandler<GetPendingBranchReviewQuery, ApplicationResult<List<LoanAppDtos.LoanApplicationSummaryDto>>>, GetPendingBranchReviewHandler>();

        // StatementAnalysis handlers
        services.AddScoped<TransactionCategorizationService>();
        services.AddScoped<CashflowAnalysisService>();
        services.AddScoped<IRequestHandler<UploadStatementCommand, ApplicationResult<StatementDtos.BankStatementDto>>, UploadStatementHandler>();
        services.AddScoped<IRequestHandler<AddTransactionsCommand, ApplicationResult<int>>, AddTransactionsHandler>();
        services.AddScoped<IRequestHandler<AnalyzeStatementCommand, ApplicationResult<StatementDtos.StatementAnalysisResultDto>>, AnalyzeStatementHandler>();
        services.AddScoped<IRequestHandler<GetStatementByIdQuery, ApplicationResult<StatementDtos.BankStatementDto>>, GetStatementByIdHandler>();
        services.AddScoped<IRequestHandler<GetStatementTransactionsQuery, ApplicationResult<List<StatementDtos.StatementTransactionDto>>>, GetStatementTransactionsHandler>();
        services.AddScoped<IRequestHandler<GetStatementsByLoanApplicationQuery, ApplicationResult<List<StatementDtos.BankStatementSummaryDto>>>, GetStatementsByLoanApplicationHandler>();

        return services;
    }
}

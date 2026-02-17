using CRMS.Application.Advisory.Commands;
using CRMS.Application.Advisory.DTOs;
using CRMS.Application.Advisory.Queries;
using CRMS.Application.Audit.DTOs;
using CRMS.Application.Audit.Queries;
using CRMS.Application.LoanPack.Commands;
using CRMS.Application.LoanPack.Queries;
using CRMS.Application.Collateral.Commands;
using CRMS.Application.Collateral.Queries;
using CRMS.Application.Common;
using CRMS.Application.Configuration.Commands;
using CRMS.Application.Configuration.DTOs;
using CRMS.Application.Configuration.Queries;
using CRMS.Application.Committee.Commands;
using CRMS.Application.Committee.DTOs;
using CRMS.Application.Committee.Queries;
using CRMS.Application.Workflow.Commands;
using CRMS.Application.Workflow.DTOs;
using CRMS.Application.Workflow.Queries;
using CRMS.Application.CoreBanking.Queries;
using CRMS.Application.CreditBureau.Commands;
using CRMS.Application.CreditBureau.Queries;
using CRMS.Application.FinancialAnalysis.Commands;
using CRMS.Application.FinancialAnalysis.Queries;
using CRMS.Application.Guarantor.Commands;
using CRMS.Application.Guarantor.Queries;
using CRMS.Application.Identity.Commands;
using CRMS.Application.Identity.Queries;
using CRMS.Application.LoanApplication.Commands;
using CRMS.Application.LoanApplication.Queries;
using CRMS.Application.ProductCatalog.Commands;
using CRMS.Application.ProductCatalog.Queries;
using CRMS.Application.StatementAnalysis.Commands;
using CRMS.Application.StatementAnalysis.Queries;
using CRMS.Domain.Services;
using BureauDtos = CRMS.Application.CreditBureau.DTOs;
using CollateralDtos = CRMS.Application.Collateral.DTOs;
using CoreBankingDtos = CRMS.Application.CoreBanking.DTOs;
using FinancialDtos = CRMS.Application.FinancialAnalysis.DTOs;
using GuarantorDtos = CRMS.Application.Guarantor.DTOs;
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

        // CreditBureau handlers
        services.AddScoped<IRequestHandler<RequestBureauReportCommand, ApplicationResult<BureauDtos.BureauReportDto>>, RequestBureauReportHandler>();
        services.AddScoped<IRequestHandler<GetBureauReportByIdQuery, ApplicationResult<BureauDtos.BureauReportDto>>, GetBureauReportByIdHandler>();
        services.AddScoped<IRequestHandler<GetBureauReportsByLoanApplicationQuery, ApplicationResult<List<BureauDtos.BureauReportSummaryDto>>>, GetBureauReportsByLoanApplicationHandler>();
        services.AddScoped<IRequestHandler<SearchBureauByBVNQuery, ApplicationResult<BureauDtos.BureauSearchResultDto>>, SearchBureauByBVNHandler>();
        services.AddScoped<IRequestHandler<ProcessLoanCreditChecksCommand, ApplicationResult<CreditCheckBatchResultDto>>, ProcessLoanCreditChecksHandler>();

        // Collateral handlers
        services.AddScoped<IRequestHandler<AddCollateralCommand, ApplicationResult<CollateralDtos.CollateralDto>>, AddCollateralHandler>();
        services.AddScoped<IRequestHandler<SetCollateralValuationCommand, ApplicationResult<CollateralDtos.CollateralDto>>, SetCollateralValuationHandler>();
        services.AddScoped<IRequestHandler<ApproveCollateralCommand, ApplicationResult<CollateralDtos.CollateralDto>>, ApproveCollateralHandler>();
        services.AddScoped<IRequestHandler<RecordPerfectionCommand, ApplicationResult<CollateralDtos.CollateralDto>>, RecordPerfectionHandler>();
        services.AddScoped<IRequestHandler<GetCollateralByIdQuery, ApplicationResult<CollateralDtos.CollateralDto>>, GetCollateralByIdHandler>();
        services.AddScoped<IRequestHandler<GetCollateralByLoanApplicationQuery, ApplicationResult<List<CollateralDtos.CollateralSummaryDto>>>, GetCollateralByLoanApplicationHandler>();
        services.AddScoped<IRequestHandler<CalculateLTVQuery, ApplicationResult<LTVCalculationDto>>, CalculateLTVHandler>();

        // Guarantor handlers
        services.AddScoped<IRequestHandler<AddIndividualGuarantorCommand, ApplicationResult<GuarantorDtos.GuarantorDto>>, AddIndividualGuarantorHandler>();
        services.AddScoped<IRequestHandler<RunGuarantorCreditCheckCommand, ApplicationResult<GuarantorDtos.GuarantorCreditCheckResultDto>>, RunGuarantorCreditCheckHandler>();
        services.AddScoped<IRequestHandler<ApproveGuarantorCommand, ApplicationResult<GuarantorDtos.GuarantorDto>>, ApproveGuarantorHandler>();
        services.AddScoped<IRequestHandler<RejectGuarantorCommand, ApplicationResult<GuarantorDtos.GuarantorDto>>, RejectGuarantorHandler>();
        services.AddScoped<IRequestHandler<GetGuarantorByIdQuery, ApplicationResult<GuarantorDtos.GuarantorDto>>, GetGuarantorByIdHandler>();
        services.AddScoped<IRequestHandler<GetGuarantorsByLoanApplicationQuery, ApplicationResult<List<GuarantorDtos.GuarantorSummaryDto>>>, GetGuarantorsByLoanApplicationHandler>();
        services.AddScoped<IRequestHandler<GetGuarantorsByBVNQuery, ApplicationResult<List<GuarantorDtos.GuarantorSummaryDto>>>, GetGuarantorsByBVNHandler>();

        // FinancialAnalysis handlers
        services.AddScoped<IRequestHandler<CreateFinancialStatementCommand, ApplicationResult<FinancialDtos.FinancialStatementDto>>, CreateFinancialStatementHandler>();
        services.AddScoped<IRequestHandler<SetBalanceSheetCommand, ApplicationResult<FinancialDtos.FinancialStatementDto>>, SetBalanceSheetHandler>();
        services.AddScoped<IRequestHandler<SetIncomeStatementCommand, ApplicationResult<FinancialDtos.FinancialStatementDto>>, SetIncomeStatementHandler>();
        services.AddScoped<IRequestHandler<SetCashFlowStatementCommand, ApplicationResult<FinancialDtos.FinancialStatementDto>>, SetCashFlowStatementHandler>();
        services.AddScoped<IRequestHandler<SubmitFinancialStatementCommand, ApplicationResult<FinancialDtos.FinancialStatementDto>>, SubmitFinancialStatementHandler>();
        services.AddScoped<IRequestHandler<VerifyFinancialStatementCommand, ApplicationResult<FinancialDtos.FinancialStatementDto>>, VerifyFinancialStatementHandler>();
        services.AddScoped<IRequestHandler<GetFinancialStatementByIdQuery, ApplicationResult<FinancialDtos.FinancialStatementDto>>, GetFinancialStatementByIdHandler>();
        services.AddScoped<IRequestHandler<GetFinancialStatementsByLoanApplicationQuery, ApplicationResult<List<FinancialDtos.FinancialStatementSummaryDto>>>, GetFinancialStatementsByLoanApplicationHandler>();
        services.AddScoped<IRequestHandler<GetFinancialRatiosTrendQuery, ApplicationResult<FinancialRatiosTrendDto>>, GetFinancialRatiosTrendHandler>();

        // Advisory handlers
        services.AddScoped<IRequestHandler<GenerateCreditAdvisoryCommand, ApplicationResult<CreditAdvisoryDto>>, GenerateCreditAdvisoryHandler>();
        services.AddScoped<IRequestHandler<GetCreditAdvisoryByIdQuery, ApplicationResult<CreditAdvisoryDto>>, GetCreditAdvisoryByIdHandler>();
        services.AddScoped<IRequestHandler<GetLatestAdvisoryByLoanApplicationQuery, ApplicationResult<CreditAdvisoryDto>>, GetLatestAdvisoryByLoanApplicationHandler>();
        services.AddScoped<IRequestHandler<GetAdvisoryHistoryByLoanApplicationQuery, ApplicationResult<List<CreditAdvisorySummaryDto>>>, GetAdvisoryHistoryByLoanApplicationHandler>();
        services.AddScoped<IRequestHandler<GetScoreMatrixQuery, ApplicationResult<ScoreMatrixDto>>, GetScoreMatrixHandler>();

        // LoanPack handlers
        services.AddScoped<GenerateLoanPackHandler>();
        services.AddScoped<GetLoanPackByIdHandler>();
        services.AddScoped<GetLatestLoanPackHandler>();
        services.AddScoped<GetLoanPackVersionsHandler>();

        // Scoring Configuration handlers (maker-checker workflow)
        services.AddScoped<GetAllScoringParametersHandler>();
        services.AddScoped<GetScoringParameterByIdHandler>();
        services.AddScoped<GetScoringParametersByCategoryHandler>();
        services.AddScoped<GetPendingChangesHandler>();
        services.AddScoped<GetCategorySummariesHandler>();
        services.AddScoped<GetParameterHistoryHandler>();
        services.AddScoped<GetRecentHistoryHandler>();
        services.AddScoped<RequestParameterChangeHandler>();
        services.AddScoped<ApproveParameterChangeHandler>();
        services.AddScoped<RejectParameterChangeHandler>();
        services.AddScoped<CancelParameterChangeHandler>();
        services.AddScoped<SeedDefaultParametersHandler>();

        // Audit handlers
        services.AddScoped<GetAuditLogByIdHandler>();
        services.AddScoped<GetAuditLogsByLoanApplicationHandler>();
        services.AddScoped<GetAuditLogsByEntityHandler>();
        services.AddScoped<GetAuditLogsByUserHandler>();
        services.AddScoped<GetRecentAuditLogsHandler>();
        services.AddScoped<GetFailedActionsHandler>();
        services.AddScoped<SearchAuditLogsHandler>();
        services.AddScoped<GetDataAccessLogsByUserHandler>();
        services.AddScoped<GetDataAccessLogsByLoanApplicationHandler>();

        // Committee handlers
        services.AddScoped<CreateCommitteeReviewHandler>();
        services.AddScoped<AddCommitteeMemberHandler>();
        services.AddScoped<StartVotingHandler>();
        services.AddScoped<CastVoteHandler>();
        services.AddScoped<AddCommitteeCommentHandler>();
        services.AddScoped<RecordCommitteeDecisionHandler>();
        services.AddScoped<CloseCommitteeReviewHandler>();
        services.AddScoped<GetCommitteeReviewByIdHandler>();
        services.AddScoped<GetCommitteeReviewByLoanApplicationHandler>();
        services.AddScoped<GetMyPendingVotesHandler>();
        services.AddScoped<GetMyCommitteeReviewsHandler>();
        services.AddScoped<GetCommitteeReviewsByStatusHandler>();
        services.AddScoped<GetOverdueCommitteeReviewsHandler>();
        services.AddScoped<GetVotingSummaryHandler>();

        // Workflow handlers
        services.AddScoped<InitializeWorkflowHandler>();
        services.AddScoped<TransitionWorkflowHandler>();
        services.AddScoped<AssignWorkflowHandler>();
        services.AddScoped<UnassignWorkflowHandler>();
        services.AddScoped<EscalateWorkflowHandler>();
        services.AddScoped<SeedCorporateLoanWorkflowHandler>();
        services.AddScoped<GetWorkflowInstanceByIdHandler>();
        services.AddScoped<GetWorkflowByLoanApplicationHandler>();
        services.AddScoped<GetAvailableActionsHandler>();
        services.AddScoped<GetWorkflowQueueByRoleHandler>();
        services.AddScoped<GetMyWorkflowQueueHandler>();
        services.AddScoped<GetOverdueWorkflowsHandler>();
        services.AddScoped<GetWorkflowDefinitionHandler>();
        services.AddScoped<GetQueueSummaryHandler>();

        return services;
    }
}

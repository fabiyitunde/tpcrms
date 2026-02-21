using System.Threading.Channels;
using CRMS.Application.Identity.Interfaces;
using CRMS.Application.Notification.Interfaces;
using CRMS.Application.Notification.Services;
using CRMS.Domain.Aggregates.Committee;
using CRMS.Domain.Aggregates.Configuration;
using CRMS.Domain.Aggregates.LoanApplication;
using CRMS.Domain.Aggregates.Notification;
using CRMS.Domain.Aggregates.Workflow;
using CRMS.Domain.Common;
using CRMS.Domain.Configuration;
using CRMS.Domain.Interfaces;
using CRMS.Domain.Services;
using CRMS.Infrastructure.BackgroundServices;
using CRMS.Application.Advisory.Interfaces;
using CRMS.Infrastructure.Events;
using CRMS.Infrastructure.Events.Handlers;
using CRMS.Infrastructure.ExternalServices.AI;
using CRMS.Infrastructure.ExternalServices.AIServices;
using CRMS.Infrastructure.ExternalServices.CoreBanking;
using CRMS.Infrastructure.ExternalServices.CreditBureau;
using CRMS.Infrastructure.ExternalServices.Notifications;
using CRMS.Application.Reporting.Interfaces;
using CRMS.Infrastructure.Services;
using CRMS.Infrastructure.Identity;
using CRMS.Infrastructure.Persistence;
using CRMS.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CRMS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString, IConfiguration configuration)
    {
        var serverVersion = ServerVersion.AutoDetect(connectionString);

        // Domain Event Infrastructure (registered before DbContext to break circular dependency)
        services.AddSingleton<DomainEventPublishingInterceptor>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

        services.AddDbContext<CRMSDbContext>((serviceProvider, options) =>
        {
            var interceptor = serviceProvider.GetRequiredService<DomainEventPublishingInterceptor>();
            options.UseMySql(connectionString, serverVersion)
                   .AddInterceptors(interceptor);
        });

        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<CRMSDbContext>());

        // Domain Event Handlers - Critical Flows to Audit
        services.AddScoped<IDomainEventHandler<WorkflowTransitionedEvent>, WorkflowTransitionAuditHandler>();
        services.AddScoped<IDomainEventHandler<CommitteeVoteCastEvent>, CommitteeVoteAuditHandler>();
        services.AddScoped<IDomainEventHandler<CommitteeDecisionRecordedEvent>, CommitteeDecisionAuditHandler>();
        services.AddScoped<IDomainEventHandler<ScoringParameterChangeApprovedEvent>, ScoringParameterChangeAuditHandler>();
        services.AddScoped<IDomainEventHandler<LoanApplicationCreatedEvent>, LoanApplicationCreatedAuditHandler>();
        services.AddScoped<IDomainEventHandler<LoanApplicationApprovedEvent>, LoanApplicationApprovedAuditHandler>();
        
        // ProductCatalog
        services.AddScoped<ILoanProductRepository, LoanProductRepository>();
        
        // Identity
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.Configure<Identity.JwtSettings>(configuration.GetSection("JwtSettings"));
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IAuthService, AuthService>();

        // Core Banking (Mock for now - will be replaced with real Fineract client)
        services.AddScoped<ICoreBankingService, MockCoreBankingService>();

        // LoanApplication
        services.AddScoped<ILoanApplicationRepository, LoanApplicationRepository>();
        services.AddScoped<ILoanApplicationDocumentRepository, LoanApplicationDocumentRepository>();

        // StatementAnalysis
        services.AddScoped<IBankStatementRepository, BankStatementRepository>();

        // CreditBureau
        services.AddScoped<IBureauReportRepository, BureauReportRepository>();
        var creditRegistrySection = configuration.GetSection(CreditRegistrySettings.SectionName);
        if (creditRegistrySection.Exists() && !creditRegistrySection.GetValue<bool>("UseMock"))
        {
            services.Configure<CreditRegistrySettings>(creditRegistrySection);
            services.AddHttpClient<ICreditBureauProvider, CreditRegistryProvider>();
        }
        else
        {
            services.AddScoped<ICreditBureauProvider, MockCreditBureauProvider>();
        }

        // Collateral & Guarantor
        services.AddScoped<ICollateralRepository, CollateralRepository>();
        services.AddScoped<IGuarantorRepository, GuarantorRepository>();

        // Consent
        services.AddScoped<IConsentRecordRepository, ConsentRecordRepository>();

        // FinancialStatement
        services.AddScoped<IFinancialStatementRepository, FinancialStatementRepository>();

        // Advisory
        services.AddScoped<ICreditAdvisoryRepository, CreditAdvisoryRepository>();
        services.AddScoped<IAIAdvisoryService, MockAIAdvisoryService>();

        // Scoring Configuration (database-driven with maker-checker workflow)
        services.AddScoped<IScoringParameterRepository, ScoringParameterRepository>();
        services.AddScoped<ScoringConfigurationService>();

        // Workflow
        services.AddScoped<IWorkflowDefinitionRepository, WorkflowDefinitionRepository>();
        services.AddScoped<IWorkflowInstanceRepository, WorkflowInstanceRepository>();
        services.AddScoped<WorkflowService>();

        // Committee
        services.AddScoped<ICommitteeReviewRepository, CommitteeReviewRepository>();

        // Audit (with IAuditContextProvider for IP capture and sensitive data masking)
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IDataAccessLogRepository, DataAccessLogRepository>();
        services.AddScoped<IHttpContextService, HttpContextService>();
        services.AddScoped<IAuditContextProvider>(sp => sp.GetRequiredService<IHttpContextService>());
        services.AddScoped<AuditService>();

        // LoanPack
        services.AddScoped<ILoanPackRepository, LoanPackRepository>();
        services.AddScoped<Application.LoanPack.Interfaces.ILoanPackGenerator, Documents.LoanPackPdfGenerator>();

        // File Storage (configurable: Local or S3)
        var storageProvider = configuration.GetValue<string>("FileStorage:Provider") ?? "Local";
        if (storageProvider.Equals("S3", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IFileStorageService, Storage.S3FileStorageService>();
        }
        else
        {
            services.AddScoped<IFileStorageService, Storage.LocalFileStorageService>();
        }

        // Notification
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<INotificationTemplateRepository, NotificationTemplateRepository>();
        services.AddScoped<INotificationService, NotificationOrchestrator>();
        services.AddScoped<INotificationSender, MockEmailSender>();
        services.AddScoped<INotificationSender, MockSmsSender>();
        services.AddScoped<INotificationSender, MockWhatsAppSender>();
        services.AddHostedService<NotificationProcessingService>();
        
        // Reporting
        services.AddScoped<IReportingService, ReportingService>();

        // Notification Event Handlers
        services.AddScoped<IDomainEventHandler<WorkflowSLABreachedEvent>, WorkflowSLABreachedNotificationHandler>();
        services.AddScoped<IDomainEventHandler<WorkflowEscalatedEvent>, WorkflowEscalatedNotificationHandler>();
        services.AddScoped<IDomainEventHandler<WorkflowAssignedEvent>, WorkflowAssignedNotificationHandler>();
        services.AddScoped<IDomainEventHandler<CommitteeVotingStartedEvent>, CommitteeVotingStartedNotificationHandler>();

        // Workflow Integration Event Handlers (auto-transitions based on domain events)
        services.AddScoped<IDomainEventHandler<CommitteeDecisionRecordedEvent>, CommitteeDecisionWorkflowHandler>();
        services.AddScoped<IDomainEventHandler<AllCreditChecksCompletedEvent>, AllCreditChecksCompletedWorkflowHandler>();

        // Background Services - Credit Check Queue
        var creditCheckChannel = Channel.CreateUnbounded<CreditCheckRequest>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        services.AddSingleton(creditCheckChannel);
        services.AddSingleton<Application.CreditBureau.Interfaces.ICreditCheckQueue, CreditCheckQueue>();
        services.AddHostedService<CreditCheckBackgroundService>();

        // AI/LLM Services
        var openAISection = configuration.GetSection(OpenAISettings.SectionName);
        if (openAISection.Exists() && !string.IsNullOrEmpty(openAISection["ApiKey"]))
        {
            services.Configure<OpenAISettings>(openAISection);
            services.AddHttpClient<ILLMService, OpenAIService>();
            services.AddScoped<LLMTransactionCategorizationService>();
        }

        // Application Layer Command/Query Handlers
        // LoanApplication
        services.AddScoped<Application.LoanApplication.Commands.InitiateCorporateLoanHandler>();
        services.AddScoped<Application.LoanApplication.Commands.SubmitLoanApplicationHandler>();
        services.AddScoped<Application.LoanApplication.Commands.UploadDocumentHandler>();
        services.AddScoped<Application.LoanApplication.Commands.VerifyDocumentHandler>();
        services.AddScoped<Application.LoanApplication.Commands.RejectDocumentHandler>();
        services.AddScoped<Application.LoanApplication.Queries.GetLoanApplicationByIdHandler>();
        services.AddScoped<Application.LoanApplication.Queries.GetLoanApplicationsByStatusHandler>();
        services.AddScoped<Application.LoanApplication.Queries.GetMyLoanApplicationsHandler>();
        
        // ProductCatalog
        services.AddScoped<Application.ProductCatalog.Queries.GetActiveLoanProductsByTypeHandler>();
        services.AddScoped<Application.ProductCatalog.Queries.GetAllLoanProductsHandler>();
        services.AddScoped<Application.ProductCatalog.Commands.CreateLoanProductHandler>();
        services.AddScoped<Application.ProductCatalog.Commands.UpdateLoanProductHandler>();
        
        // Workflow
        services.AddScoped<Application.Workflow.Commands.TransitionWorkflowHandler>();
        services.AddScoped<Application.Workflow.Queries.GetWorkflowByLoanApplicationHandler>();
        services.AddScoped<Application.Workflow.Queries.GetMyWorkflowQueueHandler>();
        services.AddScoped<Application.Workflow.Queries.GetWorkflowQueueByRoleHandler>();
        services.AddScoped<Application.Workflow.Queries.GetQueueSummaryHandler>();
        services.AddScoped<Application.Workflow.Queries.GetOverdueWorkflowsHandler>();
        
        // Committee
        services.AddScoped<Application.Committee.Commands.CastVoteHandler>();
        services.AddScoped<Application.Committee.Queries.GetMyPendingVotesHandler>();
        services.AddScoped<Application.Committee.Queries.GetCommitteeReviewsByStatusHandler>();
        
        // Advisory
        services.AddScoped<Application.Advisory.Commands.GenerateCreditAdvisoryHandler>();
        
        // LoanPack
        services.AddScoped<Application.LoanPack.Commands.GenerateLoanPackHandler>();
        
        // Collateral
        services.AddScoped<Application.Collateral.Commands.AddCollateralHandler>();
        services.AddScoped<Application.Collateral.Commands.SetCollateralValuationHandler>();
        services.AddScoped<Application.Collateral.Commands.ApproveCollateralHandler>();
        services.AddScoped<Application.Collateral.Queries.GetCollateralByIdHandler>();
        services.AddScoped<Application.Collateral.Queries.GetCollateralByLoanApplicationHandler>();
        
        // Guarantor
        services.AddScoped<Application.Guarantor.Commands.AddIndividualGuarantorHandler>();
        services.AddScoped<Application.Guarantor.Commands.ApproveGuarantorHandler>();
        services.AddScoped<Application.Guarantor.Commands.RejectGuarantorHandler>();
        services.AddScoped<Application.Guarantor.Queries.GetGuarantorByIdHandler>();
        services.AddScoped<Application.Guarantor.Queries.GetGuarantorsByLoanApplicationHandler>();
        
        // Audit
        services.AddScoped<Application.Audit.Queries.GetRecentAuditLogsHandler>();
        
        // Identity
        services.AddScoped<Application.Identity.Queries.GetAllUsersHandler>();
        
        // Financial Statement
        services.AddScoped<Application.FinancialAnalysis.Commands.CreateFinancialStatementHandler>();
        services.AddScoped<Application.FinancialAnalysis.Commands.SetBalanceSheetHandler>();
        services.AddScoped<Application.FinancialAnalysis.Commands.SetIncomeStatementHandler>();
        services.AddScoped<Application.FinancialAnalysis.Commands.SetCashFlowStatementHandler>();
        services.AddScoped<Application.FinancialAnalysis.Commands.SubmitFinancialStatementHandler>();
        services.AddScoped<Application.FinancialAnalysis.Commands.VerifyFinancialStatementHandler>();
        services.AddScoped<Application.FinancialAnalysis.Queries.GetFinancialStatementByIdHandler>();
        services.AddScoped<Application.FinancialAnalysis.Queries.GetFinancialStatementsByLoanApplicationHandler>();
        services.AddScoped<Application.FinancialAnalysis.Queries.GetFinancialRatiosTrendHandler>();

        return services;
    }
}

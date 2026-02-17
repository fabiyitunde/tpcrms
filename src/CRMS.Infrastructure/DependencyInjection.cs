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

        // Scoring Configuration (parameterized AI scoring)
        services.Configure<ScoringConfiguration>(
            configuration.GetSection(ScoringConfiguration.SectionName));
        
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
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IAuthService, AuthService>();

        // Core Banking (Mock for now - will be replaced with real Fineract client)
        services.AddScoped<ICoreBankingService, MockCoreBankingService>();

        // LoanApplication
        services.AddScoped<ILoanApplicationRepository, LoanApplicationRepository>();

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

        // FinancialStatement
        services.AddScoped<IFinancialStatementRepository, FinancialStatementRepository>();

        // Advisory
        services.AddScoped<ICreditAdvisoryRepository, CreditAdvisoryRepository>();
        services.AddScoped<IAIAdvisoryService, MockAIAdvisoryService>();

        // Scoring Configuration
        services.AddScoped<IScoringParameterRepository, ScoringParameterRepository>();

        // Workflow
        services.AddScoped<IWorkflowDefinitionRepository, WorkflowDefinitionRepository>();
        services.AddScoped<IWorkflowInstanceRepository, WorkflowInstanceRepository>();
        services.AddScoped<WorkflowService>();

        // Committee
        services.AddScoped<ICommitteeReviewRepository, CommitteeReviewRepository>();

        // Audit
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IDataAccessLogRepository, DataAccessLogRepository>();
        services.AddScoped<AuditService>();

        // LoanPack
        services.AddScoped<ILoanPackRepository, LoanPackRepository>();
        services.AddScoped<Application.LoanPack.Interfaces.ILoanPackGenerator, Documents.LoanPackPdfGenerator>();

        // Notification
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<INotificationTemplateRepository, NotificationTemplateRepository>();
        services.AddScoped<INotificationService, NotificationOrchestrator>();
        services.AddScoped<INotificationSender, MockEmailSender>();
        services.AddScoped<INotificationSender, MockSmsSender>();
        services.AddScoped<INotificationSender, MockWhatsAppSender>();
        services.AddHostedService<NotificationProcessingService>();
        
        // Notification Event Handlers
        services.AddScoped<IDomainEventHandler<WorkflowSLABreachedEvent>, WorkflowSLABreachedNotificationHandler>();
        services.AddScoped<IDomainEventHandler<WorkflowEscalatedEvent>, WorkflowEscalatedNotificationHandler>();
        services.AddScoped<IDomainEventHandler<WorkflowAssignedEvent>, WorkflowAssignedNotificationHandler>();
        services.AddScoped<IDomainEventHandler<CommitteeVotingStartedEvent>, CommitteeVotingStartedNotificationHandler>();

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

        return services;
    }
}

using System.Threading.Channels;
using CRMS.Application.Identity.Interfaces;
using CRMS.Domain.Interfaces;
using CRMS.Domain.Services;
using CRMS.Infrastructure.BackgroundServices;
using CRMS.Application.Advisory.Interfaces;
using CRMS.Infrastructure.ExternalServices.AI;
using CRMS.Infrastructure.ExternalServices.AIServices;
using CRMS.Infrastructure.ExternalServices.CoreBanking;
using CRMS.Infrastructure.ExternalServices.CreditBureau;
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
        
        services.AddDbContext<CRMSDbContext>(options =>
            options.UseMySql(connectionString, serverVersion));

        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<CRMSDbContext>());
        
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

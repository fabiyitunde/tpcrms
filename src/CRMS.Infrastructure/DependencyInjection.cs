using CRMS.Application.Identity.Interfaces;
using CRMS.Domain.Interfaces;
using CRMS.Infrastructure.ExternalServices.CoreBanking;
using CRMS.Infrastructure.Identity;
using CRMS.Infrastructure.Persistence;
using CRMS.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CRMS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
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

        return services;
    }
}

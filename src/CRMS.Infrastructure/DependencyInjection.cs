using CRMS.Domain.Interfaces;
using CRMS.Infrastructure.Persistence;
using CRMS.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CRMS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<CRMSDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<CRMSDbContext>());
        services.AddScoped<ILoanProductRepository, LoanProductRepository>();

        return services;
    }
}

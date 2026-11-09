using System.Security.Cryptography;
using CRMS.Application.Identity.Interfaces;
using CRMS.Domain.Constants;
using CRMS.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CRMS.Infrastructure.Persistence;

/// <summary>
/// Creates the first Billing Manager from configuration, because nobody inside the application is
/// allowed to: the bank's administrators cannot grant the vendor role, and until one holder exists
/// there is no Billing Manager to grant it either. Runs at start-up in both hosts; does nothing once
/// any Billing Manager exists, so a later change of the setting never adds a second account.
///
/// Settings (the vendor's, in the host's appsettings):
///   Billing:Manager:Email, Billing:Manager:FirstName, Billing:Manager:LastName
///
/// The account gets an unguessable password. The person signs in by using "Forgot password" on the
/// login page, which emails them a reset link; that is logged here so whoever deploys knows to say so.
/// </summary>
public static class BillingSeeder
{
    public static async Task SeedManagerAsync(CRMSDbContext context, IConfiguration configuration, IPasswordHasher passwordHasher, ILogger logger)
    {
        var email = configuration["Billing:Manager:Email"]?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
            return;

        var role = await context.Roles.FirstOrDefaultAsync(r => r.Name == Roles.BillingManager);
        if (role is null)
        {
            logger.LogError("Billing Manager could not be seeded: the role is missing. Roles are seeded first; check SeedRolesAsync.");
            return;
        }

        var anyHolder = await context.Users.AnyAsync(u => u.UserRoles.Any(ur => ur.RoleId == role.Id));
        if (anyHolder)
            return;

        var existing = await context.Users.Include(u => u.UserRoles).FirstOrDefaultAsync(u => u.Email == email);
        if (existing is not null)
        {
            existing.AddRole(role);
            if (existing.Status != UserStatus.Active) existing.Activate();
            await context.SaveChangesAsync();
            logger.LogWarning("Billing Manager role granted to the existing account {Email} from Billing:Manager:Email", email);
            return;
        }

        var firstName = configuration["Billing:Manager:FirstName"]?.Trim();
        var lastName = configuration["Billing:Manager:LastName"]?.Trim();
        if (string.IsNullOrWhiteSpace(firstName)) firstName = "Billing";
        if (string.IsNullOrWhiteSpace(lastName)) lastName = "Manager";

        var stem = email.Split('@')[0];
        var userName = stem;
        for (var n = 2; await context.Users.AnyAsync(u => u.UserName == userName); n++)
            userName = $"{stem}{n}";

        var created = ApplicationUser.Create(email, userName, firstName, lastName, UserType.Staff, null, null);
        if (created.IsFailure)
        {
            logger.LogError("Billing Manager could not be seeded: {Error}", created.Error);
            return;
        }
        var user = created.Value;
        user.SetPasswordHash(passwordHasher.HashPassword(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))));
        user.AddRole(role);
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();
        logger.LogWarning("Billing Manager account {Email} seeded. It has no usable password yet: use 'Forgot password' on the login page to set one.", email);
    }
}

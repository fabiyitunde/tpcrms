using CRMS.Application.CreditBureau;
using CRMS.Domain.Constants;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CRMS.Infrastructure.ExternalServices.SmartComply;

/// <summary>
/// Tells the system administrators, on the bell, the moment SmartComply refuses a call for lack of
/// funds, with SmartComply's own words. Every bureau check fails or loses its score until the wallet
/// is topped up, and staff on a case cannot fix that themselves. Raised at most once every two hours
/// per server, so a run of failures does not bury the bell. Never throws: an alert that cannot be
/// written is logged and the bureau call carries on.
/// </summary>
public sealed class SmartComplyWalletAlert
{
    private static readonly TimeSpan Quiet = TimeSpan.FromHours(2);
    private static readonly object Gate = new();
    private static DateTime _lastRaised = DateTime.MinValue;

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<SmartComplyWalletAlert> _logger;

    public SmartComplyWalletAlert(IServiceScopeFactory scopes, ILogger<SmartComplyWalletAlert> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    public async Task RaiseIfShortfallAsync(string? error, string operation)
    {
        if (!BureauErrors.IsWalletShortfall(error))
            return;

        _logger.LogCritical("SmartComply refused {Operation} for lack of funds: {Error}", operation, error);
        var now = DateTime.UtcNow;
        lock (Gate)
        {
            if (now - _lastRaised < Quiet) return;
            _lastRaised = now;
        }

        try
        {
            // Its own scope: the caller's DbContext may be mid-save, and this must not join it.
            using var scope = _scopes.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var notifications = scope.ServiceProvider.GetRequiredService<INotificationRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var admins = await users.GetByRoleAsync(Roles.SystemAdmin);
            foreach (var admin in admins.Where(a => a.Status == Domain.Entities.Identity.UserStatus.Active))
            {
                var created = Domain.Aggregates.Notification.Notification.CreateInApp(
                    NotificationType.CreditCheckFailed, NotificationPriority.Urgent, admin.Id, admin.FullName,
                    "SmartComply wallet balance too low",
                    $"SmartComply refused a bureau call ({operation}) at {now:dd MMM yyyy HH:mm} UTC. Its answer: {error} " +
                    "Bureau checks will fail or lose their scores until the wallet is funded; run them again afterwards.",
                    "/admin/diagnostics");
                if (created.IsSuccess)
                    await notifications.AddAsync(created.Value);
            }
            await unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "The SmartComply wallet alert could not be written.");
        }
    }
}

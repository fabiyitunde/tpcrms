using CRMS.Domain.Enums;
using CRMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CRMS.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that monitors Conditions Subsequent (CS) items for disbursed loans.
/// Runs once daily (at midnight UTC) and:
///  1. Marks Pending CS items as Overdue when DueDate has passed
///  2. Logs tiered notifications at T-7, T-1, T+0, T+7, T+30, T+90 relative to DueDate
/// </summary>
public class CsMonitoringBackgroundService : BackgroundService
{
    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CsMonitoringBackgroundService> _logger;

    public CsMonitoringBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<CsMonitoringBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CS Monitoring Background Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunMonitoringCycleAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Unexpected error in CS monitoring cycle");
            }

            try
            {
                await Task.Delay(RunInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("CS Monitoring Background Service stopped");
    }

    private async Task RunMonitoringCycleAsync(CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        _logger.LogDebug("CS monitoring cycle running for date {Date}", today);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CRMSDbContext>();

        // Load all active CS items from disbursed applications that are not yet resolved.
        // Join via LoanApplicationId to filter on application status and retrieve the app number.
        var csItemData = await (
            from item in db.DisbursementChecklistItems
            join app in db.LoanApplications on item.LoanApplicationId equals app.Id
            where item.ConditionType == ConditionType.Subsequent
                && item.DueDate != null
                && item.Status != ChecklistItemStatus.Satisfied
                && item.Status != ChecklistItemStatus.Waived
                && app.Status == LoanApplicationStatus.Disbursed
            select new { Item = item, AppNumber = app.ApplicationNumber }
        ).ToListAsync(ct);

        // Extract tracked entities for MarkOverdue mutations
        var csItems = csItemData.Select(x => (x.Item, x.AppNumber)).ToList();

        if (csItems.Count == 0)
        {
            _logger.LogDebug("CS monitoring: no active items found");
            return;
        }

        var markedOverdue = 0;
        var notifications = 0;

        foreach (var (item, appNumber) in csItems)
        {
            var dueDate = item.DueDate!.Value.Date;
            var daysToOrFromDue = (dueDate - today).TotalDays; // negative = overdue

            // Mark overdue
            if (daysToOrFromDue < 0 && item.Status == ChecklistItemStatus.Pending)
            {
                item.MarkOverdue();
                markedOverdue++;
            }

            // Tiered notification thresholds (days relative to due date)
            var triggerDays = new[] { 7, 1, 0, -7, -30, -90 };
            var roundedDays = (int)Math.Round(daysToOrFromDue);

            if (triggerDays.Contains(roundedDays))
            {
                var tier = roundedDays switch
                {
                    7 => "T-7 (1 week warning)",
                    1 => "T-1 (1 day warning)",
                    0 => "T+0 (due today)",
                    -7 => "T+7 (1 week overdue)",
                    -30 => "T+30 (1 month overdue)",
                    -90 => "T+90 (3 months overdue — escalate to RiskManager)",
                    _ => $"T{roundedDays:+0;-0}"
                };

                _logger.LogWarning(
                    "CS Item Notification [{Tier}] — App: {AppNumber} | Item: {ItemName} | Due: {DueDate:dd-MMM-yyyy} | Status: {Status}",
                    tier,
                    appNumber,
                    item.ItemName,
                    item.DueDate!.Value,
                    item.Status);

                // TODO: Dispatch email/in-app notification via INotificationService when implemented
                notifications++;
            }
        }

        if (markedOverdue > 0 || notifications > 0)
            await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "CS monitoring cycle complete — {MarkedOverdue} items marked overdue, {Notifications} notifications fired",
            markedOverdue, notifications);
    }
}

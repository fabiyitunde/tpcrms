using System.Net;
using System.Text;
using CRMS.Application.Billing.Commands;
using CRMS.Application.Common;
using CRMS.Application.Notification.Commands;
using CRMS.Application.Notification.Interfaces;
using CRMS.Domain.Aggregates.Billing;
using CRMS.Domain.Constants;
using CRMS.Domain.Entities.Identity;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace CRMS.Application.Billing.Alerts;

public sealed record BillingAlertDto(Guid Id, BillingAlertKind Kind, string KindName, BillingAlertSeverity Severity, BillingAlertStatus Status, string Title, string Detail, string? Link,
    DateTime FirstSeenAt, DateTime LastSeenAt, int TimesSeen, DateTime? SnoozedUntil, DateTime? ResolvedAt);

public sealed record BillingAlertRunResultDto(int Findings, int Added, int Updated, int Resolved, int Emailed, DateTime RanAt);

public static class BillingAlertNames
{
    public const string TemplateCode = "BILLING_ALERT";

    public static string KindName(BillingAlertKind kind) => kind switch
    {
        BillingAlertKind.SpendSpike => "Spend above norm",
        BillingAlertKind.HighFailureRate => "Failing calls",
        BillingAlertKind.UnattributedCalls => "Unattributed calls",
        BillingAlertKind.UnpricedActivity => "Unpriced item",
        BillingAlertKind.OverdueStatement => "Overdue statement",
        BillingAlertKind.MonthNotClosed => "Month not closed",
        _ => kind.ToString(),
    };

    public static BillingAlertDto ToDto(BillingAlert a) =>
        new(a.Id, a.Kind, KindName(a.Kind), a.Severity, a.Status, a.Title, a.Detail, a.Link, a.FirstSeenAt, a.LastSeenAt, a.TimesSeen, a.SnoozedUntil, a.ResolvedAt);
}

// ───────────────────────────── Run the checks ─────────────────────────────

/// <param name="ActorUserId">Null when the background job runs it; a Billing Manager when run from the page.</param>
public sealed record RunBillingAlertChecksCommand(Guid? ActorUserId) : IRequest<ApplicationResult<BillingAlertRunResultDto>>;

/// <summary>
/// Runs every check and reconciles the findings with the alerts on record: a new condition becomes
/// an Open alert and is emailed to the Billing Managers; a known one is updated in place, however it
/// was silenced; a recorded condition the checks no longer find is resolved.
/// </summary>
public sealed class RunBillingAlertChecksHandler : IRequestHandler<RunBillingAlertChecksCommand, ApplicationResult<BillingAlertRunResultDto>>
{
    private readonly BillingAlertChecks _checks;
    private readonly IBillingAlertRepository _alerts;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notifications;
    private readonly IEmailSenderInfo _senderInfo;
    private readonly BillingSettings _settings;
    private readonly ILogger<RunBillingAlertChecksHandler> _logger;

    public RunBillingAlertChecksHandler(BillingAlertChecks checks, IBillingAlertRepository alerts, IUserRepository users, IUnitOfWork uow,
        INotificationService notifications, IEmailSenderInfo senderInfo, BillingSettings settings, ILogger<RunBillingAlertChecksHandler> logger)
    {
        _checks = checks;
        _alerts = alerts;
        _users = users;
        _uow = uow;
        _notifications = notifications;
        _senderInfo = senderInfo;
        _settings = settings;
        _logger = logger;
    }

    public async Task<ApplicationResult<BillingAlertRunResultDto>> Handle(RunBillingAlertChecksCommand request, CancellationToken ct = default)
    {
        if (request.ActorUserId is { } actor)
        {
            var access = await BillingAccess.RequireManagerAsync(_users, actor, ct);
            if (!access.IsSuccess) return ApplicationResult<BillingAlertRunResultDto>.Failure(access.Error!);
        }

        var findings = await _checks.RunAsync(ct);
        var active = (await _alerts.GetActiveAsync(ct)).ToDictionary(a => a.Key, StringComparer.Ordinal);
        var added = new List<BillingAlert>();
        var updated = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var f in findings)
        {
            if (!seen.Add(f.Key)) continue;
            if (active.TryGetValue(f.Key, out var existing))
            {
                existing.SeenAgain(f.Severity, f.Title, f.Detail);
                updated++;
                continue;
            }
            var created = BillingAlert.Create(f.Kind, f.Severity, f.Key, f.Title, f.Detail, f.Link);
            if (created.IsFailure) { _logger.LogWarning("Billing alert not recorded: {Error}", created.Error); continue; }
            await _alerts.AddAsync(created.Value, ct);
            added.Add(created.Value);
        }

        var resolved = 0;
        foreach (var stale in active.Values.Where(a => !seen.Contains(a.Key)))
        {
            stale.Resolve();
            resolved++;
        }

        var emailed = 0;
        if (added.Count > 0 && _settings.Alerts.EmailManagers)
        {
            emailed = await EmailManagersAsync(added, ct);
            foreach (var a in added) a.MarkNotified();
        }
        await _uow.SaveChangesAsync(ct);

        return ApplicationResult<BillingAlertRunResultDto>.Success(new BillingAlertRunResultDto(findings.Count, added.Count, updated, resolved, emailed, DateTime.UtcNow));
    }

    private async Task<int> EmailManagersAsync(List<BillingAlert> added, CancellationToken ct)
    {
        var managers = (await _users.GetByRoleAsync(Roles.BillingManager, ct)).Where(u => u.Status == UserStatus.Active).ToList();
        if (managers.Count == 0) return 0;

        var site = (_senderInfo.Get().SiteUrl ?? "").TrimEnd('/');
        var text = new StringBuilder();
        var html = new StringBuilder();
        foreach (var a in added.OrderByDescending(a => a.Severity))
        {
            text.AppendLine($"[{a.Severity}] {a.Title}");
            text.AppendLine($"    {a.Detail}");
            if (a.Link is not null) text.AppendLine($"    {site}{a.Link}");
            text.AppendLine();
            var colour = a.Severity switch { BillingAlertSeverity.Critical => "#b91c1c", BillingAlertSeverity.Warning => "#b45309", _ => "#1f7a3d" };
            html.Append("<tr><td style=\"padding:10px 12px;border-bottom:1px solid #e5e7eb;vertical-align:top;\">")
                .Append($"<span style=\"display:inline-block;padding:2px 8px;border-radius:10px;background:{colour};color:#fff;font-size:11px;font-weight:bold;\">{a.Severity}</span></td>")
                .Append("<td style=\"padding:10px 12px;border-bottom:1px solid #e5e7eb;\">")
                .Append($"<div style=\"font-weight:bold;color:#111827;\">{WebUtility.HtmlEncode(a.Title)}</div>")
                .Append($"<div style=\"color:#4b5563;font-size:13px;margin-top:2px;\">{WebUtility.HtmlEncode(a.Detail)}</div>");
            if (a.Link is not null)
                html.Append($"<div style=\"margin-top:4px;\"><a href=\"{WebUtility.HtmlEncode(site + a.Link)}\" style=\"color:#1f7a3d;font-size:13px;\">Open in CRMS</a></div>");
            html.Append("</td></tr>");
        }

        var sent = 0;
        foreach (var m in managers)
        {
            try
            {
                await _notifications.SendEmailAsync(NotificationType.SystemAlert, m.Email, m.FullName, BillingAlertNames.TemplateCode,
                    new Dictionary<string, string>
                    {
                        ["RecipientName"] = m.FullName,
                        ["Count"] = added.Count.ToString(),
                        ["Plural"] = added.Count == 1 ? "" : "s",
                        ["AlertListText"] = text.ToString().TrimEnd(),
                        ["AlertListHtml"] = html.ToString(),
                        ["DashboardLink"] = site + "/billing",
                    },
                    recipientUserId: m.Id, priority: NotificationPriority.Normal, ct: ct);
                sent++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Billing alert email to {Email} could not be queued", m.Email);
            }
        }
        return sent;
    }
}

// ───────────────────────────── Read and act ─────────────────────────────

public sealed record GetBillingAlertsQuery(bool IncludeHistory) : IRequest<ApplicationResult<IReadOnlyList<BillingAlertDto>>>;

public sealed class GetBillingAlertsHandler : IRequestHandler<GetBillingAlertsQuery, ApplicationResult<IReadOnlyList<BillingAlertDto>>>
{
    private readonly IBillingAlertRepository _alerts;
    public GetBillingAlertsHandler(IBillingAlertRepository alerts) => _alerts = alerts;

    public async Task<ApplicationResult<IReadOnlyList<BillingAlertDto>>> Handle(GetBillingAlertsQuery request, CancellationToken ct = default)
    {
        var list = request.IncludeHistory ? await _alerts.GetRecentAsync(100, ct) : await _alerts.GetActiveAsync(ct);
        var now = DateTime.UtcNow;
        var dtos = list
            .Where(a => request.IncludeHistory || a.Status == BillingAlertStatus.Open || (a.Status == BillingAlertStatus.Snoozed && a.SnoozedUntil <= now))
            .OrderByDescending(a => a.Status == BillingAlertStatus.Open || a.Status == BillingAlertStatus.Snoozed)
            .ThenByDescending(a => a.Severity).ThenByDescending(a => a.LastSeenAt)
            .Select(BillingAlertNames.ToDto).ToList();
        return ApplicationResult<IReadOnlyList<BillingAlertDto>>.Success(dtos);
    }
}

public sealed record ActOnBillingAlertCommand(Guid Id, string Action, Guid ActorUserId) : IRequest<ApplicationResult>;

/// <summary>Snooze (for the configured number of days), dismiss or reopen an alert. Billing Managers only.</summary>
public sealed class ActOnBillingAlertHandler : IRequestHandler<ActOnBillingAlertCommand, ApplicationResult>
{
    private readonly IBillingAlertRepository _alerts;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;
    private readonly BillingSettings _settings;

    public ActOnBillingAlertHandler(IBillingAlertRepository alerts, IUserRepository users, IUnitOfWork uow, BillingSettings settings)
    {
        _alerts = alerts;
        _users = users;
        _uow = uow;
        _settings = settings;
    }

    public async Task<ApplicationResult> Handle(ActOnBillingAlertCommand request, CancellationToken ct = default)
    {
        var access = await BillingAccess.RequireManagerAsync(_users, request.ActorUserId, ct);
        if (!access.IsSuccess) return access;
        var alert = await _alerts.GetByIdAsync(request.Id, ct);
        if (alert is null) return ApplicationResult.Failure("Alert not found.");
        var result = request.Action.ToLowerInvariant() switch
        {
            "snooze" => alert.Snooze(DateTime.UtcNow.AddDays(Math.Max(1, _settings.Alerts.SnoozeDays)), request.ActorUserId),
            "dismiss" => alert.Dismiss(request.ActorUserId),
            "reopen" => alert.Reopen(request.ActorUserId),
            _ => Domain.Common.Result.Failure("Unknown action."),
        };
        if (result.IsFailure) return ApplicationResult.Failure(result.Error);
        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}

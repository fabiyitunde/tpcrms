using CRMS.Application.Notification.Commands;
using CRMS.Application.Notification.Queries;
using CRMS.Domain.Aggregates.Notification;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using CRMS.Infrastructure.Persistence;
using CRMS.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CRMS.Infrastructure.Tests;

/// <summary>
/// Admin > Outbound Email, through the real repository on an in-memory database: the page lists
/// what the system tried to send, and lets an administrator send a copy or free a stuck row.
/// The statements that the in-memory provider cannot vouch for (grouping, Contains) are also built
/// with the MySQL provider against an address nothing listens on, as in
/// <see cref="ApiConsumerSetupLinkRepositoryTests"/>.
/// </summary>
public class OutboundEmailTests
{
    private sealed class RecordingAudit : IOutboundEmailAudit
    {
        public List<OutboundEmailAuditEntry> Entries { get; } = [];
        public Task RecordAsync(OutboundEmailAuditEntry entry, CancellationToken ct = default) { Entries.Add(entry); return Task.CompletedTask; }
    }

    private static CRMSDbContext Memory(string name) =>
        new(new DbContextOptionsBuilder<CRMSDbContext>().UseInMemoryDatabase(name).Options);

    private static Notification Email(string template, string to, string subject = "Hello", NotificationPriority priority = NotificationPriority.Normal) =>
        Notification.Create(NotificationType.Custom, NotificationChannel.Email, priority, "Someone", to, template, subject, "body", "<p>body</p>").Value;

    private static Notification Failed(Notification n, string reason)
    {
        n.MarkAsSending();
        n.MarkAsFailed(reason, canRetry: false);
        return n;
    }

    private static Notification Sent(Notification n)
    {
        n.MarkAsSending();
        n.MarkAsSent("msg-1", "AmazonSES", "MessageId=msg-1");
        return n;
    }

    [Fact]
    public async Task The_queue_lists_emails_newest_first_and_filters_by_status_template_and_recipient()
    {
        await using var db = Memory(nameof(The_queue_lists_emails_newest_first_and_filters_by_status_template_and_recipient));
        var older = Failed(Email("PASSWORD_RESET", "ada@bank.test", "Reset your password"), "SES error (Forbidden). not authorized to perform: ses:SendEmail");
        var newer = Sent(Email("ACCOUNT_CREATED", "bola@bank.test", "Your account"));
        var sms = Notification.Create(NotificationType.Custom, NotificationChannel.SMS, NotificationPriority.Normal, "Someone", "+2340000", "SMS_X", "", "body").Value;
        db.Notifications.AddRange(older, newer, sms);
        await db.SaveChangesAsync();
        var repo = new NotificationRepository(db);

        var all = await new SearchOutboundEmailsHandler(repo).Handle(new SearchOutboundEmailsQuery(null, null, null, null, null, 1, 25));
        Assert.True(all.IsSuccess);
        Assert.Equal(2, all.Data!.Total);                       // the SMS is not an email
        Assert.Equal(newer.Id, all.Data.Items[0].Id);           // newest first
        Assert.Equal("Failed", all.Data.Items[1].Status);
        Assert.Contains("ses:SendEmail", all.Data.Items[1].FailureReason);

        var failed = await new SearchOutboundEmailsHandler(repo).Handle(new SearchOutboundEmailsQuery("failed", null, null, null, null, 1, 25));
        Assert.Equal([older.Id], failed.Data!.Items.Select(i => i.Id));

        var byTemplate = await new SearchOutboundEmailsHandler(repo).Handle(new SearchOutboundEmailsQuery(null, "ACCOUNT", null, null, null, 1, 25));
        Assert.Equal([newer.Id], byTemplate.Data!.Items.Select(i => i.Id));

        var byRecipient = await new SearchOutboundEmailsHandler(repo).Handle(new SearchOutboundEmailsQuery(null, null, "ada@", null, null, 1, 25));
        Assert.Equal([older.Id], byRecipient.Data!.Items.Select(i => i.Id));

        var unknown = await new SearchOutboundEmailsHandler(repo).Handle(new SearchOutboundEmailsQuery("Lost", null, null, null, null, 1, 25));
        Assert.False(unknown.IsSuccess);
    }

    [Fact]
    public async Task The_body_of_an_email_that_carries_a_credential_is_withheld()
    {
        await using var db = Memory(nameof(The_body_of_an_email_that_carries_a_credential_is_withheld));
        var reset = Sent(Email("PASSWORD_RESET", "ada@bank.test", "Reset your password"));
        var plain = Sent(Email("NAMP_ACTION_REQUIRED", "bola@bank.test", "Your stage is ready"));
        db.Notifications.AddRange(reset, plain);
        await db.SaveChangesAsync();
        var handler = new GetOutboundEmailHandler(new NotificationRepository(db));

        var withheld = (await handler.Handle(new GetOutboundEmailQuery(reset.Id))).Data!;
        var shown = (await handler.Handle(new GetOutboundEmailQuery(plain.Id))).Data!;

        Assert.True(withheld.BodyWithheld);
        Assert.Empty(withheld.Body);
        Assert.Null(withheld.BodyHtml);
        Assert.Equal("Reset your password", withheld.Summary.Subject);   // the row itself still shows
        Assert.False(shown.BodyWithheld);
        Assert.Equal("body", shown.Body);
        Assert.Equal("<p>body</p>", shown.BodyHtml);
    }

    [Fact]
    public async Task The_tiles_count_sent_failed_waiting_and_stuck()
    {
        await using var db = Memory(nameof(The_tiles_count_sent_failed_waiting_and_stuck));
        db.Notifications.AddRange(
            Sent(Email("A", "a@x.test")),
            Failed(Email("B", "b@x.test"), "boom"),
            Email("C", "c@x.test"),                               // Pending
            Email("D", "d@x.test"));                              // will be left in Sending, just now
        var stuck = Email("E", "e@x.test");
        stuck.MarkAsSending();
        typeof(Domain.Common.Entity).GetProperty(nameof(Notification.CreatedAt))!.SetValue(stuck, DateTime.UtcNow.AddMinutes(-30));
        db.Notifications.Add(stuck);
        await db.SaveChangesAsync();
        var sendingNow = await db.Notifications.SingleAsync(n => n.TemplateCode == "D");
        sendingNow.MarkAsSending();
        await db.SaveChangesAsync();

        var tiles = await new GetOutboundEmailSummaryHandler(new NotificationRepository(db)).Handle(new GetOutboundEmailSummaryQuery());

        Assert.True(tiles.IsSuccess);
        Assert.Equal(1, tiles.Data!.SentLast24Hours);
        Assert.Equal(1, tiles.Data.FailedLast24Hours);
        Assert.Equal(1, tiles.Data.FailedLast7Days);
        Assert.Equal(2, tiles.Data.Waiting);                      // the Pending one and the one being sent right now
        Assert.Equal(1, tiles.Data.Stuck);
    }

    [Fact]
    public async Task Sending_again_queues_an_identical_copy_and_keeps_the_original()
    {
        await using var db = Memory(nameof(Sending_again_queues_an_identical_copy_and_keeps_the_original));
        var original = Failed(Email("ACCOUNT_CREATED", "ada@bank.test", "Your account", NotificationPriority.High), "SES error (Forbidden).");
        db.Notifications.Add(original);
        await db.SaveChangesAsync();
        var audit = new RecordingAudit();
        var handler = new ResendOutboundEmailHandler(new NotificationRepository(db), audit, db);

        var result = await handler.Handle(new ResendOutboundEmailCommand(original.Id, Guid.NewGuid(), "Admin"));

        Assert.True(result.IsSuccess, result.Error);
        var copy = await db.Notifications.SingleAsync(n => n.Id == result.Data);
        Assert.Equal(NotificationStatus.Pending, copy.Status);
        Assert.Equal(original.Subject, copy.Subject);
        Assert.Equal(original.Body, copy.Body);
        Assert.Equal(original.BodyHtml, copy.BodyHtml);
        Assert.Equal(original.RecipientAddress, copy.RecipientAddress);
        Assert.Equal(original.Priority, copy.Priority);
        Assert.Equal(NotificationStatus.Failed, (await db.Notifications.SingleAsync(n => n.Id == original.Id)).Status);
        var entry = Assert.Single(audit.Entries);
        Assert.Equal(OutboundEmailAuditEntry.Resent, entry.Event);
        Assert.Contains("Admin", entry.Description);

        // A second click while the copy waits would only send the same email twice.
        var again = await handler.Handle(new ResendOutboundEmailCommand(original.Id, Guid.NewGuid(), "Admin"));
        Assert.False(again.IsSuccess);
        Assert.Contains("already waiting", again.Error);
    }

    [Fact]
    public async Task An_email_still_on_its_first_attempt_cannot_be_sent_again()
    {
        await using var db = Memory(nameof(An_email_still_on_its_first_attempt_cannot_be_sent_again));
        var pending = Email("A", "a@x.test");
        db.Notifications.Add(pending);
        await db.SaveChangesAsync();

        var result = await new ResendOutboundEmailHandler(new NotificationRepository(db), new RecordingAudit(), db)
            .Handle(new ResendOutboundEmailCommand(pending.Id, Guid.NewGuid(), "Admin"));

        Assert.False(result.IsSuccess);
        Assert.Contains("not finished", result.Error);
    }

    [Fact]
    public async Task An_expired_password_reset_is_not_sent_again_but_a_fresh_one_is()
    {
        await using var db = Memory(nameof(An_expired_password_reset_is_not_sent_again_but_a_fresh_one_is));
        var stale = Failed(Email("PASSWORD_RESET", "ada@bank.test", "Reset"), "boom");
        typeof(Domain.Common.Entity).GetProperty(nameof(Notification.CreatedAt))!.SetValue(stale, DateTime.UtcNow.AddMinutes(-61));
        var fresh = Failed(Email("PASSWORD_RESET", "bola@bank.test", "Reset"), "boom");
        db.Notifications.AddRange(stale, fresh);
        await db.SaveChangesAsync();
        var handler = new ResendOutboundEmailHandler(new NotificationRepository(db), new RecordingAudit(), db);

        var refused = await handler.Handle(new ResendOutboundEmailCommand(stale.Id, Guid.NewGuid(), "Admin"));
        var allowed = await handler.Handle(new ResendOutboundEmailCommand(fresh.Id, Guid.NewGuid(), "Admin"));

        Assert.False(refused.IsSuccess);
        Assert.Contains("expired", refused.Error);
        Assert.True(allowed.IsSuccess, allowed.Error);
    }

    [Fact]
    public async Task A_row_stuck_in_Sending_can_be_released_but_a_fresh_one_cannot()
    {
        await using var db = Memory(nameof(A_row_stuck_in_Sending_can_be_released_but_a_fresh_one_cannot));
        var stuck = Email("A", "a@x.test");
        stuck.MarkAsSending();
        typeof(Domain.Common.Entity).GetProperty(nameof(Notification.CreatedAt))!.SetValue(stuck, DateTime.UtcNow.AddMinutes(-11));
        var fresh = Email("B", "b@x.test");
        fresh.MarkAsSending();
        var failed = Failed(Email("C", "c@x.test"), "boom");
        db.Notifications.AddRange(stuck, fresh, failed);
        await db.SaveChangesAsync();
        var audit = new RecordingAudit();
        var handler = new ReleaseStuckOutboundEmailHandler(new NotificationRepository(db), audit, db);

        var released = await handler.Handle(new ReleaseStuckOutboundEmailCommand(stuck.Id, Guid.NewGuid(), "Admin"));
        var tooSoon = await handler.Handle(new ReleaseStuckOutboundEmailCommand(fresh.Id, Guid.NewGuid(), "Admin"));
        var notStuck = await handler.Handle(new ReleaseStuckOutboundEmailCommand(failed.Id, Guid.NewGuid(), "Admin"));

        Assert.True(released.IsSuccess, released.Error);
        Assert.Equal(NotificationStatus.Pending, (await db.Notifications.SingleAsync(n => n.Id == stuck.Id)).Status);
        Assert.Equal(OutboundEmailAuditEntry.Released, Assert.Single(audit.Entries).Event);
        Assert.False(tooSoon.IsSuccess);
        Assert.Contains("may still be on its way", tooSoon.Error);
        Assert.False(notStuck.IsSuccess);
        Assert.Contains("Only an email stuck in Sending", notStuck.Error);
    }

    [Fact]
    public void Only_a_Sending_notification_returns_to_the_queue()
    {
        var pending = Email("A", "a@x.test");
        Assert.True(pending.ReturnToQueue().IsFailure);

        pending.MarkAsSending();
        Assert.True(pending.ReturnToQueue().IsSuccess);
        Assert.Equal(NotificationStatus.Pending, pending.Status);
        Assert.Null(pending.NextRetryAt);
    }

    // ── MySQL translation ──────────────────────────────────────────────────────────────────

    private static CRMSDbContext Nowhere() =>
        new(new DbContextOptionsBuilder<CRMSDbContext>()
            .UseMySql("Server=127.0.0.1;Port=1;Database=none;User=none;Password=none;Connection Timeout=2;",
                new MySqlServerVersion(new Version(8, 0, 36)))
            .Options);

    private static async Task AssertTranslatesAsync(Func<NotificationRepository, Task> statement)
    {
        await using var context = Nowhere();
        var failure = await Record.ExceptionAsync(() => statement(new NotificationRepository(context)));
        Assert.NotNull(failure);
        var text = failure.ToString();
        Assert.DoesNotContain("could not be translated", text);
        Assert.DoesNotContain("not supported", text);
        Assert.Contains("connect", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public Task Searching_with_every_filter_translates() =>
        AssertTranslatesAsync(r => r.SearchAsync(new NotificationSearch(
            NotificationChannel.Email, NotificationStatus.Failed, "PASSWORD", "ada", DateTime.UtcNow.AddDays(-7), DateTime.UtcNow, 2, 25)));

    [Fact]
    public Task Counting_by_status_translates() =>
        AssertTranslatesAsync(r => r.CountByStatusAsync(NotificationChannel.Email, DateTime.UtcNow.AddDays(-1)));

    [Fact]
    public Task The_queued_check_translates() =>
        AssertTranslatesAsync(r => r.IsQueuedAsync(NotificationChannel.Email, "ada@bank.test", "Reset"));
}

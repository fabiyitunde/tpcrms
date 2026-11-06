using CRMS.Application.CustomerCheck;
using CRMS.Application.CustomerCheck.Commands;
using CRMS.Application.CustomerCheck.DTOs;
using CRMS.Application.CustomerCheck.Queries;
using CRMS.Domain.Aggregates.CustomerCheck;
using CRMS.Domain.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace CRMS.Application.Tests.CustomerCheck;

/// <summary>
/// The one-time setup link: a consumer's key and signing secret are handed over by an emailed link
/// that issues them when it is opened, once. These cover what makes that safe: nothing secret in
/// the email or the database, one opening only, and every way a link dies.
/// </summary>
public class ApiConsumerSetupLinkTests
{
    private const string Site = "https://crms.example.com";
    private static readonly Guid Admin = Guid.NewGuid();

    private readonly FakeConsumers _consumers = new();
    private readonly FakeLinks _links = new();
    private readonly FakeMailer _mailer = new();
    private readonly FakeAudit _audit = new();
    private readonly FakeCipher _cipher = new();
    private readonly FakeUnitOfWork _uow = new();

    private ApiConsumer Consumer(string? email = "integrations@agripay.example", bool active = true)
    {
        var c = ApiConsumer.Create("AgriPay", ApiKeys.Hash("old-key"), "crms_oldkey", CustomerCheckSections.All, 30, false, 30, null, Admin).Value;
        c.SetCallbackSecret(_cipher.Protect("old-secret"), "crms_whsec_old", Admin);
        if (email is not null) Assert.True(c.SetContactEmail(email, Admin).IsSuccess);
        if (!active) c.SetActive(false, Admin);
        _consumers.Items.Add(c);
        return c;
    }

    private SendApiConsumerSetupLinkHandler Sender() =>
        new(_consumers, _links, _mailer, _audit, _uow, NullLogger<SendApiConsumerSetupLinkHandler>.Instance);

    private RedeemApiConsumerSetupLinkHandler Redeemer() =>
        new(_consumers, _links, _cipher, _audit, _uow, NullLogger<RedeemApiConsumerSetupLinkHandler>.Instance);

    private PeekApiConsumerSetupLinkHandler Peeker() => new(_consumers, _links);

    private async Task<string> SendAsync(ApiConsumer c, bool key = true, bool secret = true)
    {
        var sent = await Sender().Handle(new SendApiConsumerSetupLinkCommand(c.Id, key, secret, Site, 24, Admin, "Ada Admin"));
        Assert.True(sent.IsSuccess, sent.Error);
        return TokenOf(_mailer.Sent.Last().Link);
    }

    private static string TokenOf(string link) => link[(link.LastIndexOf('/') + 1)..];

    // ── Contact email ────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("integrations@agripay.example", true)]
    [InlineData("  ops.team+crms@sub.agripay.co.ng ", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("not-an-email", false)]
    [InlineData("a@b", false)]
    [InlineData("two@addresses.example, other@addresses.example", false)]
    [InlineData("a@@b.example", false)]
    [InlineData("name <a@b.example>", false)]
    public void Contact_email_must_be_one_usable_address(string email, bool ok)
    {
        var c = ApiConsumer.Create("AgriPay", "h", "p", CustomerCheckSections.All, 30, false, 30, null, Admin).Value;
        Assert.Equal(ok, c.SetContactEmail(email, Admin).IsSuccess);
        if (ok) Assert.Equal(email.Trim(), c.ContactEmail);
        else Assert.Null(c.ContactEmail);
    }

    [Fact]
    public async Task A_new_consumer_cannot_be_created_without_a_contact_email()
    {
        var handler = new CreateApiConsumerHandler(_consumers, _cipher, _uow);
        var without = await handler.Handle(new CreateApiConsumerCommand("AgriPay", null, CustomerCheckSections.All.ToList(), 30, false, 30, Admin));
        Assert.False(without.IsSuccess);
        Assert.Contains("contact email", without.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(_consumers.Items);

        var with = await handler.Handle(new CreateApiConsumerCommand(
            "AgriPay", null, CustomerCheckSections.All.ToList(), 30, false, 30, Admin, ContactEmail: "integrations@agripay.example"));
        Assert.True(with.IsSuccess, with.Error);
        Assert.Equal("integrations@agripay.example", with.Data!.Consumer.ContactEmail);
    }

    // ── Sending ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Sending_emails_a_link_and_keeps_no_secret_anywhere()
    {
        var c = Consumer();
        var token = await SendAsync(c);

        var mail = Assert.Single(_mailer.Sent);
        Assert.Equal("integrations@agripay.example", mail.To);
        Assert.StartsWith(Site + ApiConsumerSetupLinks.Path, mail.Link);
        Assert.Equal("API key and callback signing secret", mail.Issues);
        Assert.Equal(24, mail.ValidForHours);
        // 32 random bytes, URL-safe, so nothing in the link needs encoding.
        Assert.Matches("^[A-Za-z0-9_-]{43}$", token);

        // The database holds the hash of the token, never the token.
        var link = Assert.Single(_links.Items);
        Assert.Equal(ApiConsumerSetupLinks.Hash(token), link.TokenHash);
        Assert.NotEqual(token, link.TokenHash);
        Assert.Equal("Ada Admin", link.SentByName);
        Assert.Equal(ApiConsumerSetupLink.Pending, link.State(DateTime.UtcNow));

        // Nothing is issued by sending: the consumer's credentials are untouched.
        Assert.Equal(ApiKeys.Hash("old-key"), c.KeyHash);
        Assert.Equal("old-secret", _cipher.Unprotect(c.CallbackSecretCiphertext!));

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal(ApiConsumerAuditEntry.LinkSent, entry.Event);
        Assert.Equal(Admin, entry.UserId);
        Assert.DoesNotContain(token, entry.Description);
    }

    [Fact]
    public async Task Sending_needs_an_active_consumer_with_a_contact_email_and_something_to_issue()
    {
        var noEmail = Consumer(email: null);
        var r1 = await Sender().Handle(new SendApiConsumerSetupLinkCommand(noEmail.Id, true, true, Site, 24, Admin, "Ada"));
        Assert.False(r1.IsSuccess);
        Assert.Contains("contact email", r1.Error);

        _consumers.Items.Clear();
        var inactive = Consumer(active: false);
        var r2 = await Sender().Handle(new SendApiConsumerSetupLinkCommand(inactive.Id, true, true, Site, 24, Admin, "Ada"));
        Assert.False(r2.IsSuccess);

        _consumers.Items.Clear();
        var c = Consumer();
        var r3 = await Sender().Handle(new SendApiConsumerSetupLinkCommand(c.Id, false, false, Site, 24, Admin, "Ada"));
        Assert.False(r3.IsSuccess);
        var r4 = await Sender().Handle(new SendApiConsumerSetupLinkCommand(c.Id, true, true, "not a url", 24, Admin, "Ada"));
        Assert.False(r4.IsSuccess);

        Assert.Empty(_mailer.Sent);
        Assert.Empty(_links.Items);
    }

    [Fact]
    public async Task A_newer_link_cancels_the_one_before_it()
    {
        var c = Consumer();
        var first = await SendAsync(c);
        var second = await SendAsync(c);

        var old = await Redeemer().Handle(new RedeemApiConsumerSetupLinkCommand(first, "10.0.0.1"));
        Assert.False(old.IsSuccess);
        Assert.Equal(ApiConsumerSetupLink.Replaced, (await Peeker().Handle(new PeekApiConsumerSetupLinkQuery(first))).Data!.State);

        var fresh = await Redeemer().Handle(new RedeemApiConsumerSetupLinkCommand(second, "10.0.0.1"));
        Assert.True(fresh.IsSuccess, fresh.Error);
    }

    [Fact]
    public async Task A_link_that_could_not_be_emailed_is_cancelled()
    {
        var c = Consumer();
        _mailer.Fail = true;

        var sent = await Sender().Handle(new SendApiConsumerSetupLinkCommand(c.Id, true, true, Site, 24, Admin, "Ada"));

        Assert.False(sent.IsSuccess);
        Assert.Equal(ApiConsumerSetupLink.Replaced, Assert.Single(_links.Items).State(DateTime.UtcNow));
        Assert.Empty(_audit.Entries);
    }

    // ── Opening ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Looking_at_a_link_changes_nothing()
    {
        var c = Consumer();
        var token = await SendAsync(c);

        for (var i = 0; i < 3; i++)
        {
            var view = (await Peeker().Handle(new PeekApiConsumerSetupLinkQuery(token))).Data!;
            Assert.Equal(ApiConsumerSetupLink.Pending, view.State);
            Assert.Equal("AgriPay", view.ConsumerName);
            Assert.True(view.IssuesKey && view.IssuesSecret);
        }

        Assert.Equal(ApiKeys.Hash("old-key"), c.KeyHash);
        Assert.Null(_links.Items.Single().OpenedAt);
    }

    [Fact]
    public async Task Opening_issues_new_credentials_once_and_retires_the_old_ones()
    {
        var c = Consumer();
        var token = await SendAsync(c);
        _audit.Entries.Clear();

        var opened = await Redeemer().Handle(new RedeemApiConsumerSetupLinkCommand(token, "203.0.113.7"));

        Assert.True(opened.IsSuccess, opened.Error);
        var issued = opened.Data!;
        Assert.Equal("AgriPay", issued.ConsumerName);
        Assert.StartsWith(ApiKeys.Prefix, issued.Key);
        Assert.StartsWith(ApiKeys.SecretPrefix, issued.CallbackSecret);

        // The consumer now answers to the new key only, and signs with the new secret.
        Assert.Equal(ApiKeys.Hash(issued.Key!), c.KeyHash);
        Assert.NotEqual(ApiKeys.Hash("old-key"), c.KeyHash);
        Assert.Equal(issued.CallbackSecret, _cipher.Unprotect(c.CallbackSecretCiphertext!));

        var link = _links.Items.Single();
        Assert.Equal(ApiConsumerSetupLink.Opened, link.State(DateTime.UtcNow));
        Assert.Equal("203.0.113.7", link.OpenedFromIp);

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal(ApiConsumerAuditEntry.LinkOpened, entry.Event);
        Assert.Equal("203.0.113.7", entry.IpAddress);
        Assert.DoesNotContain(issued.Key!, entry.Description);
        Assert.DoesNotContain(issued.CallbackSecret!, entry.Description);

        // A second opening gets nothing and changes nothing.
        var keyAfterFirst = c.KeyHash;
        var again = await Redeemer().Handle(new RedeemApiConsumerSetupLinkCommand(token, "198.51.100.9"));
        Assert.False(again.IsSuccess);
        Assert.Null(again.Data);
        Assert.Equal(keyAfterFirst, c.KeyHash);
        Assert.Equal("203.0.113.7", _links.Items.Single().OpenedFromIp);

        var view = (await Peeker().Handle(new PeekApiConsumerSetupLinkQuery(token))).Data!;
        Assert.Equal(ApiConsumerSetupLink.Opened, view.State);
        Assert.NotNull(view.OpenedAt);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task A_link_issues_only_what_it_was_sent_for(bool key, bool secret)
    {
        var c = Consumer();
        var token = await SendAsync(c, key, secret);

        var issued = (await Redeemer().Handle(new RedeemApiConsumerSetupLinkCommand(token, null))).Data!;

        Assert.Equal(key, issued.Key is not null);
        Assert.Equal(secret, issued.CallbackSecret is not null);
        Assert.Equal(key, c.KeyHash != ApiKeys.Hash("old-key"));
        Assert.Equal(secret, _cipher.Unprotect(c.CallbackSecretCiphertext!) != "old-secret");
    }

    [Fact]
    public async Task An_expired_link_issues_nothing()
    {
        var c = Consumer();
        var (token, hash) = ApiConsumerSetupLinks.NewToken();
        _links.Items.Add(ApiConsumerSetupLink.Issue(
            c.Id, hash, c.ContactEmail!, true, true, Admin, "Ada", DateTime.UtcNow.AddHours(-25), TimeSpan.FromHours(24)).Value);

        var opened = await Redeemer().Handle(new RedeemApiConsumerSetupLinkCommand(token, null));

        Assert.False(opened.IsSuccess);
        Assert.Equal(ApiKeys.Hash("old-key"), c.KeyHash);
        Assert.Equal(ApiConsumerSetupLink.Expired, (await Peeker().Handle(new PeekApiConsumerSetupLinkQuery(token))).Data!.State);
    }

    [Theory]
    [InlineData("")]
    [InlineData("nonsense")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task An_unknown_token_is_simply_invalid(string token)
    {
        Consumer();

        Assert.Equal(ApiConsumerSetupLinkView.InvalidState, (await Peeker().Handle(new PeekApiConsumerSetupLinkQuery(token))).Data!.State);
        Assert.False((await Redeemer().Handle(new RedeemApiConsumerSetupLinkCommand(token, null))).IsSuccess);
    }

    [Fact]
    public async Task Switching_the_consumer_off_kills_its_link_for_good()
    {
        var c = Consumer();
        var token = await SendAsync(c);
        var toggle = new SetApiConsumerActiveHandler(_consumers, _links, _uow);

        await toggle.Handle(new SetApiConsumerActiveCommand(c.Id, false, Admin));
        Assert.Equal(ApiConsumerSetupLinkView.InvalidState, (await Peeker().Handle(new PeekApiConsumerSetupLinkQuery(token))).Data!.State);
        Assert.False((await Redeemer().Handle(new RedeemApiConsumerSetupLinkCommand(token, null))).IsSuccess);

        // Switching it back on does not bring the old email back to life.
        await toggle.Handle(new SetApiConsumerActiveCommand(c.Id, true, Admin));
        Assert.False((await Redeemer().Handle(new RedeemApiConsumerSetupLinkCommand(token, null))).IsSuccess);
        Assert.Equal(ApiKeys.Hash("old-key"), c.KeyHash);
    }

    [Fact]
    public async Task Changing_the_contact_email_cancels_a_link_sent_to_the_old_one()
    {
        var c = Consumer();
        var token = await SendAsync(c);
        var update = new UpdateApiConsumerHandler(_consumers, _links, _uow);

        var same = await update.Handle(new UpdateApiConsumerCommand(
            c.Id, c.Name, null, CustomerCheckSections.All.ToList(), 30, false, 30, Admin, ContactEmail: "Integrations@AgriPay.example"));
        Assert.True(same.IsSuccess, same.Error);
        Assert.Equal(ApiConsumerSetupLink.Pending, _links.Items.Single().State(DateTime.UtcNow));

        var moved = await update.Handle(new UpdateApiConsumerCommand(
            c.Id, c.Name, null, CustomerCheckSections.All.ToList(), 30, false, 30, Admin, ContactEmail: "new-owner@agripay.example"));
        Assert.True(moved.IsSuccess, moved.Error);
        Assert.False((await Redeemer().Handle(new RedeemApiConsumerSetupLinkCommand(token, null))).IsSuccess);

        var bad = await update.Handle(new UpdateApiConsumerCommand(
            c.Id, c.Name, null, CustomerCheckSections.All.ToList(), 30, false, 30, Admin, ContactEmail: "nope"));
        Assert.False(bad.IsSuccess);
    }

    [Fact]
    public async Task If_issuing_fails_the_link_can_be_opened_again()
    {
        var c = Consumer();
        var token = await SendAsync(c);

        _uow.FailNext = true;
        var failed = await Redeemer().Handle(new RedeemApiConsumerSetupLinkCommand(token, null));
        Assert.False(failed.IsSuccess);
        Assert.Equal(ApiConsumerSetupLink.Pending, _links.Items.Single().State(DateTime.UtcNow));

        var retried = await Redeemer().Handle(new RedeemApiConsumerSetupLinkCommand(token, null));
        Assert.True(retried.IsSuccess, retried.Error);
    }

    // ── What the administrator sees ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_consumer_list_and_history_show_what_became_of_each_link()
    {
        var c = Consumer();
        var first = await SendAsync(c);
        await SendAsync(c);
        Assert.NotNull(first);

        var history = (await new GetApiConsumerSetupLinksHandler(_links).Handle(new GetApiConsumerSetupLinksQuery(c.Id))).Data!;
        Assert.Equal([ApiConsumerSetupLink.Pending, ApiConsumerSetupLink.Replaced], history.Select(h => h.State));

        var listed = (await new GetApiConsumersHandler(_consumers, _links).Handle(new GetApiConsumersQuery())).Data!.Single();
        Assert.Equal("integrations@agripay.example", listed.ContactEmail);
        Assert.Equal(ApiConsumerSetupLink.Pending, listed.LatestSetupLink!.State);
    }

    [Fact]
    public async Task An_administrator_can_view_the_signing_secret_and_every_look_is_audited()
    {
        var c = Consumer();
        var handler = new RevealApiConsumerCallbackSecretHandler(_consumers, _cipher, _audit);

        var shown = await handler.Handle(new RevealApiConsumerCallbackSecretCommand(c.Id, Admin, "Ada Admin"));

        Assert.True(shown.IsSuccess, shown.Error);
        Assert.Equal("old-secret", shown.Data);
        var entry = Assert.Single(_audit.Entries);
        Assert.Equal(ApiConsumerAuditEntry.SecretViewed, entry.Event);
        Assert.Equal(Admin, entry.UserId);
        Assert.Equal("Ada Admin", entry.UserName);
        Assert.DoesNotContain("old-secret", entry.Description);

        // Nobody anonymous, and nothing shown when the look cannot be recorded.
        Assert.False((await handler.Handle(new RevealApiConsumerCallbackSecretCommand(c.Id, Guid.Empty, ""))).IsSuccess);
        _audit.Fail = true;
        await Assert.ThrowsAnyAsync<Exception>(() => handler.Handle(new RevealApiConsumerCallbackSecretCommand(c.Id, Admin, "Ada Admin")));
    }

    // ── Fakes ────────────────────────────────────────────────────────────────────────────────────

    private sealed class FakeConsumers : IApiConsumerRepository
    {
        public List<ApiConsumer> Items { get; } = [];
        public Task<ApiConsumer?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(c => c.Id == id));
        public Task<ApiConsumer?> GetByKeyHashAsync(string keyHash, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(c => c.KeyHash == keyHash));
        public Task<ApiConsumer?> GetByNameAsync(string name, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(c => c.Name == name));
        public Task<List<ApiConsumer>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Items.ToList());
        public Task AddAsync(ApiConsumer consumer, CancellationToken ct = default) { Items.Add(consumer); return Task.CompletedTask; }
        public void Update(ApiConsumer consumer) { }
    }

    /// <summary>Behaves like the real repository: revoke, claim and release act on the stored row
    /// at once, and a claim lands only on a link that can still be opened.</summary>
    private sealed class FakeLinks : IApiConsumerSetupLinkRepository
    {
        public List<ApiConsumerSetupLink> Items { get; } = [];

        private static void Set(ApiConsumerSetupLink link, string property, object? value) =>
            typeof(ApiConsumerSetupLink).GetProperty(property)!.SetValue(link, value);

        public Task AddAsync(ApiConsumerSetupLink link, CancellationToken ct = default) { Items.Add(link); return Task.CompletedTask; }
        public Task<ApiConsumerSetupLink?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(l => l.TokenHash == tokenHash));
        public Task<List<ApiConsumerSetupLink>> GetByConsumerAsync(Guid consumerId, int take, CancellationToken ct = default) =>
            Task.FromResult(Enumerable.Reverse(Items).Where(l => l.ConsumerId == consumerId).Take(take).ToList());
        public Task<List<ApiConsumerSetupLink>> GetLatestPerConsumerAsync(CancellationToken ct = default) =>
            Task.FromResult(Items.GroupBy(l => l.ConsumerId).Select(g => g.Last()).ToList());

        public Task<int> RevokeOpenAsync(Guid consumerId, DateTime now, CancellationToken ct = default)
        {
            var open = Items.Where(l => l.ConsumerId == consumerId && l.OpenedAt is null && l.RevokedAt is null).ToList();
            foreach (var l in open) Set(l, nameof(ApiConsumerSetupLink.RevokedAt), now);
            return Task.FromResult(open.Count);
        }

        public Task<bool> TryClaimAsync(Guid linkId, DateTime now, string? ipAddress, CancellationToken ct = default)
        {
            var link = Items.FirstOrDefault(l => l.Id == linkId && l.OpenedAt is null && l.RevokedAt is null && l.ExpiresAt > now);
            if (link is null) return Task.FromResult(false);
            Set(link, nameof(ApiConsumerSetupLink.OpenedAt), now);
            Set(link, nameof(ApiConsumerSetupLink.OpenedFromIp), ipAddress);
            return Task.FromResult(true);
        }

        public Task ReleaseAsync(Guid linkId, CancellationToken ct = default)
        {
            var link = Items.First(l => l.Id == linkId);
            Set(link, nameof(ApiConsumerSetupLink.OpenedAt), null);
            Set(link, nameof(ApiConsumerSetupLink.OpenedFromIp), null);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeMailer : IApiConsumerSetupMailer
    {
        public List<ApiConsumerSetupMail> Sent { get; } = [];
        public bool Fail { get; set; }
        public Task SendAsync(ApiConsumerSetupMail mail, CancellationToken ct = default)
        {
            if (Fail) throw new InvalidOperationException("mail server down");
            Sent.Add(mail);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAudit : IApiConsumerAudit
    {
        public List<ApiConsumerAuditEntry> Entries { get; } = [];
        public bool Fail { get; set; }
        public Task RecordAsync(ApiConsumerAuditEntry entry, CancellationToken ct = default)
        {
            if (Fail) throw new InvalidOperationException("audit store down");
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCipher : ICustomerCheckCipher
    {
        public string Protect(string plaintext) => "sealed:" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plaintext));
        public string Unprotect(string ciphertext) => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(ciphertext["sealed:".Length..]));
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public bool FailNext { get; set; }
        public Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            if (!FailNext) return Task.FromResult(1);
            FailNext = false;
            throw new InvalidOperationException("database down");
        }
    }
}

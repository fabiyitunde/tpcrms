using System.Text.RegularExpressions;
using CRMS.Application.Identity.Commands;
using CRMS.Application.Identity.DTOs;
using CRMS.Application.Identity.Interfaces;
using CRMS.Application.Notification.Interfaces;
using CRMS.Application.Notification.Services;
using CRMS.Domain.Entities.Identity;
using CRMS.Domain.Enums;
using CRMS.Infrastructure.Identity;
using CRMS.Infrastructure.Persistence;
using CRMS.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit.Abstractions;

namespace CRMS.Infrastructure.Tests;

/// <summary>
/// Confirms the email wirings (account creation, password change, forgot/reset password) actually
/// produce notifications and render their templates. Uses an in-memory database with the REAL
/// notification orchestrator and the REAL seeder — so the templates are applied exactly as in
/// production (SeedData.SeedAsync). A capturing sender stands in for SES/SMTP, so nothing is sent.
/// These run with no external dependencies (CI-safe).
/// </summary>
public class EmailWiringTests
{
    private readonly ITestOutputHelper _output;

    public EmailWiringTests(ITestOutputHelper output) => _output = output;

    /// <summary>A test email channel sender that records every message instead of sending it.</summary>
    private sealed class CapturingEmailSender : INotificationSender
    {
        public List<NotificationMessage> Sent { get; } = [];
        public NotificationChannel Channel => NotificationChannel.Email;
        public Task<NotificationSendResult> SendAsync(NotificationMessage message, CancellationToken ct = default)
        {
            Sent.Add(message);
            return Task.FromResult(new NotificationSendResult(true, Guid.NewGuid().ToString(), "CapturingTestSender"));
        }
    }

    private sealed class Harness : IDisposable
    {
        public CRMSDbContext Context { get; }
        public NotificationOrchestrator Orchestrator { get; }
        public CapturingEmailSender Sender { get; }
        public RegisterUserHandler RegisterHandler { get; }
        public AuthService Auth { get; }
        public PasswordHasher Hasher { get; }
        public UserRepository UserRepo { get; }

        public Harness()
        {
            var options = new DbContextOptionsBuilder<CRMSDbContext>()
                .UseInMemoryDatabase("EmailWiring_" + Guid.NewGuid())
                .Options;
            Context = new CRMSDbContext(options);

            // Seed roles + the security email templates exactly as production does.
            SeedData.SeedAsync(Context, NullLogger.Instance).GetAwaiter().GetResult();

            var notifRepo = new NotificationRepository(Context);
            var templateRepo = new NotificationTemplateRepository(Context);
            UserRepo = new UserRepository(Context);
            var roleRepo = new RoleRepository(Context);
            var permRepo = new PermissionRepository(Context);
            Hasher = new PasswordHasher();
            Sender = new CapturingEmailSender();

            // Real orchestrator; context doubles as the IUnitOfWork.
            Orchestrator = new NotificationOrchestrator(notifRepo, templateRepo, [Sender], Context);

            RegisterHandler = new RegisterUserHandler(
                UserRepo, roleRepo, permRepo, Hasher, Context, Orchestrator,
                new Mock<ILogger<RegisterUserHandler>>().Object);

            Auth = new AuthService(
                UserRepo, roleRepo, permRepo, new Mock<ITokenService>().Object, Hasher, Context,
                Options.Create(new JwtSettings()), Orchestrator,
                new Mock<ILogger<AuthService>>().Object);
        }

        public void Dispose() => Context.Dispose();
    }

    // ── Account creation ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreatingUser_QueuesWelcomeEmail_WithRenderedTemplate()
    {
        using var h = new Harness();

        var result = await h.RegisterHandler.Handle(new RegisterUserCommand(
            "newuser@boa.ng", "newuser", "Welcome@1234", "New", "User",
            UserType.Staff, null, null, null));

        Assert.True(result.IsSuccess, result.Error);

        var notif = await h.Context.Notifications
            .FirstOrDefaultAsync(n => n.RecipientAddress == "newuser@boa.ng" && n.TemplateCode == "ACCOUNT_CREATED");
        Assert.NotNull(notif);
        Assert.Equal(NotificationChannel.Email, notif!.Channel);
        // Template rendered the variables:
        Assert.Contains("newuser", notif.Body);       // {{Username}}
        Assert.Contains("Welcome@1234", notif.Body);  // {{TempPassword}}
        Assert.False(string.IsNullOrWhiteSpace(notif.Subject));

        // Pipeline delivers it to the (captured) sender.
        await h.Orchestrator.ProcessPendingAsync();
        Assert.Contains(h.Sender.Sent, m => m.RecipientAddress == "newuser@boa.ng");
        _output.WriteLine($"Welcome email queued + sent. Subject: {notif.Subject}");
    }

    // ── Password change ───────────────────────────────────────────────────────

    [Fact]
    public async Task ChangingPassword_QueuesConfirmationEmail()
    {
        using var h = new Harness();
        var userId = await CreateUserAsync(h, "changer@boa.ng", "OldPass123!");

        var result = await h.Auth.ChangePasswordAsync(userId,
            new ChangePasswordRequest("OldPass123!", "NewPass123!"));
        Assert.True(result.IsSuccess, result.Error);

        var notif = await h.Context.Notifications
            .FirstOrDefaultAsync(n => n.RecipientAddress == "changer@boa.ng" && n.TemplateCode == "PASSWORD_CHANGED");
        Assert.NotNull(notif);
        Assert.Contains("changed", notif!.Body, StringComparison.OrdinalIgnoreCase);

        // New password is in effect.
        var user = await h.UserRepo.GetByEmailAsync("changer@boa.ng");
        Assert.True(h.Hasher.VerifyPassword("NewPass123!", user!.PasswordHash));
        _output.WriteLine("Password change confirmation queued ✓");
    }

    // ── Forgot + reset password ───────────────────────────────────────────────

    [Fact]
    public async Task ForgotPassword_ThenReset_WorksEndToEnd()
    {
        using var h = new Harness();
        await CreateUserAsync(h, "forgetful@boa.ng", "OldPass123!");

        // 1. Request reset — queues PASSWORD_RESET email and stores a token hash.
        var req = await h.Auth.RequestPasswordResetAsync("forgetful@boa.ng", "https://crms.test/reset-password");
        Assert.True(req.IsSuccess);

        var user = await h.UserRepo.GetByEmailAsync("forgetful@boa.ng");
        Assert.False(string.IsNullOrEmpty(user!.PasswordResetTokenHash));
        Assert.NotNull(user.PasswordResetTokenExpiresAt);

        var notif = await h.Context.Notifications
            .FirstOrDefaultAsync(n => n.RecipientAddress == "forgetful@boa.ng" && n.TemplateCode == "PASSWORD_RESET");
        Assert.NotNull(notif);
        Assert.Contains("https://crms.test/reset-password", notif!.Body); // {{ResetLink}} rendered

        // 2. Extract the raw token from the emailed link and reset.
        var token = Uri.UnescapeDataString(Regex.Match(notif.Body, @"token=([^&\s""<]+)").Groups[1].Value);
        Assert.False(string.IsNullOrEmpty(token));

        var reset = await h.Auth.ResetPasswordAsync(new ResetPasswordRequest(token, "BrandNew123!"));
        Assert.True(reset.IsSuccess, reset.Error);

        var after = await h.UserRepo.GetByEmailAsync("forgetful@boa.ng");
        Assert.True(h.Hasher.VerifyPassword("BrandNew123!", after!.PasswordHash));
        Assert.Null(after.PasswordResetTokenHash); // single-use: cleared

        // 3. Re-using the same token must fail (single-use enforcement).
        var reused = await h.Auth.ResetPasswordAsync(new ResetPasswordRequest(token, "Another123!"));
        Assert.False(reused.IsSuccess);
        _output.WriteLine("Forgot → reset → single-use enforcement all verified ✓");
    }

    [Fact]
    public async Task ForgotPassword_ForUnknownEmail_ReportsSuccess_ButQueuesNothing()
    {
        using var h = new Harness();

        var req = await h.Auth.RequestPasswordResetAsync("nobody@nowhere.ng", "https://crms.test/reset-password");
        Assert.True(req.IsSuccess); // anti-enumeration: always success

        var any = await h.Context.Notifications.AnyAsync(n => n.TemplateCode == "PASSWORD_RESET");
        Assert.False(any); // ...but no email is queued for a non-existent account
    }

    [Fact]
    public async Task ResetPassword_WithInvalidToken_Fails()
    {
        using var h = new Harness();
        await CreateUserAsync(h, "victim@boa.ng", "OldPass123!");

        var reset = await h.Auth.ResetPasswordAsync(
            new ResetPasswordRequest("not-a-real-token", "Whatever123!"));
        Assert.False(reset.IsSuccess);
    }

    // ── Templates seeded? (directly answers "are templates applied") ──────────

    [Fact]
    public async Task SeedData_AppliesSecurityEmailTemplates()
    {
        using var h = new Harness();

        foreach (var code in new[] { "ACCOUNT_CREATED", "PASSWORD_CHANGED", "PASSWORD_RESET" })
            Assert.True(
                await h.Context.NotificationTemplates.AnyAsync(t => t.Code == code && t.Channel == NotificationChannel.Email),
                $"Template '{code}' should be seeded by SeedData");
    }

    private static async Task<Guid> CreateUserAsync(Harness h, string email, string password)
    {
        var result = await h.RegisterHandler.Handle(new RegisterUserCommand(
            email, email.Split('@')[0], password, "Test", "User",
            UserType.Staff, null, null, null));
        Assert.True(result.IsSuccess, result.Error);
        return result.Data!.Id;
    }
}

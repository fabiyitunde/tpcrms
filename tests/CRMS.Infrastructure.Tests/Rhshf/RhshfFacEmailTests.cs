using CRMS.Application.Notification.Interfaces;
using CRMS.Application.Notification.Services;
using CRMS.Domain.Aggregates.Notification;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using CRMS.Infrastructure.BackgroundServices;
using CRMS.Infrastructure.Events.Handlers;
using CRMS.Infrastructure.ExternalServices.Rhshf;
using CRMS.Infrastructure.Persistence;
using CRMS.Infrastructure.Persistence.Repositories;
using CRMS.Infrastructure.Persistence.Repositories.Rhshf;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace CRMS.Infrastructure.Tests.Rhshf;

/// <summary>Emails to the FAC and the offer expiry reminder (CRMSQA#2), through the real notification
/// queue on an in-memory database — the queued notification is the delivery evidence staff see.</summary>
public class RhshfFacEmailTests
{
    private const decimal Total = 1_000_000m;

    private static ServiceProvider Services(string dbName)
    {
        var services = new ServiceCollection();
        services.AddDbContext<CRMSDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<CRMSDbContext>());
        services.AddScoped<IRhshfCreditProfileRepository, RhshfCreditProfileRepository>();
        services.AddScoped<IRhshfOfferRepository, RhshfOfferRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<INotificationTemplateRepository, NotificationTemplateRepository>();
        services.AddScoped<INotificationService, NotificationOrchestrator>();
        services.AddSingleton<IEnumerable<INotificationSender>>([]);
        services.AddSingleton<IOptions<RhshfSettings>>(Options.Create(new RhshfSettings { PortalUrl = "https://portal.test" }));
        return services.BuildServiceProvider();
    }

    /// <summary>A case awaiting the FAC's acceptance of an offer issued <paramref name="daysAgo"/> days ago.</summary>
    private static async Task<(RhshfCreditProfile Profile, RhshfOffer Offer)> SeedAwaitingAcceptance(IServiceProvider sp, double daysAgo)
    {
        var db = sp.GetRequiredService<CRMSDbContext>();
        db.NotificationTemplates.Add(NotificationTemplate.Create(
            RhshfOfferReminderBackgroundService.TemplateCode, "Reminder", "Reminder", NotificationType.Reminder,
            NotificationChannel.Email, "{{Reference}} lapses on {{Deadline}}", Guid.Empty,
            subject: "Reminder: {{Reference}} lapses on {{Deadline}}").Value);

        var profile = RhshfCreditProfile.Create(
            submissionId: Guid.NewGuid(), programmeCode: "NG2026", programmeName: "FYP-NAG2026",
            sessionCode: "2026-DRY", sessionName: "FYP-YOKRI", facId: Guid.NewGuid(),
            companyName: "Grandbai Limited", rcNumber: "RC1", tin: null,
            boaAccountNumber: "0123456789", contactEmail: "fac@grandbai.test", contactPhone: "+2340000000000",
            state: "Kano", lga: "", totalEopValue: Total, currency: "NGN", farmerCount: 30,
            callbackUrl: "https://portal.test/cb", certifiedByAdmin: null, certifiedAt: null, rawSubmissionPayload: "{}",
            eopLines: null, resolvedBranchId: null, resolvedOfficeId: null).Value;
        foreach (var stage in new[]
        {
            RhshfProfilingStage.CompanyVerification, RhshfProfilingStage.CreditBureauCheck,
            RhshfProfilingStage.EopReview, RhshfProfilingStage.SupportingDocuments, RhshfProfilingStage.ReviewAndSubmit,
        })
            profile.AdvanceStageForTest(stage);
        profile.AppraiseWithFinancials(Guid.NewGuid(), RhshfAppraisalOutcome.Proceed, null);
        profile.ClearLegalForTest();
        profile.ReviewRisk(Guid.NewGuid(), RhshfRiskReviewOutcome.Cleared, null);
        profile.AdvanceToRatification();
        profile.Ratify(Guid.NewGuid(), RhshfRatificationOutcome.Ratified, Total, null, null, []);
        profile.ClearDomainEvents();

        var offer = RhshfOffer.Create(profile.Id, profile.CurrentCycleNumber, "rhshf-offers/x.pdf").Value;
        typeof(RhshfOffer).GetProperty(nameof(RhshfOffer.GeneratedAt))!.SetValue(offer, DateTime.UtcNow.AddDays(-daysAgo));

        db.RhshfCreditProfiles.Add(profile);
        db.RhshfOffers.Add(offer);
        await db.SaveChangesAsync();
        return (profile, offer);
    }

    [Fact]
    public async Task Reminder_IsQueuedOnce_InTheLastTwoDays_FiledUnderTheCase()
    {
        await using var sp = Services("RhshfEmail_" + Guid.NewGuid());
        using var scope = sp.CreateScope();
        var (profile, offer) = await SeedAwaitingAcceptance(scope.ServiceProvider, daysAgo: 5.5);

        var first = await RhshfOfferReminderBackgroundService.SendDueRemindersAsync(scope.ServiceProvider, DateTime.UtcNow);
        var second = await RhshfOfferReminderBackgroundService.SendDueRemindersAsync(scope.ServiceProvider, DateTime.UtcNow);

        Assert.Equal(1, first);
        Assert.Equal(0, second); // the history says this offer was already reminded
        var queued = Assert.Single(await scope.ServiceProvider.GetRequiredService<INotificationRepository>()
            .GetByLoanApplicationNumberAsync(profile.Reference));
        Assert.Equal("fac@grandbai.test", queued.RecipientAddress);
        Assert.Equal(NotificationStatus.Pending, queued.Status); // the queue sends it; the record is the evidence
        Assert.Contains(offer.GeneratedAt.AddDays(7).ToString("d MMMM yyyy"), queued.Subject);
    }

    [Theory]
    [InlineData(1)]   // far from lapsing
    [InlineData(7.5)] // already lapsed — a reminder after the date would be wrong
    public async Task Reminder_IsNotSent_OutsideTheWindow(double daysAgo)
    {
        await using var sp = Services("RhshfEmail_" + Guid.NewGuid());
        using var scope = sp.CreateScope();
        await SeedAwaitingAcceptance(scope.ServiceProvider, daysAgo);

        var sent = await RhshfOfferReminderBackgroundService.SendDueRemindersAsync(scope.ServiceProvider, DateTime.UtcNow);

        Assert.Equal(0, sent);
    }

    [Fact]
    public async Task EmailValues_EscapeTheOfficersNote_AndPointToThePortal()
    {
        await using var sp = Services("RhshfEmail_" + Guid.NewGuid());
        using var scope = sp.CreateScope();
        var (profile, offer) = await SeedAwaitingAcceptance(scope.ServiceProvider, daysAgo: 0);

        var values = RhshfFacEmailVariables.Build(profile, offer, "https://portal.test");

        Assert.Equal("Grandbai Limited", values["RecipientName"]);
        Assert.Equal("1", values["OfferVersion"]);
        Assert.Equal(offer.GeneratedAt.AddDays(7).ToString("d MMMM yyyy"), values["Deadline"]);
        Assert.Contains("https://portal.test", values["PortalInstruction"]);
        Assert.Contains("Continue on CRMS", values["PortalInstruction"]);
    }

    [Fact]
    public void EmailValues_CarryTheOfficersNote_EscapedForTheHtmlEmail()
    {
        // The template engine substitutes values verbatim, so a note with markup in it would otherwise
        // be rendered as markup in the FAC's email.
        var profile = RhshfCreditProfile.Create(
            submissionId: Guid.NewGuid(), programmeCode: "NG2026", programmeName: "FYP-NAG2026",
            sessionCode: "2026-DRY", sessionName: "FYP-YOKRI", facId: Guid.NewGuid(),
            companyName: "A & B Farms", rcNumber: "RC1", tin: null,
            boaAccountNumber: "0123456789", contactEmail: "fac@ab.test", contactPhone: "+2340000000000",
            state: "Kano", lga: "", totalEopValue: Total, currency: "NGN", farmerCount: 30,
            callbackUrl: "https://portal.test/cb", certifiedByAdmin: null, certifiedAt: null, rawSubmissionPayload: "{}",
            eopLines: null, resolvedBranchId: null, resolvedOfficeId: null).Value;
        foreach (var stage in new[]
        {
            RhshfProfilingStage.CompanyVerification, RhshfProfilingStage.CreditBureauCheck,
            RhshfProfilingStage.EopReview, RhshfProfilingStage.SupportingDocuments, RhshfProfilingStage.ReviewAndSubmit,
        })
            profile.AdvanceStageForTest(stage);
        profile.Appraise(Guid.NewGuid(), RhshfAppraisalOutcome.ReturnToFac, "<b>Bank statement</b> missing June & July",
            RhshfProfilingStage.SupportingDocuments);

        var values = RhshfFacEmailVariables.Build(profile, offer: null, portalUrl: null);

        Assert.Equal("<b>Bank statement</b> missing June & July", values["Reason"]);            // plain-text email
        Assert.Equal("&lt;b&gt;Bank statement&lt;/b&gt; missing June &amp; July", values["ReasonHtml"]);
        Assert.Equal("A &amp; B Farms", values["RecipientNameHtml"]);
        Assert.Equal("Supporting Documents", values["Stage"]);
        Assert.DoesNotContain("http", values["PortalInstruction"]); // no address configured: instruction only
    }
}

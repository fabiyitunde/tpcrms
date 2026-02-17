using CRMS.Domain.Aggregates.Notification;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Notification;

public class NotificationConfiguration : IEntityTypeConfiguration<Domain.Aggregates.Notification.Notification>
{
    public void Configure(EntityTypeBuilder<Domain.Aggregates.Notification.Notification> builder)
    {
        builder.ToTable("Notifications");

        builder.HasKey(x => x.Id);

        // Indexes
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.Channel);
        builder.HasIndex(x => x.RecipientUserId);
        builder.HasIndex(x => x.LoanApplicationId);
        builder.HasIndex(x => new { x.Status, x.ScheduledAt });
        builder.HasIndex(x => x.CreatedAt);

        builder.Property(x => x.Type)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Channel)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Priority)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.RecipientName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.RecipientAddress)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.TemplateCode)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Subject)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.Body)
            .IsRequired();

        builder.Property(x => x.BodyHtml);

        builder.Property(x => x.LoanApplicationNumber)
            .HasMaxLength(50);

        builder.Property(x => x.ContextData)
            .HasColumnType("json");

        builder.Property(x => x.FailureReason)
            .HasMaxLength(1000);

        builder.Property(x => x.ExternalMessageId)
            .HasMaxLength(200);

        builder.Property(x => x.ProviderName)
            .HasMaxLength(100);

        builder.Property(x => x.ProviderResponse)
            .HasMaxLength(2000);
    }
}

public class NotificationTemplateConfiguration : IEntityTypeConfiguration<NotificationTemplate>
{
    public void Configure(EntityTypeBuilder<NotificationTemplate> builder)
    {
        builder.ToTable("NotificationTemplates");

        builder.HasKey(x => x.Id);

        // Indexes
        builder.HasIndex(x => new { x.Code, x.Channel, x.Language }).IsUnique();
        builder.HasIndex(x => x.Type);
        builder.HasIndex(x => x.IsActive);

        builder.Property(x => x.Code)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.Property(x => x.Type)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Channel)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Language)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(x => x.Subject)
            .HasMaxLength(500);

        builder.Property(x => x.BodyTemplate)
            .IsRequired();

        builder.Property(x => x.BodyHtmlTemplate);

        builder.Property(x => x.AvailableVariables)
            .HasColumnType("json");
    }
}

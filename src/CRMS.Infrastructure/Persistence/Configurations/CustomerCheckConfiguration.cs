using CRMS.Domain.Aggregates.CustomerCheck;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations;

public class ApiConsumerConfiguration : IEntityTypeConfiguration<ApiConsumer>
{
    public void Configure(EntityTypeBuilder<ApiConsumer> builder)
    {
        builder.ToTable("ApiConsumers");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.Property(x => x.ContactEmail).HasMaxLength(256);
        // SHA-256 hex — 64 characters.
        builder.Property(x => x.KeyHash).IsRequired().HasMaxLength(64);
        builder.Property(x => x.KeyPrefix).IsRequired().HasMaxLength(20);
        builder.Property(x => x.AllowedSections).IsRequired().HasMaxLength(200);
        builder.Property(x => x.CallbackUrl).HasMaxLength(500);
        builder.Property(x => x.AllowedCallbackHosts).IsRequired().HasMaxLength(1000).HasDefaultValue(string.Empty);
        builder.Property(x => x.CallbackSecretCiphertext).HasMaxLength(500);
        builder.Property(x => x.CallbackSecretPrefix).HasMaxLength(30);
        builder.Property(x => x.MaxQueuedJobs).HasDefaultValue(100);
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        builder.HasIndex(x => x.KeyHash).IsUnique();
        builder.HasIndex(x => x.Name).IsUnique();

        builder.Ignore(x => x.DomainEvents);
        builder.Ignore(x => x.AllowedSectionList);
        builder.Ignore(x => x.AllowedCallbackHostList);
    }
}

public class ApiConsumerSetupLinkConfiguration : IEntityTypeConfiguration<ApiConsumerSetupLink>
{
    public void Configure(EntityTypeBuilder<ApiConsumerSetupLink> builder)
    {
        builder.ToTable("ApiConsumerSetupLinks");
        builder.HasKey(x => x.Id);

        // SHA-256 hex — 64 characters.
        builder.Property(x => x.TokenHash).IsRequired().HasMaxLength(64);
        builder.Property(x => x.SentTo).IsRequired().HasMaxLength(256);
        builder.Property(x => x.SentByName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.OpenedFromIp).HasMaxLength(64);
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => new { x.ConsumerId, x.CreatedAt });
    }
}

public class CustomerCheckJobConfiguration : IEntityTypeConfiguration<CustomerCheckJob>
{
    public void Configure(EntityTypeBuilder<CustomerCheckJob> builder)
    {
        builder.ToTable("CustomerCheckJobs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ConsumerName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.RequestId).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(20);
        builder.Property(x => x.BvnHash).IsRequired().HasMaxLength(64);
        builder.Property(x => x.BvnLast4).IsRequired().HasMaxLength(4);
        builder.Property(x => x.SectionsRequested).IsRequired().HasMaxLength(200);
        builder.Property(x => x.RequestCiphertext).IsRequired().HasColumnType("longtext");
        builder.Property(x => x.CallbackUrl).HasMaxLength(500);
        builder.Property(x => x.CallbackStatus).IsRequired().HasMaxLength(20);
        builder.Property(x => x.EventId).IsRequired().HasMaxLength(50);
        builder.Property(x => x.ResultCiphertext).HasColumnType("longtext");
        builder.Property(x => x.SectionOutcomes).HasMaxLength(500);
        builder.Property(x => x.BureauUsed).HasMaxLength(50);
        builder.Property(x => x.ErrorCode).HasMaxLength(50);
        builder.Property(x => x.ErrorMessage).HasMaxLength(1000);
        builder.Property(x => x.LastCallbackError).HasMaxLength(500);
        builder.Property(x => x.AdvisoryScore).HasPrecision(5, 2);
        builder.Property(x => x.AdvisoryRating).HasMaxLength(50);
        builder.Property(x => x.AdvisoryRecommendation).HasMaxLength(50);
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        // Idempotency: the consumer's job for a request id. The claim query: runnable jobs, oldest
        // first. The callback query: pending callbacks that are due. History: a consumer's jobs by
        // date, and by person.
        builder.HasIndex(x => new { x.ConsumerId, x.RequestId });
        builder.HasIndex(x => new { x.Status, x.CreatedAt });
        builder.HasIndex(x => new { x.CallbackStatus, x.NextCallbackAt });
        builder.HasIndex(x => new { x.ConsumerId, x.CreatedAt });
        builder.HasIndex(x => x.BvnHash);

        builder.Ignore(x => x.DomainEvents);
        builder.Ignore(x => x.SectionList);
        builder.Ignore(x => x.IsOpen);
    }
}

public class CustomerCheckLogConfiguration : IEntityTypeConfiguration<CustomerCheckLog>
{
    public void Configure(EntityTypeBuilder<CustomerCheckLog> builder)
    {
        builder.ToTable("CustomerCheckLogs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RequestId).IsRequired().HasMaxLength(100);
        builder.Property(x => x.ConsumerName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.BvnHash).IsRequired().HasMaxLength(64);
        builder.Property(x => x.NubanHashes).HasMaxLength(1000);
        builder.Property(x => x.SectionsRequested).IsRequired().HasMaxLength(200);
        builder.Property(x => x.SectionOutcomes).IsRequired().HasMaxLength(500);
        builder.Property(x => x.BureauUsed).HasMaxLength(50);
        builder.Property(x => x.Error).HasMaxLength(1000);
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        builder.HasIndex(x => x.ConsumerId);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.BvnHash);
        builder.HasIndex(x => x.JobId);

        builder.Ignore(x => x.DomainEvents);
    }
}

public class CustomerCheckPriceConfiguration : IEntityTypeConfiguration<CustomerCheckPrice>
{
    public void Configure(EntityTypeBuilder<CustomerCheckPrice> builder)
    {
        builder.ToTable("CustomerCheckPrices");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Item).IsRequired().HasMaxLength(50);
        builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        builder.HasIndex(x => x.Item).IsUnique();

        builder.Ignore(x => x.DomainEvents);
    }
}

public class CustomerBureauReportConfiguration : IEntityTypeConfiguration<CustomerBureauReport>
{
    public void Configure(EntityTypeBuilder<CustomerBureauReport> builder)
    {
        builder.ToTable("CustomerBureauReports");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.BvnHash).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Bureau).IsRequired().HasMaxLength(50);
        builder.Property(x => x.ReportJson).IsRequired().HasColumnType("longtext");
        builder.Property(x => x.ScoreJson).HasColumnType("longtext");
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        // The reuse lookup: newest report for this person from this bureau.
        builder.HasIndex(x => new { x.BvnHash, x.Bureau, x.FetchedAt });

        builder.Ignore(x => x.DomainEvents);
    }
}

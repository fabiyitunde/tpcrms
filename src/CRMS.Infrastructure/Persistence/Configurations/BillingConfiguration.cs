using CRMS.Domain.Aggregates.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations;

public class BillingUsageConfiguration : IEntityTypeConfiguration<BillingUsage>
{
    public void Configure(EntityTypeBuilder<BillingUsage> builder)
    {
        builder.ToTable("BillingUsages");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Item).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Category).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Flow).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.CaseReference).HasMaxLength(100);
        builder.Property(x => x.SubjectType).HasMaxLength(50);
        builder.Property(x => x.SubjectHint).HasMaxLength(30);
        builder.Property(x => x.Detail).HasMaxLength(BillingUsage.DetailMaxLength);
        builder.Property(x => x.Host).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Source).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.SourceKey).HasMaxLength(150);
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        // Dashboard: counts by month, item and flow. Drill-downs: by case, by reference, by time.
        // Idempotency: derived and reconstructed rows carry a unique source key.
        builder.HasIndex(x => new { x.Month, x.Item, x.Flow });
        builder.HasIndex(x => x.CaseId);
        builder.HasIndex(x => x.CaseReference);
        builder.HasIndex(x => x.OccurredAt);
        builder.HasIndex(x => x.SourceKey).IsUnique();

        builder.Ignore(x => x.DomainEvents);
    }
}

public class BillingRateConfiguration : IEntityTypeConfiguration<BillingRate>
{
    public void Configure(EntityTypeBuilder<BillingRate> builder)
    {
        builder.ToTable("BillingRates");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Item).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Flow).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.UnitCost).HasPrecision(18, 2);
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        builder.Property(x => x.Note).HasMaxLength(300);
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        // Not unique: MySQL treats every NULL flow as distinct, so the handler looks the row up first.
        builder.HasIndex(x => new { x.Item, x.Flow, x.EffectiveFrom });

        builder.Ignore(x => x.DomainEvents);
    }
}

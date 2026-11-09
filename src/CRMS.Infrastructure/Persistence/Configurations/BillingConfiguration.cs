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
        builder.HasIndex(x => x.StatementId);

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

public class BillingStatementConfiguration : IEntityTypeConfiguration<BillingStatement>
{
    public void Configure(EntityTypeBuilder<BillingStatement> builder)
    {
        builder.ToTable("BillingStatements");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Number).IsRequired().HasMaxLength(30);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        builder.Property(x => x.Subtotal).HasPrecision(18, 2);
        builder.Property(x => x.VatRatePercent).HasPrecision(5, 2);
        builder.Property(x => x.VatAmount).HasPrecision(18, 2);
        builder.Property(x => x.Total).HasPrecision(18, 2);
        builder.Property(x => x.CostTotal).HasPrecision(18, 2);
        builder.Property(x => x.IssuedByName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.Property(x => x.PaymentReference).HasMaxLength(100);
        builder.Property(x => x.PaymentNote).HasMaxLength(500);
        builder.Property(x => x.VoidReason).HasMaxLength(500);
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        builder.HasIndex(x => x.Number).IsUnique();
        builder.HasIndex(x => new { x.Month, x.Status });

        builder.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.StatementId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(x => x.DomainEvents);
    }
}

public class BillingStatementLineConfiguration : IEntityTypeConfiguration<BillingStatementLine>
{
    public void Configure(EntityTypeBuilder<BillingStatementLine> builder)
    {
        builder.ToTable("BillingStatementLines");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Item).IsRequired().HasMaxLength(50);
        builder.Property(x => x.ItemName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Category).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Flow).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.UnitCost).HasPrecision(18, 2);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        builder.HasIndex(x => x.StatementId);
    }
}

using CRMS.Domain.Aggregates.Rhshf;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Rhshf;

public class RhshfRoutingConfigConfiguration : IEntityTypeConfiguration<RhshfRoutingConfig>
{
    public void Configure(EntityTypeBuilder<RhshfRoutingConfig> builder)
    {
        builder.ToTable("RhshfRoutingConfigs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Tier)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.MinEopValue)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.MaxEopValue)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.Priority)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        builder.HasIndex(x => x.IsActive);
        builder.HasIndex(x => new { x.IsActive, x.Priority });
    }
}

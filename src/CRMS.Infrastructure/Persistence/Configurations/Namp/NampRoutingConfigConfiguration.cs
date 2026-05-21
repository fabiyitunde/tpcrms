using CRMS.Domain.Aggregates.Namp;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Namp;

public class NampRoutingConfigConfiguration : IEntityTypeConfiguration<NampRoutingConfig>
{
    public void Configure(EntityTypeBuilder<NampRoutingConfig> builder)
    {
        builder.ToTable("NampRoutingConfigs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ApplicantCategory)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.CommitteeTier)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.MinEquipmentValue)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.MaxEquipmentValue)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.Priority)
            .IsRequired()
            .HasDefaultValue(0)
            .ValueGeneratedNever();

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.CreatedBy)
            .HasMaxLength(100);

        builder.Property(x => x.ModifiedBy)
            .HasMaxLength(100);

        // Indexes
        builder.HasIndex(x => x.ApplicantCategory);
        builder.HasIndex(x => x.IsActive);
        builder.HasIndex(x => new { x.ApplicantCategory, x.IsActive, x.Priority });
    }
}

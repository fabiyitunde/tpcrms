using CRMS.Domain.Aggregates.Namp;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Namp;

public class NampViabilityScoreConfigConfiguration : IEntityTypeConfiguration<NampViabilityScoreConfig>
{
    public void Configure(EntityTypeBuilder<NampViabilityScoreConfig> builder)
    {
        builder.ToTable("NampViabilityScoreConfigs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ViabilityRating)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.Score)
            .IsRequired()
            .HasColumnType("decimal(6,2)");

        builder.Property(x => x.CategoryWeight)
            .IsRequired()
            .HasColumnType("decimal(8,2)");

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.CreatedBy)
            .HasMaxLength(100);

        builder.Property(x => x.ModifiedBy)
            .HasMaxLength(100);

        builder.HasIndex(x => x.ViabilityRating).IsUnique();
        builder.HasIndex(x => x.IsActive);
    }
}

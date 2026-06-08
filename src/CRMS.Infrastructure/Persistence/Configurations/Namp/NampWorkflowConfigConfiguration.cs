using CRMS.Domain.Aggregates.Namp;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Namp;

public class NampWorkflowConfigConfiguration : IEntityTypeConfiguration<NampWorkflowConfig>
{
    public void Configure(EntityTypeBuilder<NampWorkflowConfig> builder)
    {
        builder.ToTable("NampWorkflowConfigs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(x => x.DisplayName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.Property(x => x.AssignedRole)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.SlaHours)
            .IsRequired();

        builder.Property(x => x.SortOrder)
            .IsRequired();

        builder.Property(x => x.CreatedBy)
            .HasMaxLength(100);

        builder.Property(x => x.ModifiedBy)
            .HasMaxLength(100);

        builder.HasIndex(x => x.Status).IsUnique();
        builder.HasIndex(x => x.SortOrder);
    }
}

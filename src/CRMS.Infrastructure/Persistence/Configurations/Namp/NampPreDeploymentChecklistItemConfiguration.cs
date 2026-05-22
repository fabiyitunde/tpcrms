using CRMS.Domain.Aggregates.Namp;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Namp;

public class NampPreDeploymentChecklistItemConfiguration
    : IEntityTypeConfiguration<NampPreDeploymentChecklistItem>
{
    public void Configure(EntityTypeBuilder<NampPreDeploymentChecklistItem> builder)
    {
        builder.ToTable("NampPreDeploymentChecklistItems");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.NampApplicationId).IsRequired();
        builder.Property(x => x.TemplateItemId).IsRequired();

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasColumnType("text");

        builder.Property(x => x.DocumentCategory)
            .HasConversion<int?>();

        builder.Property(x => x.Notes)
            .HasColumnType("text");

        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        builder.HasIndex(x => x.NampApplicationId);
        builder.HasIndex(x => new { x.NampApplicationId, x.TemplateItemId }).IsUnique();
    }
}

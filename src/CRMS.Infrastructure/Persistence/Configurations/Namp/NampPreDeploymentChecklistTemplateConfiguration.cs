using CRMS.Domain.Aggregates.Namp;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Namp;

public class NampPreDeploymentChecklistTemplateConfiguration
    : IEntityTypeConfiguration<NampPreDeploymentChecklistTemplate>
{
    public void Configure(EntityTypeBuilder<NampPreDeploymentChecklistTemplate> builder)
    {
        builder.ToTable("NampPreDeploymentChecklistTemplates");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasColumnType("text");

        builder.Property(x => x.DocumentCategory)
            .HasConversion<int?>();

        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        builder.HasIndex(x => x.SortOrder);
        builder.HasIndex(x => x.IsActive);
    }
}

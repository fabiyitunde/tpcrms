using CRMS.Domain.Aggregates.Rhshf;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Rhshf;

public class RhshfPreDeploymentChecklistTemplateConfiguration : IEntityTypeConfiguration<RhshfPreDeploymentChecklistTemplate>
{
    public void Configure(EntityTypeBuilder<RhshfPreDeploymentChecklistTemplate> builder)
    {
        builder.ToTable("RhshfPreDeploymentChecklistTemplates");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Description).HasColumnType("longtext");
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        builder.HasIndex(x => x.IsActive);
    }
}

public class RhshfPreDeploymentChecklistItemConfiguration : IEntityTypeConfiguration<RhshfPreDeploymentChecklistItem>
{
    public void Configure(EntityTypeBuilder<RhshfPreDeploymentChecklistItem> builder)
    {
        builder.ToTable("RhshfPreDeploymentChecklistItems");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Description).HasColumnType("longtext");
        builder.Property(x => x.Notes).HasColumnType("longtext");
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        builder.HasIndex(x => new { x.RhshfCreditProfileId, x.CycleNumber });
    }
}

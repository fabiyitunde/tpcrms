using CRMS.Domain.Aggregates.Namp;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Namp;

public class NampWorkflowInstanceConfiguration : IEntityTypeConfiguration<NampWorkflowInstance>
{
    public void Configure(EntityTypeBuilder<NampWorkflowInstance> builder)
    {
        builder.ToTable("NampWorkflowInstances");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.NampApplicationId)
            .IsRequired();

        builder.Property(x => x.CurrentStatus)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.FinalStatus)
            .HasConversion<int?>();

        builder.Property(x => x.CurrentStageName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.AssignedRole)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.CreatedBy)
            .HasMaxLength(100);

        builder.Property(x => x.ModifiedBy)
            .HasMaxLength(100);

        builder.HasIndex(x => x.NampApplicationId).IsUnique();
        builder.HasIndex(x => x.AssignedRole);
        builder.HasIndex(x => x.IsSLABreached);
        builder.HasIndex(x => new { x.IsCompleted, x.AssignedRole });
    }
}

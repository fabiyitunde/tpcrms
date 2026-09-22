using CRMS.Domain.Aggregates.Rhshf;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Rhshf;

public class RhshfDocumentRequirementConfiguration : IEntityTypeConfiguration<RhshfDocumentRequirement>
{
    public void Configure(EntityTypeBuilder<RhshfDocumentRequirement> builder)
    {
        builder.ToTable("RhshfDocumentRequirements");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Category).IsRequired().HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Description).HasColumnType("longtext");

        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        builder.HasIndex(x => x.IsActive);
        // One rule per category — two mandatory rules for the same category would be ambiguous.
        builder.HasIndex(x => x.Category).IsUnique();
    }
}

public class RhshfStageConfirmationConfiguration : IEntityTypeConfiguration<RhshfStageConfirmation>
{
    public void Configure(EntityTypeBuilder<RhshfStageConfirmation> builder)
    {
        builder.ToTable("RhshfStageConfirmations");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Stage).IsRequired().HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.IpAddress).HasMaxLength(45);
        builder.Property(x => x.UserAgent).HasMaxLength(400);

        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        builder.HasIndex(x => new { x.RhshfCreditProfileId, x.CycleNumber });
    }
}

public class RhshfStatusHistoryConfiguration : IEntityTypeConfiguration<RhshfStatusHistory>
{
    public void Configure(EntityTypeBuilder<RhshfStatusHistory> builder)
    {
        builder.ToTable("RhshfStatusHistory");
        builder.HasKey(x => x.Id);

        // Stored by name, like every other enum in this schema — an ordinal would silently
        // reinterpret historical rows the moment a value is inserted into the middle of an enum,
        // which for an audit trail is the one thing that must never happen.
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.InternalStage).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.ProfilingStage).HasConversion<string>().HasMaxLength(40);

        builder.Property(x => x.Action).IsRequired().HasMaxLength(200);
        builder.Property(x => x.ActorLabel).HasMaxLength(50);
        builder.Property(x => x.Note).HasMaxLength(2000);

        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        builder.HasIndex(x => new { x.RhshfCreditProfileId, x.ChangedAt });
    }
}

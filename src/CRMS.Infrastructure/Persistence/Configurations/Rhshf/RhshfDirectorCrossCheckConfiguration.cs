using CRMS.Domain.Aggregates.Rhshf;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Rhshf;

/// <summary>One saved cross-check snapshot per case (unique on the profile id). SnapshotJson is a
/// serialized read-model, so it is stored as free text (longtext).</summary>
public class RhshfDirectorCrossCheckConfiguration : IEntityTypeConfiguration<RhshfDirectorCrossCheck>
{
    public void Configure(EntityTypeBuilder<RhshfDirectorCrossCheck> builder)
    {
        builder.ToTable("RhshfDirectorCrossChecks");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SnapshotJson).IsRequired().HasColumnType("longtext");
        builder.HasIndex(x => x.RhshfCreditProfileId).IsUnique();
    }
}

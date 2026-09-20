using CRMS.Domain.Aggregates.Rhshf;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Rhshf;

/// <summary>Own aggregate, own table — see RhshfEligibilityCheck's own doc comment.</summary>
public class RhshfEligibilityCheckConfiguration : IEntityTypeConfiguration<RhshfEligibilityCheck>
{
    public void Configure(EntityTypeBuilder<RhshfEligibilityCheck> builder)
    {
        builder.ToTable("RhshfEligibilityChecks");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Criterion).IsRequired().HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.Notes).HasColumnType("longtext");

        builder.HasIndex(x => new { x.RhshfCreditProfileId, x.CycleNumber });
    }
}

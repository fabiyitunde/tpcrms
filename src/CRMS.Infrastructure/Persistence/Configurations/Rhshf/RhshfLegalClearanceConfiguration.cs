using CRMS.Domain.Aggregates.Rhshf;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Rhshf;

/// <summary>Own aggregate, own table — see RhshfLegalClearance's own doc comment. No unique index on
/// (profile, cycle): a case can accumulate more than one clearance attempt per cycle if Ratification
/// repeats after a Returned outcome.</summary>
public class RhshfLegalClearanceConfiguration : IEntityTypeConfiguration<RhshfLegalClearance>
{
    public void Configure(EntityTypeBuilder<RhshfLegalClearance> builder)
    {
        builder.ToTable("RhshfLegalClearances");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Outcome).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Comments).HasColumnType("longtext");

        builder.HasIndex(x => new { x.RhshfCreditProfileId, x.CycleNumber });
    }
}

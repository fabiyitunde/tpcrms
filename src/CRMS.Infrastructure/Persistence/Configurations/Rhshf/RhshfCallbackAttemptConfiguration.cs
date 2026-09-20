using CRMS.Domain.Aggregates.Rhshf;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Rhshf;

/// <summary>Own aggregate, own table — see RhshfCallbackAttempt's own doc comment for why this
/// deviates from the plan's "child entity" phrasing.</summary>
public class RhshfCallbackAttemptConfiguration : IEntityTypeConfiguration<RhshfCallbackAttempt>
{
    public void Configure(EntityTypeBuilder<RhshfCallbackAttempt> builder)
    {
        builder.ToTable("RhshfCallbackAttempts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.EventId).IsRequired().HasMaxLength(64);
        builder.Property(x => x.EventType).IsRequired().HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(x => x.EventId);
        builder.HasIndex(x => new { x.Succeeded, x.NextRetryAt });
    }
}

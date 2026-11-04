using CRMS.Domain.Aggregates.Rhshf;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Rhshf;

public class RhshfReasonCodeConfiguration : IEntityTypeConfiguration<RhshfReasonCode>
{
    public void Configure(EntityTypeBuilder<RhshfReasonCode> builder)
    {
        builder.ToTable("RhshfReasonCodes");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Type).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Label).IsRequired().HasMaxLength(200);

        // The portal stores and matches on the code, so it is unique across both types.
        builder.HasIndex(x => x.Code).IsUnique();
    }
}

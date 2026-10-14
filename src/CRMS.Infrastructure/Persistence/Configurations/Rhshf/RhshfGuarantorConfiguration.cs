using CRMS.Domain.Aggregates.Rhshf;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Rhshf;

/// <summary>Own aggregate, own table — see RhshfGuarantor's own doc comment.</summary>
public class RhshfGuarantorConfiguration : IEntityTypeConfiguration<RhshfGuarantor>
{
    public void Configure(EntityTypeBuilder<RhshfGuarantor> builder)
    {
        builder.ToTable("RhshfGuarantors");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.GuarantorType).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.FullName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Bvn).HasMaxLength(11);
        builder.Property(x => x.RcNumber).HasMaxLength(50);
        // The category goes in its own column, by name. Converting the original free-text column to a
        // string-persisted enum would throw on reading any row holding something that is not an enum
        // member — and every pre-existing row holds whatever staff typed.
        builder.Property(x => x.RelationshipCategory).HasConversion<string>().HasMaxLength(30);

        // The original Relationship column, now the Other specification. Mapped explicitly so the
        // rename stays a rename: the data (including the backfilled historical text) stays put.
        builder.Property(x => x.RelationshipOther).HasColumnName("Relationship").HasMaxLength(100);
        builder.Ignore(x => x.RelationshipDisplay);
        builder.Property(x => x.PhoneNumber).HasMaxLength(30);
        builder.Property(x => x.Email).HasMaxLength(200);
        builder.Property(x => x.Address).HasMaxLength(500);
        builder.Property(x => x.GuaranteeAmount).HasColumnType("decimal(18,2)");
        builder.Property(x => x.Notes).HasColumnType("longtext");

        builder.HasIndex(x => x.RhshfCreditProfileId);
    }
}

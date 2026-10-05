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
        builder.Property(x => x.Relationship).HasMaxLength(100);
        builder.Property(x => x.PhoneNumber).HasMaxLength(30);
        builder.Property(x => x.Email).HasMaxLength(200);
        builder.Property(x => x.Address).HasMaxLength(500);
        builder.Property(x => x.GuaranteeAmount).HasColumnType("decimal(18,2)");
        builder.Property(x => x.Notes).HasColumnType("longtext");

        builder.HasIndex(x => x.RhshfCreditProfileId);
    }
}

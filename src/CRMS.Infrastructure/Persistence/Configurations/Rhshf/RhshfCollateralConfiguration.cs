using CRMS.Domain.Aggregates.Rhshf;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Rhshf;

/// <summary>Own aggregate, own table — see RhshfCollateral's own doc comment.</summary>
public class RhshfCollateralConfiguration : IEntityTypeConfiguration<RhshfCollateral>
{
    public void Configure(EntityTypeBuilder<RhshfCollateral> builder)
    {
        builder.ToTable("RhshfCollaterals");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ReferenceNumber).HasMaxLength(100);
        builder.Property(x => x.Notes).HasColumnType("longtext");
        builder.Property(x => x.GuarantorBankName).HasMaxLength(200);
        builder.Property(x => x.GuaranteeAmount).HasColumnType("decimal(18,2)");
        builder.Property(x => x.CrgCoveragePercentage).HasColumnType("decimal(5,2)");
        builder.Property(x => x.PropertyDescription).HasColumnType("longtext");
        builder.Property(x => x.PropertyValue).HasColumnType("decimal(18,2)");
        builder.Property(x => x.TitleReferenceNumber).HasMaxLength(100);
        builder.Property(x => x.RegistrationAuthority).HasMaxLength(200);
        builder.Property(x => x.PerfectionStatus).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(x => new { x.RhshfCreditProfileId, x.CycleNumber });

        builder.HasMany(x => x.Documents)
            .WithOne()
            .HasForeignKey(x => x.RhshfCollateralId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class RhshfCollateralDocumentConfiguration : IEntityTypeConfiguration<RhshfCollateralDocument>
{
    public void Configure(EntityTypeBuilder<RhshfCollateralDocument> builder)
    {
        builder.ToTable("RhshfCollateralDocuments");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.FileName).IsRequired().HasMaxLength(255);
        builder.Property(x => x.ContentType).IsRequired().HasMaxLength(100);
        builder.Property(x => x.StoragePath).IsRequired().HasMaxLength(500);
    }
}

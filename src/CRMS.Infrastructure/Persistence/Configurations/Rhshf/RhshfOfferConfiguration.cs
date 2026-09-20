using CRMS.Domain.Aggregates.Rhshf;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Rhshf;

/// <summary>Own aggregate, own table — see RhshfOffer's own doc comment.</summary>
public class RhshfOfferConfiguration : IEntityTypeConfiguration<RhshfOffer>
{
    public void Configure(EntityTypeBuilder<RhshfOffer> builder)
    {
        builder.ToTable("RhshfOffers");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.OfferDocumentPath).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.FacResponseNotes).HasColumnType("longtext");

        // Not unique (as of Phase 8): a Legal Clearance "Returned" outcome routes back to
        // Ratification within the SAME cycle, and a second Ratified there generates a second offer
        // row for that cycle — the repository resolves "the" offer for a cycle as the most recent.
        builder.HasIndex(x => new { x.RhshfCreditProfileId, x.CycleNumber });

        builder.HasMany(x => x.Documents)
            .WithOne()
            .HasForeignKey(x => x.RhshfOfferId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class RhshfOfferDocumentConfiguration : IEntityTypeConfiguration<RhshfOfferDocument>
{
    public void Configure(EntityTypeBuilder<RhshfOfferDocument> builder)
    {
        builder.ToTable("RhshfOfferDocuments");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.FileName).IsRequired().HasMaxLength(255);
        builder.Property(x => x.ContentType).IsRequired().HasMaxLength(100);
        builder.Property(x => x.StoragePath).IsRequired().HasMaxLength(500);
    }
}

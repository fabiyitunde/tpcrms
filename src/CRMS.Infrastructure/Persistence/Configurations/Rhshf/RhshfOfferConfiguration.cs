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

        builder.HasIndex(x => new { x.RhshfCreditProfileId, x.CycleNumber }).IsUnique();
    }
}

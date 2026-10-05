using CRMS.Domain.Aggregates.Rhshf;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Rhshf;

/// <summary>Own aggregate, own table — see RhshfComment's own doc comment.</summary>
public class RhshfCommentConfiguration : IEntityTypeConfiguration<RhshfComment>
{
    public void Configure(EntityTypeBuilder<RhshfComment> builder)
    {
        builder.ToTable("RhshfComments");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Content).IsRequired().HasMaxLength(4000);
        builder.HasIndex(x => x.RhshfCreditProfileId);
    }
}

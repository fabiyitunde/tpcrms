using CRMS.Domain.Aggregates.Namp;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Namp;

public class NampDocumentTemplateConfiguration : IEntityTypeConfiguration<NampDocumentTemplate>
{
    public void Configure(EntityTypeBuilder<NampDocumentTemplate> builder)
    {
        builder.ToTable("NampDocumentTemplates");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.DocumentType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.BodyContent)
            .IsRequired()
            .HasColumnType("longtext");

        builder.Property(x => x.ConditionsContent)
            .HasColumnType("longtext");

        builder.HasIndex(x => x.DocumentType).IsUnique();
    }
}

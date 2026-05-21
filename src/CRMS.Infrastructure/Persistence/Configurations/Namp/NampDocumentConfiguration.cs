using CRMS.Domain.Aggregates.Namp;
using CRMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Namp;

public class NampDocumentConfiguration : IEntityTypeConfiguration<NampDocument>
{
    public void Configure(EntityTypeBuilder<NampDocument> builder)
    {
        builder.ToTable("NampDocuments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.NampApplicationId)
            .IsRequired();

        builder.Property(x => x.Stage)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.Category)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(NampDocumentCategory.General);

        builder.Property(x => x.FileName)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(x => x.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.FileSize)
            .IsRequired();

        builder.Property(x => x.StoragePath)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.Property(x => x.CreatedBy)
            .HasMaxLength(100);

        builder.Property(x => x.ModifiedBy)
            .HasMaxLength(100);

        builder.HasIndex(x => x.NampApplicationId);
        builder.HasIndex(x => new { x.NampApplicationId, x.Stage });
    }
}

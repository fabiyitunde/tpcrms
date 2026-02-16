using CRMS.Domain.Aggregates.ProductCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations;

public class DocumentRequirementConfiguration : IEntityTypeConfiguration<DocumentRequirement>
{
    public void Configure(EntityTypeBuilder<DocumentRequirement> builder)
    {
        builder.ToTable("DocumentRequirements");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.LoanProductId)
            .IsRequired();

        builder.Property(x => x.DocumentType)
            .IsRequired();

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.IsMandatory)
            .IsRequired();

        builder.Property(x => x.MaxFileSizeMB);

        builder.Property(x => x.AllowedExtensions)
            .HasMaxLength(100);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.CreatedBy)
            .IsRequired()
            .HasMaxLength(100);
    }
}

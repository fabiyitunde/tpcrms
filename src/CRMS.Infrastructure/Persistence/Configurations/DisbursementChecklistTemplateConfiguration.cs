using CRMS.Domain.Aggregates.ProductCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations;

public class DisbursementChecklistTemplateConfiguration : IEntityTypeConfiguration<DisbursementChecklistTemplate>
{
    public void Configure(EntityTypeBuilder<DisbursementChecklistTemplate> builder)
    {
        builder.ToTable("DisbursementChecklistTemplates");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.LoanProductId)
            .IsRequired();

        builder.HasIndex(x => x.LoanProductId);

        builder.Property(x => x.ItemName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.Property(x => x.ConditionType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.CreatedBy)
            .HasMaxLength(200);

        builder.Property(x => x.ModifiedBy)
            .HasMaxLength(200);
    }
}

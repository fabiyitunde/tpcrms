using CRMS.Domain.Aggregates.LoanApplication;
using CRMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.LoanApplication;

public class DisbursementChecklistItemConfiguration : IEntityTypeConfiguration<DisbursementChecklistItem>
{
    public void Configure(EntityTypeBuilder<DisbursementChecklistItem> builder)
    {
        builder.ToTable("DisbursementChecklistItems");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.LoanApplicationId)
            .IsRequired();

        builder.HasIndex(x => x.LoanApplicationId);

        builder.Property(x => x.TemplateItemId)
            .IsRequired();

        builder.Property(x => x.ItemName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.Property(x => x.ConditionType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.HasIndex(x => new { x.LoanApplicationId, x.Status });

        builder.Property(x => x.LegalReturnReason)
            .HasMaxLength(1000);

        builder.Property(x => x.WaiverReason)
            .HasMaxLength(1000);

        builder.Property(x => x.WaiverRejectionReason)
            .HasMaxLength(1000);

        builder.Property(x => x.ExtensionReason)
            .HasMaxLength(1000);

        builder.Property(x => x.CreatedBy)
            .HasMaxLength(200);

        builder.Property(x => x.ModifiedBy)
            .HasMaxLength(200);
    }
}

using CRMS.Domain.Aggregates.ProductCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations;

public class EligibilityRuleConfiguration : IEntityTypeConfiguration<EligibilityRule>
{
    public void Configure(EntityTypeBuilder<EligibilityRule> builder)
    {
        builder.ToTable("EligibilityRules");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.LoanProductId)
            .IsRequired();

        builder.Property(x => x.RuleType)
            .IsRequired();

        builder.Property(x => x.FieldName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Operator)
            .IsRequired();

        builder.Property(x => x.Value)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.IsHardRule)
            .IsRequired();

        builder.Property(x => x.FailureMessage)
            .HasMaxLength(500);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.CreatedBy)
            .IsRequired()
            .HasMaxLength(100);
    }
}

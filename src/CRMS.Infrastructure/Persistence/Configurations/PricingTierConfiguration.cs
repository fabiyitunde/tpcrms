using CRMS.Domain.Aggregates.ProductCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations;

public class PricingTierConfiguration : IEntityTypeConfiguration<PricingTier>
{
    public void Configure(EntityTypeBuilder<PricingTier> builder)
    {
        builder.ToTable("PricingTiers");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.LoanProductId)
            .IsRequired();

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.InterestRatePerAnnum)
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(x => x.RateType)
            .IsRequired();

        builder.Property(x => x.ProcessingFeePercent)
            .HasPrecision(5, 2);

        builder.OwnsOne(x => x.ProcessingFeeFixed, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("ProcessingFeeFixed")
                .HasPrecision(18, 2);

            money.Property(m => m.Currency)
                .HasColumnName("ProcessingFeeCurrency")
                .HasMaxLength(3);
        });

        builder.Property(x => x.MinCreditScore);
        builder.Property(x => x.MaxCreditScore);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.CreatedBy)
            .IsRequired()
            .HasMaxLength(100);
    }
}

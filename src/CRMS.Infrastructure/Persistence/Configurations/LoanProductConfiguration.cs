using CRMS.Domain.Aggregates.ProductCatalog;
using CRMS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations;

public class LoanProductConfiguration : IEntityTypeConfiguration<LoanProduct>
{
    public void Configure(EntityTypeBuilder<LoanProduct> builder)
    {
        builder.ToTable("LoanProducts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(x => x.Code)
            .IsUnique();

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasMaxLength(1000);

        builder.Property(x => x.Type)
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.MinTenorMonths)
            .IsRequired();

        builder.Property(x => x.MaxTenorMonths)
            .IsRequired();

        builder.OwnsOne(x => x.MinAmount, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("MinAmount")
                .HasPrecision(18, 2)
                .IsRequired();

            money.Property(m => m.Currency)
                .HasColumnName("MinAmountCurrency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.OwnsOne(x => x.MaxAmount, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("MaxAmount")
                .HasPrecision(18, 2)
                .IsRequired();

            money.Property(m => m.Currency)
                .HasColumnName("MaxAmountCurrency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.CreatedBy)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.ModifiedAt);

        builder.Property(x => x.ModifiedBy)
            .HasMaxLength(100);

        builder.HasMany(x => x.PricingTiers)
            .WithOne()
            .HasForeignKey(x => x.LoanProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.EligibilityRules)
            .WithOne()
            .HasForeignKey(x => x.LoanProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.DocumentRequirements)
            .WithOne()
            .HasForeignKey(x => x.LoanProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.PricingTiers).AutoInclude();
        builder.Navigation(x => x.EligibilityRules).AutoInclude();
        builder.Navigation(x => x.DocumentRequirements).AutoInclude();
    }
}

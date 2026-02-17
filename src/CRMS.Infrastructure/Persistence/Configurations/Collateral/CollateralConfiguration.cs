using CRMS.Domain.Aggregates.Collateral;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Collateral;

public class CollateralConfiguration : IEntityTypeConfiguration<Domain.Aggregates.Collateral.Collateral>
{
    public void Configure(EntityTypeBuilder<Domain.Aggregates.Collateral.Collateral> builder)
    {
        builder.ToTable("Collaterals");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CollateralReference)
            .HasMaxLength(30)
            .IsRequired();

        builder.HasIndex(x => x.CollateralReference).IsUnique();
        builder.HasIndex(x => x.LoanApplicationId);

        builder.Property(x => x.Type)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(x => x.Status);

        builder.Property(x => x.PerfectionStatus)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.Description)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.AssetIdentifier)
            .HasMaxLength(100);

        builder.Property(x => x.Location)
            .HasMaxLength(500);

        builder.Property(x => x.OwnerName)
            .HasMaxLength(200);

        builder.Property(x => x.OwnershipType)
            .HasMaxLength(50);

        // Value Objects - owned
        builder.OwnsOne(x => x.MarketValue, mv =>
        {
            mv.Property(m => m.Amount).HasColumnName("MarketValue").HasPrecision(18, 2);
            mv.Property(m => m.Currency).HasColumnName("MarketValueCurrency").HasMaxLength(3);
        });

        builder.OwnsOne(x => x.ForcedSaleValue, fsv =>
        {
            fsv.Property(m => m.Amount).HasColumnName("ForcedSaleValue").HasPrecision(18, 2);
            fsv.Property(m => m.Currency).HasColumnName("ForcedSaleValueCurrency").HasMaxLength(3);
        });

        builder.OwnsOne(x => x.AcceptableValue, av =>
        {
            av.Property(m => m.Amount).HasColumnName("AcceptableValue").HasPrecision(18, 2);
            av.Property(m => m.Currency).HasColumnName("AcceptableValueCurrency").HasMaxLength(3);
        });

        builder.OwnsOne(x => x.InsuredValue, iv =>
        {
            iv.Property(m => m.Amount).HasColumnName("InsuredValue").HasPrecision(18, 2);
            iv.Property(m => m.Currency).HasColumnName("InsuredValueCurrency").HasMaxLength(3);
        });

        builder.Property(x => x.HaircutPercentage)
            .HasPrecision(5, 2);

        // Lien details
        builder.Property(x => x.LienType)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.LienReference)
            .HasMaxLength(100);

        builder.Property(x => x.LienRegistrationAuthority)
            .HasMaxLength(200);

        // Insurance
        builder.Property(x => x.InsurancePolicyNumber)
            .HasMaxLength(50);

        builder.Property(x => x.InsuranceCompany)
            .HasMaxLength(200);

        // Audit
        builder.Property(x => x.RejectionReason)
            .HasMaxLength(1000);

        builder.Property(x => x.Notes)
            .HasMaxLength(2000);

        // Relationships
        builder.HasMany(x => x.Valuations)
            .WithOne()
            .HasForeignKey(x => x.CollateralId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Documents)
            .WithOne()
            .HasForeignKey(x => x.CollateralId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(x => x.DomainEvents);
    }
}

public class CollateralValuationConfiguration : IEntityTypeConfiguration<CollateralValuation>
{
    public void Configure(EntityTypeBuilder<CollateralValuation> builder)
    {
        builder.ToTable("CollateralValuations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.OwnsOne(x => x.MarketValue, mv =>
        {
            mv.Property(m => m.Amount).HasColumnName("MarketValue").HasPrecision(18, 2).IsRequired();
            mv.Property(m => m.Currency).HasColumnName("Currency").HasMaxLength(3).IsRequired();
        });

        builder.OwnsOne(x => x.ForcedSaleValue, fsv =>
        {
            fsv.Property(m => m.Amount).HasColumnName("ForcedSaleValue").HasPrecision(18, 2);
            fsv.Property(m => m.Currency).HasColumnName("ForcedSaleValueCurrency").HasMaxLength(3);
        });

        builder.Property(x => x.ValuerName)
            .HasMaxLength(200);

        builder.Property(x => x.ValuerCompany)
            .HasMaxLength(200);

        builder.Property(x => x.ValuerLicense)
            .HasMaxLength(50);

        builder.Property(x => x.ValuationReportReference)
            .HasMaxLength(100);

        builder.Property(x => x.Remarks)
            .HasMaxLength(1000);
    }
}

public class CollateralDocumentConfiguration : IEntityTypeConfiguration<CollateralDocument>
{
    public void Configure(EntityTypeBuilder<CollateralDocument> builder)
    {
        builder.ToTable("CollateralDocuments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DocumentType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.FileName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.StoragePath)
            .HasMaxLength(500);

        builder.Property(x => x.ContentType)
            .HasMaxLength(100);

        builder.Property(x => x.Description)
            .HasMaxLength(500);
    }
}

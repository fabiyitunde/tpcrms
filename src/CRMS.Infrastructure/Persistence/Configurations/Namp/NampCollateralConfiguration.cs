using CRMS.Domain.Aggregates.Namp;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Namp;

public class NampCollateralConfiguration : IEntityTypeConfiguration<NampCollateral>
{
    public void Configure(EntityTypeBuilder<NampCollateral> builder)
    {
        builder.ToTable("NampCollaterals");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.NampApplicationId).IsRequired();

        builder.Property(x => x.CollateralType).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Description).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.AssetIdentifier).HasMaxLength(200);
        builder.Property(x => x.Location).HasMaxLength(500);
        builder.Property(x => x.OwnerName).HasMaxLength(300);
        builder.Property(x => x.OwnershipType).HasMaxLength(50);
        builder.Property(x => x.MarketValue).HasColumnType("decimal(18,2)");
        builder.Property(x => x.ForcedSaleValue).HasColumnType("decimal(18,2)");
        builder.Property(x => x.LienType).HasMaxLength(50);
        builder.Property(x => x.LienReference).HasMaxLength(200);
        builder.Property(x => x.LienRegistrationAuthority).HasMaxLength(300);
        builder.Property(x => x.InsurancePolicyNumber).HasMaxLength(100);
        builder.Property(x => x.InsuranceCompany).HasMaxLength(300);
        builder.Property(x => x.InsuredValue).HasColumnType("decimal(18,2)");
        builder.Property(x => x.InsuranceExpiryDate).HasMaxLength(20);

        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        builder.HasIndex(x => x.NampApplicationId);
    }
}

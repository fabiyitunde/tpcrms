using CRMS.Domain.Aggregates.Namp;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Namp;

public class NampGuarantorConfiguration : IEntityTypeConfiguration<NampGuarantor>
{
    public void Configure(EntityTypeBuilder<NampGuarantor> builder)
    {
        builder.ToTable("NampGuarantors");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.NampApplicationId).IsRequired();

        builder.Property(x => x.FullName).IsRequired().HasMaxLength(300);
        builder.Property(x => x.Bvn).HasMaxLength(20);
        builder.Property(x => x.Nin).HasMaxLength(20);
        builder.Property(x => x.DateOfBirth).HasMaxLength(20);
        builder.Property(x => x.Gender).HasMaxLength(20);
        builder.Property(x => x.CompanyName).HasMaxLength(300);
        builder.Property(x => x.CompanyRegistrationNumber).HasMaxLength(50);
        builder.Property(x => x.Email).HasMaxLength(200);
        builder.Property(x => x.Phone).HasMaxLength(30);
        builder.Property(x => x.Address).HasMaxLength(500);
        builder.Property(x => x.GuarantorType).IsRequired().HasMaxLength(50);
        builder.Property(x => x.GuaranteeType).IsRequired().HasMaxLength(50);
        builder.Property(x => x.RelationshipToApplicant).HasMaxLength(200);
        builder.Property(x => x.ShareholdingPercentage).HasColumnType("decimal(5,2)");
        builder.Property(x => x.DeclaredNetWorth).HasColumnType("decimal(18,2)");
        builder.Property(x => x.Occupation).HasMaxLength(200);
        builder.Property(x => x.EmployerName).HasMaxLength(300);
        builder.Property(x => x.MonthlyIncome).HasColumnType("decimal(18,2)");
        builder.Property(x => x.GuaranteeLimit).HasColumnType("decimal(18,2)");

        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        builder.HasIndex(x => x.NampApplicationId);
    }
}

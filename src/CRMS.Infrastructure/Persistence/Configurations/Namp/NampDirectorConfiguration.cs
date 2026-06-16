using CRMS.Domain.Aggregates.Namp;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Namp;

public class NampDirectorConfiguration : IEntityTypeConfiguration<NampDirector>
{
    public void Configure(EntityTypeBuilder<NampDirector> builder)
    {
        builder.ToTable("NampDirectors");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.NampApplicationId).IsRequired();

        builder.Property(x => x.CacDirectorId);
        builder.Property(x => x.SourcedFromCac).IsRequired();

        builder.Property(x => x.FullName).IsRequired().HasMaxLength(300);
        builder.Property(x => x.Surname).HasMaxLength(150);
        builder.Property(x => x.FirstName).HasMaxLength(150);
        builder.Property(x => x.OtherName).HasMaxLength(150);
        builder.Property(x => x.Gender).HasMaxLength(40);
        builder.Property(x => x.DateOfBirth).HasMaxLength(50);
        builder.Property(x => x.Nationality).HasMaxLength(100);
        builder.Property(x => x.Occupation).HasMaxLength(200);

        builder.Property(x => x.Email).HasMaxLength(200);
        builder.Property(x => x.PhoneNumber).HasMaxLength(30);
        builder.Property(x => x.Address).HasMaxLength(500);
        builder.Property(x => x.City).HasMaxLength(100);
        builder.Property(x => x.State).HasMaxLength(100);

        builder.Property(x => x.IsChairman).IsRequired();
        builder.Property(x => x.DateOfAppointment).HasMaxLength(50);
        builder.Property(x => x.AffiliateType).HasMaxLength(100);
        builder.Property(x => x.RoleStatus).HasMaxLength(100);

        builder.Property(x => x.TypeOfShares).HasMaxLength(100);
        builder.Property(x => x.NumSharesAllotted);
        builder.Property(x => x.ShareholdingPercent).HasColumnType("decimal(5,2)");

        builder.Property(x => x.Bvn).HasMaxLength(20);
        builder.Property(x => x.IdentityNumber).HasMaxLength(50);
        builder.Property(x => x.BvnVerified).IsRequired();

        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        builder.HasIndex(x => x.NampApplicationId);
    }
}

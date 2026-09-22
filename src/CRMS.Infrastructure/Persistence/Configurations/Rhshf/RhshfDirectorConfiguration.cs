using CRMS.Domain.Aggregates.Rhshf;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Rhshf;

public class RhshfDirectorConfiguration : IEntityTypeConfiguration<RhshfDirector>
{
    public void Configure(EntityTypeBuilder<RhshfDirector> builder)
    {
        builder.ToTable("RhshfDirectors");
        builder.HasKey(x => x.Id);

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

        builder.Property(x => x.DateOfAppointment).HasMaxLength(50);
        builder.Property(x => x.AffiliateType).HasMaxLength(100);
        builder.Property(x => x.RoleStatus).HasMaxLength(100);

        builder.Property(x => x.TypeOfShares).HasMaxLength(100);
        builder.Property(x => x.ShareholdingPercent).HasColumnType("decimal(5,2)");

        builder.Property(x => x.Bvn).HasMaxLength(20);
        builder.Property(x => x.IdentityNumber).HasMaxLength(50);

        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        builder.HasIndex(x => x.RhshfCreditProfileId);
        // Bureau subject lists are built by filtering on BVN presence — index it.
        builder.HasIndex(x => x.Bvn);
    }
}

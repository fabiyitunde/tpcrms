using CRMS.Domain.Aggregates.Rhshf;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Rhshf;

public class RhshfScheduleFarmerConfiguration : IEntityTypeConfiguration<RhshfScheduleFarmer>
{
    public void Configure(EntityTypeBuilder<RhshfScheduleFarmer> builder)
    {
        builder.ToTable("RhshfScheduleFarmers");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.FarmerRef).HasMaxLength(100);
        builder.Property(x => x.Bvn).HasMaxLength(11);
        builder.Property(x => x.Nin).HasMaxLength(11);
        builder.Property(x => x.Nuban).HasMaxLength(10);
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        // Its own table, linked by id only — no navigation from the profile, so the profile's eager
        // load never pulls the farmer list. Rows go when their case does.
        builder.HasOne<RhshfCreditProfile>()
            .WithMany()
            .HasForeignKey(x => x.RhshfCreditProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.RhshfCreditProfileId);
        // The party-exposure lookup (CRMSQA#4) finds a scheduled farmer by each identifier.
        builder.HasIndex(x => x.Bvn);
        builder.HasIndex(x => x.Nin);
        builder.HasIndex(x => x.Nuban);
    }
}

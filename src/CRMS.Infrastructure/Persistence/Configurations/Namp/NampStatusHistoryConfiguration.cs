using CRMS.Domain.Aggregates.Namp;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Namp;

public class NampStatusHistoryConfiguration : IEntityTypeConfiguration<NampStatusHistory>
{
    public void Configure(EntityTypeBuilder<NampStatusHistory> builder)
    {
        builder.ToTable("NampStatusHistory");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.NampApplicationId)
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Note)
            .HasMaxLength(2000);

        builder.Property(x => x.CreatedBy)
            .HasMaxLength(100);

        builder.Property(x => x.ModifiedBy)
            .HasMaxLength(100);

        builder.HasIndex(x => x.NampApplicationId);
        builder.HasIndex(x => new { x.NampApplicationId, x.ChangedAt });
    }
}

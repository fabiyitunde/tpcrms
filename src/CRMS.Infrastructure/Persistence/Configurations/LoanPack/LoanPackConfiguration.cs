using CRMS.Domain.Aggregates.LoanPack;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.LoanPack;

public class LoanPackConfiguration : IEntityTypeConfiguration<Domain.Aggregates.LoanPack.LoanPack>
{
    public void Configure(EntityTypeBuilder<Domain.Aggregates.LoanPack.LoanPack> builder)
    {
        builder.ToTable("LoanPacks");

        builder.HasKey(x => x.Id);

        // Indexes
        builder.HasIndex(x => x.LoanApplicationId);
        builder.HasIndex(x => x.ApplicationNumber);
        builder.HasIndex(x => new { x.LoanApplicationId, x.Version }).IsUnique();
        builder.HasIndex(x => x.GeneratedAt);
        builder.HasIndex(x => x.Status);

        builder.Property(x => x.ApplicationNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.GeneratedByUserName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.FileName)
            .HasMaxLength(255);

        builder.Property(x => x.StoragePath)
            .HasMaxLength(500);

        builder.Property(x => x.ContentHash)
            .HasMaxLength(100);

        builder.Property(x => x.CustomerName)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.ProductName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.RequestedAmount)
            .HasPrecision(18, 2);

        builder.Property(x => x.RecommendedAmount)
            .HasPrecision(18, 2);

        builder.Property(x => x.RiskRating)
            .HasMaxLength(50);
    }
}

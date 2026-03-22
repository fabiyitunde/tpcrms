using CRMS.Domain.Aggregates.OfferLetter;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations;

public class OfferLetterConfiguration : IEntityTypeConfiguration<OfferLetter>
{
    public void Configure(EntityTypeBuilder<OfferLetter> builder)
    {
        builder.ToTable("OfferLetters");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.LoanApplicationId).IsRequired();
        builder.Property(x => x.ApplicationNumber).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Version).IsRequired();
        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.GeneratedAt).IsRequired();
        builder.Property(x => x.GeneratedByUserId).IsRequired();
        builder.Property(x => x.GeneratedByUserName).IsRequired().HasMaxLength(200);

        builder.Property(x => x.FileName).HasMaxLength(500);
        builder.Property(x => x.StoragePath).HasMaxLength(1000);
        builder.Property(x => x.ContentHash).HasMaxLength(100);

        builder.Property(x => x.CustomerName).HasMaxLength(500);
        builder.Property(x => x.ProductName).HasMaxLength(200);
        builder.Property(x => x.ApprovedAmount).HasPrecision(18, 2);
        builder.Property(x => x.ApprovedInterestRate).HasPrecision(8, 4);
        builder.Property(x => x.TotalInterest).HasPrecision(18, 2);
        builder.Property(x => x.TotalRepayment).HasPrecision(18, 2);
        builder.Property(x => x.MonthlyInstallment).HasPrecision(18, 2);
        builder.Property(x => x.ScheduleSource).HasMaxLength(50);

        builder.HasIndex(x => x.LoanApplicationId);
        builder.HasIndex(x => new { x.LoanApplicationId, x.Version }).IsUnique();
    }
}

using CRMS.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Outbox;

public class CreditCheckOutboxConfiguration : IEntityTypeConfiguration<CreditCheckOutboxEntry>
{
    public void Configure(EntityTypeBuilder<CreditCheckOutboxEntry> builder)
    {
        builder.ToTable("CreditCheckOutbox");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.LoanApplicationId).IsRequired();
        builder.Property(e => e.SystemUserId).IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);
        builder.Property(e => e.ErrorMessage).HasMaxLength(1000);
        builder.Property(e => e.AttemptCount).IsRequired().HasDefaultValue(0).ValueGeneratedNever();

        // Fast lookup for the polling query
        builder.HasIndex(e => e.Status).HasDatabaseName("IX_CreditCheckOutbox_Status");
        builder.HasIndex(e => e.LoanApplicationId).HasDatabaseName("IX_CreditCheckOutbox_LoanApplicationId");
    }
}

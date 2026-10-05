using CRMS.Domain.Aggregates.Rhshf;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRMS.Infrastructure.Persistence.Configurations.Rhshf;

/// <summary>Flat, one-row-per-year financial statement (see RhshfFinancialStatement's doc comment).
/// Every line item is decimal(18,2); the enums are stored by name. Unique per profile + year.</summary>
public class RhshfFinancialStatementConfiguration : IEntityTypeConfiguration<RhshfFinancialStatement>
{
    public void Configure(EntityTypeBuilder<RhshfFinancialStatement> builder)
    {
        builder.ToTable("RhshfFinancialStatements");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.RhshfCreditProfileId, x.FinancialYear }).IsUnique();

        builder.Property(x => x.YearType).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.InputMethod).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        builder.Property(x => x.YearEndDate).HasMaxLength(30);
        builder.Property(x => x.AuditorName).HasMaxLength(200);
        builder.Property(x => x.AuditorFirm).HasMaxLength(200);
        builder.Property(x => x.AuditDate).HasMaxLength(30);
        builder.Property(x => x.AuditOpinion).HasMaxLength(500);
        builder.Property(x => x.OriginalFileName).HasMaxLength(255);
        builder.Property(x => x.FilePath).HasMaxLength(500);
        builder.Property(x => x.VerificationNotes).HasMaxLength(1000);
        builder.Property(x => x.RejectionReason).HasMaxLength(1000);

        // Every monetary line item is decimal(18,2). Applied in bulk so the ~60 nullable figures don't
        // each need a line here and can't drift in precision.
        foreach (var prop in builder.Metadata.GetProperties()
                     .Where(p => p.ClrType == typeof(decimal?) || p.ClrType == typeof(decimal)))
        {
            prop.SetPrecision(18);
            prop.SetScale(2);
        }
    }
}
